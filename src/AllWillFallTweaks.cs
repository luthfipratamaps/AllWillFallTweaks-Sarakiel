using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sarakiel.AllWillFall
{
    [BepInPlugin("id.sarakiel.allwillfall.resourcetweaks", "All Will Fall - Resource Tweaks", "0.8.3.1")]
    public class ResourceTweaks : BaseUnityPlugin
    {
        private readonly string[] names = { "Water", "Food", "Junk", "Wood", "Metal", "Sand", "Fuel", "Goods", "Tools" };
        private bool visible;
        private string amountText = "100";
        private string status = "Press F8 to open";
        private Rect window = new Rect(110, 90, 564, 505);
        private ConfigEntry<float> savedX, savedY;
        private ConfigEntry<KeyCode> toggleKey;
        private ConfigEntry<int> defaultAmount;
        private ConfigEntry<bool> strictMouseBlock;
        private ConfigEntry<bool> newInputSystemBlock;
        private ConfigEntry<int> storageMultiplier, housingMultiplier;
        private ConfigEntry<bool> enableWater, enableFood, enableFuel, enableWarehouse, enableHouse;
        private bool capacityTab;
        private bool constructionTab;
        private bool researchTab;
        private bool productionTab;
        private ConfigEntry<bool> productionYieldEnabled, productionSpeedEnabled;
        private ConfigEntry<int> productionYieldMultiplier, productionSpeedMultiplier;
        private bool productionAuditWritten;
        private static MethodInfo producerProduceMethod;
        private static bool replayingProduction;
        private bool yieldHookReady, speedHookReady;
        private readonly Dictionary<object, bool> resourceWorkProviderCache = new Dictionary<object, bool>(ReferenceEqualityComparer.Instance);
        private static FieldInfo workProviderProducersField;
        private bool warnedSpeedProviderRead;
        private int productionYieldCalls, productionSpeedCalls;
        private bool warnedProductionReplay;
        private ConfigEntry<bool> instantResearchRequested, freeResearchRequested;
        private Type researchManagerType;
        private MethodInfo forceCompleteResearchMethod;
        private int researchHooksPatched;
        private float nextResearchCheckAt;
        private bool researchAuditWritten;
        private float nextResearchWarningAt;
        private object researchManager;
        private MethodInfo getCurrentResearchMethod;
        private float nextResearchDiscoveryAt;
        private object lastCompletedResearch;
        private float lastResearchCompletionAt;
        private ConfigEntry<bool> instantConstructionRequested, freeConstructionRequested;
        private bool constructionScanned;
        private int constructionMethodCount;
        private int constructionOverridesPatched;
        private int constructionDirectPatched;
        private readonly List<QueuedBuild> queuedConstruction = new List<QueuedBuild>();
        private sealed class QueuedBuild { public CityConstruction city; public Buildable buildable; public float readyAt; }
        private ConfigEntry<bool> autoScanEnabled;
        private ConfigEntry<float> autoScanInterval;
        private ConfigEntry<bool> verboseDiagnostics;
        private int observedSceneHandle = int.MinValue;
        private float nextAutoScanAt;
        private ConfigEntry<bool> performanceDiagnostics;
        private ConfigEntry<float> slowOperationThresholdMs;
        private float nextFrameHitchLogAt;
        private float nextPerformanceSummaryAt;
        private int observedFrameSpikes;
        private float worstFrameMs;
        private int scannedStorageRuns;
        private float maxStorageScanMs;
        private float maxCapacitySyncMs;
        private int syncedCapacityRuns;
        private float maxResourceRefreshMs;
        private int resourceRefreshRuns;
        private readonly Dictionary<Type, FieldInfo> capacityFieldCache = new Dictionary<Type, FieldInfo>();
        private bool capacitySettingsDirty;
        private int lastStorageMultiplier;
        private bool lastWaterEnabled, lastFoodEnabled, lastFuelEnabled, lastWarehouseEnabled;

        private void RememberCapacitySettings()
        {
            lastStorageMultiplier = storageMultiplier.Value;
            lastWaterEnabled = enableWater.Value;
            lastFoodEnabled = enableFood.Value;
            lastFuelEnabled = enableFuel.Value;
            lastWarehouseEnabled = enableWarehouse.Value;
        }

        private void DetectCapacitySettingsChange()
        {
            if (storageMultiplier.Value == lastStorageMultiplier &&
                enableWater.Value == lastWaterEnabled &&
                enableFood.Value == lastFoodEnabled &&
                enableFuel.Value == lastFuelEnabled &&
                enableWarehouse.Value == lastWarehouseEnabled) return;
            RememberCapacitySettings();
            capacitySettingsDirty = true;
        }

        private int autoScanAttempts;
        private bool lastManagerReady;

        private readonly List<string> capacityDiagnostics = new List<string>();
        private int capacityPatched;
        private int inputMethodsPatched;
        private Harmony harmony;
        private GUIStyle panelStyle, headingStyle, subtitleStyle, mutedStyle, bodyStyle, numberStyle, inputStyle, buttonStyle, presetStyle, primaryStyle, cardStyle, closeStyle;
        private Texture2D panelTex, buttonTex, primaryTex, fieldTex, cardTex, borderTex;
        private bool stylesReady;
        private bool pointerCaptured;
        private float lastCountRefresh;
        private readonly Dictionary<string, string> counts = new Dictionary<string, string>();
        private static ResourceTweaks active;
        private const int WindowId = 98311;
        private Type managerType;
        private object manager;
        private bool inspected;
        private bool diagnosticsWritten;
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private void Awake()
        {
            active = this;
            savedX = Config.Bind("UI", "WindowX", 110f, "Last window X position");
            savedY = Config.Bind("UI", "WindowY", 90f, "Last window Y position");
            toggleKey = Config.Bind("UI", "ToggleKey", KeyCode.F8, "Toggle the resource menu");
            defaultAmount = Config.Bind("Resources", "DefaultAmount", 100, "Starting amount for a new session");
            strictMouseBlock = Config.Bind("Input", "BlockWorldMouseWhileMenuOpen", true, "Experimental: suppress legacy Unity mouse button polling whenever the tweak window is open. Turn off if incompatible.");
            newInputSystemBlock = Config.Bind("Input", "BlockNewInputSystemWhileMenuOpen", true, "Suppress newer Unity Input System mouse buttons and actions while the tweak menu is open. Disable if incompatible.");
            storageMultiplier = Config.Bind("Capacity", "StorageMultiplier", 10, "Storage building capacity multiplier (1-100). Calculated from the original getter capacity, not saved to assets.");
            housingMultiplier = Config.Bind("Capacity", "HousingMultiplier", 5, "Housing building capacity multiplier (1-20). Applied by safe getter patch.");
            enableWater = Config.Bind("Capacity", "WaterStorage", true, "Enable water storage building multiplier");
            enableFood = Config.Bind("Capacity", "FoodWarehouse", true, "Enable food warehouse multiplier");
            enableFuel = Config.Bind("Capacity", "FuelWarehouse", true, "Enable fuel warehouse multiplier");
            enableWarehouse = Config.Bind("Capacity", "GeneralWarehouse", true, "Enable general warehouse multiplier");
            enableHouse = Config.Bind("Capacity", "Housing", true, "Enable house occupancy multiplier");
            autoScanEnabled = Config.Bind("Startup", "AutoScanOnCityLoad", true, "Automatically discover buildings/resources after loading a city. Manual Rescan is still available.");
            autoScanInterval = Config.Bind("Startup", "BackgroundScanIntervalSeconds", 5f, "Interval for discovering newly constructed storages (minimum 1 second).");
            verboseDiagnostics = Config.Bind("Diagnostics", "VerboseLogging", false, "Log resource reflection details, per-storage traces and scan attempts for troubleshooting.");
            performanceDiagnostics = Config.Bind("Diagnostics", "PerformanceDiagnostics", false,
                "Periodically report frame hitches and worst scan timings (no per-frame logging).");
            slowOperationThresholdMs = Config.Bind("Diagnostics", "SlowOperationThresholdMs", 8f,
                "Log a scan/synchronization immediately if it exceeds this many milliseconds.");
            // Avoid ConfigEntryBase.SettingChanged: unavailable in some BepInEx 5 builds.
            // Remember the initial settings; a cheap value comparison detects UI and
            // external config changes without allocating delegates or using reflection.
            RememberCapacitySettings();
            instantConstructionRequested = Config.Bind("Construction", "InstantConstruction", true,
                "Instantly complete new building construction. Changes in the F8 menu are saved to this config.");
            freeConstructionRequested = Config.Bind("Construction", "FreeConstruction", true,
                "Disable construction material requirements. Changes in the F8 menu are saved to this config.");
            instantResearchRequested = Config.Bind("Research", "InstantResearch", true,
                "Complete active research using the game's own ForceCompleteResearch(Research) API.");
            freeResearchRequested = Config.Bind("Research", "FreeResearch", true,
                "Suppress research material payments without affecting construction or production costs.");
            productionYieldEnabled = Config.Bind("Production", "YieldEnabled", false,
                "Multiply only the final ResourceProducer.Produce batch (not input consumption).");
            productionSpeedEnabled = Config.Bind("Production", "SpeedEnabled", false,
                "Divide WorkProvider.WorkInterval for resource-producing buildings only. Experimental.");
            productionYieldMultiplier = Config.Bind("Production", "YieldMultiplier", 10,
                "Final produced resources multiplier; does not scale material costs.");
            productionSpeedMultiplier = Config.Bind("Production", "SpeedMultiplier", 5,
                "Resource-producer work interval divisor. Requires active workers; minimum interval is 0.1.");
            amountText = Math.Max(1, Math.Min(1000000, defaultAmount.Value)).ToString();
            window.x = Mathf.Clamp(savedX.Value, 0, Mathf.Max(0, Screen.width - window.width));
            window.y = Mathf.Clamp(savedY.Value, 0, Mathf.Max(0, Screen.height - window.height));
            try
            {
                // Many Unity games call EventSystem.IsPointerOverGameObject before selecting/building.
                // This optional compatibility patch makes those checks recognize the mod window.
                Type eventSystem = AccessTools.TypeByName("UnityEngine.EventSystems.EventSystem");
                if (eventSystem != null)
                {
                    harmony = new Harmony("id.sarakiel.allwillfall.resourcetweaks.ui");
                    MethodInfo postfix = typeof(ResourceTweaks).GetMethod("PointerOverUIPostfix", BindingFlags.NonPublic | BindingFlags.Static);
                    int patched = 0;
                    foreach (MethodInfo m in eventSystem.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (m.Name != "IsPointerOverGameObject" || m.ReturnType != typeof(bool)) continue;
                        if (m.GetParameters().Length > 1) continue;
                        try { harmony.Patch(m, postfix: new HarmonyMethod(postfix)); patched++; }
                        catch (Exception ex) { Logger.LogWarning("UI pointer patch skipped: " + ex.Message); }
                    }
                    Logger.LogInfo("UI pointer guard: " + patched + " EventSystem overload(s) patched");
                }
                else Logger.LogWarning("EventSystem UI guard unavailable; IMGUI event capture remains active");
            }
            catch (Exception ex) { Logger.LogWarning("Optional UI pointer guard could not initialize: " + ex.Message); }
            InstallStrictMouseGuard();
            InstallNewInputGuard();
            InstallCapacityPatches();
            InstallConstructionOverrides();
            InstallConstructionDirectPatches();
            InstallResearchHooks();
            InstallProductionHooks();
            // City data is unavailable during Awake; defer scene scan to AutoScanCity.
            nextAutoScanAt = Time.unscaledTime + 1f;
        }

        // IMGUI uses Event.current, while many games poll UnityEngine.Input separately.
        // These patches aim to hide button state from the GAME whenever our menu is open.
        // Note: games using the new Input System directly may bypass these legacy getters.
        private void InstallStrictMouseGuard()
        {
            if (harmony == null) harmony = new Harmony("id.sarakiel.allwillfall.resourcetweaks.ui");
            Type input = typeof(UnityEngine.Input);
            var preButton = new HarmonyMethod(typeof(ResourceTweaks).GetMethod("BlockMouseButtons", BindingFlags.Static | BindingFlags.NonPublic));
            var preKey = new HarmonyMethod(typeof(ResourceTweaks).GetMethod("BlockMouseKey", BindingFlags.Static | BindingFlags.NonPublic));
            foreach (string methodName in new [] { "GetMouseButton", "GetMouseButtonDown", "GetMouseButtonUp" })
            {
                try
                {
                    MethodInfo m = AccessTools.Method(input, methodName, new [] { typeof(int) });
                    if (m == null) { Logger.LogWarning("Missing legacy input method: " + methodName); continue; }
                    harmony.Patch(m, prefix: preButton);
                    inputMethodsPatched++;
                }
                catch (Exception ex) { Logger.LogWarning("Strict mouse patch " + methodName + " unavailable: " + ex.Message); }
            }
            foreach (string methodName in new [] { "GetKey", "GetKeyDown", "GetKeyUp" })
            {
                try
                {
                    MethodInfo m = AccessTools.Method(input, methodName, new [] { typeof(KeyCode) });
                    if (m == null) continue;
                    harmony.Patch(m, prefix: preKey);
                    inputMethodsPatched++;
                }
                catch (Exception ex) { Logger.LogWarning("Strict key patch " + methodName + " unavailable: " + ex.Message); }
            }
            Logger.LogInfo("Strict mouse guard: " + inputMethodsPatched + "/6 legacy input getter(s) patched. New Input System polling is not covered.");
        }

        // Unity 6 games frequently poll the package com.unity.inputsystem instead of
        // UnityEngine.Input. Harmony patches the public bool getters/actions, without
        // adding a compile-time dependency on Unity.InputSystem.dll.
        // This is an EXPERIMENTAL broad guard: while open, game InputActions are muted.
        // IMGUI uses Event.current and continues receiving its own input normally.
        private void InstallNewInputGuard()
        {
            if (harmony == null) harmony = new Harmony("id.sarakiel.allwillfall.resourcetweaks.ui");
            int patched = 0;
            string[] actionMethods = { "WasPressedThisFrame", "WasReleasedThisFrame", "WasPerformedThisFrame", "IsPressed", "get_triggered" };
            string[] buttonMethods = { "get_isPressed", "get_wasPressedThisFrame", "get_wasReleasedThisFrame", "IsPressed" };
            string[] mouseButtons = { "leftButton", "rightButton", "middleButton" };
            var prefix = new HarmonyMethod(typeof(ResourceTweaks).GetMethod("BlockNewInputBool", BindingFlags.Static | BindingFlags.NonPublic));
            try
            {
                Type action = AccessTools.TypeByName("UnityEngine.InputSystem.InputAction");
                Type button = AccessTools.TypeByName("UnityEngine.InputSystem.Controls.ButtonControl");
                Type pointer = AccessTools.TypeByName("UnityEngine.InputSystem.Pointer");
                Type mouse = AccessTools.TypeByName("UnityEngine.InputSystem.Mouse");
                Logger.LogInfo("New Input System types: InputAction=" + (action != null) + " ButtonControl=" + (button != null) + " Mouse=" + (mouse != null));
                if (action != null) foreach (string name in actionMethods)
                {
                    MethodInfo m = AccessTools.Method(action, name, Type.EmptyTypes);
                    if (m == null || m.ReturnType != typeof(bool)) continue;
                    try { harmony.Patch(m, prefix: prefix); patched++; }
                    catch (Exception ex) { Logger.LogWarning("InputAction patch " + name + ": " + ex.Message); }
                }
                if (button != null) foreach (string name in buttonMethods)
                {
                    MethodInfo m = AccessTools.Method(button, name, Type.EmptyTypes);
                    if (m == null || m.ReturnType != typeof(bool)) continue;
                    try { harmony.Patch(m, prefix: prefix); patched++; }
                    catch (Exception ex) { Logger.LogWarning("ButtonControl patch " + name + ": " + ex.Message); }
                }
                // Check mouse property signals (some versions expose direct mouse-button
                // access via action-backed properties; ButtonControl getters cover them).
                if (mouse != null)
                    foreach (string name in mouseButtons)
                        Logger.LogInfo("InputSystem Mouse." + name + "=" + (AccessTools.Property(mouse, name) != null));
                Logger.LogInfo("New Input System guard: " + patched + " boolean methods patched. World input suppression is experimental.");
            }
            catch (Exception ex) { Logger.LogWarning("New Input System guard unavailable: " + ex); }
        }

        private static bool BlockNewInputBool(ref bool __result)
        {
            if (active == null || !active.visible || active.newInputSystemBlock == null || !active.newInputSystemBlock.Value) return true;
            __result = false;
            return false;
        }

        private static bool ShouldBlockMouse()
        {
            return active != null && active.visible && active.strictMouseBlock != null && active.strictMouseBlock.Value;
        }

        private static bool BlockMouseButtons(ref bool __result)
        {
            if (!ShouldBlockMouse()) return true;
            __result = false;
            return false;
        }

        private static bool BlockMouseKey(KeyCode __0, ref bool __result)
        {
            if (!ShouldBlockMouse() || __0 < KeyCode.Mouse0 || __0 > KeyCode.Mouse6) return true;
            __result = false;
            return false;
        }

        private static void PointerOverUIPostfix(ref bool __result)
        {
            if (!__result && active != null && active.visible && active.IsMouseOverPanel()) __result = true;
        }

        private bool IsMouseOverPanel()
        {
            Vector3 mouse = Input.mousePosition;
            return window.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
        }

        private void Update()
        {
            ProcessQueuedConstruction();
            ResearchPeriodicCheck();
            if (autoScanEnabled != null && autoScanEnabled.Value) AutoScanCity();
            // Five primitive comparisons; no Unity scene scan or allocations.
            DetectCapacitySettingsChange();
            // Do not wake the reflection-based capacity synchronizer every 0.65 s.
            // It runs after city scans or immediately when its config changes.
            if (capacitySettingsDirty)
            {
                capacitySettingsDirty = false;
                ApplyStorageCapacities();
            }
            DiagnoseFrameHitches();
            if (Input.GetKeyDown(toggleKey != null ? toggleKey.Value : KeyCode.F8))
            {
                visible = !visible;
                if (!visible) { pointerCaptured = false; SaveWindowPosition(); }
            }
        }

        // City objects are often not ready during Awake(). Detect new scene sessions,
        // retry during loading, and keep a slow background scan for new construction.
        // Never clear the base-capacity cache on routine scans: values are already
        // multiplied in the live Storage.capacity field.
        private void AutoScanCity()
        {
            int sceneHandle = SceneManager.GetActiveScene().handle;
            if (sceneHandle != observedSceneHandle)
            {
                observedSceneHandle = sceneHandle;
                queuedConstruction.Clear();
                researchAuditWritten = false;
                productionAuditWritten = false;
                resourceWorkProviderCache.Clear();
                researchManager = null;
                lastCompletedResearch = null;
                nextResearchDiscoveryAt = 0f;
                nextResearchCheckAt = Time.unscaledTime + 1.5f;
                manager = null;
                inspected = false;
                diagnosticsWritten = false;
                lastManagerReady = false;
                capacitySettingsDirty = false;
                counts.Clear();
                storageKinds.Clear();
                storageBaseCapacities.Clear();
                unknownStorageLogged.Clear();
                tracedStorage.Clear();
                appliedCapacityLog.Clear();
                autoScanAttempts = 0;
                nextAutoScanAt = Time.unscaledTime + 1f;
                lastFullStorageScan = -100f;
                Logger.LogInfo("AUTO SCAN: scene changed; scheduling city discovery");
            }
            if (Time.unscaledTime < nextAutoScanAt) return;
            float interval = Mathf.Max(1f, autoScanInterval.Value);
            // Retry quickly while the city is initializing, then stay on a low-frequency
            // scan so new structures are picked up without opening the UI.
            nextAutoScanAt = Time.unscaledTime + (lastManagerReady ? interval : 1.5f);
            try
            {
                // A non-null reference can point to a destroyed Unity component.
                var unityManager = manager as UnityEngine.Object;
                if (unityManager != null && !unityManager) manager = null;
                if (manager == null) LocateManager();
                bool ready = manager != null;
                if (ready && !lastManagerReady)
                {
                    Logger.LogInfo("AUTO SCAN: CityResourceManager ready");
                    lastCountRefresh = -100f;
                }
                // One complete scan as soon as the city manager becomes ready.
                // This resolves storage ownership and applies multipliers during
                // initialization, not after the 30-second fallback window.
                // Later scheduled checks use the inexpensive city registry.
                bool initialCityScan = ready && !lastManagerReady;
                lastManagerReady = ready;
                Stopwatch scanWatch = Stopwatch.StartNew();
                if (ready) ScanStorageInstances(initialCityScan);
                scanWatch.Stop();
                RecordPerformance("Storage discovery", scanWatch.Elapsed.TotalMilliseconds, ref scannedStorageRuns, ref maxStorageScanMs);
                Stopwatch syncWatch = Stopwatch.StartNew();
                ApplyStorageCapacities();
                syncWatch.Stop();
                RecordPerformance("Capacity synchronization", syncWatch.Elapsed.TotalMilliseconds, ref syncedCapacityRuns, ref maxCapacitySyncMs);
                // Resource count discovery is expensive; the counts aren't visible
                // while F8 is closed. Refresh only when the Resources tab is shown.
                if (ready && visible && !capacityTab && !constructionTab && !researchTab && !productionTab)
                {
                    Stopwatch resourceWatch = Stopwatch.StartNew();
                    RefreshCounts();
                    resourceWatch.Stop();
                    RecordPerformance("Resource refresh", resourceWatch.Elapsed.TotalMilliseconds, ref resourceRefreshRuns, ref maxResourceRefreshMs);
                }
                autoScanAttempts++;
                if (verboseDiagnostics.Value && (autoScanAttempts <= 3 || autoScanAttempts == 10))
                    Logger.LogInfo("AUTO SCAN: attempt=" + autoScanAttempts + " city=" + ready + " tracked=" + storageKinds.Count);
            }
            catch (Exception ex)
            {
                if (autoScanAttempts++ < 3) Logger.LogWarning("AUTO SCAN: retrying after " + ex.Message);
            }
        }

        private void RecordPerformance(string operation, double ms, ref int count, ref float max)
        {
            if (performanceDiagnostics == null || !performanceDiagnostics.Value) return;
            count++;
            if (ms > max) max = (float)ms;
            if (ms >= Mathf.Max(1f, slowOperationThresholdMs.Value))
                Logger.LogWarning("PERF SLOW: " + operation + " took " + ms.ToString("F1") + " ms");
        }

        private void DiagnoseFrameHitches()
        {
            if (performanceDiagnostics == null || !performanceDiagnostics.Value) return;
            float now = Time.unscaledTime;
            float ms = Time.unscaledDeltaTime * 1000f;
            if (ms >= 90f)
            {
                observedFrameSpikes++;
                if (ms > worstFrameMs) worstFrameMs = ms;
                if (now >= nextFrameHitchLogAt)
                {
                    Logger.LogWarning("PERF FRAME HITCH: " + ms.ToString("F1") +
                        " ms, trackedStorages=" + storageKinds.Count + ", queuedBuilds=" + queuedConstruction.Count);
                    nextFrameHitchLogAt = now + 10f;
                }
            }
            if (nextPerformanceSummaryAt == 0f) nextPerformanceSummaryAt = now + 30f;
            if (now < nextPerformanceSummaryAt) return;
            Logger.LogInfo("PERF SUMMARY (30s): frameSpikes>=90ms=" + observedFrameSpikes +
                " worstFrame=" + worstFrameMs.ToString("F1") + "ms" +
                " storageScans=" + scannedStorageRuns + " maxScan=" + maxStorageScanMs.ToString("F1") + "ms" +
                " capacitySyncs=" + syncedCapacityRuns + " maxSync=" + maxCapacitySyncMs.ToString("F1") + "ms" +
                " resourceRefreshes=" + resourceRefreshRuns + " maxRefresh=" + maxResourceRefreshMs.ToString("F1") + "ms");
            nextPerformanceSummaryAt = now + 30f;
            observedFrameSpikes = 0; worstFrameMs = 0;
            scannedStorageRuns = 0; maxStorageScanMs = 0;
            syncedCapacityRuns = 0; maxCapacitySyncMs = 0;
            resourceRefreshRuns = 0; maxResourceRefreshMs = 0;
        }

        private void OnDisable() { SaveWindowPosition(); if (active == this) active = null; if (harmony != null) harmony.UnpatchSelf(); }

        private void SaveWindowPosition()
        {
            if (savedX == null || savedY == null) return;
            savedX.Value = window.x;
            savedY.Value = window.y;
        }

        private Texture2D Solid(Color color)
        {
            Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, color); t.Apply();
            return t;
        }

        private GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = size; style.fontStyle = weight; style.alignment = anchor;
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.padding = new RectOffset(4, 4, 2, 2);
            return style;
        }

        private GUIStyle MakeButton(Texture2D bg, int size, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.normal.background = bg;
            style.hover.background = cardTex;
            style.active.background = fieldTex;
            style.normal.textColor = color;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.fontSize = size; style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.border = new RectOffset(4, 4, 4, 4);
            style.padding = new RectOffset(6, 6, 3, 3);
            return style;
        }

        private void InitStyles()
        {
            if (stylesReady) return;
            panelTex = Solid(new Color(0.055f, 0.075f, 0.105f, .97f));
            cardTex = Solid(new Color(0.13f, 0.17f, 0.22f, 1f));
            buttonTex = Solid(new Color(0.105f, 0.14f, 0.19f, 1f));
            primaryTex = Solid(new Color(0.12f, 0.65f, 0.69f, 1f));
            fieldTex = Solid(new Color(0.045f, 0.065f, 0.09f, 1f));
            borderTex = Solid(new Color(0.23f, 0.32f, 0.39f, 1f));
            panelStyle = new GUIStyle(GUI.skin.window);
            panelStyle.normal.background = panelTex;
            panelStyle.onNormal.background = panelTex;
            panelStyle.border = new RectOffset(4, 4, 4, 4);
            panelStyle.padding = new RectOffset(0, 0, 0, 0);
            panelStyle.fontSize = 1;
            panelStyle.normal.textColor = new Color(0, 0, 0, 0);
            headingStyle = Style(18, Color.white, FontStyle.Bold);
            subtitleStyle = Style(11, new Color(.55f,.75f,.82f));
            mutedStyle = Style(11, new Color(.60f,.67f,.73f));
            bodyStyle = Style(12, new Color(.88f,.93f,.96f));
            numberStyle = Style(12, new Color(.47f,.87f,.85f), FontStyle.Bold, TextAnchor.MiddleRight);
            buttonStyle = MakeButton(buttonTex, 12, Color.white);
            presetStyle = MakeButton(cardTex, 11, new Color(.83f,.92f,.96f));
            primaryStyle = MakeButton(primaryTex, 13, Color.white);
            closeStyle = MakeButton(buttonTex, 16, Color.white);
            inputStyle = new GUIStyle(GUI.skin.textField);
            inputStyle.normal.background = fieldTex;
            inputStyle.focused.background = fieldTex;
            inputStyle.normal.textColor = Color.white;
            inputStyle.focused.textColor = Color.white;
            inputStyle.fontSize = 15;
            inputStyle.alignment = TextAnchor.MiddleCenter;
            inputStyle.padding = new RectOffset(4, 4, 4, 4);
            stylesReady = true;
        }

        private void OnGUI()
        {
            if (!visible) return;
            InitStyles();
            window.x = Mathf.Clamp(window.x, 0, Mathf.Max(0, Screen.width - window.width));
            window.y = Mathf.Clamp(window.y, 0, Mathf.Max(0, Screen.height - window.height));
            window = GUI.Window(WindowId, window, DrawWindow, "", panelStyle);
            // IMGUI-only capture is insufficient for games polling input in Update().
            // Complement it with the optional EventSystem compatibility patch above.
            Event e = Event.current;
            bool over = window.Contains(e.mousePosition);
            if (over && e.type == EventType.MouseDown) pointerCaptured = true;
            if ((over || pointerCaptured) && (e.isMouse || e.type == EventType.ScrollWheel))
            {
                if (e.type == EventType.MouseUp) pointerCaptured = false;
                e.Use();
            }
        }

        private void DrawWindow(int id)
        {
            GUI.DrawTexture(new Rect(0, 0, 564, 4), primaryTex);
            GUI.Label(new Rect(19, 14, 400, 29), "ALL WILL FALL  /  TWEAKS", headingStyle);
            GUI.Label(new Rect(22, 42, 450, 17), "SARAKIEL  •  TWEAKS DASHBOARD   v0.8.1", subtitleStyle);
            if (GUI.Button(new Rect(516, 18, 30, 28), "×", closeStyle)) { visible = false; pointerCaptured = false; SaveWindowPosition(); }
            GUI.DrawTexture(new Rect(18, 69, 528, 1), borderTex);
            // Five compact tabs fit the existing window without hiding any controls.
            if (GUI.Button(new Rect(18, 77, 100, 30), "RESOURCES", !capacityTab && !constructionTab && !researchTab && !productionTab ? primaryStyle : buttonStyle)) { capacityTab = false; constructionTab = false; researchTab = false; productionTab = false; }
            if (GUI.Button(new Rect(125, 77, 100, 30), "CAPACITY", capacityTab ? primaryStyle : buttonStyle)) { capacityTab = true; constructionTab = false; researchTab = false; productionTab = false; }
            if (GUI.Button(new Rect(232, 77, 100, 30), "BUILD", constructionTab ? primaryStyle : buttonStyle)) { constructionTab = true; capacityTab = false; researchTab = false; productionTab = false; }
            if (GUI.Button(new Rect(339, 77, 100, 30), "RESEARCH", researchTab ? primaryStyle : buttonStyle)) { researchTab = true; capacityTab = false; constructionTab = false; productionTab = false; }
            if (GUI.Button(new Rect(446, 77, 100, 30), "OUTPUT", productionTab ? primaryStyle : buttonStyle)) { productionTab = true; capacityTab = false; constructionTab = false; researchTab = false; }
            if (capacityTab) { DrawCapacityTab(); GUI.DragWindow(new Rect(0, 5, 500, 65)); return; }
            if (constructionTab) { DrawConstructionTab(); GUI.DragWindow(new Rect(0, 5, 500, 65)); return; }
            if (researchTab) { DrawResearchTab(); GUI.DragWindow(new Rect(0, 5, 500, 65)); return; }
            if (productionTab) { DrawProductionTab(); GUI.DragWindow(new Rect(0, 5, 500, 65)); return; }

            GUI.Label(new Rect(19, 118, 85, 24), "ADD AMOUNT", mutedStyle);
            amountText = GUI.TextField(new Rect(113, 117, 133, 31), amountText, 8, inputStyle);
            int[] presets = { 1, 10, 100, 1000 };
            for (int i=0; i<presets.Length; i++)
                if (GUI.Button(new Rect(258 + i*72, 117, 65, 31), presets[i].ToString(), presetStyle)) amountText = presets[i].ToString();

            GUI.Label(new Rect(19, 159, 300, 21), "RESOURCES", headingStyle);
            GUI.Label(new Rect(418, 160, 125, 20), "CURRENT STOCK", mutedStyle);
            for (int i=0; i<names.Length; i++)
            {
                int col = i%3, row = i/3;
                float x = 18 + col*179;
                float y = 151 + row*64 + 35;
                GUI.DrawTexture(new Rect(x,y,170,57), cardTex);
                string label = names[i];
                GUI.Label(new Rect(x+9,y+5,95,21),label,bodyStyle);
                string count;
                if (!counts.TryGetValue(label,out count)) count = "—";
                GUI.Label(new Rect(x+104,y+5,55,21),count,numberStyle);
                if (GUI.Button(new Rect(x+8,y+30,154,22),"+  ADD " + label.ToUpperInvariant(), buttonStyle)) Add(label);
            }
            // Only the Resources tab requests stock updates (and never when F8 is closed).
            if (Time.realtimeSinceStartup - lastCountRefresh > 1.5f) RefreshCounts();
            if (GUI.Button(new Rect(18, 395, 367, 38), "＋  ADD ALL NINE RESOURCES", primaryStyle))
            {
                foreach (string name in names) Add(name);
                status = "All resource additions requested (see log for results)";
                RefreshCounts();
            }
            if (GUI.Button(new Rect(394, 395, 152, 38), "⟳  RESCAN", buttonStyle))
            { inspected = false; manager = null; counts.Clear(); LocateManager(); ScanStorageInstances(true); ApplyStorageCapacities(); RefreshCounts(); }
            GUI.DrawTexture(new Rect(18, 445, 528, 1), borderTex);
            GUI.Label(new Rect(19, 451, 524, 32), status, mutedStyle);
            GUI.DragWindow(new Rect(0, 5, 500, 65));
        }


        // Exact capacity getters discovered in v0.4.1 runtime diagnostics.
        // Patch return values rather than writing game asset fields (prevents compounding).
        private void InstallCapacityPatches()
        {
            if (harmony == null) harmony = new Harmony("id.sarakiel.allwillfall.resourcetweaks.ui");
            Assembly game = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ScriptsAssembly");
            if (game == null) { Logger.LogWarning("CAPACITY: ScriptsAssembly is not loaded"); return; }
            foreach (string fullName in new[] { "Storage", "House" })
            {
                Type t = game.GetType(fullName, false);
                if (t == null) { Logger.LogWarning("CAPACITY: type unavailable " + fullName); continue; }
                MethodInfo getter = t.GetMethod("get_Capacity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (getter == null || getter.ReturnType != typeof(int)) { Logger.LogWarning("CAPACITY: int getter unavailable " + fullName); continue; }
                try
                {
                    MethodInfo postfix = typeof(ResourceTweaks).GetMethod(fullName == "House" ? "HouseCapacityPostfix" : "StorageCapacityPostfix", BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(getter, postfix: new HarmonyMethod(postfix));
                    capacityPatched++;
                    Logger.LogInfo("CAPACITY PATCH: " + t.FullName + ".get_Capacity -> " + postfix.Name);
                }
                catch (Exception e) { Logger.LogWarning("CAPACITY PATCH FAILED: " + fullName + " " + e); }
            }
            Logger.LogInfo("CAPACITY: " + capacityPatched + "/2 exact getters patched. Verify actual simulation on backup save.");
        }

        private static void HouseCapacityPostfix(ref int __result)
        {
            if (active == null || !active.enableHouse.Value || __result <= 0) return;
            int factor = Mathf.Clamp(active.housingMultiplier.Value, 1, 20);
            __result = MultiplySafe(__result, factor);
        }

        private readonly HashSet<object> tracedStorage = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private readonly HashSet<object> tracedUnknown = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private static void StorageCapacityPostfix(object __instance, ref int __result)
        {
            if (active == null || __instance == null || __result <= 0) return;
            int factor = Mathf.Clamp(active.storageMultiplier.Value, 1, 100);
            string kind = active.ResolveStorageKind(__instance);
            bool enabled = kind == "water" ? active.enableWater.Value :
                kind == "food" ? active.enableFood.Value :
                kind == "fuel" ? active.enableFuel.Value :
                kind == "warehouse" ? active.enableWarehouse.Value : false;
            if (active.verboseDiagnostics.Value && active.tracedStorage.Add(__instance) && active.tracedStorage.Count <= 48)
            {
                int raw = -1;
                try { var f = __instance.GetType().GetField("capacity", Flags); if (f != null) raw = (int)f.GetValue(__instance); } catch { }
                active.Logger.LogInfo("CAPACITY TRACE: kind=" + (kind.Length == 0 ? "UNKNOWN" : kind) +
                    " enabled=" + enabled + " getterBase=" + __result + " rawField=" + raw +
                    " multiplier=" + factor + " storageHash=" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(__instance));
            }
            if (!enabled)
            {
                int baseCapacity;
                if (active.storageBaseCapacities.TryGetValue(__instance, out baseCapacity)) __result = baseCapacity;
                return;
            }
            // Calculate from the original getter result. Do NOT write back into the
            // serialized backing field, so config changes never compound capacity.
            int original;
            if (!active.storageBaseCapacities.TryGetValue(__instance, out original)) original = __result;
            __result = MultiplySafe(original, factor);
        }

        private static int MultiplySafe(int original, int factor)
        {
            return factor <= 1 ? original : (original > int.MaxValue / factor ? int.MaxValue : original * factor);
        }

        private string ResolveStorageKind(object targetStorage)
        {
            string kind;
            if (storageKinds.TryGetValue(targetStorage, out kind)) return kind;
            // Storage.get_Capacity can be called many times per frame.
            // Never traverse the scene from this hot getter. The scheduled city scan
            // will discover and synchronize new storage instances safely.
            // Unknown objects temporarily retain vanilla capacity.
            if (verboseDiagnostics.Value && unknownStorageLogged.Add(targetStorage) && unknownStorageLogged.Count <= 12)
            {
                Logger.LogWarning("CAPACITY UNKNOWN STORAGE: " + targetStorage.GetType().FullName +
                    ". No building owner matched; original capacity kept.");
            }
            return "";
        }

        private bool capacityLookupWarningWritten;
        private readonly Dictionary<object, string> storageKinds = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<object, int> storageBaseCapacities = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        private readonly HashSet<object> unknownStorageLogged = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private float lastFullStorageScan = -100f;
        private bool capacityScanBusy;
        private const float FullStorageRescanSeconds = 30f;
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object a, object b) { return object.ReferenceEquals(a,b); }
            public int GetHashCode(object x) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(x); }
        }
        private void ScanStorageInstances(bool force)
        {
            if (capacityScanBusy) return;
            capacityScanBusy = true;
            try
            {
                if (manager == null) LocateManager();
                if (manager == null) return; // No city: don't scan every loaded Unity object.

                // Cheap path: use the already-maintained CityResourceManager registry
                // on every scheduled check. This catches registered new warehouses.
                FieldInfo f = manager.GetType().GetField("resourceBuildings", Flags);
                IDictionary map = f == null ? null : f.GetValue(manager) as IDictionary;
                if (map != null)
                    foreach (DictionaryEntry item in map) RegisterStorage(item.Value, item.Key);

                // Expensive fallback: discover ResourceBuilding components missing from
                // the registry. Force once on city readiness (and on manual Rescan),
                // otherwise run at most every 30s. City registry checks run every 5s.
                if (!force && Time.unscaledTime - lastFullStorageScan < FullStorageRescanSeconds) return;
                lastFullStorageScan = Time.unscaledTime;
                Type rbType = AccessTools.TypeByName("ResourceBuilding");
                if (rbType != null && typeof(UnityEngine.Object).IsAssignableFrom(rbType))
                {
                    foreach (UnityEngine.Object rb in Resources.FindObjectsOfTypeAll(rbType))
                    {
                        if (rb == null) continue;
                        Component component = rb as Component;
                        if (component != null && !component.gameObject.scene.IsValid()) continue;
                        RegisterStorage(rb, null);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!capacityLookupWarningWritten) { capacityLookupWarningWritten = true; Logger.LogWarning("CAPACITY SCAN: " + ex); }
            }
            finally { capacityScanBusy = false; }
        }
        private void RegisterStorage(object resourceBuilding, object buildable)
        {
            if (resourceBuilding == null) return;
            FieldInfo sf = resourceBuilding.GetType().GetField("storage", Flags);
            object storage = sf == null ? null : sf.GetValue(resourceBuilding);
            if (storage == null) return;
            string id = (DescribeBuildable(buildable) + " " + DescribeBuildable(resourceBuilding)).ToLowerInvariant();
            var component = resourceBuilding as Component;
            if (component != null)
            {
                id += " " + component.gameObject.name.ToLowerInvariant();
                // Some names are stored on the parent Buildable rather than this component.
                Transform tr = component.transform;
                for (int i=0; tr != null && i<3; i++, tr=tr.parent) id += " " + tr.name.ToLowerInvariant();
            }
            string kind = id.Contains("water") ? "water" :
                id.Contains("food") ? "food" :
                id.Contains("fuel") ? "fuel" :
                (id.Contains("warehouse") || id.Contains("storage")) ? "warehouse" : "";
            if (kind.Length == 0) return;
            if (!storageKinds.ContainsKey(storage) || kind != "warehouse")
            {
                bool changed = !storageKinds.ContainsKey(storage) || storageKinds[storage] != kind;
                storageKinds[storage] = kind;
                if (changed) Logger.LogInfo("CAPACITY OWNER: " + kind + " storageHash=" +
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(storage) + " : " + id.Substring(0, Math.Min(125,id.Length)));
            }
            if (!storageBaseCapacities.ContainsKey(storage))
            {
                // Read raw backing capacity, never the patched getter.
                FieldInfo cf = storage.GetType().GetField("capacity", Flags);
                if (cf != null && cf.FieldType == typeof(int))
                {
                    int original = (int)cf.GetValue(storage);
                    if (original > 0) storageBaseCapacities[storage] = original;
                }
            }
        }
        // The game sometimes caches storage capacity before our ownership lookup has
        // completed. Updating the actual Storage.capacity field also makes the stock UI
        // and fullness bar use a consistent denominator, instead of only patching a getter.
        // Base values are stored separately: changing 10x to 5x never compounds.
        private readonly HashSet<object> appliedCapacityLog = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private void ApplyStorageCapacities()
        {
            foreach (var entry in storageKinds)
            {
                object storage = entry.Key;
                if (storage == null) continue;
                int original;
                if (!storageBaseCapacities.TryGetValue(storage, out original) || original <= 0) continue;
                bool enabled = entry.Value == "water" ? enableWater.Value :
                    entry.Value == "food" ? enableFood.Value :
                    entry.Value == "fuel" ? enableFuel.Value :
                    entry.Value == "warehouse" ? enableWarehouse.Value : false;
                int multiplier = enabled ? Mathf.Clamp(storageMultiplier.Value, 1, 100) : 1;
                int desired = MultiplySafe(original, multiplier);
                try
                {
                    Type storageType = storage.GetType();
                    FieldInfo field;
                    if (!capacityFieldCache.TryGetValue(storageType, out field))
                    {
                        field = storageType.GetField("capacity", Flags);
                        capacityFieldCache[storageType] = field;
                    }
                    if (field == null || field.FieldType != typeof(int)) continue;
                    int current = (int)field.GetValue(storage);
                    if (current == desired) continue;
                    field.SetValue(storage, desired);
                    if (appliedCapacityLog.Add(storage))
                        Logger.LogInfo("CAPACITY APPLY: " + entry.Value + " base=" + original +
                            " multiplier=" + multiplier + " current=" + current + " new=" + desired);
                }
                catch (Exception ex)
                {
                    if (appliedCapacityLog.Add(storage)) Logger.LogWarning("CAPACITY APPLY FAILED: " + entry.Value + " " + ex.Message);
                }
            }
        }

        private static string DescribeBuildable(object obj)
        {
            if (obj == null) return "";
            List<string> identifiers = new List<string>();
            var unityObject = obj as UnityEngine.Object;
            if (unityObject != null) identifiers.Add(unityObject.name ?? "");
            Type type = obj.GetType();
            identifiers.Add(type.Name);
            foreach (string name in new[] { "name", "title", "Title", "PrettyTitle", "ID", "Id", "Name", "buildable", "Buildable" })
            {
                try
                {
                    FieldInfo field = type.GetField(name, Flags);
                    object value = field != null ? field.GetValue(obj) : null;
                    if (field == null)
                    {
                        PropertyInfo property = type.GetProperty(name, Flags);
                        if (property != null && property.GetIndexParameters().Length == 0) value = property.GetValue(obj, null);
                    }
                    if (value is string) identifiers.Add((string)value);
                    else if (value is UnityEngine.Object) identifiers.Add(((UnityEngine.Object)value).name ?? "");
                }
                catch { }
            }
            return String.Join(" ", identifiers.ToArray());
        }

        private void DrawCapacityTab()
        {
            GUI.Label(new Rect(19, 120, 515, 26), "BUILDING CAPACITY MULTIPLIERS", headingStyle);
            GUI.Label(new Rect(19, 152, 430, 23), "Storage multiplier: " + Mathf.Clamp(storageMultiplier.Value, 1, 100) + "x", bodyStyle);
            int[] storageValues = { 1, 2, 5, 10, 25, 50 };
            for (int i=0;i<storageValues.Length;i++)
                if (GUI.Button(new Rect(19 + i*88, 183, 81, 31), storageValues[i] + "x", storageMultiplier.Value == storageValues[i] ? primaryStyle : presetStyle)) storageMultiplier.Value = storageValues[i];
            GUI.Label(new Rect(19, 228, 430, 23), "Housing multiplier: " + Mathf.Clamp(housingMultiplier.Value, 1, 20) + "x", bodyStyle);
            int[] housingValues = { 1, 2, 3, 5, 10, 20 };
            for (int i=0;i<housingValues.Length;i++)
                if (GUI.Button(new Rect(19 + i*88, 259, 81, 31), housingValues[i] + "x", housingMultiplier.Value == housingValues[i] ? primaryStyle : presetStyle)) housingMultiplier.Value = housingValues[i];
            GUI.Label(new Rect(19, 304, 400, 22), "ENABLED BUILDING TYPES", headingStyle);
            enableWater.Value = GUI.Toggle(new Rect(20, 335, 165, 24), enableWater.Value, "Water storage", bodyStyle);
            enableFood.Value = GUI.Toggle(new Rect(193, 335, 165, 24), enableFood.Value, "Food warehouse", bodyStyle);
            enableFuel.Value = GUI.Toggle(new Rect(365, 335, 165, 24), enableFuel.Value, "Fuel warehouse", bodyStyle);
            enableWarehouse.Value = GUI.Toggle(new Rect(20, 370, 245, 24), enableWarehouse.Value, "General warehouse", bodyStyle);
            enableHouse.Value = GUI.Toggle(new Rect(280, 370, 245, 24), enableHouse.Value, "Houses", bodyStyle);
            GUI.DrawTexture(new Rect(18, 414, 528, 1), borderTex);
            GUI.Label(new Rect(20, 420, 525, 52), "Capacity patches: " + capacityPatched + " | tracked storages: " + storageKinds.Count +
                "\nCheck CAPACITY TRACE/OWNER in LogOutput.log. Slider fill may still use base capacity.", mutedStyle);
        }

        private void RefreshCounts()
        {
            lastCountRefresh = Time.realtimeSinceStartup;
            if (manager == null) LocateManager();
            if (manager == null) return;
            try
            {
                MethodInfo store = managerType.GetMethods(Flags).FirstOrDefault(m => m.Name == "StoreResources" && m.GetParameters().Length == 1);
                if (store == null) return;
                Type blockType = store.GetParameters()[0].ParameterType.GetElementType();
                Type resourceType = GetResourceParameterType(blockType);
                foreach (string name in names)
                {
                    object resource;
                    if (TryGetResource(resourceType, name, out resource))
                    {
                        int? current = ReadCount(resource);
                        counts[name] = current.HasValue ? current.Value.ToString("N0") : "—";
                    }
                }
            }
            catch (Exception ex) { Logger.LogWarning("Could not refresh resource counts: " + ex.Message); }
        }

        private void LocateManager()
        {
            if (managerType == null)
                managerType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(SafeTypes).FirstOrDefault(t => t.Name == "CityResourceManager");
            if (managerType == null) { status = "CityResourceManager not found"; return; }
            try
            {
                manager = UnityEngine.Object.FindObjectOfType(managerType);
                if (manager == null)
                {
                    foreach (string member in new[] { "Instance", "instance", "Current", "current" })
                    {
                        PropertyInfo prop = managerType.GetProperty(member, Flags);
                        if (prop != null && prop.GetGetMethod(true).IsStatic) { manager = prop.GetValue(null, null); if (manager != null) break; }
                        FieldInfo field = managerType.GetField(member, Flags);
                        if (field != null && field.IsStatic) { manager = field.GetValue(null); if (manager != null) break; }
                    }
                }
                if (manager == null) { status = "Load a city first, then Rescan"; return; }
                if (!inspected && verboseDiagnostics.Value)
                {
                    foreach (var method in managerType.GetMethods(Flags).Where(m => m.Name.IndexOf("Resource", StringComparison.OrdinalIgnoreCase) >= 0))
                        Logger.LogInfo("RESOURCE API: " + method);
                    foreach (var field in managerType.GetFields(Flags).Where(f => f.Name.IndexOf("resource", StringComparison.OrdinalIgnoreCase) >= 0))
                        Logger.LogInfo("RESOURCE FIELD: " + field.FieldType + " " + field.Name);
                    InspectResourceTypes();
                    inspected = true;
                }
                status = "CityResourceManager found";
            }
            catch (Exception ex) { status = ex.Message; Logger.LogError(ex); }
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
            catch { return new Type[0]; }
        }

        // Unity game resource system: StoreResources(ref ResourceBlock).
        // We never silently invoke AddVirtualResources (which is not persistent inventory).
        private void Add(string resourceName)
        {
            int amount;
            if (!int.TryParse(amountText, out amount) || amount < 1 || amount > 1000000)
            { status = "Amount must be 1 to 1,000,000"; return; }
            if (manager == null) LocateManager();
            if (manager == null) return;
            try
            {
                MethodInfo store = managerType.GetMethods(Flags).FirstOrDefault(m =>
                    m.Name == "StoreResources" && m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType.IsByRef);
                if (store == null) { status = "StoreResources(ref ...) not found"; return; }
                Type blockType = store.GetParameters()[0].ParameterType.GetElementType();
                object block;
                string reason;
                if (!TryMakeBlock(blockType, resourceName, amount, out block, out reason))
                {
                    status = "Cannot construct ResourceBlock; check log";
                    Logger.LogWarning("RESOURCE BUILD " + resourceName + ": " + reason);
                    return;
                }
                // A storage method may limit insertion to remaining building capacity.
                // Do not claim the displayed amount was added without reading it back.
                object resourceObject;
                int? before = null, after = null;
                if (TryGetResource(GetResourceParameterType(blockType), resourceName, out resourceObject))
                    before = ReadCount(resourceObject);
                object[] args = { block };
                store.Invoke(manager, args);
                if (resourceObject != null) after = ReadCount(resourceObject);
                lastCountRefresh = -100f;
                if (before.HasValue && after.HasValue)
                    status = resourceName + ": " + before.Value + " -> " + after.Value + " (requested +" + amount + ")";
                else
                    status = "StoreResources called for " + resourceName + "; verify inventory";
                Logger.LogInfo("RESOURCE RESULT: " + status + "; storage capacity may apply");
            }
            catch (Exception ex)
            {
                status = "Storage call failed; see LogOutput.log";
                Logger.LogError("RESOURCE ERROR " + resourceName + ": " + ex);
            }
        }

        private bool TryMakeBlock(Type blockType, string name, int amount, out object block, out string reason)
        {
            block = null; reason = "No matching Resource asset found for the ResourceBlock(Resource, int) constructor";
            // Prefer explicit (Resource, count) constructors on ResourceBlock.
            foreach (var ctor in blockType.GetConstructors(Flags))
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length != 2) continue;
                object resource;
                if (IsInteger(parameters[1].ParameterType) && TryGetResource(parameters[0].ParameterType, name, out resource))
                {
                    block = ctor.Invoke(new[] { resource, Convert.ChangeType(amount, parameters[1].ParameterType) });
                    return true;
                }
            }
            Type amountType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes).FirstOrDefault(t => t.Name == "ResourceAmount");
            object entry;
            if (amountType != null && TryMakeAmount(amountType, name, amount, out entry))
            {
                Type arrayType = amountType.MakeArrayType();
                Array arr = Array.CreateInstance(amountType, 1); arr.SetValue(entry, 0);
                foreach (var ctor in blockType.GetConstructors(Flags))
                {
                    var pars = ctor.GetParameters();
                    if (pars.Length == 1 && pars[0].ParameterType.IsAssignableFrom(arrayType))
                    { block = ctor.Invoke(new object[] { arr }); return true; }
                    if (pars.Length == 1 && pars[0].ParameterType == amountType)
                    { block = ctor.Invoke(new[] { entry }); return true; }
                }
                // Use default struct/class layout only if there is a clearly matching ResourceAmount collection.
                try
                {
                    object candidate = Activator.CreateInstance(blockType, true);
                    foreach (var f in blockType.GetFields(Flags))
                    {
                        if (f.IsStatic || f.IsInitOnly) continue;
                        if (f.FieldType == arrayType)
                        { f.SetValue(candidate, arr); block = candidate; return true; }
                        if (f.FieldType == amountType)
                        { f.SetValue(candidate, entry); block = candidate; return true; }
                    }
                    foreach (var prop in blockType.GetProperties(Flags))
                    {
                        if (!prop.CanWrite || prop.GetIndexParameters().Length != 0) continue;
                        if (prop.PropertyType == arrayType)
                        { prop.SetValue(candidate, arr, null); block = candidate; return true; }
                    }
                }
                catch (Exception ex) { reason = "Default-layout attempt: " + ex.Message; }
            }
            // Log discoverable game resource assets (only on failed matching).
            try
            {
                Type resourceType = GetResourceParameterType(blockType);
                if (resourceType != null)
                {
                    var loaded = UnityEngine.Resources.FindObjectsOfTypeAll(resourceType);
                    Logger.LogWarning("RESOURCE ASSETS: " + loaded.Length + " loaded; requested " + name);
                    foreach (UnityEngine.Object obj in loaded.Take(50))
                    {
                        if (obj == null) continue;
                        var labels = new List<string>();
                        foreach (string key in new[] { "Title", "PrettyTitle", "ID" })
                        {
                            try
                            {
                                PropertyInfo p = resourceType.GetProperty(key, Flags);
                                if (p != null && p.GetIndexParameters().Length == 0)
                                    labels.Add(key + "=" + Convert.ToString(p.GetValue(obj, null)));
                            }
                            catch { }
                        }
                        Logger.LogInfo("RESOURCE ASSET: name=" + obj.name + " " + string.Join(" ", labels.ToArray()));
                    }
                }
            }
            catch (Exception ex) { Logger.LogWarning("RESOURCE ASSET SCAN: " + ex.Message); }
            return false;
        }

        private static bool IsInteger(Type type) { return type == typeof(int) || type == typeof(uint) || type == typeof(short) || type == typeof(long); }

        private Type GetResourceParameterType(Type blockType)
        {
            ConstructorInfo ctor = blockType.GetConstructors(Flags).FirstOrDefault(c =>
                c.GetParameters().Length == 2 && c.GetParameters()[0].ParameterType.Name == "Resource" &&
                IsInteger(c.GetParameters()[1].ParameterType));
            return ctor == null ? null : ctor.GetParameters()[0].ParameterType;
        }

        private int? ReadCount(object resource)
        {
            if (resource == null || manager == null) return null;
            try
            {
                MethodInfo get = managerType.GetMethods(Flags).FirstOrDefault(m =>
                    m.Name == "GetResourceCount" && m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == resource.GetType());
                if (get == null) return null;
                return Convert.ToInt32(get.Invoke(manager, new[] { resource }));
            }
            catch (Exception e) { Logger.LogWarning("Unable to read resource count: " + e.Message); return null; }
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        private static bool ResourceMatches(UnityEngine.Object obj, Type type, string name)
        {
            string target = Normalize(name);
            var labels = new List<string> { obj.name };
            foreach (string member in new[] { "Title", "PrettyTitle", "ID", "title" })
            {
                try
                {
                    PropertyInfo p = type.GetProperty(member, Flags);
                    if (p != null && p.GetIndexParameters().Length == 0 && p.PropertyType == typeof(string))
                        labels.Add((string)p.GetValue(obj, null));
                    FieldInfo f = type.GetField(member, Flags);
                    if (f != null && f.FieldType == typeof(string)) labels.Add((string)f.GetValue(obj));
                }
                catch { }
            }
            return labels.Any(label => {
                string n = Normalize(label);
                return n == target || n == "resource" + target || n == target + "resource";
            });
        }

        private static bool TryGetResource(Type type, string name, out object value)
        {
            value = null;
            if (type == null) return false;
            if (type == typeof(string)) { value = name; return true; }
            if (type.IsEnum)
            {
                var match = Enum.GetNames(type).FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (match == null) return false;
                value = Enum.Parse(type, match); return true;
            }
            if (type.Name == "Resource" && typeof(UnityEngine.ScriptableObject).IsAssignableFrom(type))
            {
                var loaded = UnityEngine.Resources.FindObjectsOfTypeAll(type);
                foreach (UnityEngine.Object obj in loaded)
                    if (obj != null && ResourceMatches(obj, type, name))
                    { value = obj; return true; }
            }
            return false;
        }

        private static bool TryMakeAmount(Type type, string name, int amount, out object entry)
        {
            entry = null;
            foreach (var ctor in type.GetConstructors(Flags))
            {
                var pars = ctor.GetParameters();
                if (pars.Length != 2) continue;
                object resource;
                if (IsInteger(pars[1].ParameterType) && TryGetResource(pars[0].ParameterType, name, out resource))
                { entry = ctor.Invoke(new[] { resource, Convert.ChangeType(amount, pars[1].ParameterType) }); return true; }
            }
            try
            {
                object candidate = Activator.CreateInstance(type, true);
                bool gotResource = false, gotAmount = false;
                foreach (var f in type.GetFields(Flags))
                {
                    if (f.IsStatic || f.IsInitOnly) continue;
                    if (!gotResource && (f.Name.IndexOf("resource", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        object resource;
                        if (TryGetResource(f.FieldType, name, out resource)) { f.SetValue(candidate, resource); gotResource = true; }
                    }
                    if (!gotAmount && IsInteger(f.FieldType) &&
                        (f.Name.IndexOf("amount", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("count", StringComparison.OrdinalIgnoreCase) >= 0))
                    { f.SetValue(candidate, Convert.ChangeType(amount, f.FieldType)); gotAmount = true; }
                }
                if (gotAmount && gotResource) { entry = candidate; return true; }
            }
            catch { }
            return false;
        }

        private void InspectResourceTypes()
        {
            if (diagnosticsWritten) return;
            diagnosticsWritten = true;
            foreach (var t in AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeTypes)
                .Where(t => t.Name == "ResourceBlock" || t.Name == "ResourceAmount" || t.Name == "Resource"))
            {
                Logger.LogInfo("RESOURCE TYPE: " + t.FullName + " (" + t.BaseType + ")");
                foreach (var ctor in t.GetConstructors(Flags)) Logger.LogInfo("RESOURCE CTOR: " + ctor);
                foreach (var field in t.GetFields(Flags).Where(f => !f.IsStatic))
                    Logger.LogInfo("RESOURCE MEMBER: " + t.Name + "." + field.Name + " : " + field.FieldType);
                foreach (var prop in t.GetProperties(Flags).Where(p => p.GetIndexParameters().Length == 0))
                    Logger.LogInfo("RESOURCE PROPERTY: " + t.Name + "." + prop.Name + " : " + prop.PropertyType);
            }
        }

        // v0.6.1: use game-provided construction override flags, not broad resource patches.
        // Toggle booleans are off by default. The actual behavior of these game flags
        // must be verified in-game with a backup save before calling them stable.
        private void InstallConstructionOverrides()
        {
            try
            {
                if (harmony == null) harmony = new Harmony("id.sarakiel.allwillfall.resourcetweaks.ui");
                Type cityConstruction = AccessTools.TypeByName("CityConstruction");
                if (cityConstruction == null)
                {
                    Logger.LogWarning("CONSTRUCTION PATCH: CityConstruction unavailable");
                    return;
                }
                string[] names = { "get_InstantBuildOverride", "get_FreeBuildOverride" };
                string[] callbacks = { "InstantBuildOverridePostfix", "FreeBuildOverridePostfix" };
                for (int i = 0; i < names.Length; i++)
                {
                    MethodInfo getter = AccessTools.Method(cityConstruction, names[i], Type.EmptyTypes);
                    MethodInfo callback = typeof(ResourceTweaks).GetMethod(callbacks[i], BindingFlags.Static | BindingFlags.NonPublic);
                    if (getter == null || getter.ReturnType != typeof(bool) || callback == null)
                    {
                        Logger.LogWarning("CONSTRUCTION PATCH: incompatible or missing " + names[i]);
                        continue;
                    }
                    try
                    {
                        harmony.Patch(getter, postfix: new HarmonyMethod(callback));
                        constructionOverridesPatched++;
                        Logger.LogInfo("CONSTRUCTION PATCH: CityConstruction." + names[i]);
                    }
                    catch (Exception ex) { Logger.LogWarning("CONSTRUCTION PATCH FAILED: " + names[i] + ": " + ex.Message); }
                }
                Logger.LogInfo("CONSTRUCTION: " + constructionOverridesPatched + "/2 override getters patched");
            }
            catch (Exception ex) { Logger.LogError("CONSTRUCTION PATCH ERROR: " + ex); }
        }

        private static void InstantBuildOverridePostfix(ref bool __result)
        {
            if (active != null && active.instantConstructionRequested != null && active.instantConstructionRequested.Value)
                __result = true;
        }

        private static void FreeBuildOverridePostfix(ref bool __result)
        {
            if (active != null && active.freeConstructionRequested != null && active.freeConstructionRequested.Value)
                __result = true;
        }

        // v0.6.2 direct hooks. ScriptsAssembly.dll is referenced at COMPILE TIME,
        // and is ALWAYS loaded from the game's own Managed directory at runtime.
        // No game binaries are bundled with the mod.
        private void InstallConstructionDirectPatches()
        {
            if (harmony == null) harmony = new Harmony("id.sarakiel.allwillfall.resourcetweaks.ui");
            DirectPatch(typeof(CityConstruction), "IsInstantlyBuildable", new [] { typeof(Buildable) }, "IsInstantlyBuildablePostfix");
            DirectPatch(typeof(CityConstruction), "CreateConstruction", null, "CreateConstructionPostfix");
            DirectPatch(typeof(ConstructionBuildable), "get_ConstructionResourcesEnoughToConstruct", Type.EmptyTypes, "ResourcesEnoughPostfix");
            DirectPatch(typeof(ConstructionBuildable), "GetRemainingRequiredResources", Type.EmptyTypes, "RemainingResourcesPostfix");
            DirectPatch(typeof(ConstructionBuildable), "NeedsResourceBringer", Type.EmptyTypes, "NeedsResourceBringerPostfix");
            Logger.LogInfo("CONSTRUCTION DIRECT: " + constructionDirectPatched + "/5 hooks patched");
        }

        private void DirectPatch(Type type, string methodName, Type[] parameterTypes, string callback)
        {
            try
            {
                MethodInfo target = parameterTypes == null ? type.GetMethods(Flags).FirstOrDefault(m => m.Name == methodName) : AccessTools.Method(type, methodName, parameterTypes);
                MethodInfo post = typeof(ResourceTweaks).GetMethod(callback, BindingFlags.NonPublic | BindingFlags.Static);
                if (target == null || post == null) { Logger.LogWarning("CONSTRUCTION DIRECT: missing " + type.Name + "." + methodName); return; }
                harmony.Patch(target, postfix: new HarmonyMethod(post));
                constructionDirectPatched++;
                Logger.LogInfo("CONSTRUCTION DIRECT PATCH: " + type.Name + "." + methodName);
            }
            catch (Exception ex) { Logger.LogWarning("CONSTRUCTION DIRECT PATCH FAILED: " + type.Name + "." + methodName + " " + ex); }
        }

        private static bool WantsFree()
        {
            return active != null && active.freeConstructionRequested != null && active.freeConstructionRequested.Value;
        }
        private static bool WantsInstant()
        {
            return active != null && active.instantConstructionRequested != null && active.instantConstructionRequested.Value;
        }
        private static void IsInstantlyBuildablePostfix(ref bool __result)
        {
            if (WantsInstant()) __result = true;
        }
        private static void ResourcesEnoughPostfix(ref bool __result)
        {
            if (WantsFree()) __result = true;
        }
        private static void RemainingResourcesPostfix(ref ResourceBlock __result)
        {
            if (WantsFree()) __result = new ResourceBlock();
        }
        private static void NeedsResourceBringerPostfix(ref bool __result)
        {
            if (WantsFree()) __result = false;
        }
        private static void CreateConstructionPostfix(CityConstruction __instance, ConstructionBuildable __result)
        {
            if (!WantsInstant() || active == null || __instance == null || __result == null) return;
            try
            {
                if (!__result.ConstructionConstructing || __result.buildable == null) return;
                active.EnqueueConstruction(__instance, __result.buildable);
            }
            catch (Exception ex) { active.Logger.LogWarning("CONSTRUCTION QUEUE: " + ex.Message); }
        }
        private void EnqueueConstruction(CityConstruction city, Buildable buildable)
        {
            if (queuedConstruction.Any(q => object.ReferenceEquals(q.buildable, buildable))) return;
            queuedConstruction.Add(new QueuedBuild { city = city, buildable = buildable, readyAt = Time.unscaledTime + 0.12f });
            Logger.LogInfo("CONSTRUCTION QUEUED: " + buildable.ToString());
        }
        private void ProcessQueuedConstruction()
        {
            if (queuedConstruction.Count == 0) return;
            if (!WantsInstant()) { queuedConstruction.Clear(); return; }
            for (int i = queuedConstruction.Count - 1; i >= 0; i--)
            {
                QueuedBuild queued = queuedConstruction[i];
                if (Time.unscaledTime < queued.readyAt) continue;
                queuedConstruction.RemoveAt(i);
                try
                {
                    if (queued.city == null || queued.buildable == null) continue;
                    ConstructionBuildable job = queued.city.GetConstruction(queued.buildable);
                    if (job == null || !job.ConstructionConstructing) continue;
                    queued.city.ForceCompleteConstructions(queued.buildable);
                    Logger.LogInfo("CONSTRUCTION FORCED COMPLETE: " + queued.buildable.ToString());
                }
                catch (Exception ex) { Logger.LogError("CONSTRUCTION FORCE FAILED: " + ex); }
            }
        }


        // v0.7.1: patch only CityResearch, never global resource consumption.
        // Runtime reflection avoids adding new compile-time dependencies to Research types.
        private void InstallResearchHooks()
        {
            researchManagerType = AccessTools.TypeByName("CityResearch");
            if (researchManagerType == null) { Logger.LogWarning("RESEARCH: CityResearch type not found"); return; }
            Type researchType = AccessTools.TypeByName("Research");
            if (researchType == null) { Logger.LogWarning("RESEARCH: Research type not found"); return; }
            if (harmony == null) harmony = new Harmony("id.sarakiel.allwillfall.resourcetweaks.ui");
            getCurrentResearchMethod = AccessTools.Method(researchManagerType, "GetCurrentOnGoingResearch", Type.EmptyTypes);
            forceCompleteResearchMethod = AccessTools.Method(researchManagerType, "ForceCompleteResearch", new [] { researchType });
            if (getCurrentResearchMethod == null || forceCompleteResearchMethod == null)
                Logger.LogWarning("RESEARCH: native current/force-complete API missing or incompatible; Instant Research disabled");
            else Logger.LogInfo("RESEARCH: native active research completion API ready");
            ResearchPatch("PayForResearch", new [] { researchType, typeof(ResourceBlock) }, "ResearchPaymentPrefix", true);
            ResearchPatch("GetResourcesToPayForTicks", null, "ResearchTickCostPostfix", false);
            ResearchPatch("LackingResourcesToFinish", Type.EmptyTypes, "ResearchLackingResourcesPostfix", false);
            Type researchBuildingType = AccessTools.TypeByName("ResearchBuilding");
            if (researchBuildingType != null)
                ResearchPatchOnType(researchBuildingType, "ResourcesNeededToFinishResearch", Type.EmptyTypes, "ResearchTickCostPostfix", false);
            Logger.LogInfo("RESEARCH: " + researchHooksPatched + "/4 research-only material hooks installed");
        }

        private void ResearchPatch(string name, Type[] signature, string callback, bool prefix)
        {
            ResearchPatchOnType(researchManagerType, name, signature, callback, prefix);
        }

        private void ResearchPatchOnType(Type owner, string name, Type[] signature, string callback, bool prefix)
        {
            try
            {
                MethodInfo target = signature == null
                    ? owner.GetMethods(Flags).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == 2)
                    : AccessTools.Method(owner, name, signature);
                MethodInfo patch = typeof(ResourceTweaks).GetMethod(callback, BindingFlags.NonPublic | BindingFlags.Static);
                if (target == null || patch == null) { Logger.LogWarning("RESEARCH PATCH: unavailable " + owner.Name + "." + name); return; }
                if (prefix) harmony.Patch(target, prefix: new HarmonyMethod(patch));
                else harmony.Patch(target, postfix: new HarmonyMethod(patch));
                researchHooksPatched++;
                Logger.LogInfo("RESEARCH PATCH: " + owner.Name + "." + name);
            }
            catch (Exception ex) { Logger.LogWarning("RESEARCH PATCH FAILED: " + owner.Name + "." + name + ": " + ex.Message); }
        }

        private static bool WantsFreeResearch()
        {
            return active != null && active.freeResearchRequested != null && active.freeResearchRequested.Value;
        }
        private static void ResearchPaymentPrefix(ref ResourceBlock __1)
        {
            if (WantsFreeResearch()) __1 = new ResourceBlock();
        }
        private static void ResearchTickCostPostfix(ref ResourceBlock __result)
        {
            if (WantsFreeResearch()) __result = new ResourceBlock();
        }
        private static void ResearchLackingResourcesPostfix(ref bool __result)
        {
            if (WantsFreeResearch()) __result = false;
        }

        private void ScanResearchAPI()
        {
            if (researchAuditWritten) return;
            researchAuditWritten = true;
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ScriptsAssembly");
                if (asm == null) { Logger.LogWarning("RESEARCH API: ScriptsAssembly missing"); return; }
                foreach (string typeName in new [] { "CityResearch", "ResearchBuilding", "CityTechnology", "Technology", "ResearchProgress" })
                {
                    Type t = SafeTypes(asm).FirstOrDefault(x => x != null && x.Name == typeName);
                    if (t == null) { Logger.LogInfo("RESEARCH TYPE: " + typeName + " not present"); continue; }
                    Logger.LogInfo("RESEARCH TYPE: " + t.FullName + " base=" + (t.BaseType != null ? t.BaseType.Name : "none"));
                    foreach (FieldInfo f in t.GetFields(Flags))
                        if (!f.IsStatic) Logger.LogInfo("RESEARCH FIELD: " + t.Name + "." + f.Name + " : " + f.FieldType);
                    foreach (MethodInfo m in t.GetMethods(Flags).Where(m => m.DeclaringType == t &&
                        (t.Name == "CityResearch" || m.Name.IndexOf("Research", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Progress", StringComparison.OrdinalIgnoreCase) >= 0)))
                        Logger.LogInfo("RESEARCH METHOD: " + t.Name + "." + m);
                }
                Logger.LogInfo("RESEARCH API SCAN COMPLETE; no research resources or progress modified");
            }
            catch (Exception ex) { Logger.LogWarning("RESEARCH API SCAN FAILED: " + ex.Message); researchAuditWritten = false; }
        }

        private void ResearchPeriodicCheck()
        {
            if (instantResearchRequested == null || !instantResearchRequested.Value) return;
            if (Time.unscaledTime < nextResearchCheckAt) return;
            nextResearchCheckAt = Time.unscaledTime + 0.5f;
            if (researchManagerType == null || getCurrentResearchMethod == null || forceCompleteResearchMethod == null) return;
            try
            {
                UnityEngine.Object unityResearch = researchManager as UnityEngine.Object;
                if (unityResearch != null && !unityResearch) researchManager = null;
                if (researchManager == null)
                {
                    if (Time.unscaledTime < nextResearchDiscoveryAt) return;
                    nextResearchDiscoveryAt = Time.unscaledTime + 5f;
                    researchManager = UnityEngine.Object.FindObjectOfType(researchManagerType);
                    if (researchManager == null) return;
                }
                object current = getCurrentResearchMethod.Invoke(researchManager, null);
                if (current == null) { lastCompletedResearch = null; return; }
                // A cooldown prevents repeated completion if the game leaves the same
                // research as 'current' for one or more simulation ticks.
                if (object.ReferenceEquals(current, lastCompletedResearch) && Time.unscaledTime - lastResearchCompletionAt < 4f) return;
                lastCompletedResearch = current;
                lastResearchCompletionAt = Time.unscaledTime;
                forceCompleteResearchMethod.Invoke(researchManager, new [] { current });
                Logger.LogInfo("RESEARCH COMPLETE: native ForceCompleteResearch executed for active technology");
            }
            catch (Exception ex)
            {
                if (Time.unscaledTime >= nextResearchWarningAt)
                {
                    nextResearchWarningAt = Time.unscaledTime + 30f;
                    Logger.LogWarning("RESEARCH COMPLETION FAILED: " + ex.GetBaseException().Message);
                }
            }
        }

        // v0.8.3: Yield repeats ONLY the completed output handoff. It never
        // invokes ConsumeRequirement or globally alters CityResourceManager.
        // Re-entry guard prevents a nested Harmony postfix from multiplying
        // the same batch again. No reflection or scene searches on each tick.
        private void InstallProductionHooks()
        {
            if (harmony == null) harmony = new Harmony("id.sarakiel.allwillfall.resourcetweaks.ui");
            try
            {
                producerProduceMethod = AccessTools.Method(typeof(ResourceProducer), "Produce", new [] { typeof(ResourceBlock) });
                MethodInfo prefix = AccessTools.Method(typeof(ResourceTweaks), "CaptureProductionBatch");
                MethodInfo postfix = AccessTools.Method(typeof(ResourceTweaks), "RepeatProductionBatch");
                if (producerProduceMethod != null && prefix != null && postfix != null)
                {
                    harmony.Patch(producerProduceMethod, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                    yieldHookReady = true;
                    Logger.LogInfo("PRODUCTION YIELD PATCH: ResourceProducer.Produce(ResourceBlock) output replay ready");
                }
                else Logger.LogWarning("PRODUCTION YIELD: output method/signature missing; no changes applied");
            }
            catch (Exception ex) { Logger.LogWarning("PRODUCTION YIELD HOOK FAILED: " + ex.GetBaseException().Message); }

            // The v0.8.2 runtime scan confirmed WorkProvider.get_WorkInterval() -> float,
            // and WorkProvider.producers -> List<IWorkProducer>. Do not touch general
            // work providers, including labs or jobs that have no ResourceProducer.
            try
            {
                MethodInfo getter = AccessTools.Method(typeof(WorkProvider), "get_WorkInterval", Type.EmptyTypes);
                MethodInfo postfix = AccessTools.Method(typeof(ResourceTweaks), "ProductionIntervalPostfix");
                workProviderProducersField = AccessTools.Field(typeof(WorkProvider), "producers");
                if (getter != null && getter.ReturnType == typeof(float) &&
                    postfix != null && workProviderProducersField != null)
                {
                    harmony.Patch(getter, postfix: new HarmonyMethod(postfix));
                    speedHookReady = true;
                    Logger.LogInfo("PRODUCTION SPEED PATCH: WorkProvider.get_WorkInterval() for ResourceProducer jobs only");
                }
                else Logger.LogWarning("PRODUCTION SPEED: WorkInterval getter or producers field unavailable; no speed changes");
            }
            catch (Exception ex) { Logger.LogWarning("PRODUCTION SPEED PATCH FAILED: " + ex.GetBaseException().Message); }
        }

        // This hot-path patch is allocation-free once an individual provider has
        // been recognized. Cache positive matches only: during scene loading the
        // producers list may still be empty and could be populated later.
        private static void ProductionIntervalPostfix(WorkProvider __instance, ref float __result)
        {
            ResourceTweaks mod = active;
            if (mod == null || !mod.speedHookReady || __instance == null ||
                !mod.productionSpeedEnabled.Value) return;
            int factor = Math.Max(1, Math.Min(25, mod.productionSpeedMultiplier.Value));
            if (factor <= 1 || __result <= 0f) return;
            if (!mod.IsResourceProducingWorkProvider(__instance)) return;
            __result = Math.Max(0.1f, __result / factor);
            mod.productionSpeedCalls++;
        }

        private bool IsResourceProducingWorkProvider(WorkProvider provider)
        {
            bool cached;
            if (resourceWorkProviderCache.TryGetValue(provider, out cached) && cached) return true;
            try
            {
                object value = workProviderProducersField.GetValue(provider);
                IEnumerable producers = value as IEnumerable;
                if (producers == null) return false;
                foreach (object producer in producers)
                {
                    if (producer is ResourceProducer)
                    {
                        resourceWorkProviderCache[provider] = true;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!warnedSpeedProviderRead)
                {
                    warnedSpeedProviderRead = true;
                    Logger.LogWarning("PRODUCTION SPEED provider inspection failed: " + ex.GetBaseException().Message);
                }
            }
            return false;
        }

        private static void CaptureProductionBatch(ResourceBlock __0, out ResourceBlock __state)
        {
            __state = __0;
        }

        private static void RepeatProductionBatch(ResourceProducer __instance, ResourceBlock __state)
        {
            ResourceTweaks mod = active;
            if (replayingProduction || mod == null || !mod.yieldHookReady ||
                !mod.productionYieldEnabled.Value || __instance == null) return;
            int factor = Math.Max(1, Math.Min(25, mod.productionYieldMultiplier.Value));
            if (factor <= 1 || producerProduceMethod == null) return;
            replayingProduction = true;
            try
            {
                for (int i = 1; i < factor; i++)
                {
                    producerProduceMethod.Invoke(__instance, new object[] { __state });
                    mod.productionYieldCalls++;
                }
            }
            catch (Exception ex)
            {
                if (!mod.warnedProductionReplay)
                {
                    mod.warnedProductionReplay = true;
                    mod.Logger.LogWarning("PRODUCTION YIELD replay failed; check output/stock: " + ex.GetBaseException().Message);
                }
            }
            finally { replayingProduction = false; }
        }

        // Production API discovery: deliberately read-only until exact output and
        // timer hooks are identified. Never patch global StoreResources or worker
        // efficiency: those would also alter consumption or unrelated systems.
        private void ScanProductionAPI()
        {
            if (productionAuditWritten) return;
            productionAuditWritten = true;
            try
            {
                string[] targets = { "ResourceProducer", "BuildingProducer", "ProductionStats",
                    "IWorkProducer", "IProductionRequirementProvider", "ProductionItemUI",
                    "ResourceBuilding", "CityResourceManager", "ProductionIndicator",
                    "WorkProvider", "Workplace", "WorkProvider+Data", "Workplace+Data", "ResourceBlock" };
                int total = 0;
                foreach (string typeName in targets)
                {
                    Type t = AccessTools.TypeByName(typeName);
                    if (t == null) { Logger.LogInfo("PRODUCTION TYPE MISSING: " + typeName); continue; }
                    Logger.LogInfo("PRODUCTION TYPE: " + t.FullName + " base=" + t.BaseType);
                    foreach (FieldInfo f in t.GetFields(Flags))
                    {
                        if (f.IsStatic) continue;
                        Logger.LogInfo("PRODUCTION FIELD: " + t.Name + "." + f.Name + " : " + f.FieldType);
                    }
                    foreach (PropertyInfo property in t.GetProperties(Flags))
                    {
                        if (property.GetIndexParameters().Length != 0) continue;
                        Logger.LogInfo("PRODUCTION PROPERTY: " + t.Name + "." + property.Name + " : " + property.PropertyType);
                    }
                    foreach (MethodInfo m in t.GetMethods(Flags | BindingFlags.DeclaredOnly))
                    {
                        if (m.IsSpecialName && !m.Name.StartsWith("get_")) continue;
                        Logger.LogInfo("PRODUCTION METHOD: " + t.Name + "." + m);
                        total++;
                    }
                }
                Logger.LogInfo("PRODUCTION API SCAN COMPLETE: " + total + " methods; read-only, no output or timing modified");
            }
            catch (Exception ex)
            {
                productionAuditWritten = false;
                Logger.LogWarning("PRODUCTION API SCAN FAILED: " + ex.GetBaseException().Message);
            }
        }

        private void DrawProductionTab()
        {
            GUI.Label(new Rect(19, 121, 520, 29), "PRODUCTION TWEAKS  /  GAME OVERRIDES", headingStyle);
            GUI.Label(new Rect(19, 154, 522, 47),
                "Independent output yield and cycle speed preferences.\n" +
                "Yield: final output only. Speed: production-only work interval.", bodyStyle);
            bool yield = productionYieldEnabled.Value;
            bool oldYieldEnabled = yield, oldSpeedEnabled = productionSpeedEnabled.Value;
            int oldYield = productionYieldMultiplier.Value, oldSpeed = productionSpeedMultiplier.Value;
            bool speed = productionSpeedEnabled.Value;
            GUI.Label(new Rect(23, 209, 330, 29), "Production Yield", bodyStyle);
            if (GUI.Button(new Rect(413, 207, 128, 30), yield ? "ON [X]" : "OFF", yield ? primaryStyle : buttonStyle)) yield = !yield;
            GUI.Label(new Rect(23, 252, 330, 29), "Production Speed", bodyStyle);
            if (GUI.Button(new Rect(413, 250, 128, 30), speed ? "ON [X]" : "OFF", speed ? primaryStyle : buttonStyle)) speed = !speed;
            GUI.Label(new Rect(23, 296, 110, 24), "Yield / Speed", mutedStyle);
            int[] choices = { 1, 2, 5, 10, 25 };
            for (int i = 0; i < choices.Length; i++)
            {
                int multiplier = choices[i];
                float x = 175 + i * 73;
                if (GUI.Button(new Rect(x, 293, 67, 26), multiplier + "x", productionYieldMultiplier.Value == multiplier ? primaryStyle : presetStyle))
                    productionYieldMultiplier.Value = multiplier;
                if (GUI.Button(new Rect(x, 326, 67, 26), multiplier + "x", productionSpeedMultiplier.Value == multiplier ? primaryStyle : presetStyle))
                    productionSpeedMultiplier.Value = multiplier;
            }
            GUI.Label(new Rect(23, 325, 145, 26), "Speed presets", mutedStyle);
            if (yield != productionYieldEnabled.Value || speed != productionSpeedEnabled.Value)
            {
                productionYieldEnabled.Value = yield;
                productionSpeedEnabled.Value = speed;
            }
            if (oldYield != productionYieldMultiplier.Value || oldSpeed != productionSpeedMultiplier.Value ||
                yield != oldYieldEnabled || speed != oldSpeedEnabled)
            {
                try { Config.Save(); } catch (Exception ex) { Logger.LogWarning("PRODUCTION SETTINGS SAVE FAILED: " + ex.Message); }
            }
            if (GUI.Button(new Rect(19, 369, 526, 40), "SCAN PRODUCTION API / WRITE LOG", primaryStyle))
            {
                productionAuditWritten = false;
                ScanProductionAPI();
            }
            GUI.DrawTexture(new Rect(18, 422, 528, 1), borderTex);
            GUI.Label(new Rect(19, 430, 526, 62),
                "v0.8.3.1 production hooks: yield=" + (yieldHookReady ? "READY" : "UNAVAILABLE") +
                " speed=" + (speedHookReady ? "READY" : "UNAVAILABLE") + "\n" +
                "Applied calls (since load): yield=" + productionYieldCalls + " speed=" + productionSpeedCalls + "\n" +
                "Use backup saves; report output amounts and cycle times.", mutedStyle);
        }

        private void DrawResearchTab()
        {
            GUI.Label(new Rect(19, 121, 520, 29), "RESEARCH TWEAKS  /  GAME OVERRIDES", headingStyle);
            GUI.Label(new Rect(19, 155, 520, 53),
                "Independent research settings, saved to BepInEx config.\n" +
                "Native completion + research-only material cost override.", bodyStyle);
            GUI.Label(new Rect(23, 213, 365, 34), "Instant Research", bodyStyle);
            bool instant = instantResearchRequested.Value;
            if (GUI.Button(new Rect(413, 211, 128, 34), instant ? "ON  [X]" : "OFF", instant ? primaryStyle : buttonStyle)) instant = !instant;
            GUI.Label(new Rect(23, 260, 365, 34), "Free Research / No Material Cost", bodyStyle);
            bool free = freeResearchRequested.Value;
            if (GUI.Button(new Rect(413, 258, 128, 34), free ? "ON  [X]" : "OFF", free ? primaryStyle : buttonStyle)) free = !free;
            if (instant != instantResearchRequested.Value || free != freeResearchRequested.Value)
            {
                instantResearchRequested.Value = instant;
                freeResearchRequested.Value = free;
                try { Config.Save(); Logger.LogInfo("RESEARCH SETTINGS SAVED: instant=" + instant + " free=" + free); }
                catch (Exception ex) { Logger.LogWarning("RESEARCH CONFIG SAVE FAILED: " + ex.Message); }
            }
            if (GUI.Button(new Rect(19, 322, 526, 39), "SCAN RESEARCH API  /  WRITE LOG", primaryStyle))
            {
                researchAuditWritten = false;
                ScanResearchAPI();
            }
            GUI.DrawTexture(new Rect(18, 387, 528, 1), borderTex);
            GUI.Label(new Rect(19, 399, 526, 81),
                "RESEARCH v0.8.1  —  direct research hooks.\n" +
                "Independent ON/OFF settings are saved automatically.\n" +
                "Test on a backup save; report any incomplete unlocks.", mutedStyle);
        }

        private void DrawConstructionTab()
        {
            GUI.Label(new Rect(19, 121, 520, 29), "CONSTRUCTION TWEAKS  /  GAME OVERRIDES", headingStyle);
            GUI.Label(new Rect(19, 157, 520, 43),
                "Direct construction hooks: free resource checks + force complete.\n" +
                "Both enabled by default. Changes save automatically to BepInEx config.", bodyStyle);
            // Draw actual buttons rather than GUI.Toggle with a GUI.skin.label-based
            // style: that combination hid the checkbox graphic in previous builds.
            GUI.Label(new Rect(23, 211, 360, 34), "Instant Construction", bodyStyle);
            bool nextInstant = instantConstructionRequested.Value;
            if (GUI.Button(new Rect(413, 210, 128, 34),
                nextInstant ? "ON  [X]" : "OFF", nextInstant ? primaryStyle : buttonStyle))
                nextInstant = !nextInstant;
            GUI.Label(new Rect(23, 258, 365, 34), "Free Construction / No Material Cost", bodyStyle);
            bool nextFree = freeConstructionRequested.Value;
            if (GUI.Button(new Rect(413, 257, 128, 34),
                nextFree ? "ON  [X]" : "OFF", nextFree ? primaryStyle : buttonStyle))
                nextFree = !nextFree;
            // Never write from every OnGUI event: only persist actual user changes.
            if (nextInstant != instantConstructionRequested.Value || nextFree != freeConstructionRequested.Value)
            {
                instantConstructionRequested.Value = nextInstant;
                freeConstructionRequested.Value = nextFree;
                try
                {
                    Config.Save();
                    status = "Construction settings saved";
                    Logger.LogInfo("CONSTRUCTION SETTINGS SAVED: instant=" + nextInstant + " free=" + nextFree);
                }
                catch (Exception ex)
                {
                    status = "Could not save construction settings; see log";
                    Logger.LogWarning("CONSTRUCTION CONFIG SAVE FAILED: " + ex.Message);
                }
            }
            if (GUI.Button(new Rect(19, 321, 526, 39), "SCAN CONSTRUCTION API  /  WRITE LOG", primaryStyle))
                ScanConstructionAPI();
            GUI.DrawTexture(new Rect(18, 385, 528, 1), borderTex);
            GUI.Label(new Rect(19, 399, 526, 78),
                "Game override getters: " + constructionOverridesPatched + "/2; direct hooks: " + constructionDirectPatched + "/5" +
                "\nTry a NEW Water Storage and Food Storage.\n" +
                "Toggle settings are remembered between launches.\n" +
                "Use a backup save when experimenting with construction.", mutedStyle);
        }

        private void ScanConstructionAPI()
        {
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ScriptsAssembly");
                if (asm == null) { status = "ScriptsAssembly not loaded"; Logger.LogWarning("CONSTRUCTION API: assembly not loaded"); return; }
                int methods = 0;
                foreach (var type in SafeTypes(asm))
                {
                    if (type == null) continue;
                    string tn = type.Name;
                    bool target = tn.IndexOf("Construction", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  tn.IndexOf("ConstructBuildable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  tn == "Buildable" || tn == "CityBuildingManager" || tn == "CityResourceManager";
                    if (!target) continue;
                    Logger.LogInfo("CONSTRUCTION TYPE: " + type.FullName + " : " + type.BaseType);
                    foreach (var field in type.GetFields(Flags | BindingFlags.DeclaredOnly))
                    {
                        if (field.Name.IndexOf("cost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            field.Name.IndexOf("resource", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            field.Name.IndexOf("progress", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            field.Name.IndexOf("build", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            field.Name.IndexOf("instant", StringComparison.OrdinalIgnoreCase) >= 0)
                            Logger.LogInfo("CONSTRUCTION FIELD: " + tn + "." + field.Name + " : " + field.FieldType);
                    }
                    foreach (var method in type.GetMethods(Flags | BindingFlags.DeclaredOnly))
                    {
                        if (method.IsSpecialName && !method.Name.StartsWith("get_")) continue;
                        if (tn == "CityResourceManager" && method.Name.IndexOf("construction", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        Logger.LogInfo("CONSTRUCTION API: " + type.Name + "." + method);
                        methods++;
                    }
                }
                constructionMethodCount = methods;
                constructionScanned = true;
                status = "Construction signatures logged: " + methods;
                Logger.LogInfo("CONSTRUCTION SCAN COMPLETE: " + methods + " methods. No gameplay mutations performed.");
            }
            catch (Exception ex) { status = "Construction scan failed; check log"; Logger.LogError("CONSTRUCTION SCAN FAILED: " + ex); }
        }
    }
}

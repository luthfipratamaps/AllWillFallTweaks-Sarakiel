using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sarakiel.AllWillFall
{
    [BepInPlugin("id.sarakiel.allwillfall.resourcetweaks", "All Will Fall - Resource Tweaks", "0.5.0")]
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
        private ConfigEntry<bool> autoScanEnabled;
        private ConfigEntry<float> autoScanInterval;
        private ConfigEntry<bool> verboseDiagnostics;
        private int observedSceneHandle = int.MinValue;
        private float nextAutoScanAt;
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
            ScanStorageInstances(true);
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
            if (autoScanEnabled != null && autoScanEnabled.Value) AutoScanCity();
            if (capacityPatched > 0)
            {
                ScanStorageInstances(false);
                ApplyStorageCapacities();
            }
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
                manager = null;
                inspected = false;
                diagnosticsWritten = false;
                lastManagerReady = false;
                counts.Clear();
                storageKinds.Clear();
                storageBaseCapacities.Clear();
                unknownStorageLogged.Clear();
                tracedStorage.Clear();
                appliedCapacityLog.Clear();
                autoScanAttempts = 0;
                nextAutoScanAt = Time.unscaledTime + 1f;
                Logger.LogInfo("AUTO SCAN: scene changed; scheduling city discovery");
            }
            if (Time.unscaledTime < nextAutoScanAt) return;
            float interval = Mathf.Max(1f, autoScanInterval.Value);
            // Retry quickly while the city is initializing, then stay on a low-frequency
            // scan so new structures are picked up without opening the UI.
            nextAutoScanAt = Time.unscaledTime + (lastManagerReady && storageKinds.Count > 0 ? interval : 1.5f);
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
                lastManagerReady = ready;
                ScanStorageInstances(true);
                ApplyStorageCapacities();
                if (ready) RefreshCounts();
                autoScanAttempts++;
                if (verboseDiagnostics.Value && (autoScanAttempts <= 3 || autoScanAttempts == 10))
                    Logger.LogInfo("AUTO SCAN: attempt=" + autoScanAttempts + " city=" + ready + " tracked=" + storageKinds.Count);
            }
            catch (Exception ex)
            {
                if (autoScanAttempts++ < 3) Logger.LogWarning("AUTO SCAN: retrying after " + ex.Message);
            }
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
            GUI.Label(new Rect(22, 42, 450, 17), "SARAKIEL  •  TWEAKS DASHBOARD   v0.5.0", subtitleStyle);
            if (GUI.Button(new Rect(516, 18, 30, 28), "×", closeStyle)) { visible = false; pointerCaptured = false; SaveWindowPosition(); }
            GUI.DrawTexture(new Rect(18, 69, 528, 1), borderTex);
            if (GUI.Button(new Rect(18, 77, 259, 30), "RESOURCES", capacityTab ? buttonStyle : primaryStyle)) capacityTab = false;
            if (GUI.Button(new Rect(286, 77, 260, 30), "BUILDING CAPACITY", capacityTab ? primaryStyle : buttonStyle)) capacityTab = true;
            if (capacityTab) { DrawCapacityTab(); GUI.DragWindow(new Rect(0, 5, 500, 65)); return; }

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
            if (Time.realtimeSinceStartup - lastCountRefresh > 0.9f) RefreshCounts();
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
            // Refresh only after a missed lookup. Harmony patches may execute often.
            ScanStorageInstances(false);
            if (storageKinds.TryGetValue(targetStorage, out kind)) return kind;
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
        private float lastStorageScan;
        private bool capacityScanBusy;
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object a, object b) { return object.ReferenceEquals(a,b); }
            public int GetHashCode(object x) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(x); }
        }
        private void ScanStorageInstances(bool force)
        {
            if (capacityScanBusy) return;
            if (!force && Time.unscaledTime - lastStorageScan < 0.6f) return;
            lastStorageScan = Time.unscaledTime;
            capacityScanBusy = true;
            try
            {
                // ResourceBuilding is a Unity component. Scan live objects, not only the
                // CityResourceManager registry (which may omit some buildings).
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
                if (manager == null) LocateManager();
                if (manager != null)
                {
                    FieldInfo f = manager.GetType().GetField("resourceBuildings", Flags);
                    IDictionary map = f == null ? null : f.GetValue(manager) as IDictionary;
                    if (map != null) foreach (DictionaryEntry item in map) RegisterStorage(item.Value, item.Key);
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
        private float nextCapacityApply;
        private readonly HashSet<object> appliedCapacityLog = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private void ApplyStorageCapacities()
        {
            if (Time.unscaledTime < nextCapacityApply) return;
            nextCapacityApply = Time.unscaledTime + 0.65f;
            foreach (var entry in storageKinds.ToArray())
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
                    FieldInfo field = storage.GetType().GetField("capacity", Flags);
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
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace BlasTimeManager
{
    // Blasphemous play-time tool.
    // total play time = PersistentManager.TimeStored + (Time.realtimeSinceStartup - LastTimeStored)
    //   Freeze:  each frame set TimeStored = frozenValue, LastTimeStored = realtimeSinceStartup  -> total held constant.
    //   Set:     TimeStored = value, LastTimeStored = realtimeSinceStartup  -> shows immediately and persists at next save.
    [BepInPlugin("local.blasphemous.timemanager", "Blasphemous Time Manager", "2.1.0")]
    public class TimeManager : BaseUnityPlugin
    {
        const string PMName       = "Framework.Managers.PersistentManager";
        const string DataName     = "Framework.Managers.PersistentManager+PersitentPersistenceData";
        const string CoreName     = "Framework.Managers.Core";
        const string PenitentName = "Gameplay.GameControllers.Penitent.Penitent";  // the player; only exists in-game

        const int MaxDepth = 5;
        const int MaxVisited = 1500;

        static ManualLogSource L;

        ConfigEntry<KeyCode> cfgOverlayKey;
        ConfigEntry<KeyCode> cfgFreezeKey;
        ConfigEntry<KeyCode> cfgSetKey;
        ConfigEntry<bool>    cfgOverlayOn;

        Type pmType, dataType, coreType, penitentType;
        PropertyInfo pTimeStored, pLastTimeStored;
        FieldInfo fTime;   // snapshot copy of Time (poked on Set so the save screen reflects it)

        object pmCache;
        bool overlayOn;
        bool inGame;
        float gateAccum;

        bool frozen;
        float frozenValue;

        bool setPanelOpen;
        string inputText = "";
        string setError;
        bool focusPending;

        GUIStyle overlayStyle, shadowStyle, panelLabel, panelField, panelHint;
        Texture2D panelBg;

        void Awake()
        {
            L = Logger;
            cfgOverlayKey = Config.Bind("Keys",    "Overlay", KeyCode.F7, "Toggle the on-screen play-time overlay.");
            cfgFreezeKey  = Config.Bind("Keys",    "Freeze",  KeyCode.F8, "Freeze / unfreeze play time.");
            cfgSetKey     = Config.Bind("Keys",    "SetTime", KeyCode.F9, "Open / close the 'set play time' input box.");
            cfgOverlayOn  = Config.Bind("Overlay", "ShowOnStart", true,   "Show the overlay as soon as a save is loaded.");
            overlayOn = cfgOverlayOn.Value;

            pmType       = FindType(PMName);
            dataType     = FindType(DataName);
            coreType     = FindType(CoreName);
            penitentType = FindType(PenitentName);

            var ip = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            if (pmType != null)
            {
                pTimeStored     = pmType.GetProperty("TimeStored", ip);
                pLastTimeStored = pmType.GetProperty("LastTimeStored", ip);
            }
            if (dataType != null)
                fTime = dataType.GetField("Time", ip);

            bool ok = pTimeStored != null && pLastTimeStored != null;
            L.LogInfo($"[PlayTime] v2.1 ready (net35). props={ok} penitent={penitentType != null}. " +
                      $"{cfgOverlayKey.Value}=overlay  {cfgFreezeKey.Value}=freeze  {cfgSetKey.Value}=set");
        }

        void Update()
        {
            gateAccum += Time.unscaledDeltaTime;
            if (gateAccum >= 0.5f) { gateAccum = 0f; inGame = IsInGame(); }

            if (Input.GetKeyDown(cfgOverlayKey.Value)) { overlayOn = !overlayOn; L.LogInfo($"[PlayTime] overlay {(overlayOn ? "ON" : "OFF")}."); }
            if (Input.GetKeyDown(cfgFreezeKey.Value))  ToggleFreeze();
            if (Input.GetKeyDown(cfgSetKey.Value))     ToggleSetPanel();

            if (frozen) HoldFrozen();
        }

        // ---- features -----------------------------------------------------------

        void ToggleFreeze()
        {
            frozen = !frozen;
            if (frozen)
            {
                float v = LiveSeconds();
                frozenValue = float.IsNaN(v) ? 0f : v;
                L.LogInfo($"[PlayTime] freeze ON @ {Hms(frozenValue)}.");
            }
            else L.LogInfo("[PlayTime] freeze OFF.");
        }

        void HoldFrozen()
        {
            var pm = GetPM();
            if (pm == null || pTimeStored == null || pLastTimeStored == null) return;
            SafeSet(() => pTimeStored.SetValue(pm, frozenValue, null));
            SafeSet(() => pLastTimeStored.SetValue(pm, Time.realtimeSinceStartup, null));
        }

        void ToggleSetPanel()
        {
            if (!setPanelOpen && !inGame) { L.LogInfo("[PlayTime] set box only available in-game."); return; }
            setPanelOpen = !setPanelOpen;
            if (setPanelOpen)
            {
                float v = LiveSeconds();
                inputText = float.IsNaN(v) ? "00:00:00" : Hms(v);
                setError = null;
                focusPending = true;
            }
        }

        void ApplyInput()
        {
            if (TryParseTime(inputText, out float secs))
            {
                SetTime(secs);
                setPanelOpen = false;
                setError = null;
            }
            else setError = "Invalid - use HH:MM:SS or seconds";
        }

        void SetTime(float seconds)
        {
            var pm = GetPM();
            if (pm == null || pTimeStored == null || pLastTimeStored == null) { L.LogWarning("[PlayTime] cannot set - no manager."); return; }
            float old = Conv(pTimeStored.GetValue(pm, null));
            SafeSet(() => pTimeStored.SetValue(pm, seconds, null));
            SafeSet(() => pLastTimeStored.SetValue(pm, Time.realtimeSinceStartup, null));
            var data = FindData(pm);
            if (data != null && fTime != null) SafeSet(() => fTime.SetValue(data, seconds));
            if (frozen) frozenValue = seconds;
            L.LogInfo($"[PlayTime] set {Hms(old)} -> {Hms(seconds)}. Save to persist.");
        }

        float LiveSeconds()
        {
            var pm = GetPM();
            if (pm == null || pTimeStored == null || pLastTimeStored == null) return float.NaN;
            float ts  = Conv(pTimeStored.GetValue(pm, null));
            float lts = Conv(pLastTimeStored.GetValue(pm, null));
            return ts + (Time.realtimeSinceStartup - lts);
        }

        bool IsInGame()
        {
            if (penitentType == null) return false;
            return SafeGet(() => UnityEngine.Object.FindObjectOfType(penitentType)) != null;
        }

        // ---- UI -----------------------------------------------------------------

        void OnGUI()
        {
            if (!inGame) return;
            EnsureStyles();
            if (overlayOn) DrawOverlay();
            if (setPanelOpen) DrawSetPanel();
        }

        void DrawOverlay()
        {
            float live = LiveSeconds();
            if (float.IsNaN(live)) return;
            string text = "Play time  " + Hms(live) + (frozen ? "  [FROZEN]" : "");
            overlayStyle.normal.textColor = frozen ? Color.cyan : Color.white;
            GUI.Label(new Rect(13, 11, 460, 30), text, shadowStyle);
            GUI.Label(new Rect(12, 10, 460, 30), text, overlayStyle);
        }

        void DrawSetPanel()
        {
            // Keyboard only (no mouse in-game): Enter applies, Esc cancels. Consume so the field doesn't eat it.
            var e = Event.current;
            bool doApply = false, doCancel = false;
            if (e != null && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { doApply = true; e.Use(); }
                else if (e.keyCode == KeyCode.Escape) { doCancel = true; e.Use(); }
            }

            var rect = new Rect(12, 46, 340, 112);
            if (e != null && e.type == EventType.Repaint) GUI.DrawTexture(rect, panelBg);   // opaque background

            GUILayout.BeginArea(rect);
            GUILayout.Space(8);
            GUILayout.Label("Set play time", panelLabel);
            GUILayout.Label("HH:MM:SS  or  seconds", panelHint);
            GUI.SetNextControlName("blasSetTime");
            inputText = GUILayout.TextField(inputText ?? "", panelField, GUILayout.Height(26));
            GUILayout.Space(4);
            GUILayout.Label(string.IsNullOrEmpty(setError) ? "Enter = apply      Esc = cancel" : setError, panelHint);
            GUILayout.EndArea();

            if (focusPending) { GUI.FocusControl("blasSetTime"); focusPending = false; }

            if (doApply) ApplyInput();
            else if (doCancel) { setPanelOpen = false; setError = null; }
        }

        void EnsureStyles()
        {
            if (overlayStyle != null) return;
            shadowStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            shadowStyle.normal.textColor = Color.black;
            overlayStyle = new GUIStyle(shadowStyle);
            overlayStyle.normal.textColor = Color.white;
            panelLabel = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            panelLabel.normal.textColor = Color.white;
            panelHint = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            panelHint.normal.textColor = new Color(0.78f, 0.78f, 0.82f);
            panelField = new GUIStyle(GUI.skin.textField) { fontSize = 16 };

            panelBg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            panelBg.SetPixel(0, 0, new Color(0.10f, 0.10f, 0.13f, 1f));   // fully opaque
            panelBg.Apply();
        }

        // ---- parsing ------------------------------------------------------------

        static bool TryParseTime(string s, out float seconds)
        {
            seconds = 0f;
            if (string.IsNullOrEmpty(s)) return false;
            double total = 0;
            foreach (var part in s.Trim().Split(':'))
            {
                double v;
                if (!double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
                total = total * 60 + v;   // S, M:S, or H:M:S all fold correctly
            }
            if (total < 0 || total > 360000000) return false;   // sanity cap (~100000h)
            seconds = (float)total;
            return true;
        }

        // ---- manager resolution -------------------------------------------------

        object GetPM()
        {
            if (pmCache != null)
            {
                if (pmCache is UnityEngine.Object uo) { if (uo != null) return pmCache; pmCache = null; }
                else return pmCache;
            }
            pmCache = StaticSingleton(pmType) ?? ViaUnity(pmType) ?? ViaCore();
            return pmCache;
        }

        static object StaticSingleton(Type t)
        {
            if (t == null) return null;
            for (var bt = t; bt != null && bt != typeof(object); bt = bt.BaseType)
            {
                var p = bt.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (p != null) { var v = SafeGet(() => p.GetValue(null, null)); if (v != null) return v; }
                foreach (var name in new[] { "instance", "_instance", "m_instance", "s_instance" })
                {
                    var f = bt.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (f != null) { var v = SafeGet(() => f.GetValue(null)); if (v != null) return v; }
                }
            }
            return null;
        }

        static object ViaUnity(Type t)
        {
            if (t == null || !typeof(UnityEngine.Object).IsAssignableFrom(t)) return null;
            var arr = SafeGet(() => Resources.FindObjectsOfTypeAll(t)) as UnityEngine.Object[];
            return (arr != null && arr.Length > 0) ? arr[0] : null;
        }

        object ViaCore()
        {
            if (coreType == null) return null;
            var s = ScanForPM(coreType, null);
            if (s != null) return s;
            var core = StaticSingleton(coreType) ?? ViaUnity(coreType);
            return core != null ? ScanForPM(coreType, core) : null;
        }

        object ScanForPM(Type t, object instance)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | (instance == null ? BindingFlags.Static : BindingFlags.Instance);
            foreach (var f in t.GetFields(flags))
                if (pmType.IsAssignableFrom(f.FieldType)) { var v = SafeGet(() => f.GetValue(instance)); if (v != null) return v; }
            foreach (var p in t.GetProperties(flags))
                if (pmType.IsAssignableFrom(p.PropertyType) && p.GetIndexParameters().Length == 0) { var v = SafeGet(() => p.GetValue(instance, null)); if (v != null) return v; }
            return null;
        }

        object FindData(object pm)
        {
            if (pm == null) return null;
            var visited = new HashSet<object>(RefComparer.Instance);
            int budget = MaxVisited;
            return Search(pm, 0, visited, ref budget);
        }

        object Search(object obj, int depth, HashSet<object> visited, ref int budget)
        {
            if (obj == null || depth > MaxDepth || budget-- <= 0) return null;
            if (dataType.IsInstanceOfType(obj)) return obj;
            var t = obj.GetType();
            if (!t.IsValueType && !visited.Add(obj)) return null;

            if (obj is IDictionary dict)
            {
                try { foreach (DictionaryEntry e in dict) { var r = Search(e.Value, depth + 1, visited, ref budget); if (r != null) return r; } }
                catch { }
                return null;
            }
            if (!(t.Namespace ?? "").StartsWith("Framework")) return null;

            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                object v = SafeGet(() => f.GetValue(obj));
                if (v == null || v is UnityEngine.Object || v is IEnumerator) continue;
                var vt = v.GetType();
                if (vt.IsPrimitive || vt.IsEnum || vt == typeof(string)) continue;
                if ((v is IEnumerable) && !(v is IDictionary)) continue;
                var r = Search(v, depth + 1, visited, ref budget);
                if (r != null) return r;
            }
            return null;
        }

        // ---- helpers ------------------------------------------------------------

        static float Conv(object o) { try { return Convert.ToSingle(o); } catch { return float.NaN; } }
        static object SafeGet(Func<object> f) { try { return f(); } catch { return null; } }
        static void SafeSet(Action a) { try { a(); } catch (Exception e) { L.LogWarning($"[PlayTime] set failed: {e.Message}"); } }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t; try { t = asm.GetType(fullName, false); } catch { continue; }
                if (t != null) return t;
            }
            return null;
        }

        static string Hms(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return "??:??:??";
            int s = (int)seconds;
            return string.Format("{0:00}:{1:00}:{2:00}", s / 3600, (s % 3600) / 60, s % 60);
        }

        sealed class RefComparer : IEqualityComparer<object>
        {
            public static readonly RefComparer Instance = new RefComparer();
            bool IEqualityComparer<object>.Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }
    }
}

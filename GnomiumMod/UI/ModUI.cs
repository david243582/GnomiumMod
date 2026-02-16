using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GnomiumMod.UI
{
    /// <summary>
    /// Simple localization + cached settings (MelonPreferences).
    /// English is the default language.
    /// Compatible with C# 7.3 (no target-typed new()).
    /// </summary>
    public class ModUI
    {
        // -----------------------------
        // Injected refs
        // -----------------------------
        private readonly Func<bool> isReady;
        private readonly CameraModule camModule;
        private readonly OutfitModule outfitModule;

        private readonly Func<float> getBaseSpeed;
        private readonly Action<float> setBaseSpeed;
        private readonly Func<float> getBoostSpeed;
        private readonly Action<float> setBoostSpeed;

        // -----------------------------
        // UI state
        // -----------------------------
        public bool Visible = true;

        // window rect is cached
        private Rect windowRect = new Rect(20, 20, 550, 600);

        private float uiScale = 1f;
        private Vector2 scroll;

        // cached toggles/values
        private bool liveApply = true;

        // HSV picker state (cached)
        private float hue = 0f; // 0..1
        private float sat = 1f; // 0..1
        private float val = 1f; // 0..1

        // intensity (cached)
        private float intensity = 2.0f;

        private bool draggingSV = false;
        private bool draggingHue = false;

        // optional cached speed targets
        private float cachedBaseSpeed;
        private float cachedBoostSpeed;

        // -----------------------------
        // Localization
        // -----------------------------
        private enum Lang { EN = 0, ES = 1 }
        private Lang lang = Lang.EN;

        // C# 7.3 compatible init (no target-typed new)
        private static readonly Dictionary<string, ValueTuple<string, string>> L =
            new Dictionary<string, ValueTuple<string, string>>()
        {
            // top
            { "creator",      new ValueTuple<string, string>("Creator", "Creador") },
            { "insert_hint",  new ValueTuple<string, string>("INSERT key: show/hide UI", "Botón INSERTAR: mostrar/ocultar UI") },

            // sections
            { "speed",        new ValueTuple<string, string>("Speed", "Velocidad") },
            { "camera",       new ValueTuple<string, string>("Camera", "Cámara") },
            { "hat_color",    new ValueTuple<string, string>("Hat - Color", "Gorro - Color") },

            // speed rows
            { "walk",         new ValueTuple<string, string>("Walking", "Andando") },
            { "run",          new ValueTuple<string, string>("Running", "Corriendo") },

            // camera rows
            { "first_person", new ValueTuple<string, string>("First person (F7)", "Primera persona (F7)") },

            // hat rows/buttons
            { "language",     new ValueTuple<string, string>("Language", "Idioma") },
            { "apply_live",   new ValueTuple<string, string>("Live apply", "Aplicar en vivo") },
            { "intensity",    new ValueTuple<string, string>("Intensity", "Intensidad") },
            { "preview",      new ValueTuple<string, string>("Preview:", "Preview:") },
            { "apply",        new ValueTuple<string, string>("Apply", "Aplicar") },
            { "reset",        new ValueTuple<string, string>("Reset", "Reset") },
            { "clear",        new ValueTuple<string, string>("Clear", "Clear") },
        };

        private string T(string key)
        {
            ValueTuple<string, string> v;
            if (!L.TryGetValue(key, out v)) return key;
            return (lang == Lang.ES) ? v.Item2 : v.Item1;
        }

        // -----------------------------
        // Styles / textures (OPAQUE)
        // -----------------------------
        private GUIStyle windowStyle;
        private GUIStyle headerStyle;
        private GUIStyle smallStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;

        private Texture2D winBg;
        private Texture2D boxBg;
        private Texture2D btnBg;
        private Texture2D previewTex;

        private Texture2D hueTex;    // 1 x 256
        private Texture2D svTex;     // 256 x 256
        private float lastHueForSV = -1f;

        private bool initialized = false;

        // -----------------------------
        // Cached settings helper
        // -----------------------------
        private static class ModUISettings
        {
            private static MelonPreferences_Category cat;

            // UI
            private static MelonPreferences_Entry<int> eLang;
            private static MelonPreferences_Entry<float> eWinX, eWinY, eWinW, eWinH;

            // hat
            private static MelonPreferences_Entry<bool> eLiveApply;
            private static MelonPreferences_Entry<float> eHue, eSat, eVal, eIntensity;

            // speed (optional)
            private static MelonPreferences_Entry<float> eBaseSpeed, eBoostSpeed;

            private static bool dirty;
            private static float nextSaveAt;

            public static void Init()
            {
                if (cat != null) return;

                cat = MelonPreferences.CreateCategory("GnomiumMod_UI", "GnomiumMod UI");

                eLang = cat.CreateEntry("Language", 0, "Language (0=EN,1=ES)");

                eWinX = cat.CreateEntry("WindowX", 20f);
                eWinY = cat.CreateEntry("WindowY", 20f);
                eWinW = cat.CreateEntry("WindowW", 550f);
                eWinH = cat.CreateEntry("WindowH", 600f);

                eLiveApply = cat.CreateEntry("LiveApply", true);
                eHue = cat.CreateEntry("Hue", 0f);
                eSat = cat.CreateEntry("Sat", 1f);
                eVal = cat.CreateEntry("Val", 1f);
                eIntensity = cat.CreateEntry("Intensity", 2.0f);

                eBaseSpeed = cat.CreateEntry("BaseSpeed", 0f);   // 0 means "not set"
                eBoostSpeed = cat.CreateEntry("BoostSpeed", 0f); // 0 means "not set"
            }

            public static void MarkDirty()
            {
                dirty = true;
                nextSaveAt = Time.unscaledTime + 0.75f; // debounce
            }

            public static void TickSave()
            {
                if (!dirty) return;
                if (Time.unscaledTime < nextSaveAt) return;

                dirty = false;
                try
                {
                    MelonPreferences.Save();
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("[ModUI] Failed to save preferences: " + ex.Message);
                }
            }

            // getters
            public static int Language { get { return eLang.Value; } }
            public static Rect WindowRect { get { return new Rect(eWinX.Value, eWinY.Value, eWinW.Value, eWinH.Value); } }

            public static bool LiveApply { get { return eLiveApply.Value; } }
            public static float Hue { get { return eHue.Value; } }
            public static float Sat { get { return eSat.Value; } }
            public static float Val { get { return eVal.Value; } }
            public static float Intensity { get { return eIntensity.Value; } }

            public static float BaseSpeed { get { return eBaseSpeed.Value; } }
            public static float BoostSpeed { get { return eBoostSpeed.Value; } }

            // setters
            public static void SetLanguage(int v) { eLang.Value = v; MarkDirty(); }

            public static void SetWindowRect(Rect r)
            {
                eWinX.Value = r.x;
                eWinY.Value = r.y;
                eWinW.Value = r.width;
                eWinH.Value = r.height;
                MarkDirty();
            }

            public static void SetHat(bool live, float h, float s, float v, float intensity)
            {
                eLiveApply.Value = live;
                eHue.Value = h;
                eSat.Value = s;
                eVal.Value = v;
                eIntensity.Value = intensity;
                MarkDirty();
            }

            public static void SetSpeeds(float baseSpeed, float boostSpeed)
            {
                eBaseSpeed.Value = baseSpeed;
                eBoostSpeed.Value = boostSpeed;
                MarkDirty();
            }
        }

        // -----------------------------
        // Ctor
        // -----------------------------
        public ModUI(
            CameraModule cameraModule,
            OutfitModule outfit,
            Func<bool> ready,
            Func<float> baseSpeedGetter,
            Action<float> baseSpeedSetter,
            Func<float> boostSpeedGetter,
            Action<float> boostSpeedSetter)
        {
            camModule = cameraModule;
            outfitModule = outfit;
            isReady = ready;

            getBaseSpeed = baseSpeedGetter;
            setBaseSpeed = baseSpeedSetter;
            getBoostSpeed = boostSpeedGetter;
            setBoostSpeed = boostSpeedSetter;

            // Load cached values
            ModUISettings.Init();

            lang = (ModUISettings.Language == 1) ? Lang.ES : Lang.EN;

            windowRect = ModUISettings.WindowRect;

            liveApply = ModUISettings.LiveApply;
            hue = ModUISettings.Hue;
            sat = ModUISettings.Sat;
            val = ModUISettings.Val;
            intensity = ModUISettings.Intensity;

            cachedBaseSpeed = ModUISettings.BaseSpeed;
            cachedBoostSpeed = ModUISettings.BoostSpeed;
        }

        public void Toggle() { Visible = !Visible; }

        public void OnGUI(string modName, string version, string author)
        {
            if (!Visible) return;

            EnsureUI();

            // save debounced preferences
            ModUISettings.TickSave();

            UpdateScale();
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * uiScale);

            float maxX = (Screen.width / uiScale) - windowRect.width - 10;
            float maxY = (Screen.height / uiScale) - windowRect.height - 10;
            windowRect.x = Mathf.Clamp(windowRect.x, 5, Mathf.Max(5, maxX));
            windowRect.y = Mathf.Clamp(windowRect.y, 5, Mathf.Max(5, maxY));

            Rect before = windowRect;

            windowRect = GUI.Window(
                34219,
                windowRect,
                delegate (int id) { DrawWindow(modName, version, author); },
                modName + "  v" + version,
                windowStyle
            );

            // cache window rect if moved/resized
            if (Mathf.Abs(before.x - windowRect.x) > 0.01f ||
                Mathf.Abs(before.y - windowRect.y) > 0.01f ||
                Mathf.Abs(before.width - windowRect.width) > 0.01f ||
                Mathf.Abs(before.height - windowRect.height) > 0.01f)
            {
                ModUISettings.SetWindowRect(windowRect);
            }

            GUI.matrix = old;
        }

        private void UpdateScale()
        {
            float h = Screen.height;
            uiScale = Mathf.Clamp(h / 1080f, 0.80f, 1.35f);
        }

        private void EnsureUI()
        {
            if (initialized) return;

            winBg = MakeSolidTex(new Color(0.10f, 0.10f, 0.10f, 1f));
            boxBg = MakeSolidTex(new Color(0.16f, 0.16f, 0.16f, 1f));
            btnBg = MakeSolidTex(new Color(0.22f, 0.22f, 0.22f, 1f));
            previewTex = MakeSolidTex(Color.red);

            // Hue gradient vertical
            hueTex = new Texture2D(1, 256, TextureFormat.RGBA32, false);
            hueTex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 256; y++)
            {
                float h = 1f - (y / 255f);
                hueTex.SetPixel(0, y, Color.HSVToRGB(h, 1f, 1f));
            }
            hueTex.Apply(false, false);

            svTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            svTex.wrapMode = TextureWrapMode.Clamp;
            RebuildSVTexture();

            windowStyle = new GUIStyle(GUI.skin.window);
            SetAllStatesBackground(windowStyle, winBg);
            windowStyle.fontSize = 14;
            windowStyle.padding = new RectOffset(12, 12, 24, 12);

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 15;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = Color.white;

            smallStyle = new GUIStyle(GUI.skin.label);
            smallStyle.fontSize = 11;
            smallStyle.normal.textColor = new Color(0.86f, 0.86f, 0.86f, 1f);

            boxStyle = new GUIStyle(GUI.skin.box);
            SetAllStatesBackground(boxStyle, boxBg);
            boxStyle.padding = new RectOffset(10, 10, 10, 10);

            buttonStyle = new GUIStyle(GUI.skin.button);
            SetAllStatesBackground(buttonStyle, btnBg);
            buttonStyle.fixedHeight = 26;

            initialized = true;
        }

        private static void SetAllStatesBackground(GUIStyle s, Texture2D bg)
        {
            s.normal.background = bg;
            s.hover.background = bg;
            s.active.background = bg;
            s.focused.background = bg;

            s.onNormal.background = bg;
            s.onHover.background = bg;
            s.onActive.background = bg;
            s.onFocused.background = bg;
        }

        private void DrawWindow(string modName, string version, string author)
        {
            bool ready = isReady();

            GUILayout.Label(T("creator") + ": " + author, smallStyle);
            GUILayout.Space(8);

            // vertical scroll only
            scroll = GUILayout.BeginScrollView(scroll, false, true, GUILayout.ExpandHeight(true));

            float vbarW = (GUI.skin.verticalScrollbar != null && GUI.skin.verticalScrollbar.fixedWidth > 0f)
                ? GUI.skin.verticalScrollbar.fixedWidth
                : 18f;

            float contentW = windowRect.width
                - windowStyle.padding.left - windowStyle.padding.right
                - vbarW
                - 4f;

            scroll.x = 0f;

            GUILayout.BeginVertical(GUILayout.MaxWidth(contentW), GUILayout.ExpandWidth(true));

            DrawLanguageSection(contentW);
            GUILayout.Space(10);

            DrawSpeedSection(ready);
            GUILayout.Space(10);

            DrawCameraSection(ready);
            GUILayout.Space(10);

            DrawHatColorSection(ready, contentW);

            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label(T("insert_hint"), smallStyle);

            GUI.DragWindow(new Rect(0, 0, 9999, 22));
        }

        private void DrawLanguageSection(float availableWidth)
        {
            GUILayout.Label(T("language"), headerStyle);
            GUILayout.BeginVertical(boxStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("language"), GUILayout.Width(120));

            bool isEN = (lang == Lang.EN);
            bool newEN = GUILayout.Toggle(isEN, "EN", GUILayout.Width(60));
            bool newES = GUILayout.Toggle(!isEN, "ES", GUILayout.Width(60));

            Lang before = lang;
            if (newEN && !isEN) lang = Lang.EN;
            if (newES && isEN) lang = Lang.ES;

            if (lang != before)
            {
                ModUISettings.SetLanguage(lang == Lang.ES ? 1 : 0);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawSpeedSection(bool ready)
        {
            GUILayout.Label(T("speed"), headerStyle);
            GUILayout.BeginVertical(boxStyle);

            GUI.enabled = ready;

            float baseSp = getBaseSpeed();
            float boostSp = getBoostSpeed();

            if (cachedBaseSpeed > 0f) baseSp = cachedBaseSpeed;
            if (cachedBoostSpeed > 0f) boostSp = cachedBoostSpeed;

            float newBase = SliderRow(T("walk"), baseSp, 0.5f, 8f);
            float newBoost = SliderRow(T("run"), boostSp, 0.5f, 16f);

            setBaseSpeed(newBase);
            setBoostSpeed(newBoost);

            if (Mathf.Abs(newBase - cachedBaseSpeed) > 0.0001f || Mathf.Abs(newBoost - cachedBoostSpeed) > 0.0001f)
            {
                cachedBaseSpeed = newBase;
                cachedBoostSpeed = newBoost;
                ModUISettings.SetSpeeds(cachedBaseSpeed, cachedBoostSpeed);
            }

            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        private void DrawCameraSection(bool ready)
        {
            GUILayout.Label(T("camera"), headerStyle);
            GUILayout.BeginVertical(boxStyle);

            GUI.enabled = ready;

            bool isFirstPerson = !camModule.ThirdPersonEnabled;
            bool newFirstPerson = ToggleRow(T("first_person"), isFirstPerson);
            if (newFirstPerson != isFirstPerson)
                camModule.ToggleThirdPerson();

            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        private void DrawHatColorSection(bool ready, float availableWidth)
        {
            GUILayout.Label(T("hat_color"), headerStyle);

            GUILayout.BeginVertical(boxStyle);

            GUI.enabled = ready;

            bool newLive = ToggleRow(T("apply_live"), liveApply);
            float newIntensity = SliderRow(T("intensity"), intensity, 0.50f, 20.0f);

            bool hatSettingsChanged =
                (newLive != liveApply) ||
                Mathf.Abs(newIntensity - intensity) > 0.0001f;

            liveApply = newLive;
            intensity = newIntensity;

            GUILayout.Space(8);

            float innerW = availableWidth - 20f;
            innerW = Mathf.Max(innerW, 280f);

            float hueW = 14f;
            float gap = 10f;

            float svSize = innerW - hueW - gap;
            svSize = Mathf.Clamp(svSize, 120f, 180f);
            float pickerH = svSize;

            GUILayout.BeginHorizontal();

            Rect svRect = GUILayoutUtility.GetRect(svSize, pickerH, GUILayout.ExpandWidth(false));
            float beforeHue = hue, beforeSat = sat, beforeVal = val;
            DrawSVPicker(svRect);

            GUILayout.Space(gap);

            Rect hueRect = GUILayoutUtility.GetRect(hueW, pickerH, GUILayout.ExpandWidth(false));
            DrawHueBar(hueRect);

            GUILayout.EndHorizontal();

            if (Mathf.Abs(beforeHue - hue) > 0.0001f ||
                Mathf.Abs(beforeSat - sat) > 0.0001f ||
                Mathf.Abs(beforeVal - val) > 0.0001f)
            {
                hatSettingsChanged = true;
            }

            GUILayout.Space(10);

            Color previewColor = BuildPreviewColor();
            Color applyColor = BuildApplyColor();

            SetPreviewColor(previewColor);

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("preview"), GUILayout.Width(70));
            GUILayout.Box(previewTex, GUILayout.Width(60), GUILayout.Height(22));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(T("apply"), buttonStyle, GUILayout.Width(72)))
                outfitModule.TryTintHat(applyColor);

            if (GUILayout.Button(T("reset"), buttonStyle, GUILayout.Width(72)))
            {
                hue = 0f; sat = 1f; val = 1f;
                intensity = 6.0f;
                RebuildSVTexture();
                outfitModule.TryTintHat(BuildApplyColor());
                hatSettingsChanged = true;
            }

            if (GUILayout.Button(T("clear"), buttonStyle, GUILayout.Width(72)))
                outfitModule.ClearOverrides();

            GUILayout.EndHorizontal();

            if (ready && liveApply)
                outfitModule.TryTintHat(applyColor);

            GUI.enabled = true;
            GUILayout.EndVertical();

            if (hatSettingsChanged)
            {
                ModUISettings.SetHat(liveApply, hue, sat, val, intensity);
            }
        }

        // -------------------------
        // Color build
        // -------------------------
        private Color BuildPreviewColor()
        {
            Color c = Color.HSVToRGB(hue, sat, val);
            c = ApplyIntensity(c, intensity);
            return Clamp01(c);
        }

        private Color BuildApplyColor()
        {
            Color c = Color.HSVToRGB(hue, sat, val);
            c = ApplyIntensity(c, intensity);

            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                c = c.gamma;

            c.a = 1f;
            return c;
        }

        private static Color ApplyIntensity(Color c, float k)
        {
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        private static Color Clamp01(Color c)
        {
            c.r = Mathf.Clamp01(c.r);
            c.g = Mathf.Clamp01(c.g);
            c.b = Mathf.Clamp01(c.b);
            c.a = 1f;
            return c;
        }

        // -------------------------
        // Picker drawing + input
        // -------------------------
        private void DrawSVPicker(Rect rect)
        {
            if (Mathf.Abs(hue - lastHueForSV) > 0.0001f)
                RebuildSVTexture();

            GUI.DrawTexture(rect, svTex, ScaleMode.StretchToFill, false);

            Vector2 marker = new Vector2(
                rect.x + sat * rect.width,
                rect.y + (1f - val) * rect.height
            );
            DrawCross(marker, 5f);

            HandleSVInput(rect);
        }

        private void DrawHueBar(Rect rect)
        {
            GUI.DrawTexture(rect, hueTex, ScaleMode.StretchToFill, false);

            float y = rect.y + (1f - hue) * rect.height;
            DrawLine(new Vector2(rect.x - 2, y), new Vector2(rect.xMax + 2, y), 2f);

            HandleHueInput(rect);
        }

        private void HandleSVInput(Rect rect)
        {
            Event e = Event.current;
            if (e == null) return;

            bool inRect = rect.Contains(e.mousePosition);

            if (e.type == EventType.MouseDown && e.button == 0 && inRect)
            {
                draggingSV = true;
                e.Use();
                SetSVFromMouse(rect, e.mousePosition);
            }
            else if (e.type == EventType.MouseDrag && draggingSV)
            {
                e.Use();
                SetSVFromMouse(rect, e.mousePosition);
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && draggingSV)
            {
                draggingSV = false;
                e.Use();
            }
        }

        private void HandleHueInput(Rect rect)
        {
            Event e = Event.current;
            if (e == null) return;

            bool inRect = rect.Contains(e.mousePosition);

            if (e.type == EventType.MouseDown && e.button == 0 && inRect)
            {
                draggingHue = true;
                e.Use();
                SetHueFromMouse(rect, e.mousePosition);
            }
            else if (e.type == EventType.MouseDrag && draggingHue)
            {
                e.Use();
                SetHueFromMouse(rect, e.mousePosition);
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && draggingHue)
            {
                draggingHue = false;
                e.Use();
            }
        }

        private void SetSVFromMouse(Rect rect, Vector2 mouse)
        {
            float s = Mathf.InverseLerp(rect.x, rect.xMax, mouse.x);
            float v = 1f - Mathf.InverseLerp(rect.y, rect.yMax, mouse.y);
            sat = Mathf.Clamp01(s);
            val = Mathf.Clamp01(v);
        }

        private void SetHueFromMouse(Rect rect, Vector2 mouse)
        {
            float h = 1f - Mathf.InverseLerp(rect.y, rect.yMax, mouse.y);
            hue = Mathf.Clamp01(h);
            RebuildSVTexture();
        }

        private void RebuildSVTexture()
        {
            lastHueForSV = hue;

            for (int y = 0; y < 256; y++)
            {
                float v = (y / 255f);
                for (int x = 0; x < 256; x++)
                {
                    float s = x / 255f;
                    svTex.SetPixel(x, y, Color.HSVToRGB(hue, s, v));
                }
            }
            svTex.Apply(false, false);
        }

        // -------------------------
        // Helpers
        // -------------------------
        private float SliderRow(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(120));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            GUILayout.Label(value.ToString("F2"), GUILayout.Width(55));
            GUILayout.EndHorizontal();
            return value;
        }

        private bool ToggleRow(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.ExpandWidth(true));
            value = GUILayout.Toggle(value, "");
            GUILayout.EndHorizontal();
            return value;
        }

        private void SetPreviewColor(Color c)
        {
            previewTex.SetPixel(0, 0, c);
            previewTex.Apply(false, false);
        }

        private static Texture2D MakeSolidTex(Color c)
        {
            Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.SetPixel(0, 0, c);
            t.Apply(false, false);
            return t;
        }

        private static Texture2D _whiteTex;
        private static Texture2D WhiteTex
        {
            get
            {
                if (_whiteTex != null) return _whiteTex;
                _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply(false, false);
                return _whiteTex;
            }
        }

        private void DrawCross(Vector2 center, float size)
        {
            DrawLine(new Vector2(center.x - size, center.y), new Vector2(center.x + size, center.y), 2f);
            DrawLine(new Vector2(center.x, center.y - size), new Vector2(center.x, center.y + size), 2f);
        }

        private void DrawLine(Vector2 a, Vector2 b, float thickness)
        {
            Matrix4x4 m = GUI.matrix;

            Vector2 d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float len = d.magnitude;

            GUI.color = Color.black;
            GUI.matrix = Matrix4x4.TRS(a, Quaternion.Euler(0, 0, angle), Vector3.one) * m;

            GUI.DrawTexture(new Rect(0, -thickness / 2f, len, thickness), WhiteTex);

            GUI.matrix = m;
            GUI.color = Color.white;
        }
    }
}
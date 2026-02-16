using MelonLoader;
using System;
using UnityEngine;

namespace GnomiumMod.UI
{
    public class ModUI
    {
        // Referencias (inyectadas desde Main)
        private readonly Func<bool> isReady;
        private readonly Func<float> getHealth;
        private readonly Func<Vector3> getVelocity;

        private readonly CameraModule camModule;
        private readonly OutfitModule outfitModule;
        private readonly Func<bool> getSpeedAll;
        private readonly Action<bool> setSpeedAll;
        private readonly Func<float> getBaseSpeed;
        private readonly Action<float> setBaseSpeed;
        private readonly Func<float> getBoostSpeed;
        private readonly Action<float> setBoostSpeed;

        // UI state
        public bool Visible = true;
        private Rect windowRect;
        private Vector2 scroll;

        // Color picker state (0-255)
        private int r = 255, g = 0, b = 0;
        private string rTxt = "255", gTxt = "0", bTxt = "0";
        private bool liveApply = true;

        // Styles
        private GUIStyle titleStyle;
        private GUIStyle smallStyle;
        private Texture2D previewTex;

        // Escalado “responsive”
        private float uiScale = 1f;

        public ModUI(
            CameraModule cameraModule,
            OutfitModule outfit,
            Func<bool> ready,
            Func<float> health,
            Func<Vector3> velocity,
            Func<bool> speedAllGetter,
            Action<bool> speedAllSetter,
            Func<float> baseSpeedGetter,
            Action<float> baseSpeedSetter,
            Func<float> boostSpeedGetter,
            Action<float> boostSpeedSetter)
        {
            camModule = cameraModule;
            outfitModule = outfit;

            isReady = ready;
            getHealth = health;
            getVelocity = velocity;

            getSpeedAll = speedAllGetter;
            setSpeedAll = speedAllSetter;

            getBaseSpeed = baseSpeedGetter;
            setBaseSpeed = baseSpeedSetter;

            getBoostSpeed = boostSpeedGetter;
            setBoostSpeed = boostSpeedSetter;

            windowRect = new Rect(20, 20, 520, 620);

            previewTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            SetPreviewColor(CurrentColor());
        }

        public void Toggle() => Visible = !Visible;

        public void OnGUI(string modName, string version, string author)
        {
            if (!Visible) return;

            EnsureStyles();

            UpdateScale();
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * uiScale);

            windowRect = GUI.Window(
                id: 34219,
                clientRect: windowRect,
                func: (id) => DrawWindow(modName, version, author),
                text: $"{modName}  v{version}");

            GUI.matrix = old;
        }

        private void UpdateScale()
        {
            // Ajuste simple: escala respecto a 1080p “alto”
            float h = Screen.height;
            uiScale = Mathf.Clamp(h / 1080f, 0.75f, 1.35f);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12
            };
        }

        private void DrawWindow(string modName, string version, string author)
        {
            // Panel scroll
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

            // Header
            GUILayout.Label($"{modName}", titleStyle);
            GUILayout.Label($"Version: {version}   |   Creator: {author}", smallStyle);
            GUILayout.Space(8);

            // Status
            bool ready = isReady();
            GUILayout.Label($"Estado: {(ready ? "✅ Actor OK" : "⏳ Buscando actor/cámara...")}");
            GUILayout.Label($"Vida: {(int)getHealth()}   |   Velocidad: {getVelocity().magnitude:F2}");
            GUILayout.Space(12);

            // Visibilidad UI
            GUILayout.BeginHorizontal();
            GUILayout.Label("UI:");
            Visible = GUILayout.Toggle(Visible, "Visible (INS alterna)");
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            DrawCameraSection(ready);
            GUILayout.Space(12);
            DrawSpeedSection(ready);
            GUILayout.Space(12);
            DrawHatColorSection(ready);

            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label("Hotkeys: INS UI | F7 TPS | F6 Debug", smallStyle);

            GUI.DragWindow(new Rect(0, 0, 2000, 25));
        }

        private void DrawCameraSection(bool ready)
        {
            GUILayout.Label("Cámara (TPS)", titleStyle);

            GUILayout.BeginVertical("box");
            GUI.enabled = ready;

            // TPS
            GUILayout.BeginHorizontal();
            GUILayout.Label("Third Person:", GUILayout.Width(120));
            bool tps = camModule.ThirdPersonEnabled;
            bool newTps = GUILayout.Toggle(tps, tps ? "ON" : "OFF");
            if (newTps != tps) camModule.ToggleThirdPerson();
            GUILayout.EndHorizontal();

            // Settings
            camModule.MouseSensitivity = SliderRow("Mouse Sens", camModule.MouseSensitivity, 0.1f, 10f);
            camModule.CamHeight = SliderRow("Altura", camModule.CamHeight, 0.0f, 3.5f);
            camModule.CamYaw = SliderRow("Yaw", camModule.CamYaw, -180f, 180f);

            float absDist = Mathf.Abs(camModule.CamDistance);
            absDist = SliderRow("Distancia", absDist, camModule.MinZoomAbs, camModule.MaxZoomAbs);
            camModule.CamDistance = -absDist;

            camModule.ZoomStep = SliderRow("Zoom Step", camModule.ZoomStep, 0.01f, 1.0f);

            camModule.InvertScroll = ToggleRow("Invert Scroll", camModule.InvertScroll);

            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        private void DrawSpeedSection(bool ready)
        {
            GUILayout.Label("Velocidad", titleStyle);

            GUILayout.BeginVertical("box");
            GUI.enabled = ready;

            bool spAll = getSpeedAll();
            bool newSpAll = ToggleRow("Speed ALL", spAll);
            if (newSpAll != spAll) setSpeedAll(newSpAll);

            float baseSp = getBaseSpeed();
            float boostSp = getBoostSpeed();

            baseSp = SliderRow("Base Speed", baseSp, 0.5f, 20f);
            boostSp = SliderRow("Boost Speed", boostSp, 0.5f, 40f);

            setBaseSpeed(baseSp);
            setBoostSpeed(boostSp);

            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        private void DrawHatColorSection(bool ready)
        {
            GUILayout.Label("Gorro - Color (RGB)", titleStyle);

            GUILayout.BeginVertical("box");
            GUI.enabled = ready;

            liveApply = ToggleRow("Aplicar en vivo", liveApply);

            // RGB sliders (0-255) + campos
            DrawRgbRow("R", ref r, ref rTxt);
            DrawRgbRow("G", ref g, ref gTxt);
            DrawRgbRow("B", ref b, ref bTxt);

            UnityEngine.Color c = CurrentColor();
            SetPreviewColor(c);

            GUILayout.Space(6);

            // Preview + Hex
            GUILayout.BeginHorizontal();
            GUILayout.Label("Preview:", GUILayout.Width(80));
            GUILayout.Box(previewTex, GUILayout.Width(48), GUILayout.Height(20));
            GUILayout.Label($"  HEX: {ToHex(c)}", GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Aplicar al gorro", GUILayout.Height(28)))
            {
                bool ok = outfitModule.TryTintHat(c);
                MelonLogger.Msg(ok ? "[UI] Hat tint aplicado" : "[UI] No se pudo aplicar hat tint");
            }

            if (GUILayout.Button("Reset (Rojo)", GUILayout.Height(28)))
            {
                r = 255; g = 0; b = 0;
                rTxt = "255"; gTxt = "0"; bTxt = "0";
                Color rc = CurrentColor();
                SetPreviewColor(rc);
                outfitModule.TryTintHat(rc);
            }

            if (GUILayout.Button("Clear Overrides", GUILayout.Height(28)))
            {
                outfitModule.ClearOverrides();
            }
            GUILayout.EndHorizontal();

            // Live apply
            if (liveApply)
            {
                outfitModule.TryTintHat(c);
            }

            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        // -------------------------
        // UI helpers
        // -------------------------
        private float SliderRow(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(120));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(220));
            GUILayout.Label(value.ToString("F2"), GUILayout.Width(70));
            GUILayout.EndHorizontal();
            return value;
        }

        private bool ToggleRow(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(120));
            value = GUILayout.Toggle(value, "");
            GUILayout.EndHorizontal();
            return value;
        }

        private void DrawRgbRow(string label, ref int v, ref string txt)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(20));

            float fv = GUILayout.HorizontalSlider(v, 0, 255, GUILayout.Width(220));
            int newV = Mathf.Clamp(Mathf.RoundToInt(fv), 0, 255);

            // Input box
            txt = GUILayout.TextField(txt, GUILayout.Width(60));
            if (int.TryParse(txt, out int parsed))
            {
                parsed = Mathf.Clamp(parsed, 0, 255);
                newV = parsed;
            }

            // Normaliza el texto
            txt = newV.ToString();
            v = newV;

            GUILayout.EndHorizontal();
        }

        private Color CurrentColor()
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        private void SetPreviewColor(Color c)
        {
            if (previewTex == null) return;
            previewTex.SetPixel(0, 0, c);
            previewTex.Apply(false, false);
        }

        private static string ToHex(Color c)
        {
            int rr = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int gg = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int bb = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            return $"#{rr:X2}{gg:X2}{bb:X2}";
        }
    }
}
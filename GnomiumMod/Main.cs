// Main.cs
using Lightbug.CharacterControllerPro.Core;
using MelonLoader;
using System;
using System.Reflection;
using UnityEngine;
using GnomiumMod.Utilities;

[assembly: MelonInfo(typeof(GnomiumMod.Main), "GnomiumMod", "1.3.5", "DGP")]
[assembly: MelonGame("GamesByFobri", "Gnomium")]

namespace GnomiumMod
{
    public class Main : MelonMod
    {
        private PhysicsActor playerActor;

        private float boostedSpeed = 6f;
        private float baseSpeed = 4f;

        private float currentHealth;
        private Vector3 currentVelocity = Vector3.zero;

        private Camera mainCamera;
        private readonly CameraModule cameraModule = new CameraModule();

        private readonly OutfitModule outfitModule = new OutfitModule();
        private bool outfitDbgOnce;

        private bool dbgPrinted;
        private bool speedAll;

        // Nota: lo dejé porque lo tenías, pero no lo estabas usando realmente.
        private Component playerHealth;

        // -----------------------------
        // Unity / Melon callbacks
        // -----------------------------
        public override void OnGUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16
            };
            style.normal.textColor = Color.green;

            GUILayout.BeginArea(new Rect(10, 10, 650, 320));
            GUILayout.Label($" Vida: {(int)currentHealth}", style);
            GUILayout.Label($" Velocidad: {currentVelocity.magnitude:F2}", style);
            GUILayout.Label($" Cámara TPS: {(cameraModule.ThirdPersonEnabled ? "Sí" : "No")}", style);
            GUILayout.Label($" Altura: {cameraModule.CamHeight:F1} | Distancia: {cameraModule.CamDistance:F1} | Yaw: {cameraModule.CamYaw:F1}", style);
            GUILayout.Label($" Sensibilidad: {cameraModule.MouseSensitivity:F2} | ZoomStep: {cameraModule.ZoomStep:F2}", style);
            GUILayout.EndArea();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            playerActor = null;
            playerHealth = null;
            mainCamera = null;

            dbgPrinted = false;
            outfitDbgOnce = false;
        }

        public override void OnUpdate()
        {
            try
            {
                EnsureCamera();

                if (!dbgPrinted && mainCamera != null)
                {
                    dbgPrinted = true;
                    ModDebug.ListActorsOnce();
                }

                ResolveLocalActor();

                if (playerActor == null) return;

                // Cámara
                cameraModule.SetCamera(mainCamera);
                cameraModule.UpdateInput();

                // Inputs
                HandleHotkeys();

                // Velocidad
                if (speedAll)
                {
                    float speed = Input.GetKey(KeyCode.LeftShift) ? boostedSpeed : baseSpeed;
                    ApplySpeedToAllActors(speed);
                }
                else
                {
                    ApplyLocalSpeedFromInput();
                }

                // Stats overlay
                UpdatePlayerStats();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[GnomiumMod] ❌ Update: {ex.Message}");
            }
        }

        public override void OnLateUpdate()
        {
            cameraModule.ApplyThirdPersonCamera();
        }

        // -----------------------------
        // Core
        // -----------------------------
        private void EnsureCamera()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        private void ResolveLocalActor()
        {
            var local = FindLocalActorByCamera();
            if (local == null || local == playerActor) return;

            playerActor = local;

            cameraModule.SetActor(playerActor);
            outfitModule.SetActor(playerActor);

            MelonLogger.Msg($"[GnomiumMod] ✅ Local actor: {playerActor.name}");

            // Debug outfit una vez
            if (!outfitDbgOnce && playerActor != null)
            {
                outfitDbgOnce = true;

                ModDebug.RenderersUnderActor(playerActor);

                // Si no hay renderers bajo el actor, busca root visual cercano
                var rs = playerActor.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0)
                {
                    var visualRoot = ModDebug.FindClosestVisualRoot(playerActor.transform.position, 6f);
                    if (visualRoot != null)
                    {
                        MelonLogger.Msg($"[OUTFIT] VisualRoot cercano: {visualRoot.name}");
                        outfitModule.SetCustomRoot(visualRoot);
                    }
                }
            }
        }

        private void HandleHotkeys()
        {
            // Ajuste boost
            if (Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                boostedSpeed += 1f;
                MelonLogger.Msg($"[GnomiumMod] ⏫ Boost: {boostedSpeed:F1}");
            }
            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                boostedSpeed = Mathf.Max(1f, boostedSpeed - 1f);
                MelonLogger.Msg($"[GnomiumMod] ⏬ Boost: {boostedSpeed:F1}");
            }

            // Debug camera / head
            if (Input.GetKeyDown(KeyCode.F6))
            {
                ModDebug.CameraCulling(mainCamera);
                ModDebug.HeadState(playerActor);
                ModDebug.HeadControllers(playerActor);
            }

            // TPS
            if (Input.GetKeyDown(KeyCode.F7))
            {
                cameraModule.ToggleThirdPerson();
                cameraModule.ForceHeadVisible(cameraModule.ThirdPersonEnabled, playerActor);
            }

            // Speed all
            if (Input.GetKeyDown(KeyCode.F8))
            {
                speedAll = !speedAll;
                MelonLogger.Msg($"[GnomiumMod] 🌍 Speed ALL: {(speedAll ? "ON" : "OFF")}");
            }

            // Outfit tint
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (playerActor == null) { MelonLogger.Msg("[OUTFIT] playerActor null"); return; }
                outfitModule.SetActor(playerActor);
                outfitModule.ApplyTint(Color.red);
                MelonLogger.Msg("[OUTFIT] Tint rojo aplicado");
            }

            // Outfit debug dump
            if (Input.GetKeyDown(KeyCode.F10))
            {
                if (playerActor == null) { MelonLogger.Msg("[OUTFIT] playerActor null"); return; }
                outfitModule.SetActor(playerActor);
                outfitModule.Dump(true);
            }

            // Clear overrides (lo dejo sin gating raro)
            if (Input.GetKeyDown(KeyCode.F11))
            {
                outfitModule.ClearOverrides();
                MelonLogger.Msg("[OUTFIT] Overrides limpiados");
            }

            // Test: cambiar color del gorro
            if (Input.GetKeyDown(KeyCode.F12))
            {
                if (playerActor == null) { MelonLogger.Msg("[HAT] playerActor null"); return; }

                outfitModule.SetActor(playerActor);
                bool ok = outfitModule.TryTintHat(Color.blue); // prueba azul

                MelonLogger.Msg(ok ? "[HAT] ✅ OK" : "[HAT] ❌ FAIL");
            }
        }

        private void ApplyLocalSpeedFromInput()
        {
            if (playerActor == null) return;

            float horizontal = 0f, vertical = 0f;
            if (Input.GetKey(KeyCode.W)) vertical += 0.1f;
            if (Input.GetKey(KeyCode.S)) vertical -= 0.1f;
            if (Input.GetKey(KeyCode.D)) horizontal += 0.1f;
            if (Input.GetKey(KeyCode.A)) horizontal -= 0.1f;

            Vector2 input = new Vector2(horizontal, vertical);
            if (input.magnitude < 0.01f)
            {
                SetVelocity(playerActor, Vector3.zero);
                return;
            }

            Vector3 moveDir = playerActor.transform.forward * input.y +
                              playerActor.transform.right * input.x;

            moveDir.Normalize();

            float speed = Input.GetKey(KeyCode.LeftShift) ? boostedSpeed : baseSpeed;
            SetVelocity(playerActor, moveDir * speed);
        }

        private void ApplySpeedToAllActors(float speed)
        {
            var actors = UnityEngine.Object.FindObjectsByType<PhysicsActor>(FindObjectsSortMode.None);
            foreach (var a in actors)
            {
                if (a == null) continue;

                Vector3 dir = a.transform.forward; // simple
                SetVelocity(a, dir * speed);
            }
        }

        private void SetVelocity(PhysicsActor a, Vector3 velocity)
        {
            try
            {
                var velocityProp = a.GetType().GetProperty("Velocity",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (velocityProp?.CanWrite == true)
                {
                    Vector3 current = (Vector3)velocityProp.GetValue(a);
                    velocityProp.SetValue(a, new Vector3(velocity.x, current.y, velocity.z));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[GnomiumMod] ❌ Error aplicando velocidad: {ex.Message}");
            }
        }

        private void UpdatePlayerStats()
        {
            if (playerActor == null) return;

            var velocityProp = playerActor.GetType().GetProperty("Velocity",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (velocityProp?.CanRead == true)
                currentVelocity = (Vector3)velocityProp.GetValue(playerActor);

            if (playerHealth != null)
            {
                var healthField = playerHealth.GetType().GetField("currentHealth",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                if (healthField != null)
                    currentHealth = (float)healthField.GetValue(playerHealth);
            }
        }

        // -----------------------------
        // Actor detection (local)
        // -----------------------------
        private PhysicsActor FindLocalActorByCamera()
        {
            var cam = Camera.main;
            if (cam == null) return null;

            var a = cam.GetComponentInParent<PhysicsActor>();
            if (a != null) return a;

            if (cam.transform.root != null)
                return cam.transform.root.GetComponentInChildren<PhysicsActor>(true);

            return null;
        }
    }
}
using Lightbug.CharacterControllerPro.Core;
using MelonLoader;
using System;
using System.Reflection;
using UnityEngine;
using GnomiumMod.Utilities;
using GnomiumMod.UI;

[assembly: MelonInfo(typeof(GnomiumMod.Main), "GnomiumMod", "1.3.5", "DGP")]
[assembly: MelonGame("GamesByFobri", "Gnomium")]

namespace GnomiumMod
{
    public class Main : MelonMod
    {
        // Info (para UI)
        private const string MOD_NAME = "GnomiumMod";
        private const string MOD_VERSION = "1.3.5";
        private const string MOD_AUTHOR = "DGP";

        private PhysicsActor playerActor;

        private float boostedSpeed = 6f;
        private float baseSpeed = 4f;

        private float currentHealth;
        private Vector3 currentVelocity = Vector3.zero;

        private Camera mainCamera;
        private readonly CameraModule cameraModule = new CameraModule();
        private readonly OutfitModule outfitModule = new OutfitModule();

        private ModUI ui;

        private bool dbgPrinted;
        private bool speedAll;

        // Nota: tu juego quizá lo tenga; lo mantengo como estaba (puedes conectarlo si lo encuentras)
        private Component playerHealth;

        private bool outfitDbgOnce;

        public override void OnInitializeMelon()
        {
            ui = new ModUI(
                cameraModule,
                outfitModule,
                ready: () => playerActor != null && mainCamera != null,
                baseSpeedGetter: () => baseSpeed,
                baseSpeedSetter: (v) => baseSpeed = v,
                boostSpeedGetter: () => boostedSpeed,
                boostSpeedSetter: (v) => boostedSpeed = v
            );
        }

        public override void OnGUI()
        {
            ui?.OnGUI(MOD_NAME, MOD_VERSION, MOD_AUTHOR);
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

                // Toggle UI
                if (Input.GetKeyDown(KeyCode.Insert))
                    ui?.Toggle();

                if (!dbgPrinted && mainCamera != null)
                {
                    dbgPrinted = true;
                    ModDebug.ListActorsOnce();
                }

                ResolveLocalActor();

                cameraModule.SetCamera(mainCamera);

                if (playerActor == null) return;

                cameraModule.UpdateInput();

                HandleHotkeysNonUI();

                // Velocidad
                ApplyLocalSpeedFromInput();

                UpdatePlayerStats();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[{MOD_NAME}] ❌ Update: {ex.Message}");
            }
        }

        public override void OnLateUpdate()
        {
            cameraModule.ApplyThirdPersonCamera();
        }

        // -----------------------------
        // Internals
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

            MelonLogger.Msg($"[{MOD_NAME}] ✅ Local actor: {playerActor.name}");

            // Outfit debug una vez
            if (!outfitDbgOnce)
            {
                outfitDbgOnce = true;
                ModDebug.RenderersUnderActor(playerActor);

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

        private void HandleHotkeysNonUI()
        {
            // Debug
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

            Vector3 moveDir =
                playerActor.transform.forward * input.y +
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
                Vector3 dir = a.transform.forward;
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
                MelonLogger.Error($"[{MOD_NAME}] ❌ Error aplicando velocidad: {ex.Message}");
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
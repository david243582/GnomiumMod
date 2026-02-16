using Lightbug.CharacterControllerPro.Core;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

[assembly: MelonInfo(typeof(GnomiumMod.Main), "GnomiumMod", "1.3.5", "DGP")]
[assembly: MelonGame("GamesByFobri", "Gnomium")]

namespace GnomiumMod
{
    public class Main : MelonMod
    {
        private PhysicsActor playerActor;
        private Component playerHealth;
        private Component playerNetworking;

        private float boostedSpeed = 6f;
        private float baseSpeed = 4f;
        private float currentHealth = 0f;
        private Vector3 currentVelocity = Vector3.zero;

        private Camera mainCamera;
        private CameraModule cameraModule = new CameraModule();

        private OutfitModule outfitModule = new OutfitModule();
        private bool outfitReady = false;

        public override void OnGUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 16;
            style.normal.textColor = Color.green;

            GUILayout.BeginArea(new Rect(10, 10, 600, 280));
            GUILayout.Label($" Vida: {(int)currentHealth}", style);
            GUILayout.Label($" Velocidad: {currentVelocity.magnitude:F2}", style);
            GUILayout.Label($" Cámara TPS: {(cameraModule.ThirdPersonEnabled ? "Sí" : "No")}", style);
            GUILayout.Label($" Altura: {cameraModule.CamHeight:F1} | Distancia: {cameraModule.CamDistance:F1} | Yaw: {cameraModule.CamYaw:F1}", style);
            GUILayout.Label($" Sensibilidad Ratón: {cameraModule.MouseSensitivity:F2} | ZoomStep: {cameraModule.ZoomStep:F2}", style);
            GUILayout.EndArea();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            playerActor = null;
            playerHealth = null;
            playerNetworking = null;
            mainCamera = null;
        }

        private bool tpsInit = false;
        private bool speedAll = false;
        private bool outfitDbgOnce = false;

        public override void OnUpdate()
        {
            try
            {
                if (mainCamera == null)
                    mainCamera = Camera.main;

                if (!dbgPrinted && mainCamera != null)
                {
                    dbgPrinted = true;
                    DebugListActorsOnce();
                }

                // En vez de FindFirstObjectByType:
                var local = FindLocalActorByCamera();
                if (local != null && local != playerActor)
                {
                    playerActor = local;

                    cameraModule.SetActor(playerActor);
                    outfitModule.SetActor(playerActor);

                    MelonLogger.Msg($"[SpeedMod] ✅ Local actor: {playerActor.name}");

                    if (!outfitDbgOnce && playerActor != null)
                    {
                        outfitDbgOnce = true;
                        DebugRenderersUnderActor(playerActor);

                        var rs = playerActor.GetComponentsInChildren<Renderer>(true);
                        if (rs.Length == 0)
                        {
                            var visualRoot = FindClosestVisualRoot(playerActor.transform.position, 6f);
                            if (visualRoot != null)
                            {
                                MelonLogger.Msg($"[OUTFIT] VisualRoot cercano: {visualRoot.name}");
                                outfitModule.SetCustomRoot(visualRoot); // te digo abajo cómo
                            }
                        }
                    }
                }

                cameraModule.SetCamera(mainCamera);

                if (playerActor == null) return;

                TryApplyAllSpeedMethods();
                HandleSpeedAdjustInput();
                UpdatePlayerStats();

                cameraModule.UpdateInput();

                if (Input.GetKeyDown(KeyCode.F6))
                {
                    DebugCameraCulling();
                    DebugHeadState();
                    DebugHeadControllers();
                }

                if (Input.GetKeyDown(KeyCode.F7))
                {
                    cameraModule.ToggleThirdPerson();
                    cameraModule.ForceHeadVisible(cameraModule.ThirdPersonEnabled, playerActor);
                }

                if (Input.GetKeyDown(KeyCode.F8))
                {
                    speedAll = !speedAll;
                    MelonLogger.Msg($"[SpeedMod] 🌍 Speed ALL: {(speedAll ? "ON" : "OFF")}");
                }

                if (speedAll)
                {
                    float speed = Input.GetKey(KeyCode.LeftShift) ? boostedSpeed : baseSpeed;
                    ApplySpeedToAllActors(speed);
                }
                else
                {
                    TryApplyAllSpeedMethods();
                }

                if (Input.GetKeyDown(KeyCode.F10))
                {
                    if (playerActor == null) { MelonLogger.Msg("[OUTFIT] playerActor null"); return; }
                    outfitModule.SetActor(playerActor); // por si acaso
                    outfitModule.Dump(true);
                }

                if (Input.GetKeyDown(KeyCode.F9))
                {
                    if (playerActor == null) { MelonLogger.Msg("[OUTFIT] playerActor null"); return; }
                    outfitModule.SetActor(playerActor);
                    outfitModule.ApplyTint(Color.red);
                    MelonLogger.Msg("[OUTFIT] Tint rojo aplicado");
                }

                if (outfitReady && Input.GetKeyDown(KeyCode.F11))
                {
                    outfitModule.ClearOverrides();
                    MelonLogger.Msg("[OUTFIT] Overrides limpiados");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SpeedMod] ❌ Update: {ex.Message}");
            }
        }

        public override void OnLateUpdate()
        {
            cameraModule.ApplyThirdPersonCamera();
        }

        private void UpdatePlayerStats()
        {
            if (playerActor == null) return;

            var velocityProp = playerActor.GetType().GetProperty("Velocity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (velocityProp?.CanRead == true)
                currentVelocity = (Vector3)velocityProp.GetValue(playerActor);

            if (playerHealth != null)
            {
                var healthField = playerHealth.GetType().GetField("currentHealth", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (healthField != null)
                    currentHealth = (float)healthField.GetValue(playerHealth);
            }
        }

        private void HandleSpeedAdjustInput()
        {
            if (Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                boostedSpeed += 1f;
                MelonLogger.Msg($"[SpeedMod] ⏫ Boost: {boostedSpeed:F1}");
            }
            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                boostedSpeed = Mathf.Max(1f, boostedSpeed - 1f);
                MelonLogger.Msg($"[SpeedMod] ⏬ Boost: {boostedSpeed:F1}");
            }
        }

        private void TryApplyAllSpeedMethods()
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
                SetVelocity(Vector3.zero);
                return;
            }

            Vector3 moveDir =
                playerActor.transform.forward * input.y +
                playerActor.transform.right * input.x;

            moveDir.Normalize();

            float speed = Input.GetKey(KeyCode.LeftShift) ? boostedSpeed : baseSpeed;
            SetVelocity(moveDir * speed);
        }

        private void SetVelocity(Vector3 velocity)
        {
            try
            {
                var velocityProp = playerActor.GetType().GetProperty("Velocity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (velocityProp?.CanWrite == true)
                {
                    Vector3 current = (Vector3)velocityProp.GetValue(playerActor);
                    velocityProp.SetValue(playerActor, new Vector3(velocity.x, current.y, velocity.z));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SpeedMod] ❌ Error aplicando velocidad: {ex.Message}");
            }
        }

        private Type GetTypeFromAllAssemblies(string typeName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = asm.GetType(typeName);
                if (type != null)
                    return type;
            }
            return null;
        }

        private PhysicsActor FindLocalActorByCamera()
        {
            var cam = Camera.main;
            if (cam == null) return null;

            // Si la cámara cuelga del jugador local (FPS típico), esto devuelve TU actor
            var a = cam.GetComponentInParent<PhysicsActor>();
            if (a != null) return a;

            // fallback: intenta desde el root
            if (cam.transform.root != null)
                return cam.transform.root.GetComponentInChildren<PhysicsActor>(true);

            return null;
        }

        private PhysicsActor FindLocalActor()
        {
            var actors = UnityEngine.Object.FindObjectsByType<PhysicsActor>(FindObjectsSortMode.None);
            foreach (var a in actors)
            {
                if (a == null) continue;
                if (IsOwnedByLocalClient(a.gameObject)) // <- clave
                    return a;
            }
            return null;
        }

        private bool IsOwnedByLocalClient(GameObject go)
        {
            // Busca cualquier componente cuyo tipo “huela” a Network/Photon/Mirror/FishNet/etc.
            var comps = go.GetComponentsInChildren<Component>(true);
            foreach (var c in comps)
            {
                if (c == null) continue;

                var t = c.GetType();
                var n = (t.FullName ?? "").ToLowerInvariant();

                // heurística: tipos comunes
                if (!(n.Contains("network") || n.Contains("photon") || n.Contains("mirror") || n.Contains("fishnet") || n.Contains("netcode")))
                    continue;

                // propiedades típicas de ownership
                if (GetBoolProp(c, "islocalplayer")) return true;
                if (GetBoolProp(c, "hasauthority")) return true;
                if (GetBoolProp(c, "isowner")) return true;
                if (GetBoolProp(c, "isownedbylocalclient")) return true;
                if (GetBoolProp(c, "amowner")) return true;
                if (GetBoolProp(c, "islocal")) return true;
            }
            return false;
        }

        private bool GetBoolProp(object obj, string propLower)
        {
            var t = obj.GetType();
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (!p.CanRead) continue;
                if ((p.Name ?? "").ToLowerInvariant() != propLower) continue;
                if (p.PropertyType != typeof(bool)) continue;

                try { return (bool)p.GetValue(obj); }
                catch { return false; }
            }
            return false;
        }

        private void ApplySpeedToAllActors(float speed)
        {
            var actors = UnityEngine.Object.FindObjectsByType<PhysicsActor>(FindObjectsSortMode.None);
            foreach (var a in actors)
            {
                if (a == null) continue;

                // Solo si se están moviendo (opcional)
                Vector3 dir = a.transform.forward;
                SetVelocityForActor(a, dir * speed);
            }
        }

        private void SetVelocityForActor(PhysicsActor a, Vector3 velocity)
        {
            var velocityProp = a.GetType().GetProperty("Velocity",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (velocityProp?.CanWrite == true)
            {
                Vector3 current = (Vector3)velocityProp.GetValue(a);
                velocityProp.SetValue(a, new Vector3(velocity.x, current.y, velocity.z));
            }
        }

        private bool dbgPrinted = false;

        private void DebugListActorsOnce()
        {
            var actors = UnityEngine.Object.FindObjectsByType<PhysicsActor>(FindObjectsSortMode.None);
            MelonLogger.Msg($"[DBG] PhysicsActors: {actors.Length}");
            foreach (var a in actors)
            {
                if (a == null) continue;
                MelonLogger.Msg($"[DBG] Actor: {a.name} id={a.GetInstanceID()} pos={a.transform.position}");
            }

            var camA = Camera.main?.GetComponentInParent<PhysicsActor>();
            MelonLogger.Msg($"[DBG] Camera-parent actor: {(camA ? camA.name : "null")}");
        }

        private void DebugRenderersUnderActor(PhysicsActor actor)
        {
            var rs = actor.GetComponentsInChildren<Renderer>(true);
            MelonLogger.Msg($"[OUTFIT-DBG] Renderers under actor '{actor.name}': {rs.Length}");
            foreach (var r in rs)
                MelonLogger.Msg($"[OUTFIT-DBG]  - {r.GetType().Name} '{r.name}'");
        }

        private Transform FindClosestVisualRoot(Vector3 pos, float maxDist = 5f)
        {
            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            Transform bestRoot = null;
            float best = maxDist;

            foreach (var r in all)
            {
                if (r == null) continue;
                float d = Vector3.Distance(r.bounds.center, pos);
                if (d < best)
                {
                    best = d;
                    bestRoot = r.transform.root; // root del modelo
                }
            }
            return bestRoot;
        }

        private void DebugHeadState()
        {
            if (playerActor == null) { MelonLogger.Msg("[HEAD] playerActor null"); return; }

            var head = playerActor.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(r => (r.name ?? "").IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0);

            if (head == null)
            {
                MelonLogger.Msg("[HEAD] No renderer con 'Head' encontrado");
                return;
            }

            var smr = head as SkinnedMeshRenderer;

            MelonLogger.Msg($"[HEAD] Found '{head.name}' type={head.GetType().Name}");
            MelonLogger.Msg($"[HEAD] enabled={head.enabled} activeInHierarchy={head.gameObject.activeInHierarchy}");
            MelonLogger.Msg($"[HEAD] shadowCastingMode={head.shadowCastingMode} receiveShadows={head.receiveShadows}");
            MelonLogger.Msg($"[HEAD] layer={LayerMask.LayerToName(head.gameObject.layer)}({head.gameObject.layer})");

            // Si es skinned, mira si el root bone existe (si se rompe el rig, a veces desaparece)
            if (smr != null)
                MelonLogger.Msg($"[HEAD] rootBone={(smr.rootBone ? smr.rootBone.name : "null")} bones={smr.bones?.Length ?? 0}");

            // Material / shader por si hay un keyword tipo "HIDE_HEAD" (depende del juego)
            var mats = head.sharedMaterials;
            if (mats != null)
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    MelonLogger.Msg($"[HEAD] mat[{i}] '{m.name}' shader='{(m.shader ? m.shader.name : "null")}'");
                }
            }
        }

        private void DebugCameraCulling()
        {
            if (mainCamera == null) { MelonLogger.Msg("[CAM] Camera null"); return; }
            MelonLogger.Msg($"[CAM] cullingMask=0x{mainCamera.cullingMask:X8}");
            MelonLogger.Msg($"[CAM] nearClip={mainCamera.nearClipPlane} farClip={mainCamera.farClipPlane}");
        }

        private void DebugHeadControllers()
        {
            if (playerActor == null) return;

            var headGo = playerActor.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => (t.name ?? "").IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)?.gameObject;

            if (headGo == null) { MelonLogger.Msg("[HEAD] Head GO not found"); return; }

            var comps = headGo.GetComponents<Component>();
            MelonLogger.Msg($"[HEAD] Components on '{headGo.name}': {comps.Length}");
            foreach (var c in comps)
            {
                if (c == null) continue;
                MelonLogger.Msg($"[HEAD]  - {c.GetType().FullName}");
            }
        }
    }
}

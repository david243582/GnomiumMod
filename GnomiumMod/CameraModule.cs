// CameraModule.cs
using Lightbug.CharacterControllerPro.Core;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace GnomiumMod
{
    public class CameraModule
    {
        private Camera cam;
        private PhysicsActor actor;

        public bool ThirdPersonEnabled { get; private set; }

        // -----------------------------
        // Config cámara (TPS)
        // -----------------------------
        public float CamHeight = 1.5f;
        public float CamDistance = -3.5f; // negativo = atrás
        public float CamYaw = 0f;
        public float MouseSensitivity = 1.5f;

        // Zoom con rueda
        public float ZoomStep = 0.2f;
        public float MinZoomAbs = 1.5f;
        public float MaxZoomAbs = 4.5f;
        public bool InvertScroll = false;

        // Pitch
        private float mouseY = 15f;

        // FPS backup
        private bool fpsSaved;
        private Transform fpsParent;
        private Vector3 fpsLocalPos;
        private Quaternion fpsLocalRot;

        // Drivers (CinemachineBrain etc.)
        private readonly List<Behaviour> cameraDrivers = new List<Behaviour>();
        private bool driversCached;

        // -----------------------------
        // Colisión “PRO”
        // -----------------------------
        public float MinDistanceAbs = 0.9f;

        public float SphereRadius = 0.22f;
        public float CollisionBuffer = 0.20f;

        // Suavizado
        public float ZoomInSmoothTime = 0.04f;
        public float ZoomOutSmoothTime = 0.12f;

        // Debounce colisión
        public int OverlapFramesToEngage = 3;
        private int overlapFrames;

        // Ignorar props pequeños
        public float MinObstacleSize = 1.2f;

        public LayerMask CollisionMask = ~0;

        private float currentCamDistAbs = 3.5f;
        private float distVel;

        // Cache colliders del jugador
        private readonly List<Collider> actorColliders = new List<Collider>();
        private bool actorCollidersCached;

        // Buffer OverlapSphere
        private readonly Collider[] overlapBuf = new Collider[32];

        // Head visibility backup
        private ShadowCastingMode headBackupMode = ShadowCastingMode.ShadowsOnly;
        private bool headModeSaved;

        // -----------------------------
        // Public API
        // -----------------------------
        public void SetActor(PhysicsActor a)
        {
            if (actor == a) return;

            actor = a;
            actorCollidersCached = false;
            RefreshActorColliders();
            actorCollidersCached = true;
        }

        public void SetCamera(Camera c)
        {
            cam = c;
            if (cam == null) return;

            if (!driversCached)
            {
                CacheCameraDrivers();
                driversCached = true;
            }

            // init distancia suavizada al zoom actual
            SyncSmoothedDistanceToZoom();
        }

        public void ToggleThirdPerson()
        {
            if (cam == null || actor == null) return;

            ThirdPersonEnabled = !ThirdPersonEnabled;

            if (ThirdPersonEnabled)
            {
                SaveFPSState();
                DisableDrivers();

                SyncSmoothedDistanceToZoom();
                overlapFrames = 0;

                MelonLogger.Msg("[GnomiumMod] 🎥 TPS: ON");
            }
            else
            {
                EnableDrivers();
                RestoreFPSState();
                MelonLogger.Msg("[GnomiumMod] 🎥 TPS: OFF (FPS restaurado)");
            }
        }

        public void UpdateInput()
        {
            if (!ThirdPersonEnabled) return;
            if (actor == null || cam == null) return;

            // Pitch
            mouseY -= Input.GetAxis("Mouse Y") * MouseSensitivity;
            mouseY = Mathf.Clamp(mouseY, -35f, 80f);

            // Zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                if (InvertScroll) scroll = -scroll;

                float abs = Mathf.Abs(CamDistance);
                abs -= scroll * ZoomStep * 10f; // ScrollWheel es pequeño
                abs = Mathf.Clamp(abs, MinZoomAbs, MaxZoomAbs);

                CamDistance = -abs; // mantener atrás
            }
        }

        public void ApplyThirdPersonCamera()
        {
            if (!ThirdPersonEnabled) return;
            if (actor == null || cam == null) return;

            float yaw = actor.transform.eulerAngles.y + CamYaw;
            Quaternion rot = Quaternion.Euler(mouseY, yaw, 0f);

            Vector3 pivot = actor.transform.position + Vector3.up * CamHeight;

            float zoomAbs = Mathf.Clamp(Mathf.Abs(CamDistance), MinZoomAbs, MaxZoomAbs);
            float targetAbs = Mathf.Max(MinDistanceAbs, zoomAbs);

            Vector3 backDir = (rot * Vector3.back).normalized;
            Vector3 idealPos = pivot + backDir * targetAbs;

            bool idealOverlapsBigObstacle = CheckIdealOverlapBigObstacle(idealPos);
            overlapFrames = idealOverlapsBigObstacle ? overlapFrames + 1 : 0;

            float desiredAbs = targetAbs;

            if (overlapFrames >= OverlapFramesToEngage)
            {
                Vector3 castStart = pivot + backDir * 0.08f;

                if (Physics.SphereCast(
                        castStart,
                        SphereRadius,
                        backDir,
                        out RaycastHit hit,
                        targetAbs,
                        CollisionMask,
                        QueryTriggerInteraction.Ignore))
                {
                    if (!IsSelfCollider(hit.collider) && IsBigObstacle(hit.collider))
                        desiredAbs = Mathf.Max(MinDistanceAbs, hit.distance - CollisionBuffer);
                }
            }

            float smooth = (desiredAbs < currentCamDistAbs) ? ZoomInSmoothTime : ZoomOutSmoothTime;
            currentCamDistAbs = Mathf.SmoothDamp(currentCamDistAbs, desiredAbs, ref distVel, smooth);

            cam.transform.position = pivot + backDir * currentCamDistAbs;
            cam.transform.rotation = rot;
        }

        public void ForceHeadVisible(bool visible, PhysicsActor playerActor)
        {
            if (playerActor == null) return;

            var head = playerActor.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(r => (r.name ?? "").IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0);

            if (head == null)
            {
                MelonLogger.Msg("[HEAD] No head renderer found");
                return;
            }

            if (!headModeSaved)
            {
                headBackupMode = head.shadowCastingMode;
                headModeSaved = true;
            }

            head.shadowCastingMode = visible ? ShadowCastingMode.On : headBackupMode;
            MelonLogger.Msg($"[HEAD] shadowCastingMode => {head.shadowCastingMode}");
        }

        // -----------------------------
        // Internals
        // -----------------------------
        private void SyncSmoothedDistanceToZoom()
        {
            float abs = Mathf.Clamp(Mathf.Abs(CamDistance), MinZoomAbs, MaxZoomAbs);
            currentCamDistAbs = Mathf.Max(MinDistanceAbs, abs);
            distVel = 0f;
        }

        private void RefreshActorColliders()
        {
            actorColliders.Clear();
            if (actor == null) return;
            actor.GetComponentsInChildren(true, actorColliders);
        }

        private void CacheCameraDrivers()
        {
            cameraDrivers.Clear();

            foreach (var comp in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (comp == null) continue;

                var full = comp.GetType().FullName ?? "";
                if (full.Contains("CinemachineBrain") && comp is Behaviour b)
                    cameraDrivers.Add(b);
            }
        }

        private void DisableDrivers()
        {
            for (int i = 0; i < cameraDrivers.Count; i++)
                if (cameraDrivers[i] != null) cameraDrivers[i].enabled = false;
        }

        private void EnableDrivers()
        {
            for (int i = 0; i < cameraDrivers.Count; i++)
                if (cameraDrivers[i] != null) cameraDrivers[i].enabled = true;
        }

        private void SaveFPSState()
        {
            if (cam == null) return;

            fpsParent = cam.transform.parent;
            fpsLocalPos = cam.transform.localPosition;
            fpsLocalRot = cam.transform.localRotation;
            fpsSaved = true;
        }

        private void RestoreFPSState()
        {
            if (cam == null || !fpsSaved) return;

            cam.transform.parent = fpsParent;
            cam.transform.localPosition = fpsLocalPos;
            cam.transform.localRotation = fpsLocalRot;

            Input.ResetInputAxes();
        }

        private bool CheckIdealOverlapBigObstacle(Vector3 idealPos)
        {
            int count = Physics.OverlapSphereNonAlloc(
                idealPos,
                SphereRadius,
                overlapBuf,
                CollisionMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider c = overlapBuf[i];
                if (c == null) continue;
                if (IsSelfCollider(c)) continue;
                if (!IsBigObstacle(c)) continue;
                return true;
            }
            return false;
        }

        private bool IsBigObstacle(Collider c) => c != null && c.bounds.size.magnitude >= MinObstacleSize;

        private bool IsSelfCollider(Collider c)
        {
            if (c == null || actor == null) return false;

            if (c.transform != null && c.transform.IsChildOf(actor.transform))
                return true;

            if (!actorCollidersCached) RefreshActorColliders();

            for (int i = 0; i < actorColliders.Count; i++)
                if (actorColliders[i] == c) return true;

            return false;
        }
    }
}
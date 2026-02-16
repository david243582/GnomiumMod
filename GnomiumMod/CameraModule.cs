using Lightbug.CharacterControllerPro.Core;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;


namespace GnomiumMod
{
    public class CameraModule
    {
        private Camera cam;
        private PhysicsActor actor;

        public bool ThirdPersonEnabled { get; private set; } = false;

        // Config cámara
        public float CamHeight = 1.5f;
        public float CamDistance = -3.5f; // negativo = atrás
        public float CamYaw = 0f;
        public float MouseSensitivity = 1.5f;

        // ✅ Zoom con rueda
        public float ZoomStep = 0.2f;      // cuánto cambia por “tick” de rueda (ajusta a gusto)
        public float MinZoomAbs = 1.5f;    // más cerca
        public float MaxZoomAbs = 4.5f;   // más lejos
        public bool InvertScroll = false; // por si lo quieres al revés

        private float mouseY = 15f;

        // FPS backup
        private bool fpsSaved = false;
        private Transform fpsParent;
        private Vector3 fpsLocalPos;
        private Quaternion fpsLocalRot;

        // Drivers (CinemachineBrain etc.)
        private readonly List<Behaviour> cameraDrivers = new List<Behaviour>();
        private bool driversCached = false;

        // -----------------------------
        // Colisión “PRO”: solo ajusta si la cámara IDEAL está en colisión
        // -----------------------------
        public float MinDistanceAbs = 0.9f;

        public float SphereRadius = 0.22f;     // radio de la cámara (colisión)
        public float CollisionBuffer = 0.20f;  // margen para no pegarse a la pared

        // Suavizado
        public float ZoomInSmoothTime = 0.04f;   // acercar
        public float ZoomOutSmoothTime = 0.12f;  // alejar

        // “Debounce”: exige colisión N frames seguidos antes de acercar (evita pops)
        public int OverlapFramesToEngage = 3;
        private int overlapFrames = 0;

        // Ignorar “cosas pequeñas” (props). Solo reaccionar a paredes/casas.
        public float MinObstacleSize = 1.2f; // tamaño mínimo (magnitud de bounds.size)

        // Máscara de colisión
        public LayerMask CollisionMask = ~0;

        private float currentCamDistAbs = 3.5f;
        private float distVel = 0f;

        // Cache colliders del jugador
        private readonly List<Collider> actorColliders = new List<Collider>();
        private bool actorCollidersCached = false;

        // Buffer para OverlapSphere (evita alloc)
        private readonly Collider[] overlapBuf = new Collider[32];

        private ShadowCastingMode headBackupMode = ShadowCastingMode.ShadowsOnly;
        private bool headModeSaved = false;

        public void ForceHeadVisible(bool visible, PhysicsActor playerActor)
        {
            if (playerActor == null) return;

            var head = playerActor.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(r => (r.name ?? "").IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0);

            if (head == null) { MelonLogger.Msg("[HEAD] No head renderer found"); return; }

            if (!headModeSaved)
            {
                headBackupMode = head.shadowCastingMode;
                headModeSaved = true;
            }

            head.shadowCastingMode = visible ? ShadowCastingMode.On : headBackupMode;

            MelonLogger.Msg($"[HEAD] shadowCastingMode => {head.shadowCastingMode}");
        }

        public void SetActor(PhysicsActor a)
        {
            if (actor == a) return;

            actor = a;
            actorCollidersCached = false;
            RefreshActorColliders();
            actorCollidersCached = true;
        }

        private void RefreshActorColliders()
        {
            actorColliders.Clear();
            if (actor == null) return;
            actor.GetComponentsInChildren(true, actorColliders);
        }

        public void SetCamera(Camera c)
        {
            cam = c;
            if (cam != null && !driversCached)
            {
                CacheCameraDrivers();
                driversCached = true;

                // Inicializa distancia suavizada al “zoom” actual
                float abs = Mathf.Clamp(Mathf.Abs(CamDistance), MinZoomAbs, MaxZoomAbs);
                currentCamDistAbs = Mathf.Max(MinDistanceAbs, abs);
                distVel = 0f;
            }
        }

        private void CacheCameraDrivers()
        {
            cameraDrivers.Clear();

            foreach (var comp in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (comp == null) continue;
                var full = comp.GetType().FullName ?? "";
                if (full.Contains("CinemachineBrain"))
                {
                    if (comp is Behaviour b) cameraDrivers.Add(b);
                }
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

        public void ToggleThirdPerson()
        {
            if (cam == null || actor == null) return;

            ThirdPersonEnabled = !ThirdPersonEnabled;

            if (ThirdPersonEnabled)
            {
                SaveFPSState();
                DisableDrivers();

                // Re-sync de distancia suavizada con el zoom actual
                float abs = Mathf.Clamp(Mathf.Abs(CamDistance), MinZoomAbs, MaxZoomAbs);
                currentCamDistAbs = Mathf.Max(MinDistanceAbs, abs);
                distVel = 0f;
                overlapFrames = 0;

                MelonLogger.Msg("[SpeedMod] 🎥 TPS: ON");
            }
            else
            {
                EnableDrivers();
                RestoreFPSState();
                MelonLogger.Msg("[SpeedMod] 🎥 TPS: OFF (FPS restaurado)");
            }
        }

        // Input en Update
        public void UpdateInput()
        {
            if (!ThirdPersonEnabled) return;
            if (actor == null || cam == null) return;

            // Pitch
            mouseY -= Input.GetAxis("Mouse Y") * MouseSensitivity;
            mouseY = Mathf.Clamp(mouseY, -35f, 80f);

            // ✅ Zoom con rueda
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                if (InvertScroll) scroll = -scroll;

                // scroll>0 suele ser “hacia arriba”
                // Queremos que “arriba” = acercar (menos distancia absoluta)
                float abs = Mathf.Abs(CamDistance);
                abs -= scroll * ZoomStep * 10f; // multiplicador porque ScrollWheel es pequeño

                abs = Mathf.Clamp(abs, MinZoomAbs, MaxZoomAbs);

                // Mantener signo negativo (atrás)
                CamDistance = -abs;
            }
        }

        // Cámara en LateUpdate
        public void ApplyThirdPersonCamera()
        {
            if (!ThirdPersonEnabled) return;
            if (actor == null || cam == null) return;

            float yaw = actor.transform.eulerAngles.y + CamYaw;
            Quaternion rot = Quaternion.Euler(mouseY, yaw, 0f);

            Vector3 pivot = actor.transform.position + Vector3.up * CamHeight;

            // ✅ targetAbs viene del zoom actual (CamDistance)
            float zoomAbs = Mathf.Clamp(Mathf.Abs(CamDistance), MinZoomAbs, MaxZoomAbs);
            float targetAbs = Mathf.Max(MinDistanceAbs, zoomAbs);

            Vector3 backDir = (rot * Vector3.back).normalized;

            // 1) Posición IDEAL (sin colisión)
            Vector3 idealPos = pivot + backDir * targetAbs;

            // 2) SOLO si la IDEAL está en colisión con algo grande
            bool idealOverlapsBigObstacle = CheckIdealOverlapBigObstacle(idealPos);

            if (idealOverlapsBigObstacle)
                overlapFrames++;
            else
                overlapFrames = 0;

            float desiredAbs = targetAbs;

            if (overlapFrames >= OverlapFramesToEngage)
            {
                // 3) Distancia segura con SphereCast
                Vector3 castStart = pivot + backDir * 0.08f;

                if (Physics.SphereCast(castStart, SphereRadius, backDir, out RaycastHit hit, targetAbs, CollisionMask, QueryTriggerInteraction.Ignore))
                {
                    if (!IsSelfCollider(hit.collider) && IsBigObstacle(hit.collider))
                        desiredAbs = Mathf.Max(MinDistanceAbs, hit.distance - CollisionBuffer);
                }
            }

            // 4) Suavizado asimétrico
            float smooth = (desiredAbs < currentCamDistAbs) ? ZoomInSmoothTime : ZoomOutSmoothTime;
            currentCamDistAbs = Mathf.SmoothDamp(currentCamDistAbs, desiredAbs, ref distVel, smooth);

            Vector3 finalPos = pivot + backDir * currentCamDistAbs;
            cam.transform.position = finalPos;
            cam.transform.rotation = rot;
        }

        private bool CheckIdealOverlapBigObstacle(Vector3 idealPos)
        {
            int count = Physics.OverlapSphereNonAlloc(
                idealPos,
                SphereRadius,
                overlapBuf,
                CollisionMask,
                QueryTriggerInteraction.Ignore
            );

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

        private bool IsBigObstacle(Collider c)
        {
            float sizeMag = c.bounds.size.magnitude;
            return sizeMag >= MinObstacleSize;
        }

        private bool IsSelfCollider(Collider c)
        {
            if (c == null || actor == null) return false;

            if (c.transform != null && c.transform.IsChildOf(actor.transform))
                return true;

            for (int i = 0; i < actorColliders.Count; i++)
                if (actorColliders[i] == c) return true;

            return false;
        }
    }
}

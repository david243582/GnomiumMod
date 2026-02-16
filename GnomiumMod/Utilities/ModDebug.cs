// Utilities/ModDebug.cs
using Lightbug.CharacterControllerPro.Core;
using MelonLoader;
using System;
using System.Linq;
using UnityEngine;

namespace GnomiumMod.Utilities
{
    internal static class ModDebug
    {
        public static void ListActorsOnce()
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

        public static void RenderersUnderActor(PhysicsActor actor)
        {
            if (actor == null) return;

            var rs = actor.GetComponentsInChildren<Renderer>(true);
            MelonLogger.Msg($"[OUTFIT-DBG] Renderers under actor '{actor.name}': {rs.Length}");
            foreach (var r in rs)
                MelonLogger.Msg($"[OUTFIT-DBG]  - {r.GetType().Name} '{r.name}'");
        }

        public static Transform FindClosestVisualRoot(Vector3 pos, float maxDist = 5f)
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
                    bestRoot = r.transform.root;
                }
            }

            return bestRoot;
        }

        public static void CameraCulling(Camera cam)
        {
            if (cam == null) { MelonLogger.Msg("[CAM] Camera null"); return; }
            MelonLogger.Msg($"[CAM] cullingMask=0x{cam.cullingMask:X8}");
            MelonLogger.Msg($"[CAM] nearClip={cam.nearClipPlane} farClip={cam.farClipPlane}");
        }

        public static void HeadState(PhysicsActor playerActor)
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

            if (smr != null)
                MelonLogger.Msg($"[HEAD] rootBone={(smr.rootBone ? smr.rootBone.name : "null")} bones={smr.bones?.Length ?? 0}");

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

        public static void HeadControllers(PhysicsActor playerActor)
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
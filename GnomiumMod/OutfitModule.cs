using Lightbug.CharacterControllerPro.Core;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GnomiumMod
{
    public class OutfitModule
    {
        private PhysicsActor actor;
        private Transform root;

        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        private bool cached;

        public bool Enabled { get; set; } = true;
        public bool OnlyClothes { get; set; } = true;

        public readonly HashSet<string> IncludeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cloth","clothes","outfit","wear","gear","armor","armour",
            "shirt","pants","boot","shoe","glove","hat","cap","helmet",
            "backpack","bag","cape","cloak","jacket"
        };

        public readonly HashSet<string> ExcludeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "body","skin","face","head","eye","eyes","hair","teeth","tongue"
        };

        // Shader props
        internal static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        internal static readonly int ColorId = Shader.PropertyToID("_Color");
        internal static readonly int TintId = Shader.PropertyToID("_TintColor");
        internal static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        // ----------------------------
        // Public API
        // ----------------------------
        public void SetActor(PhysicsActor a)
        {
            actor = a;
            root = a != null ? a.transform : null;
            InvalidateCache();
            RebuildCache();
        }

        public void SetCustomRoot(Transform customRoot)
        {
            root = customRoot;
            InvalidateCache();
            RebuildCache();
        }

        public void InvalidateCache()
        {
            cached = false;
            renderers.Clear();
        }

        public void RebuildCache()
        {
            renderers.Clear();
            if (root == null) { cached = false; return; }

            root.GetComponentsInChildren(true, renderers);
            cached = true;

            MelonLogger.Msg($"[OUTFIT] Cache renderers: {renderers.Count} root='{root.name}'");
        }

        public void Dump(bool includeMaterials = true)
        {
            if (actor == null) { MelonLogger.Msg("[OUTFIT] Dump: actor null"); return; }
            if (!cached) RebuildCache();

            MelonLogger.Msg($"[OUTFIT] Dump actor='{actor.name}' renderers={renderers.Count}");
            foreach (var r in renderers)
            {
                if (r == null) continue;

                string path = GetTransformPath(r.transform, actor.transform);
                bool ok = ShouldAffectRenderer(r, path);

                MelonLogger.Msg($"[OUTFIT] {(ok ? "✅" : "❌")} {r.GetType().Name} '{r.name}' path='{path}' mats={r.sharedMaterials?.Length ?? 0}");

                if (!includeMaterials || r.sharedMaterials == null) continue;

                for (int i = 0; i < r.sharedMaterials.Length; i++)
                {
                    var m = r.sharedMaterials[i];
                    if (m == null) continue;
                    MelonLogger.Msg($"        mat[{i}] '{m.name}' shader='{(m.shader ? m.shader.name : "null")}'");
                }
            }
        }

        public void ApplyTint(Color tint, bool alsoEmission = false)
        {
            if (!Enabled) return;
            if (actor == null) return;
            if (!cached) RebuildCache();

            foreach (var r in renderers)
            {
                if (r == null) continue;

                string path = GetTransformPath(r.transform, actor.transform);
                if (!ShouldAffectRenderer(r, path)) continue;

                r.GetPropertyBlock(mpb);

                if (RendererHasProp(r, BaseColorId)) mpb.SetColor(BaseColorId, tint);
                if (RendererHasProp(r, ColorId)) mpb.SetColor(ColorId, tint);
                if (RendererHasProp(r, TintId)) mpb.SetColor(TintId, tint);

                if (alsoEmission && RendererHasProp(r, EmissionId))
                    mpb.SetColor(EmissionId, tint);

                r.SetPropertyBlock(mpb);
            }
        }

        public void ClearOverrides()
        {
            if (actor == null) return;
            if (!cached) RebuildCache();

            foreach (var r in renderers)
            {
                if (r == null) continue;
                mpb.Clear();
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Cambia el color del gorro/ropa que está dentro del renderer de la cabeza
        /// usando property blocks por sub-material (submesh).
        /// </summary>
        public bool TryTintHat(Color tint, bool alsoEmission = false)
        {
            if (actor == null) return false;

            var headR = actor.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(r => (r.name ?? "").IndexOf("Head.001", StringComparison.OrdinalIgnoreCase) >= 0
                                  || (r.name ?? "").IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0);

            if (headR == null)
            {
                MelonLogger.Msg("[HAT] No se encontró renderer Head");
                return false;
            }

            var mats = headR.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                MelonLogger.Msg("[HAT] Head sin materiales");
                return false;
            }

            int clothesMatIndex = -1;

            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;

                string mn = (m.name ?? "").ToLowerInvariant();
                string sn = (m.shader ? m.shader.name : "").ToLowerInvariant();

                // En tu dump: "Clothes (Instance)"
                if (mn.Contains("clothes") || mn.Contains("cloth") || mn.Contains("hat") || mn.Contains("cap") || sn.Contains("cloth"))
                {
                    clothesMatIndex = i;
                    break;
                }
            }

            if (clothesMatIndex < 0)
            {
                MelonLogger.Msg("[HAT] No se encontró material de ropa en Head (clothes/cloth/hat/cap)");
                return false;
            }

            // PropertyBlock por submesh
            headR.GetPropertyBlock(mpb, clothesMatIndex);

            if (RendererHasProp(headR, BaseColorId)) mpb.SetColor(BaseColorId, tint);
            if (RendererHasProp(headR, ColorId)) mpb.SetColor(ColorId, tint);
            if (RendererHasProp(headR, TintId)) mpb.SetColor(TintId, tint);

            if (alsoEmission && RendererHasProp(headR, EmissionId))
                mpb.SetColor(EmissionId, tint);

            headR.SetPropertyBlock(mpb, clothesMatIndex);

            //MelonLogger.Msg($"[HAT] Tint aplicado a Head='{headR.name}' matIndex={clothesMatIndex} mat='{mats[clothesMatIndex]?.name}'");
            return true;
        }

        // ----------------------------
        // Internals
        // ----------------------------
        private bool ShouldAffectRenderer(Renderer r, string path)
        {
            if (!OnlyClothes) return true;

            string name = (r.name ?? "").ToLowerInvariant();
            string p = (path ?? "").ToLowerInvariant();

            string matInfo = "";
            var mats = r.sharedMaterials;
            if (mats != null)
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    matInfo += " " + (m.name ?? "");
                    matInfo += " " + (m.shader ? m.shader.name : "");
                }
            }
            matInfo = matInfo.ToLowerInvariant();

            if (ExcludeKeywords.Any(k => name.Contains(k) || p.Contains(k) || matInfo.Contains(k)))
                return false;

            if (IncludeKeywords.Any(k => name.Contains(k) || p.Contains(k) || matInfo.Contains(k)))
                return true;

            return false;
        }

        private bool RendererHasProp(Renderer r, int propId)
        {
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) return false;

            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                try
                {
                    if (m.HasProperty(propId)) return true;
                }
                catch { }
            }
            return false;
        }

        private static string GetTransformPath(Transform t, Transform root)
        {
            if (t == null) return "";

            var parts = new List<string>();
            while (t != null && t != root)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
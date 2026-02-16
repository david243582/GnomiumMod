using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Lightbug.CharacterControllerPro.Core;

namespace GnomiumMod
{
    public class OutfitModule
    {
        private PhysicsActor actor;

        // Cache
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        private bool cached = false;

        // Config / filtros
        public bool Enabled { get; set; } = true;

        // Si es true, filtra por keywords (ropa). Si es false, pinta todo (cuerpo+ropa)
        public bool OnlyClothes { get; set; } = true;

        // Keywords típicas para detectar “ropa” por nombre/path/material/shader
        public readonly HashSet<string> IncludeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cloth","clothes","outfit","wear","gear","armor","armour",
            "shirt","pants","boot","shoe","glove","hat","cap","helmet",
            "backpack","bag","cape","cloak","jacket"
        };

        // Cosas que normalmente NO quieres teñir
        public readonly HashSet<string> ExcludeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "body","skin","face","head","eye","eyes","hair","teeth","tongue"
        };

        // Propiedades comunes de shader para color
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TintId = Shader.PropertyToID("_TintColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private Transform root;

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

                // Aplica solo si el material soporta la propiedad
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

        // ----------------------------
        // Internals
        // ----------------------------
        private bool ShouldAffectRenderer(Renderer r, string path)
        {
            if (!OnlyClothes) return true;

            // Construye un “texto” para matching
            string name = (r.name ?? "").ToLowerInvariant();
            string p = (path ?? "").ToLowerInvariant();

            // materiales/shaders también ayudan a identificar
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

            // Exclude gana siempre
            if (ExcludeKeywords.Any(k => name.Contains(k) || p.Contains(k) || matInfo.Contains(k)))
                return false;

            // Include: si contiene cualquiera, lo consideramos ropa
            if (IncludeKeywords.Any(k => name.Contains(k) || p.Contains(k) || matInfo.Contains(k)))
                return true;

            return false;
        }

        private bool RendererHasProp(Renderer r, int propId)
        {
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) return false;

            // Con que uno lo soporte, aplicamos (suele bastar)
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                try
                {
                    if (m.HasProperty(propId)) return true;
                }
                catch { /* ignore */ }
            }
            return false;
        }

        private string GetTransformPath(Transform t, Transform root)
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
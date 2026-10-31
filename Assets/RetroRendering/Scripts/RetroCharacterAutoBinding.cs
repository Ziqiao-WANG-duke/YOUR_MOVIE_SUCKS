using System.Collections.Generic;
using UnityEngine;

namespace YourMovieSucks.Retro
{
    /// <summary>
    /// Opt-in runtime binding for spawned characters. The shader is swapped in place on
    /// each renderer's material instances, so gameplay code that cached material.color
    /// (e.g. enemy threat tint) keeps writing to the same material object.
    /// Only runtime instances are touched; shared material assets are never modified.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetroCharacterAutoBinding : MonoBehaviour
    {
        public RetroRenderingController controller;
        public Shader retroCharacterShader;
        [Tooltip("Component type name whose children are treated as characters.")]
        public string targetComponentName = "TypingTarget";
        [Min(0.05f)] public float rescanInterval = 0.5f;
        [Range(0, 1)] public float smoothness = 0.15f;

        private sealed class Entry
        {
            public Material material;
            public Shader originalShader;
            public Color originalBaseColor, originalEmission;
            public float originalSmoothness, originalCull, originalClip, originalCutoff;
            public bool originalEmissionKeyword;
        }

        private readonly List<Entry> entries = new List<Entry>(8);
        private readonly HashSet<Material> seen = new HashSet<Material>();
        private float nextScan;
        private bool warned;

        private void LateUpdate()
        {
            if (!controller || !controller.retroEnabled)
            {
                Restore();
                return;
            }
            if (!controller.IsActive) return;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + rescanInterval;
                Scan();
            }
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!entry.material) continue;
                if (entry.material.HasProperty("_SnapStrength")) entry.material.SetFloat("_SnapStrength", controller.vertexSnapping);
                if (entry.material.HasProperty("_AffineStrength")) entry.material.SetFloat("_AffineStrength", controller.affineMapping);
                if (entry.material.HasProperty("_SnapResolution")) entry.material.SetVector("_SnapResolution", new Vector4(controller.RenderWidth, controller.RenderHeight, 0f, 0f));
            }
        }

        private void Scan()
        {
            if (!retroCharacterShader) retroCharacterShader = Shader.Find("RetroRendering/Character");
            if (!retroCharacterShader || !retroCharacterShader.isSupported)
            {
                if (!warned) Debug.LogWarning("[RetroCharacterAutoBinding] Character shader unavailable; characters keep original materials.", this);
                warned = true;
                return;
            }
            seen.Clear();
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var behaviour in behaviours)
            {
                if (!behaviour || behaviour.GetType().Name != targetComponentName) continue;
                foreach (var renderer in behaviour.GetComponentsInChildren<Renderer>(false))
                {
                    if (!renderer || !renderer.enabled) continue;
                    // renderer.materials returns per-renderer instances; never the asset.
                    foreach (var material in renderer.materials)
                    {
                        if (!material || material.shader == retroCharacterShader || !seen.Add(material)) continue;
                        Swap(material);
                    }
                }
            }
            for (int i = entries.Count - 1; i >= 0; i--)
                if (!entries[i].material) entries.RemoveAt(i);
        }

        private void Swap(Material material)
        {
            var entry = new Entry
            {
                material = material,
                originalShader = material.shader,
                originalBaseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white,
                originalEmission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black,
                originalSmoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0f,
                originalCull = material.HasProperty("_Cull") ? material.GetFloat("_Cull") : 2f,
                originalClip = material.HasProperty("_AlphaClip") ? material.GetFloat("_AlphaClip") : 0f,
                originalCutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f,
                originalEmissionKeyword = material.IsKeywordEnabled("_EMISSION")
            };
            material.shader = retroCharacterShader;
            material.SetFloat("_UseBaseMap", material.GetTexture("_BaseMap") ? 1f : 0f);
            material.SetFloat("_UseNormalMap", material.GetTexture("_BumpMap") && entry.originalShader.name == "Universal Render Pipeline/Lit" && material.IsKeywordEnabled("_NORMALMAP") ? 1f : 0f);
            material.SetFloat("_UseEmissionMap", material.GetTexture("_EmissionMap") && entry.originalEmissionKeyword ? 1f : 0f);
            material.SetFloat("_UseVertexColor", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_VertexSnap", 1f);
            material.SetFloat("_Affine", 1f);
            entries.Add(entry);
        }

        public void Restore()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!entry.material || !entry.originalShader) continue;
                entry.material.shader = entry.originalShader;
                if (entry.material.HasProperty("_BaseColor")) entry.material.SetColor("_BaseColor", entry.originalBaseColor);
                if (entry.material.HasProperty("_EmissionColor")) entry.material.SetColor("_EmissionColor", entry.originalEmission);
                if (entry.material.HasProperty("_Smoothness")) entry.material.SetFloat("_Smoothness", entry.originalSmoothness);
                if (entry.material.HasProperty("_Cull")) entry.material.SetFloat("_Cull", entry.originalCull);
                if (entry.material.HasProperty("_AlphaClip")) entry.material.SetFloat("_AlphaClip", entry.originalClip);
                if (entry.material.HasProperty("_Cutoff")) entry.material.SetFloat("_Cutoff", entry.originalCutoff);
                if (entry.originalEmissionKeyword) entry.material.EnableKeyword("_EMISSION"); else entry.material.DisableKeyword("_EMISSION");
            }
            entries.Clear();
        }

        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();
    }
}

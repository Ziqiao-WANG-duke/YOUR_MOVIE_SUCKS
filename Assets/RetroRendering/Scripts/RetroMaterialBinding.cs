using UnityEngine;

namespace YourMovieSucks.Retro
{
    /// <summary>
    /// Runtime-only retro pass for one renderer. The runtime material is derived from the
    /// renderer's *current* material, so edits made in the Scene (color / texture / emission)
    /// are followed in Play Mode without regenerating any asset. The optional retroVariants
    /// entry only supplies the shader and the lighting knobs. Never writes material assets.
    /// </summary>
    public sealed class RetroMaterialBinding : MonoBehaviour
    {
        public Renderer target;
        [Tooltip("模板（可选）：只提供 Shader 与光照调参。留空 = 自动选 Environment/Character；颜色与贴图永远取自物体当前材质。")]
        public Material[] retroVariants;
        public bool controlSnapping = true;
        [Tooltip("Disable to use the material's own _SnapResolution grid.")]
        public bool controlSnapGrid = true;
        public bool controlAffine = true;
        [Tooltip("Environment shading selector should not flatten characters unless explicitly requested.")]
        public bool controlShading;
        private Material[] original, instances;

        public void Apply(RetroRenderingController controller)
        {
            if (!target) target = GetComponent<Renderer>();
            if (!target) return;

            // 场景里的材质槽被换过（用户改了材质）→ 按新的重新生成，而不是继续用旧副本
            if (instances != null && !SameMaterials(target.sharedMaterials, instances)) Restore();
            if (instances == null) Build();

            for (int i = 0; i < instances.Length; i++)
            {
                var m = instances[i];
                // A null variant is an untouched slot, even when the original is retro.
                if (!m || m == original[i] || !m.shader.name.StartsWith("RetroRendering/")) continue;
                if (controlSnapping && m.HasProperty("_SnapStrength")) m.SetFloat("_SnapStrength", controller.vertexSnapping);
                if (controlAffine && m.HasProperty("_AffineStrength")) m.SetFloat("_AffineStrength", controller.affineMapping);
                if (controlSnapGrid && m.HasProperty("_SnapResolution")) m.SetVector("_SnapResolution", new Vector4(controller.RenderWidth, controller.RenderHeight, 0, 0));
                if (controlShading && m.HasProperty("_FlatShading")) m.SetFloat("_FlatShading", controller.shading == RetroShading.Faceted ? 1 : 0);
            }
        }

        private static bool SameMaterials(Material[] live, Material[] built)
        {
            if (live == null || built == null || live.Length != built.Length) return false;
            for (int i = 0; i < live.Length; i++) if (live[i] != built[i]) return false;
            return true;
        }

        /// <summary>按 target 当前的材质生成运行时材质：保留颜色/贴图/发光/裁切，只换 Shader 并套调参。</summary>
        private void Build()
        {
            original = target.sharedMaterials;
            instances = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                var src = original[i];
                if (!src) { instances[i] = src; continue; }

                var template = retroVariants != null && i < retroVariants.Length ? retroVariants[i] : null;
                var shader = template && template.shader
                    ? template.shader
                    : Shader.Find(IsCharacter(src) ? "RetroRendering/Character" : "RetroRendering/Environment");
                if (!shader) { instances[i] = src; continue; }

                // 从当前材质复制 → 用户在 Scene 里调的颜色/贴图/平滑度都会带进来
                var m = new Material(src) { name = src.name + " (Retro Runtime)" };
                m.shader = shader;

                // 贴图开关按当前材质实际有没有贴图来定（不是照抄模板）
                if (m.HasProperty("_UseBaseMap")) m.SetFloat("_UseBaseMap", src.GetTexture("_BaseMap") ? 1f : 0f);
                if (m.HasProperty("_UseNormalMap")) m.SetFloat("_UseNormalMap", src.GetTexture("_BumpMap") && src.IsKeywordEnabled("_NORMALMAP") ? 1f : 0f);
                if (m.HasProperty("_UseEmissionMap")) m.SetFloat("_UseEmissionMap", src.GetTexture("_EmissionMap") ? 1f : 0f);
                if (m.HasProperty("_UseVertexColor")) m.SetFloat("_UseVertexColor", 0f);
                if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", src.HasProperty("_AlphaClip") ? src.GetFloat("_AlphaClip") : 0f);
                if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", src.HasProperty("_Cull") ? src.GetFloat("_Cull") : 2f);

                CopyTuning(template, m);
                instances[i] = m;
            }
            target.sharedMaterials = instances;
        }

        /// <summary>把模板上的光照调参搬到运行时材质；没有模板就用一套默认值。</summary>
        private static void CopyTuning(Material template, Material m)
        {
            float Default(string name, float value) => template && template.HasProperty(name) ? template.GetFloat(name) : value;
            if (m.HasProperty("_AmbientColor"))
                m.SetColor("_AmbientColor", template && template.HasProperty("_AmbientColor") ? template.GetColor("_AmbientColor") : new Color(0.55f, 0.47f, 0.68f, 1f));
            if (m.HasProperty("_AmbientStrength")) m.SetFloat("_AmbientStrength", Default("_AmbientStrength", 1.15f));
            if (m.HasProperty("_ShadowDarkness")) m.SetFloat("_ShadowDarkness", Default("_ShadowDarkness", 1f));
            if (m.HasProperty("_LightingBands")) m.SetFloat("_LightingBands", Default("_LightingBands", 4f));
            if (m.HasProperty("_DiffuseWrap")) m.SetFloat("_DiffuseWrap", Default("_DiffuseWrap", 0.75f));
            if (m.HasProperty("_SpecularStrength")) m.SetFloat("_SpecularStrength", Default("_SpecularStrength", 0.6f));
            if (m.HasProperty("_SpecularPower")) m.SetFloat("_SpecularPower", Default("_SpecularPower", 16f));
            if (m.HasProperty("_FlatShading")) m.SetFloat("_FlatShading", Default("_FlatShading", 1f));
            if (m.HasProperty("_VertexSnap")) m.SetFloat("_VertexSnap", Default("_VertexSnap", 1f));
            if (m.HasProperty("_SnapNearFade")) m.SetFloat("_SnapNearFade", Default("_SnapNearFade", 0.4f));
            bool character = m.shader && m.shader.name == "RetroRendering/Character";
            if (m.HasProperty("_Affine")) m.SetFloat("_Affine", Default("_Affine", character ? 1f : 0f));
            if (m.HasProperty("_AffineStrength")) m.SetFloat("_AffineStrength", Default("_AffineStrength", character ? 0.15f : 0f));
        }

        /// <summary>带蒙皮的物体按角色着色器处理（保留法线图/裁切等角色特性）。</summary>
        private bool IsCharacter(Material source)
        {
            if (target && target.GetComponent<SkinnedMeshRenderer>()) return true;
            return source && source.shader && source.shader.name == "RetroRendering/Character";
        }

        public void Restore()
        {
            if (instances == null) return;
            if (target) target.sharedMaterials = original;
            for (int i = 0; i < instances.Length; i++)
                if (instances[i] && (original == null || i >= original.Length || instances[i] != original[i])) Destroy(instances[i]);
            instances = null;
            original = null;
        }
        private void OnDisable() => Restore();
    }
}

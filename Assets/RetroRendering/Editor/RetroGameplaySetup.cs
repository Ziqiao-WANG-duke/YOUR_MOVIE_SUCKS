using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace YourMovieSucks.Retro.Editor
{
    /// <summary>
    /// Wires the retro look into the existing gameplay scene as scene-only components.
    /// Source materials, prefabs and render pipeline assets are never modified.
    /// </summary>
    public static class RetroGameplaySetup
    {
        private const string Root = "Assets/RetroRendering";
        private const string GameplayScene = "Assets/Scenes/PolePosition.unity";
        private const string MaterialsFolder = Root + "/Materials/PolePosition";
        private const string ControllerName = "Retro Rendering";

        [MenuItem("Tools/Retro Rendering/Apply to PolePosition scene")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Leave Play Mode before changing the scene.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != GameplayScene) throw new System.InvalidOperationException("Open " + GameplayScene + " first.");
            var envShader = Shader.Find("RetroRendering/Environment");
            var characterShader = Shader.Find("RetroRendering/Character");
            if (!envShader || !characterShader || ShaderUtil.ShaderHasError(envShader) || ShaderUtil.ShaderHasError(characterShader))
                throw new System.InvalidOperationException("Retro shaders failed to compile.");

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
                AssetDatabase.CreateFolder(Root + "/Materials", "PolePosition");
            }

            var controller = FindOrCreateController(scene);
            var variants = new Dictionary<Material, Material>();
            var bindings = new List<RetroMaterialBinding>();
            var venue = GameObject.Find("Venue");
            if (!venue) throw new System.InvalidOperationException("Venue object not found.");
            foreach (var renderer in venue.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer) continue;
                var sources = renderer.sharedMaterials;
                var built = new Material[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    var source = sources[i];
                    if (!source) continue;
                    if (source.shader.name != "Universal Render Pipeline/Lit")
                        throw new System.InvalidOperationException(source.name + " is not URP Lit; add a manual variant instead.");
                    if (!variants.TryGetValue(source, out var variant)) variants.Add(source, variant = CreateVariant(source, envShader));
                    built[i] = variant;
                }
                var binding = renderer.GetComponent<RetroMaterialBinding>();
                if (!binding) binding = renderer.gameObject.AddComponent<RetroMaterialBinding>();
                binding.target = renderer;
                binding.retroVariants = built;
                binding.controlSnapping = true;
                binding.controlAffine = true;
                binding.controlShading = true;
                binding.controlSnapGrid = true;
                bindings.Add(binding);
            }
            controller.materialBindings = bindings.ToArray();

            var auto = controller.GetComponent<RetroCharacterAutoBinding>();
            if (!auto) auto = controller.gameObject.AddComponent<RetroCharacterAutoBinding>();
            auto.controller = controller;
            auto.retroCharacterShader = characterShader;
            auto.targetComponentName = "TypingTarget";

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Retro] Applied to PolePosition: {bindings.Count} environment renderers, {variants.Count} variant materials, runtime character auto-binding. Toggle 'retroEnabled' to compare.");
        }

        [MenuItem("Tools/Retro Rendering/Remove from PolePosition scene")]
        public static void Remove()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Leave Play Mode before changing the scene.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != GameplayScene) throw new System.InvalidOperationException("Open " + GameplayScene + " first.");
            var controller = scene.GetRootGameObjects().Select(go => go.GetComponentInChildren<RetroRenderingController>(true)).FirstOrDefault(c => c);
            var venue = GameObject.Find("Venue");
            if (venue)
                foreach (var binding in venue.GetComponentsInChildren<RetroMaterialBinding>(true))
                    Object.DestroyImmediate(binding);
            var auto = Object.FindFirstObjectByType<RetroCharacterAutoBinding>();
            if (auto) Object.DestroyImmediate(auto);
            if (controller) Object.DestroyImmediate(controller.gameObject);
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            if (AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.DeleteAsset(MaterialsFolder);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Retro] Removed scene wiring. Original materials and assets were never modified.");
        }

        private static RetroRenderingController FindOrCreateController(Scene scene)
        {
            RetroRenderingController controller = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                controller = root.GetComponentInChildren<RetroRenderingController>(true);
                if (controller) break;
            }
            if (!controller)
            {
                var go = new GameObject(ControllerName);
                controller = go.AddComponent<RetroRenderingController>();
            }
            var camera = Camera.main;
            if (!camera) throw new System.InvalidOperationException("Main camera not found.");
            controller.sceneCamera = camera;
            controller.compositeShader = Shader.Find("RetroRendering/ScreenComposite");
            controller.preset = AssetDatabase.LoadAssetAtPath<RetroPreset>(Root + "/Presets/ArcadeHorror.asset");
            controller.retroEnabled = true;
            controller.ApplyPreset();
            RetroSetup.ConfigurePipeline(controller);
            RetroSetup.EnsurePresentationPipeline();
            return controller;
        }

        private static Material CreateVariant(Material source, Shader envShader)
        {
            string path = MaterialsFolder + "/" + source.name + "_Retro.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var variant = new Material(envShader) { name = source.name + "_Retro" };
            var baseMap = source.GetTexture("_BaseMap");
            if (baseMap)
            {
                variant.SetTexture("_BaseMap", baseMap);
                variant.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                variant.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                variant.SetFloat("_UseBaseMap", 1f);
            }
            variant.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white);
            if (source.IsKeywordEnabled("_EMISSION")) variant.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
            variant.SetFloat("_Cull", source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 2f);
            variant.SetColor("_AmbientColor", new Color(0.10f, 0.09f, 0.14f, 1f));
            variant.SetFloat("_AmbientStrength", 1f);
            variant.SetFloat("_ShadowDarkness", 0.75f);
            variant.SetFloat("_LightingBands", 4f);
            variant.SetFloat("_FlatShading", 1f);
            variant.SetFloat("_VertexSnap", 1f);
            variant.SetFloat("_SnapStrength", 0.15f);
            variant.SetFloat("_SnapNearFade", 0.4f);
            variant.SetFloat("_Affine", 0f);
            variant.SetFloat("_UseVertexColor", 0f);
            variant.SetFloat("_AlphaClip", 0f);
            AssetDatabase.CreateAsset(variant, path);
            return variant;
        }
    }
}

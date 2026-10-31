using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YourMovieSucks.Retro.Editor
{
    public static class RetroSetup
    {
        public const string Root = "Assets/RetroRendering";
        public const string TestScene = Root + "/Scenes/RetroTest.unity";

        [MenuItem("Tools/Retro Rendering/Create test assets and scene")]
        public static void CreateTestAssets()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Leave Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene))
            {
                Debug.Log("RetroTest already exists; existing edits were preserved. Open it from Assets/RetroRendering/Scenes.");
                return;
            }
            foreach (string folder in new[] { "Materials", "Presets", "Scenes", "Textures" })
                if (!AssetDatabase.IsValidFolder(Root + "/" + folder)) AssetDatabase.CreateFolder(Root, folder);
            var envShader = Shader.Find("RetroRendering/Environment");
            var characterShader = Shader.Find("RetroRendering/Character");
            if (!envShader || !characterShader || ShaderUtil.ShaderHasError(envShader) || ShaderUtil.ShaderHasError(characterShader))
                throw new InvalidOperationException("Retro shaders are missing or failed compilation.");

            var colors = new[] { new Color(.20f,.09f,.12f), new Color(.13f,.18f,.18f), new Color(.24f,.22f,.17f), new Color(.18f,.14f,.23f) };
            var names = new[] { "DriedCrimson", "OxidizedTeal", "DustyConcrete", "BruisedViolet" };
            var env = new Material[4];
            for (int i=0;i<4;i++)
            {
                int index=i;
                env[i] = MaterialAsset(names[i], () => { var m=new Material(envShader); m.SetColor("_BaseColor", colors[index]); return m; });
            }
            var checker = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Checker.asset");
            if (!checker)
            {
                checker = new Texture2D(64,64,TextureFormat.RGBA32,false) { name="Checker", filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Repeat };
                var pixels=new Color[64*64];
                for(int y=0;y<64;y++) for(int x=0;x<64;x++) pixels[y*64+x]=((x/8+y/8)%2==0)?new Color(.8f,.65f,.34f):new Color(.12f,.19f,.24f);
                checker.SetPixels(pixels); checker.Apply(); AssetDatabase.CreateAsset(checker,Root + "/Textures/Checker.asset");
            }
            var wallMat=MaterialAsset("CheckerRetro",()=> { var m=new Material(envShader); m.SetTexture("_BaseMap",checker); m.SetFloat("_UseBaseMap",1); m.SetFloat("_Affine",1); m.SetColor("_BaseColor",Color.white); return m; });
            var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Monsters/ZombieV2/M_ZombieV2.mat");
            var zombie=MaterialAsset("ZombieRetro",()=>CreateCharacterVariant(original));
            var clean=PresetAsset(RetroStyle.CleanRetro);
            var arcade=PresetAsset(RetroStyle.ArcadeHorror);
            PresetAsset(RetroStyle.AggressivePS1);
            AssetDatabase.SaveAssets();
            EnsurePresentationPipeline();

            // Additive construction preserves every open scene, including unsaved work.
            Scene previous=SceneManager.GetActiveScene();
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.ambientMode=AmbientMode.Flat;
                RenderSettings.ambientLight=new Color(.23f,.25f,.30f);
                RenderSettings.fog=false;
                var camera=new GameObject("Test Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
                camera.tag="MainCamera"; camera.transform.position=new Vector3(0,2.4f,-7.8f);
                camera.transform.LookAt(new Vector3(0,1.3f,1)); camera.nearClipPlane=.05f; camera.farClipPlane=50;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.045f,.065f);
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
                var controller=new GameObject("Retro Rendering").AddComponent<RetroRenderingController>();
                controller.sceneCamera=camera; controller.compositeShader=Shader.Find("RetroRendering/ScreenComposite");
                ConfigurePipeline(controller);
                controller.preset=arcade; controller.ApplyPreset(); controller.showRuntimeControls=true;
                var bindings=new List<RetroMaterialBinding>();
                var lit=Shader.Find("Universal Render Pipeline/Lit");
                var baseline=MaterialAsset("TestBaseline",()=> { var m=new Material(lit); m.SetColor("_BaseColor",new Color(.3f,.3f,.32f)); m.SetFloat("_Smoothness",0); return m; });
                GameObject Box(string name,Vector3 position,Vector3 scale,Material retro)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
                    go.transform.position=position; go.transform.localScale=scale;
                    go.GetComponent<Renderer>().sharedMaterial=baseline;
                    var binding=go.AddComponent<RetroMaterialBinding>(); binding.target=go.GetComponent<Renderer>(); binding.retroVariants=new[]{retro}; binding.controlShading=true; bindings.Add(binding);
                    return go;
                }
                Box("Untextured Floor",new Vector3(0,-.15f,1),new Vector3(12,.3f,13),env[1]);
                Box("Untextured Crimson Wall",new Vector3(-4,1.5f,3),new Vector3(1,3,4),env[0]);
                Box("Untextured Concrete",new Vector3(4,.8f,2),new Vector3(1.5f,1.6f,2),env[2]);
                var wall=Box("Affine Checker Wall",new Vector3(0,2.1f,3.4f),new Vector3(5.5f,2.8f,.1f),wallMat);
                wall.transform.rotation=Quaternion.Euler(0,28,0);
                // Baseline wall also retains the texture when the entire system is disabled.
                var checkerBaseline=MaterialAsset("CheckerOriginal",()=>{var m=new Material(lit);m.SetTexture("_BaseMap",checker);m.SetFloat("_Smoothness",0);return m;});
                wall.GetComponent<Renderer>().sharedMaterial=checkerBaseline;
                var key=new GameObject("Directional Light").AddComponent<Light>(); key.type=LightType.Directional; key.intensity=1.6f; key.shadows=LightShadows.Soft; key.transform.rotation=Quaternion.Euler(45,-25,0);
                var fill=new GameObject("Teal Fill").AddComponent<Light>(); fill.type=LightType.Point; fill.color=new Color(.2f,.8f,1);fill.intensity=8;fill.range=9;fill.transform.position=new Vector3(-3,2,-1);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StripperZombie.prefab");
                var animators=new List<Animator>();
                for(int i=0;i<2;i++)
                {
                    var enemy=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                    PrefabUtility.UnpackPrefabInstance(enemy,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                    enemy.name=i==0?"Zombie Original":"Zombie Retro Copy";
                    // Only the scene copies lose combat behaviours, never the source prefab.
                    var behaviours=enemy.GetComponentsInChildren<MonoBehaviour>(true);
                    for(int j=behaviours.Length-1;j>=0;j--) UnityEngine.Object.DestroyImmediate(behaviours[j]);
                    enemy.transform.position=new Vector3(i==0?-1.5f:1.5f,0,0);
                    enemy.transform.rotation=Quaternion.Euler(0,180,0);
                    var animator=enemy.GetComponentInChildren<Animator>(); animators.Add(animator);
                    if(i==1) foreach(var renderer in enemy.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var b=renderer.gameObject.AddComponent<RetroMaterialBinding>(); b.target=renderer;
                        b.retroVariants=new Material[renderer.sharedMaterials.Length]; for(int j=0;j<b.retroVariants.Length;j++)b.retroVariants[j]=zombie; bindings.Add(b);
                    }
                    MakeWorldLabel(i==0?"ORIGINAL":"<color=#78FF88>RET</color>RO ZOMBIE",new Vector3(i==0?-1.5f:1.5f,2.65f,0),camera);
                }
                MakeOverlay();
                controller.materialBindings=bindings.ToArray();
                var motion=controller.gameObject.AddComponent<RetroTestMotion>();motion.testCamera=camera;motion.texturedWall=wall.transform;motion.animators=animators.ToArray();
                EditorSceneManager.SaveScene(scene,TestScene);
            }
            finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true); }
            AssetDatabase.SaveAssets();
            Debug.Log("Created non-destructive RetroTest scene, presets and material variants.");
        }

        private static Material MaterialAsset(string name,Func<Material> create)
        {
            string path=Root+"/Materials/"+name+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path); if(existing)return existing;
            var material=create(); material.name=name; AssetDatabase.CreateAsset(material,path); return material;
        }

        private static RetroPreset PresetAsset(RetroStyle style)
        {
            string path=Root+"/Presets/"+style+".asset";
            var preset=AssetDatabase.LoadAssetAtPath<RetroPreset>(path); if(preset)return preset;
            preset=ScriptableObject.CreateInstance<RetroPreset>();
            preset.shading=style==RetroStyle.CleanRetro?RetroShading.Smooth:RetroShading.Faceted;
            preset.resolution=style==RetroStyle.AggressivePS1?RetroResolution.R320x240:RetroResolution.R426x240;
            preset.vertexSnapping=style==RetroStyle.CleanRetro?0:style==RetroStyle.AggressivePS1?.8f:.15f;
            preset.affineMapping=style==RetroStyle.CleanRetro?0:style==RetroStyle.AggressivePS1?.75f:.15f;
            preset.colorLevels=style==RetroStyle.AggressivePS1?16:32;
            preset.dithering=style==RetroStyle.AggressivePS1?.65f:0;
            AssetDatabase.CreateAsset(preset,path); return preset;
        }

        public static Material CreateCharacterVariant(Material source)
        {
            if(!source || source.shader.name!="Universal Render Pipeline/Lit") throw new InvalidOperationException("Only standard URP Lit conversion is supported; retain unsupported shaders unchanged.");
            foreach(string map in new[]{"_MetallicGlossMap","_SpecGlossMap","_OcclusionMap","_ParallaxMap","_DetailAlbedoMap","_DetailNormalMap"})
                if(source.HasProperty(map) && source.GetTexture(map)) throw new InvalidOperationException("Unsupported map "+map+"; source material was not changed.");
            if(source.GetFloat("_WorkflowMode")<.5f || source.GetFloat("_SmoothnessTextureChannel")>.5f)
                throw new InvalidOperationException("Specular workflow and albedo-alpha smoothness are not supported.");
            bool transparent=source.GetFloat("_Surface")>.5f;
            if(transparent && source.GetFloat("_Blend")!=0) throw new InvalidOperationException("Only straight-alpha transparency is supported. Keep additive/premultiplied materials unchanged.");
            var result=new Material(Shader.Find("RetroRendering/Character"));
            foreach(string map in new[]{"_BaseMap","_BumpMap","_EmissionMap"}) result.SetTexture(map,source.GetTexture(map));
            result.SetTextureScale("_BaseMap",source.GetTextureScale("_BaseMap"));result.SetTextureOffset("_BaseMap",source.GetTextureOffset("_BaseMap"));
            foreach(string prop in new[]{"_Metallic","_Smoothness","_BumpScale","_AlphaClip","_Cutoff","_Cull"})result.SetFloat(prop,source.GetFloat(prop));
            result.SetColor("_BaseColor",source.GetColor("_BaseColor"));result.SetColor("_EmissionColor",source.IsKeywordEnabled("_EMISSION")?source.GetColor("_EmissionColor"):Color.black);
            result.SetFloat("_UseNormalMap",source.IsKeywordEnabled("_NORMALMAP")&&source.GetTexture("_BumpMap")?1:0);
            result.SetFloat("_UseEmissionMap",source.IsKeywordEnabled("_EMISSION")&&source.GetTexture("_EmissionMap")?1:0);
            result.SetFloat("_Surface",transparent?1:0);result.SetFloat("_SrcBlend",transparent?(float)BlendMode.SrcAlpha:(float)BlendMode.One);
            result.SetFloat("_DstBlend",transparent?(float)BlendMode.OneMinusSrcAlpha:(float)BlendMode.Zero);result.SetFloat("_ZWrite",transparent?0:1);
            result.SetOverrideTag("RenderType",transparent?"Transparent":source.GetFloat("_AlphaClip")>.5f?"TransparentCutout":"Opaque");
            result.renderQueue=source.renderQueue;
            if(transparent) {result.SetShaderPassEnabled("ShadowCaster",false);result.SetShaderPassEnabled("DepthOnly",false);result.SetShaderPassEnabled("DepthNormals",false);}
            return result;
        }

        [MenuItem("Assets/Retro Rendering/Create character variant (no assignment)")]
        private static void ConvertSelected()
        {
            var source=Selection.activeObject as Material;
            var variant=CreateCharacterVariant(source);
            string path=AssetDatabase.GenerateUniqueAssetPath(Root+"/Materials/"+source.name+"_Retro.mat");
            AssetDatabase.CreateAsset(variant,path); AssetDatabase.SaveAssets(); Selection.activeObject=variant;
        }

        [MenuItem("Tools/Retro Rendering/Preview gameplay (Play Mode only)")]
        public static void PreviewGameplay()
        {
            if(!Application.isPlaying) {Debug.LogWarning("Enter Play Mode in PolePosition first. This preview is runtime-only.");return;}
            var existing=UnityEngine.Object.FindFirstObjectByType<RetroRenderingController>();if(existing){Selection.activeGameObject=existing.gameObject;return;}
            var go=new GameObject("Retro Gameplay Preview (Runtime only)");var controller=go.AddComponent<RetroRenderingController>();
            controller.sceneCamera=Camera.main;controller.compositeShader=Shader.Find("RetroRendering/ScreenComposite");controller.showRuntimeControls=true;
            ConfigurePipeline(controller);
            controller.preset=AssetDatabase.LoadAssetAtPath<RetroPreset>(Root+"/Presets/ArcadeHorror.asset");controller.ApplyPreset();Selection.activeGameObject=go;
        }

        public static void EnsurePresentationPipeline()
        {
            if(!AssetDatabase.IsValidFolder(Root+"/Settings"))AssetDatabase.CreateFolder(Root,"Settings");
            string rendererPath=Root+"/Settings/PresentationRenderer.asset";
            string pipelinePath=Root+"/Settings/Retro_PC_RPAsset.asset";
            if(!AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath))
            {
                AssetDatabase.CopyAsset("Assets/Settings/PC_Renderer.asset",rendererPath);
                var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                renderer.rendererFeatures.Clear();
                var serialized=new SerializedObject(renderer);
                serialized.FindProperty("m_RendererFeatureMap").arraySize=0;
                serialized.FindProperty("m_RenderingMode").intValue=0;serialized.ApplyModifiedPropertiesWithoutUndo();
                renderer.SetDirty();EditorUtility.SetDirty(renderer);
            }
            if(!AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath))
            {
                AssetDatabase.CopyAsset("Assets/Settings/PC_RPAsset.asset",pipelinePath);
                var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
                var serialized=new SerializedObject(pipeline);var list=serialized.FindProperty("m_RendererDataList");list.arraySize=2;
                list.GetArrayElementAtIndex(0).objectReferenceValue=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
                list.GetArrayElementAtIndex(1).objectReferenceValue=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
            }
            AssetDatabase.SaveAssets();
        }
        public static void ConfigurePipeline(RetroRenderingController controller)
        {
            controller.pipelineOverride=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Settings/Retro_PC_RPAsset.asset");
            controller.expectedPipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            controller.presentationRendererIndex=1;
        }

        private static Font TestFont()=>Resources.Load<Font>("Fonts/SpecialElite") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static void MakeWorldLabel(string text,Vector3 position,Camera camera)
        {
            var go=new GameObject("World Word Label",typeof(RectTransform),typeof(Canvas));var canvas=go.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=camera;canvas.sortingOrder=20;
            var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(550,90);rect.localScale=Vector3.one*.007f;rect.position=position;rect.rotation=camera.transform.rotation;
            var label=new GameObject("Highlighted Text",typeof(RectTransform),typeof(Text)).GetComponent<Text>();label.transform.SetParent(go.transform,false);
            label.font=TestFont();label.fontSize=40;label.text=text;label.alignment=TextAnchor.MiddleCenter;label.supportRichText=true;label.raycastTarget=false;
            label.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shaders/M_WordLabelOnTop.mat");
            var tr=(RectTransform)label.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=Vector2.zero;tr.offsetMax=Vector2.zero;
        }
        private static void MakeOverlay()
        {
            var go=new GameObject("Native HUD",typeof(Canvas),typeof(CanvasScaler));go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var label=new GameObject("Subtitle",typeof(RectTransform),typeof(Text)).GetComponent<Text>();label.transform.SetParent(go.transform,false);
            label.font=TestFont();label.fontSize=28;label.text="RETRO LAB | World pixels, native-resolution words";label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
            var rt=(RectTransform)label.transform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);rt.sizeDelta=new Vector2(0,60);rt.anchoredPosition=Vector2.zero;
        }
    }
}

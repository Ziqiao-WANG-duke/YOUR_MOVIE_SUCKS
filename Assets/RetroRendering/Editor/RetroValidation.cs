using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YourMovieSucks.Retro.Editor
{
    public static class RetroValidation
    {
        [MenuItem("Tools/Retro Rendering/Validate assets and shaders")]
        public static void ValidateAssets()
        {
            var lines=new List<string>();int failures=0;
            void Check(bool ok,string name){lines.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
            foreach(string name in new[]{"Environment","Character","ScreenComposite"})
            {
                var shader=Shader.Find("RetroRendering/"+name);
                Check(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader),"Shader "+name);
                if(shader)foreach(var message in ShaderUtil.GetShaderMessages(shader))lines.Add(message.severity+": "+message.message);
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{RetroSetup.Root}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                Check(m && m.shader && !ShaderUtil.ShaderHasError(m.shader),path);
            }
            var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Monsters/ZombieV2/M_ZombieV2.mat");
            var variant=AssetDatabase.LoadAssetAtPath<Material>(RetroSetup.Root+"/Materials/ZombieRetro.mat");
            Check(original && original.shader.name=="Universal Render Pipeline/Lit","Original zombie material remains Lit");
            Check(variant && variant.mainTexture==original.mainTexture && variant.mainTextureScale==original.mainTextureScale && variant.mainTextureOffset==original.mainTextureOffset,"Enemy texture reference and UV transform preserved");
            foreach(RetroStyle style in Enum.GetValues(typeof(RetroStyle)))Check(AssetDatabase.LoadAssetAtPath<RetroPreset>(RetroSetup.Root+"/Presets/"+style+".asset"),"Preset "+style);
            Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(RetroSetup.TestScene),"Test scene exists");
            var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(RetroSetup.Root+"/Settings/Retro_PC_RPAsset.asset");
            var renderer=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(RetroSetup.Root+"/Settings/PresentationRenderer.asset");
            Check(renderer && renderer.rendererFeatures.Count==0,"Presentation renderer has no duplicated SSAO features");
            Check(pipeline,"Pipeline copy exists");
            if(pipeline)
            {
                var rendererList=new SerializedObject(pipeline).FindProperty("m_RendererDataList");
                Check(rendererList.arraySize==2 && rendererList.GetArrayElementAtIndex(1).objectReferenceValue==renderer,"Pipeline copy references presentation renderer at index 1");
            }
            // Validate conversion state without touching an existing material asset.
            var synthetic=new Material(original);
            synthetic.SetFloat("_Surface",1);synthetic.SetFloat("_Blend",0);synthetic.SetFloat("_AlphaClip",1);synthetic.SetFloat("_Cutoff",.37f);synthetic.renderQueue=3000;
            var converted=RetroSetup.CreateCharacterVariant(synthetic);
            Check(converted.GetFloat("_AlphaClip")==1 && Mathf.Approximately(converted.GetFloat("_Cutoff"),.37f) && converted.renderQueue==3000 && converted.GetFloat("_ZWrite")==0,"Alpha clip and straight-alpha conversion state preserved");
            UnityEngine.Object.DestroyImmediate(converted);UnityEngine.Object.DestroyImmediate(synthetic);
            lines.Add("Failures="+failures);Directory.CreateDirectory(RetroValidationProbe.Output);File.WriteAllLines(RetroValidationProbe.Output+"/assets.txt",lines);
            Debug.Log(string.Join("\n",lines));
            if(failures>0)throw new InvalidOperationException("Retro asset validation failed.");
        }

        [MenuItem("Tools/Retro Rendering/Run real-frame tests (RetroTest Play Mode)")]
        public static void RunFrames()
        {
            if(!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=RetroSetup.TestScene)
                throw new InvalidOperationException("Open RetroTest and enter Play Mode first.");
            var existing=UnityEngine.Object.FindFirstObjectByType<RetroValidationProbe>();
            if(existing && !existing.Finished)throw new InvalidOperationException("Probe already running.");
            if(existing)UnityEngine.Object.Destroy(existing.gameObject);
            new GameObject("Retro Validation (Runtime only)").AddComponent<RetroValidationProbe>();
        }
    }
}

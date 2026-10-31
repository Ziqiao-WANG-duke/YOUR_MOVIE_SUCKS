using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace YourMovieSucks.Retro
{
    /// <summary>Opt-in, real-frame regression probe for RetroTest; never manually calls Camera.Render.</summary>
    public sealed class RetroValidationProbe : MonoBehaviour
    {
        public bool Finished { get; private set; }
        public int Failures { get; private set; }
        public const string Output = "Library/Locus/tmp/retro-validation";
        private readonly List<string> results = new List<string>();
        private void Check(bool value, string label)
        {
            results.Add((value ? "PASS " : "FAIL ") + label);
            if (!value) Failures++;
        }
        private IEnumerator Capture(string name)
        {
            // Leave state changes two complete render frames to settle.
            yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(Output + "/" + name + ".png");
            yield return null; yield return null; yield return null;
        }
        private static Color32[] Pixels(string name)
        {
            var t = new Texture2D(2,2,TextureFormat.RGBA32,false);
            t.LoadImage(File.ReadAllBytes(Output + "/" + name + ".png"));
            var p=t.GetPixels32(); Destroy(t); return p;
        }
        private static int Difference(string a,string b)
        {
            var pa=Pixels(a);var pb=Pixels(b); if(pa.Length!=pb.Length)return -1;
            int changed=0;
            for(int i=0;i<pa.Length;i++) if(!pa[i].Equals(pb[i]))changed++;
            return changed;
        }
        private IEnumerator Start()
        {
            var c=FindFirstObjectByType<RetroRenderingController>();
            var motion=FindFirstObjectByType<RetroTestMotion>();
            if(!c || !motion) {Debug.LogError("Run the probe only in RetroTest."); Finished=true;yield break;}
            Directory.CreateDirectory(Output);
            string settings=JsonUtility.ToJson(c);
            int fps=Application.targetFrameRate;
            bool moving=motion.motion;
            var animators=FindObjectsByType<Animator>(FindObjectsSortMode.None);
            var speeds=new float[animators.Length];
            for(int i=0;i<animators.Length;i++)speeds[i]=animators[i].speed;
            var materials=new Dictionary<Renderer,Material[]>();
            var layers=new Dictionary<GameObject,int>();
            var cameras=new Dictionary<Canvas,Camera>();
            var meshes=new Dictionary<SkinnedMeshRenderer,Mesh>();
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            bool completed=false;
            try
            {
                Application.targetFrameRate=60; motion.motion=false;c.showRuntimeControls=false;
                c.retroEnabled=false; yield return null;yield return null;
                pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                foreach(var b in c.materialBindings) materials.Add(b.target,b.target.sharedMaterials);
                foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    cameras.Add(canvas,canvas.worldCamera);
                    foreach(var t in canvas.GetComponentsInChildren<Transform>())layers[t.gameObject]=t.gameObject.layer;
                }
                foreach(var r in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))meshes.Add(r,r.sharedMesh);
                int mask=c.sceneCamera.cullingMask; Rect rect=c.sceneCamera.rect;float aspect=c.sceneCamera.aspect;
                float time=animators[0].GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return new WaitForSeconds(.25f);
                Check(animators[0].isHuman && animators[0].GetCurrentAnimatorStateInfo(0).normalizedTime!=time,"Animator advances with valid Humanoid avatar");
                for(int i=0;i<animators.Length;i++)animators[i].speed=0;
                yield return Capture("baseline-disabled");
                c.retroEnabled=true;c.resolution=RetroResolution.Native;c.quantization=false;c.vertexSnapping=0;c.affineMapping=0;
                yield return Capture("native");
                Check(c.IsActive && c.RenderWidth==Screen.width && c.RenderHeight==Screen.height,"Native target matches output");
                c.resolution=RetroResolution.R320x240;
                yield return Capture("320-point");
                Check(c.SceneTexture && c.RenderWidth==320 && c.RenderHeight==240 && c.SceneTexture.antiAliasing==1,"True 320x240, single-sample scene RT");
                float sx=c.ContentRect.width*Screen.width/320f,sy=c.ContentRect.height*Screen.height/240f;
                Check(Mathf.Abs(sx-sy)<.001f && Mathf.Abs(sx-Mathf.Round(sx))<.001f,"Point upscale uses equal integer X/Y pixel scale with letterboxing");
                Check(c.SceneTexture.filterMode==FilterMode.Point,"Nearest-neighbour filtering");
                c.filtering=FilterMode.Bilinear;yield return Capture("320-bilinear");
                Check(c.SceneTexture.filterMode==FilterMode.Bilinear && Difference("320-point","320-bilinear")>100,"Bilinear filtering visibly differs");
                c.filtering=FilterMode.Point;c.resolution=RetroResolution.R640x480;yield return null;yield return null;
                Check(c.RenderWidth==640 && c.RenderHeight==480,"640x480 preset");
                c.resolution=RetroResolution.R426x240;yield return null;yield return null;
                Check(c.RenderWidth==426 && c.RenderHeight==240,"426x240 preset");
                c.resolution=RetroResolution.R320x240;c.quantization=true;c.colorLevels=4;c.dithering=0;
                yield return Capture("quantized4");
                Check(Difference("320-point","quantized4")>100,"RGB quantization changes real output pixels");
                c.dithering=1;yield return Capture("dither4");
                Check(Difference("quantized4","dither4")>100,"Ordered dithering changes real output pixels");
                c.quantization=false;c.vertexSnapping=1;
                yield return Capture("snap1");
                int snapDiff=Difference("320-point","snap1");
                Check(snapDiff>10,"Vertex snapping changes rendered geometry: "+snapDiff+" pixels");
                yield return Capture("snap-repeat");
                Check(Difference("snap1","snap-repeat")==0,"Stationary snapped frame is deterministic");
                c.vertexSnapping=0;c.affineMapping=1;
                yield return Capture("affine1");
                int affineDiff=Difference("320-point","affine1");
                Check(affineDiff>100,"Affine UV interpolation changes textured surface: "+affineDiff+" pixels");
                var native=Pixels("320-point");var affine=Pixels("affine1");int headerDiff=0;
                for(int y=Screen.height-65;y<Screen.height;y++)for(int x=0;x<Screen.width;x++)if(!native[y*Screen.width+x].Equals(affine[y*Screen.width+x]))headerDiff++;
                Check(headerDiff==0,"Screen-space header unaffected by object UV effect");
                bool protectedUI=true;
                foreach(var pair in cameras) if(pair.Key.renderMode!=RenderMode.ScreenSpaceOverlay)
                    protectedUI &= pair.Key.worldCamera==c.PresentationCamera && pair.Key.gameObject.layer==5;
                Check(protectedUI,"World-space canvas rendered by native presentation camera");
                foreach(var b in c.materialBindings)
                {
                    var assigned=b.target.sharedMaterials;
                    for(int i=0;i<assigned.Length;i++)if(b.retroVariants[i])
                        Check(assigned[i]!=b.retroVariants[i] && assigned[i].shader.isSupported,"Runtime material clone: "+b.target.name);
                }
                foreach(var pair in meshes)Check(pair.Key.sharedMesh==pair.Value,"Imported skinned mesh reference unchanged: "+pair.Key.name);
                c.ApplyStyle(RetroStyle.ArcadeHorror);yield return Capture("arcade-default");
                c.retroEnabled=false;yield return Capture("restored");
                Check(!c.IsActive && !c.sceneCamera.targetTexture && c.sceneCamera.cullingMask==mask && c.sceneCamera.rect==rect && Mathf.Approximately(c.sceneCamera.aspect,aspect),"Disable restores camera state");
                bool restored=true;
                foreach(var pair in materials){var current=pair.Key.sharedMaterials;for(int i=0;i<current.Length;i++)restored &= current[i]==pair.Value[i];}
                foreach(var pair in layers) restored &= pair.Key.layer==pair.Value;
                foreach(var pair in cameras)restored &= pair.Key.worldCamera==pair.Value;
                Check(restored,"Disable restores exact material references, UI layers and canvas cameras");
                Check(Difference("baseline-disabled","restored")==0,"Disabled rendering matches baseline screenshot pixel-for-pixel");
                for(int i=0;i<3;i++){c.retroEnabled=true;yield return null;yield return null;c.retroEnabled=false;yield return null;yield return null;}
                Check(!c.PresentationCamera && !c.SceneTexture,"Repeated enable/disable releases owned camera and RT");
                Check(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline==pipeline,"Disable restores original active render pipeline");
                completed=true;
            }
            finally
            {
                c.retroEnabled=false;
                JsonUtility.FromJsonOverwrite(settings,c);
                motion.motion=moving;Application.targetFrameRate=fps;
                for(int i=0;i<animators.Length;i++)if(animators[i])animators[i].speed=speeds[i];
                Check(completed,"Regression suite reached its final assertion");
                results.Add("Failures="+Failures);
                File.WriteAllLines(Output+"/automated.txt",results);
                Finished=true;
                Debug.Log("[RetroValidation] "+string.Join("\n",results));
            }
        }
    }
}

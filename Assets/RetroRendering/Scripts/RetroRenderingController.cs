using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace YourMovieSucks.Retro
{
    /// <summary>Native URP RenderGraph camera targets, optional reversible pipeline copy. No legacy render pass or source asset writes.</summary>
    [DefaultExecutionOrder(30000)]
    [DisallowMultipleComponent]
    public sealed class RetroRenderingController : MonoBehaviour
    {
        public Camera sceneCamera;
        public Shader compositeShader;
        [Tooltip("Optional reversible pipeline copy with a UI-only renderer; originals remain untouched.")]
        public UniversalRenderPipelineAsset pipelineOverride;
        public UniversalRenderPipelineAsset expectedPipeline;
        [Min(0)] public int presentationRendererIndex = 1;
        public bool retroEnabled = true;
        public RetroPreset preset;
        public RetroResolution resolution = RetroResolution.R426x240;
        public FilterMode filtering = FilterMode.Point;
        public bool quantization = true;
        [Range(2, 256)] public int colorLevels = 32;
        [Range(0, 1)] public float dithering;
        [Range(0, 1)] public float vertexSnapping = 0.15f;
        [Range(0, 1)] public float affineMapping = 0.15f;
        public RetroShading shading = RetroShading.Faceted;
        [Tooltip("Explicitly selected renderers only. Original assets are never modified.")]
        public RetroMaterialBinding[] materialBindings = new RetroMaterialBinding[0];
        public bool showRuntimeControls;

        public int RenderWidth => sceneTexture ? sceneTexture.width : Screen.width;
        public int RenderHeight => sceneTexture ? sceneTexture.height : Screen.height;
        public long EstimatedTargetBytes => sceneTexture ? (long)RenderWidth * RenderHeight *
            (UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetBlockSize(sceneTexture.graphicsFormat) +
             UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetBlockSize(sceneTexture.depthStencilFormat)) : 0;
        // Format-dependent allocation estimate, excluding driver alignment and URP transient targets.
        public RenderTexture SceneTexture => sceneTexture;
        public Camera PresentationCamera => presentation;

        /// <summary>合成材质（运行时创建）。导演模式滤镜把参数写到这里；未初始化时为 null。</summary>
        public Material CompositeMaterial => composite;

        [Tooltip("弱点标记层：由原生分辨率相机绘制，不经过低分辨率/去色合成，从而在黑白画面里保持纯红。")]
        public int weakPointLayer = 6;
        [Tooltip("UI 层（世界空间词条等），同样由原生分辨率相机绘制。")]
        public int uiLayer = 5;
        public Rect ContentRect { get; private set; }
        public bool IsActive => presentation;

        private Camera presentation;
        private RenderTexture sceneTexture, savedTarget;
        private Material composite;
        private Mesh quadMesh;
        private GameObject presentationRoot, quad;
        private RetroUIProtection ui;
        private int savedMask;
        private float savedAspect;
        private Rect savedRect;
        private bool savedMSAA;
        private bool warned;
        private Camera ownedCamera;
        private UnityEngine.Rendering.RenderPipelineAsset savedQualityPipeline, ownedPipeline;
        private bool savedAutomaticAspect;
        private static readonly string[] ResolutionNames = { "320x240", "426x240", "640x480", "Native" };

        public void ApplyPreset()
        {
            if (!preset) return;
            resolution = preset.resolution; filtering = preset.filtering;
            quantization = preset.quantization; colorLevels = preset.colorLevels;
            dithering = preset.dithering; vertexSnapping = preset.vertexSnapping;
            affineMapping = preset.affineMapping; shading = preset.shading;
        }

        public void ApplyStyle(RetroStyle style)
        {
            filtering = FilterMode.Point; quantization = true;
            shading = style == RetroStyle.CleanRetro ? RetroShading.Smooth : RetroShading.Faceted;
            resolution = style == RetroStyle.AggressivePS1 ? RetroResolution.R320x240 : RetroResolution.R426x240;
            colorLevels = style == RetroStyle.AggressivePS1 ? 16 : 32;
            dithering = style == RetroStyle.AggressivePS1 ? 0.65f : 0;
            vertexSnapping = style == RetroStyle.CleanRetro ? 0 : style == RetroStyle.AggressivePS1 ? 0.8f : 0.15f;
            affineMapping = style == RetroStyle.CleanRetro ? 0 : style == RetroStyle.AggressivePS1 ? 0.75f : 0.15f;
        }

        private void LateUpdate()
        {
            if (!retroEnabled) { Release(); return; }
            if (ownedCamera && ownedCamera != sceneCamera) Release();
            if (!sceneCamera) sceneCamera = Camera.main;
            if (!sceneCamera) return;
            if (!presentation && !Initialize()) return;
            ResolveSize(out int width, out int height);
            if (!sceneTexture || sceneTexture.width != width || sceneTexture.height != height)
            {
                sceneCamera.targetTexture = savedTarget;
                if (sceneTexture) { sceneTexture.Release(); Destroy(sceneTexture); }
                sceneTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { name = "Retro Scene (Runtime)", antiAliasing = 1, useMipMap = false, wrapMode = TextureWrapMode.Clamp };
                sceneTexture.Create();
            }
            sceneTexture.filterMode = filtering == FilterMode.Point ? FilterMode.Point : FilterMode.Bilinear;
            sceneCamera.targetTexture = sceneTexture;
            sceneCamera.cullingMask = savedMask & ~(1 << uiLayer) & ~(1 << weakPointLayer);
            sceneCamera.rect = new Rect(0, 0, 1, 1);
            sceneCamera.aspect = (float)width / height;
            sceneCamera.allowMSAA = false;

            float scale = Mathf.Min((float)Screen.width / width, (float)Screen.height / height);
            if (filtering == FilterMode.Point)
                scale = scale >= 1 ? Mathf.Floor(scale) : 1f / Mathf.Ceil(1f / Mathf.Max(scale, 0.0001f));
            float sx = width * scale / Mathf.Max(1, Screen.width), sy = height * scale / Mathf.Max(1, Screen.height);
            ContentRect = new Rect((1 - sx) * 0.5f, (1 - sy) * 0.5f, sx, sy);
            presentation.CopyFrom(sceneCamera);
            presentation.targetTexture = null;
            presentation.rect = new Rect(0, 0, 1, 1);
            presentation.cullingMask = (1 << uiLayer) | (1 << weakPointLayer);
            presentation.depth = sceneCamera.depth + 1;
            presentation.clearFlags = CameraClearFlags.SolidColor;
            presentation.backgroundColor = Color.black;
            presentation.allowHDR = false;
            presentation.allowMSAA = false;
            presentation.useOcclusionCulling = false;
            presentation.transform.SetPositionAndRotation(sceneCamera.transform.position, sceneCamera.transform.rotation);
            Matrix4x4 projection = sceneCamera.projectionMatrix;
            for (int i = 0; i < 4; i++) { projection[0, i] *= sx; projection[1, i] *= sy; }
            presentation.projectionMatrix = projection;
            quad.transform.position = presentation.transform.position + presentation.transform.forward;
            composite.SetTexture("_MainTex", sceneTexture);
            composite.SetVector("_ContentRect", new Vector4(ContentRect.x, ContentRect.y, sx, sy));
            composite.SetVector("_SourceSize", new Vector4(width, height, 0, 0));
            composite.SetFloat("_Quantize", quantization ? 1 : 0);
            composite.SetFloat("_Levels", Mathf.Clamp(colorLevels, 2, 256));
            composite.SetFloat("_Dither", Mathf.Clamp01(dithering));
            ui.Refresh(sceneCamera, presentation);
            foreach (var binding in materialBindings) if (binding && binding.isActiveAndEnabled) binding.Apply(this);
        }

        private bool Initialize()
        {
            if (!compositeShader) compositeShader = Shader.Find("RetroRendering/ScreenComposite");
            var data = sceneCamera.GetComponent<UniversalAdditionalCameraData>();
            if (!compositeShader || sceneCamera.stereoEnabled || sceneCamera.targetDisplay != 0 || sceneCamera.targetTexture || sceneCamera.rect != new Rect(0, 0, 1, 1) ||
                (data && (data.renderType != CameraRenderType.Base || data.cameraStack.Count > 0)))
            {
                if (!warned) Debug.LogWarning("Retro Rendering requires a non-XR display-0 full-viewport Base camera without a pre-existing render target or camera stack. Original configuration was left untouched.", this);
                warned = true;
                return false;
            }
            if (pipelineOverride && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != expectedPipeline)
            {
                if (!warned) Debug.LogWarning("Retro pipeline copy does not match the active source pipeline. No rendering configuration was changed.", this);
                warned = true; return false;
            }
            savedTarget = sceneCamera.targetTexture; savedMask = sceneCamera.cullingMask;
            if (pipelineOverride)
            {
                savedQualityPipeline = QualitySettings.renderPipeline;
                ownedPipeline = pipelineOverride;
                QualitySettings.renderPipeline = pipelineOverride;
            }
            ownedCamera = sceneCamera;
            savedAutomaticAspect = Mathf.Approximately(sceneCamera.aspect, sceneCamera.pixelRect.width / Mathf.Max(1, sceneCamera.pixelRect.height));
            savedAspect = sceneCamera.aspect; savedRect = sceneCamera.rect; savedMSAA = sceneCamera.allowMSAA;
            composite = new Material(compositeShader) { name = "Retro Composite (Runtime)" };
            presentationRoot = new GameObject("Retro Presentation (Runtime)");
            presentation = presentationRoot.AddComponent<Camera>();
            var presentationData = presentationRoot.AddComponent<UniversalAdditionalCameraData>();
            if (ownedPipeline) presentationData.SetRenderer(presentationRendererIndex);
            presentationData.renderPostProcessing = false;
            presentationData.renderShadows = false;
            presentationData.volumeLayerMask = 0;
            presentationData.requiresColorOption = CameraOverrideOption.Off;
            presentationData.requiresDepthOption = CameraOverrideOption.Off;
            ui = presentationRoot.AddComponent<RetroUIProtection>();
            quad = new GameObject("Retro Background (Runtime)", typeof(MeshFilter), typeof(MeshRenderer));
            quad.transform.SetParent(presentationRoot.transform, false);
            quad.layer = 5;
            quadMesh = new Mesh { name = "Retro Composite Quad" };
            quadMesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0), new Vector3(1,-1,0) };
            quadMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            quadMesh.triangles = new[] { 0,1,2,0,2,3 };
            quadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000);
            quad.GetComponent<MeshFilter>().sharedMesh = quadMesh;
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = composite;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return true;
        }

        private void ResolveSize(out int width, out int height)
        {
            switch (resolution)
            {
                case RetroResolution.R320x240: width=320; height=240; break;
                case RetroResolution.R426x240: width=426; height=240; break;
                case RetroResolution.R640x480: width=640; height=480; break;
                default: width=Mathf.Max(1,Screen.width); height=Mathf.Max(1,Screen.height); break;
            }
        }

        private void Release()
        {
            if (!presentation && !ownedCamera && !ownedPipeline) return;
            if (ui) ui.Restore();
            foreach (var binding in materialBindings) if (binding) binding.Restore();
            if (ownedCamera)
            {
                ownedCamera.targetTexture = savedTarget; ownedCamera.cullingMask = savedMask;
                ownedCamera.rect = savedRect; ownedCamera.allowMSAA = savedMSAA;
                if (savedAutomaticAspect) ownedCamera.ResetAspect(); else ownedCamera.aspect = savedAspect;
            }
            if (presentation) presentation.enabled = false;
            if (ownedPipeline && QualitySettings.renderPipeline == ownedPipeline)
                QualitySettings.renderPipeline = savedQualityPipeline;
            ownedPipeline = null; savedQualityPipeline = null;
            if (sceneTexture) { sceneTexture.Release(); Destroy(sceneTexture); }
            Destroy(presentationRoot); Destroy(composite); Destroy(quadMesh);
            presentation = null; sceneTexture = null; ownedCamera = null;
        }
        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        private void OnGUI()
        {
            if (!showRuntimeControls) return;
            GUILayout.BeginArea(new Rect(12, Screen.height - 150, 390, 140), GUI.skin.box);
            retroEnabled = GUILayout.Toggle(retroEnabled, "Retro rendering (world only)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clean")) ApplyStyle(RetroStyle.CleanRetro);
            if (GUILayout.Button("Arcade")) ApplyStyle(RetroStyle.ArcadeHorror);
            if (GUILayout.Button("PS1")) ApplyStyle(RetroStyle.AggressivePS1);
            GUILayout.EndHorizontal();
            resolution = (RetroResolution)GUILayout.SelectionGrid((int)resolution, ResolutionNames, 4);
            GUILayout.Label($"Target {RenderWidth} x {RenderHeight} | RGB levels {colorLevels}");
            GUILayout.EndArea();
        }
    }
}

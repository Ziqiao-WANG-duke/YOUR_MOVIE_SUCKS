Shader "RetroRendering/Character"
{
    Properties
    {
        [MainColor] _BaseColor ("Base color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("Base texture", 2D) = "white" {}
        [ToggleUI] _UseBaseMap ("Use texture", Float) = 1
        [ToggleUI] _UseVertexColor ("Use vertex colors", Float) = 0
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        [ToggleUI] _UseNormalMap ("Use normal map", Float) = 0
        _BumpScale ("Normal strength", Range(0,2)) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.15
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _EmissionMap ("Emission map", 2D) = "white" {}
        [ToggleUI] _UseEmissionMap ("Use emission map", Float) = 0
        [ToggleUI] _FlatShading ("Faceted normals", Float) = 0
        [ToggleUI] _VertexSnap ("Allow vertex snapping", Float) = 1
        _SnapStrength ("Snap strength", Range(0,1)) = 0.15
        _SnapResolution ("Snap grid (pixels)", Vector) = (426,240,0,0)
        _SnapNearFade ("Near plane fade distance", Float) = 0.4
        [ToggleUI] _Affine ("Allow affine UVs", Float) = 1
        _AffineStrength ("Affine strength", Range(0,1)) = 0.15
        [ToggleUI] _AlphaClip ("Alpha clip", Float) = 0
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #define RETRO_CHARACTER 1
        #include "RetroObjectCommon.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull] ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RetroObjectVertex
            #pragma fragment RetroForwardFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull [_Cull] ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RetroObjectVertex
            #pragma fragment RetroDepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RetroObjectVertex
            #pragma fragment RetroDepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull] ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RetroShadowVertex
            #pragma fragment RetroShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
    }
    FallBack Off
}

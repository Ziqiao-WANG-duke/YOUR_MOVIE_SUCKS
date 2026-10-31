#ifndef RETRO_OBJECT_COMMON_INCLUDED
#define RETRO_OBJECT_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// All controls belong to the cloned material, never shader globals. Keep this
// layout identical across every pass of each shader (no keyword-dependent fields).
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float4 _EmissionColor;
    float4 _SnapResolution;
    float _UseBaseMap;
    float _UseVertexColor;
    float _UseEmissionMap;
    float _AlphaClip;
    float _Cutoff;
    float _VertexSnap;
    float _SnapStrength;
    float _SnapNearFade;
    float _Affine;
    float _AffineStrength;
    float _FlatShading;
    float _Cull;
#if defined(RETRO_CHARACTER)
    float _UseNormalMap;
    float _BumpScale;
    float _Metallic;
    float _Smoothness;
    float _Surface;
    float _SrcBlend;
    float _DstBlend;
    float _ZWrite;
#else
    float4 _AmbientColor;
    float _AmbientStrength;
    float _ShadowDarkness;
    float _LightingBands;
    float _DiffuseWrap;
    float _SpecularStrength;
    float _SpecularPower;
#endif
CBUFFER_END

TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
#if defined(RETRO_CHARACTER)
TEXTURE2D(_BumpMap);     SAMPLER(sampler_BumpMap);
#endif

struct RetroAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct RetroVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float4 tangentWS  : TEXCOORD2;
    float2 uv         : TEXCOORD3;
    float3 affineUV   : TEXCOORD4;
    half4 fogAndVertexLight : TEXCOORD5;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float4 RetroSnapPosition(float4 positionCS, float viewDepth)
{
    // Do not change z/w: homogeneous depth, near clipping and perspective remain
    // valid. No time/noise input, so a stationary vertex is deterministic.
    if (_VertexSnap < 0.5 || _SnapStrength <= 0.0 || positionCS.w <= 0.00001)
        return positionCS;

    float2 resolution = max(_SnapResolution.xy, float2(1.0, 1.0));
    float2 pixelPosition = (positionCS.xy / positionCS.w * 0.5 + 0.5) * resolution;
    float2 snappedNDC = floor(pixelPosition + 0.5) / resolution * 2.0 - 1.0;
    // Fade from the actual camera near plane over a distance in world units.
    // View-space depth also works for orthographic cameras, where clip w is 1.
    float nearFade = smoothstep(0.0, max(_SnapNearFade, 0.0001),
                               viewDepth - _ProjectionParams.y);
    positionCS.xy = lerp(positionCS.xy, snappedNDC * positionCS.w,
                         saturate(_SnapStrength) * nearFade);
    return positionCS;
}

RetroVaryings RetroObjectVertex(RetroAttributes input)
{
    RetroVaryings output = (RetroVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionWS = position.positionWS;
    output.positionCS = RetroSnapPosition(position.positionCS, -position.positionVS.z);
    output.normalWS = normal.normalWS;
    output.tangentWS = float4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    // Perspective interpolation divides both varyings by clip w. Dividing the
    // interpolated uv*w by the interpolated w cancels that correction, yielding
    // screen-linear UVs. SV_POSITION.w is NOT the denominator for this operation.
    output.affineUV = float3(output.uv * position.positionCS.w, position.positionCS.w);
    output.color = input.color;
    output.fogAndVertexLight.x = ComputeFogFactor(position.positionCS.z);
    output.fogAndVertexLight.yzw = VertexLighting(position.positionWS, normal.normalWS);
    return output;
}

float2 RetroGetUV(RetroVaryings input)
{
    float strength = _Affine >= 0.5 ? saturate(_AffineStrength) : 0.0;
    if (strength <= 0.0 || abs(input.affineUV.z) < 0.00001)
        return input.uv;
    return lerp(input.uv, input.affineUV.xy / input.affineUV.z, strength);
}

float4 RetroSampleBase(float2 uv, float4 vertexColor)
{
    float4 texel = 1.0;
    if (_UseBaseMap >= 0.5)
        texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    // Deliberately NOT saturated: material.color is also used for a 2.5 HDR hit
    // flash. The white fallback makes an untextured material usable as-is.
    float4 color = texel * _BaseColor;
    if (_UseVertexColor >= 0.5)
        color *= vertexColor;
    return color;
}

void RetroClipAlpha(float alpha)
{
    // Float control instead of a material keyword: SetFloat works immediately.
    if (_AlphaClip >= 0.5)
        clip(alpha - _Cutoff);
}

float3 RetroSampleEmission(float2 uv)
{
    float3 emission = _EmissionColor.rgb;
    if (_UseEmissionMap >= 0.5)
        emission *= SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
    return emission;
}

float3 RetroGetNormalWS(RetroVaryings input, float2 uv)
{
    float3 normalWS = SafeNormalize(input.normalWS);
    if (_FlatShading >= 0.5)
    {
        float3 faceNormal = cross(ddx(input.positionWS), ddy(input.positionWS));
        float lengthSquared = dot(faceNormal, faceNormal);
        if (lengthSquared > 0.000000000001)
        {
            faceNormal *= rsqrt(lengthSquared);
            // Derivative winding differs across graphics APIs/target flips.
            // Align to the mesh normal instead of assuming a particular winding.
            normalWS = dot(faceNormal, normalWS) < 0.0 ? -faceNormal : faceNormal;
        }
    }
#if defined(RETRO_CHARACTER)
    if (_UseNormalMap >= 0.5)
    {
        // Re-orthogonalize against the faceted normal. Missing/degenerate mesh
        // tangents gracefully leave the geometric normal intact.
        float3 tangent = input.tangentWS.xyz - normalWS * dot(input.tangentWS.xyz, normalWS);
        float tangentLengthSquared = dot(tangent, tangent);
        if (tangentLengthSquared > 0.000001)
        {
            tangent *= rsqrt(tangentLengthSquared);
            float3 bitangent = cross(normalWS, tangent) * input.tangentWS.w;
            float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
            normalWS = SafeNormalize(TransformTangentToWorld(normalTS, float3x3(tangent, bitangent, normalWS)));
        }
    }
#endif
    return normalWS;
}

InputData RetroGetInputData(RetroVaryings input, float3 normalWS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    // Screen-space shadows use the rasterized (snapped) pixel, not an unsnapped
    // vertex shadow coordinate. Other shadows remain stable in world space.
    inputData.shadowCoord = float4(inputData.normalizedScreenSpaceUV, 0.0, 1.0);
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    inputData.fogCoord = input.fogAndVertexLight.x;
    inputData.vertexLighting = input.fogAndVertexLight.yzw;
    // Intentional scope: legacy SH/light probes, not lightmaps or APV.
    inputData.bakedGI = SampleSH(normalWS);
    inputData.shadowMask = unity_ProbesOcclusion;
    return inputData;
}

#if !defined(RETRO_CHARACTER)
float3 RetroEnvironmentLight(Light light, float3 normalWS, float3 viewDirectionWS)
{
    float diffuse = saturate(dot(normalWS, light.direction));
    // Optional wrap term: cheap half-Lambert approximation for the soft, grazing
    // light response of early Gouraud-shaded console games. 0 = pure Lambert.
    float wrap = saturate(_DiffuseWrap);
    diffuse = saturate((diffuse + wrap) / (1.0 + wrap));
    if (_LightingBands >= 2.0)
    {
        float intervals = max(floor(_LightingBands + 0.5) - 1.0, 1.0);
        diffuse = floor(diffuse * intervals + 0.5) / intervals;
    }
    float shadow = lerp(1.0, light.shadowAttenuation, saturate(_ShadowDarkness));
    // A single narrow Blinn-Phong lobe keeps grazing light on walls readable
    // without the cost of a full BRDF. Strength 0 disables it entirely.
    float3 halfway = SafeNormalize(light.direction + viewDirectionWS);
    float specular = pow(saturate(dot(normalWS, halfway)), max(_SpecularPower, 1.0)) * saturate(_SpecularStrength);
    // Quantize only N.L, never HDR light colour or distance attenuation.
    // 1/PI keeps the diffuse term on the same irradiance scale as URP's PBR
    // (UniversalFragmentPBR) so the same albedo and lights stay comparable.
    const float kDiffuseNormalization = 0.31830988618;
    return light.color * ((diffuse * kDiffuseNormalization + specular) * light.distanceAttenuation * shadow);
}

float3 RetroEnvironmentLighting(InputData inputData)
{
    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    half4 shadowMask = CalculateShadowMask(inputData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, ao);
    float3 direct = 0.0;
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
        direct += RetroEnvironmentLight(mainLight, inputData.normalWS, inputData.viewDirectionWS);

#if defined(_ADDITIONAL_LIGHTS)
    // Forward+ reports zero from GetAdditionalLightsCount(). The cluster loop
    // below, not a hand-written for(count), is what visits point/spot lights.
    uint pixelLightCount = GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, ao);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
            direct += RetroEnvironmentLight(light, inputData.normalWS, inputData.viewDirectionWS);
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, ao);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
            direct += RetroEnvironmentLight(light, inputData.normalWS, inputData.viewDirectionWS);
    LIGHT_LOOP_END
#endif
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    // Non-cluster Forward fallback; vertex lights cannot have per-pixel bands.
    direct += inputData.vertexLighting * ao.directAmbientOcclusion;
#endif
    float3 ambient = (inputData.bakedGI + _AmbientColor.rgb) * max(_AmbientStrength, 0.0);
    return direct + ambient * ao.indirectAmbientOcclusion;
}
#endif

void RetroForwardFragment(RetroVaryings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = RetroGetUV(input);
    float4 base = RetroSampleBase(uv, input.color);
    // Evaluate derivatives before any alpha discard (also in DepthNormals).
    float3 normalWS = RetroGetNormalWS(input, uv);
    RetroClipAlpha(base.a);
    InputData inputData = RetroGetInputData(input, normalWS);
#if defined(RETRO_CHARACTER)
    SurfaceData surface = (SurfaceData)0;
    surface.albedo = base.rgb;
    surface.metallic = saturate(_Metallic);
    surface.specular = half3(0.0, 0.0, 0.0);
    surface.smoothness = saturate(_Smoothness);
    surface.normalTS = half3(0.0, 0.0, 1.0); // Normal already resolved in InputData.
    surface.occlusion = 1.0;
    surface.emission = RetroSampleEmission(uv);
    surface.alpha = _Surface > 0.5 ? base.a : 1.0; // Standard straight-alpha transparency.
    half4 color = UniversalFragmentPBR(inputData, surface);
#else
    half4 color = half4(base.rgb * RetroEnvironmentLighting(inputData) + RetroSampleEmission(uv), 1.0);
#endif
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    outColor = color;
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

half RetroDepthFragment(RetroVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    if (_AlphaClip >= 0.5)
        RetroClipAlpha(RetroSampleBase(RetroGetUV(input), input.color).a);
    return input.positionCS.z;
}

void RetroDepthNormalsFragment(RetroVaryings input, out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = RetroGetUV(input);
    float3 normalWS = RetroGetNormalWS(input, uv);
    if (_AlphaClip >= 0.5)
        RetroClipAlpha(RetroSampleBase(uv, input.color).a);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    half3 packedNormalWS = PackFloat2To888(saturate(octNormalWS * 0.5 + 0.5));
    outNormalWS = half4(packedNormalWS, 0.0);
#else
    outNormalWS = half4(normalWS, 0.0);
#endif
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

// Shadows are deliberately unsnapped and use perspective-correct UVs. Otherwise
// camera-dependent pixel snapping/affine mapping would make the shadow map swim.
// Cutout shadows therefore need not exactly match the stylized camera silhouette.
float3 _LightDirection;
float3 _LightPosition;

struct RetroShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

RetroShadowVaryings RetroShadowVertex(RetroAttributes input)
{
    RetroShadowVaryings output = (RetroShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = SafeNormalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.color = input.color;
    return output;
}

half4 RetroShadowFragment(RetroShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    if (_AlphaClip >= 0.5)
        RetroClipAlpha(RetroSampleBase(input.uv, input.color).a);
    return 0.0;
}

#endif

Shader "RetroRendering/ScreenComposite"
{
    Properties { _MainTex ("Scene", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Name "Retro composite"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _ContentRect; // normalized xy offset, zw size
            float _Quantize, _Levels, _Dither;
            float4 _SourceSize;

            // ---- Director mode film look (monochrome, vintage camera) ----
            float _DirectorAmount;        // 0 = off, 1 = full
            float _DirectorContrast;      // 1 = flat, 1.8 = punchy
            float _DirectorLift;          // lifts blacks slightly
            float _DirectorBrightness;    // exposure gain on the monochrome image
            float _DirectorThreshold;     // black point: below this becomes pure black
            float _DirectorGrain;         // film grain
            float _DirectorGrainScale;    // grain cell size (px)
            float _DirectorFlicker;       // exposure flicker
            float _DirectorFlickerSpeed;  // flicker steps per second
            float _DirectorVignette;      // corner falloff
            float _DirectorScratch;       // vertical film scratches
            float _DirectorScratchSpeed;  // scratches per second
            float _DirectorStripe;        // vertical interference bands
            float _DirectorTime;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float DirectorHash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float DirectorHash21(float2 p)
            {
                float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 导演模式：显示空间去色 + 高对比 + 胶片颗粒/闪烁/暗角/竖向干扰与划痕。
            // 只处理场景画面；UI 与弱点标记由后期的原生分辨率相机绘制，不会经过这里。
            float3 DirectorFilm(float3 rgb, float2 uv)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                float3 disp = saturate(rgb);
                #else
                float3 disp = LinearToSRGB(saturate(rgb));
                #endif

                float luma = dot(disp, float3(0.2126, 0.7152, 0.0722));

                // 阈值（黑位）：把 [threshold..1] 重新映射到 [0..1]，低于阈值的直接压成纯黑
                float threshold = saturate(_DirectorThreshold);
                float d = saturate((luma - threshold) / max(1.0 - threshold, 0.001));

                // 亮度（曝光增益）先于对比：不会把中间调压死
                d = saturate(d * max(_DirectorBrightness, 0.0));

                d = saturate((d - 0.5) * _DirectorContrast + 0.5 + _DirectorLift);

                float t = _DirectorTime;

                // 闪烁：整帧曝光轻微跳动（按固定步进而不是每帧随机，避免噪点底噪）
                float flickerStep = floor(t * max(_DirectorFlickerSpeed, 0.001));
                d *= 1.0 + (DirectorHash11(flickerStep) - 0.5) * 0.28 * saturate(_DirectorFlicker);

                // 胶片颗粒
                float2 px = uv * max(_SourceSize.xy, float2(1.0, 1.0));
                float grain = DirectorHash21(px / max(_DirectorGrainScale, 0.25) + frac(t) * 53.0) - 0.5;
                d += grain * 0.22 * saturate(_DirectorGrain);

                // 竖向干扰条纹：整列亮度按时间变换的滚动波带
                float stripeAmount = saturate(_DirectorStripe);
                if (stripeAmount > 0.001)
                {
                    float col = floor(uv.x * 180.0);
                    float wave = DirectorHash11(col * 0.37 + floor(t * 14.0) * 0.11);
                    float band = step(1.0 - 0.18 * stripeAmount, wave);
                    d *= 1.0 - band * 0.32 * stripeAmount;
                    // 细密的滚动亮带
                    float roll = frac(uv.x * 3.0 + t * 0.35);
                    d += smoothstep(0.985, 1.0, roll) * 0.10 * stripeAmount;
                }

                // 胶片划痕：偶尔出现的细竖线（亮 / 暗两种），位置随时间跳变
                float scratchAmount = saturate(_DirectorScratch);
                if (scratchAmount > 0.001)
                {
                    float seed = floor(t * max(_DirectorScratchSpeed, 0.01));
                    float x = DirectorHash11(seed) * 0.9 + 0.05;
                    float width = 0.0012 + 0.0022 * DirectorHash11(seed + 7.31);
                    float scratchLine = 1.0 - smoothstep(0.0, width, abs(uv.x - x));
                    float polarity = DirectorHash11(seed + 3.17) > 0.45 ? 1.0 : -0.35;
                    d += scratchLine * polarity * 0.55 * scratchAmount;
                }

                // 暗角
                float2 centered = uv - 0.5;
                centered.x *= 1.15; // 非方形画面时保持圆形暗角
                float vig = saturate(1.0 - dot(centered, centered) * (1.6 + 1.9 * saturate(_DirectorVignette)));
                d *= lerp(1.0, vig, saturate(_DirectorVignette));

                float3 mono = saturate(d).xxx;
                #if defined(UNITY_COLORSPACE_GAMMA)
                return lerp(rgb, mono, saturate(_DirectorAmount));
                #else
                return lerp(rgb, SRGBToLinear(mono), saturate(_DirectorAmount));
                #endif
            }
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionCS=float4(i.positionOS.xy,0,1); o.uv=i.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv=(i.uv-_ContentRect.xy)/_ContentRect.zw;
                if(any(uv<0)||any(uv>1)) return half4(0,0,0,1);
                float3 rgb=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb;
                if(_Quantize>0.5)
                {
                    // Quantize displayed (sRGB) colors, not linear lighting energy.
                    float3 display=saturate(rgb);
                    #if !defined(UNITY_COLORSPACE_GAMMA)
                    display=LinearToSRGB(display);
                    #endif
                    uint2 p=(uint2)floor(uv*_SourceSize.xy)&3;
                    static const float bayer[16]={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
                    float noise=((bayer[p.y*4+p.x]+0.5)/16.0-0.5)*_Dither;
                    float steps=max(1,_Levels-1);
                    display=saturate(floor(display*steps+0.5+noise)/steps);
                    #if defined(UNITY_COLORSPACE_GAMMA)
                    rgb=display;
                    #else
                    rgb=SRGBToLinear(display);
                    #endif
                }
                if (_DirectorAmount > 0.001)
                    rgb = DirectorFilm(rgb, uv);

                return half4(rgb,1);
            }
            ENDHLSL        }
    }
}

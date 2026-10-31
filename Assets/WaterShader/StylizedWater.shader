// Stylized Water: rolling vertex waves, a deep-to-horizon colour ramp,
// noise foam on the crests, a sun glint, and optional shore foam from the
// depth texture. Transparent. URP 17 (Unity 6).
// Scripts For Unity, MIT licensed.
Shader "Scripts For Unity/Stylized Water"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.05, 0.32, 0.5, 0.92)
        _HorizonColor ("Horizon Color", Color) = (0.35, 0.78, 0.86, 0.8)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
        _WaveHeight ("Wave Height", Range(0, 1)) = 0.12
        _WaveFrequency ("Wave Frequency", Range(0.1, 5)) = 1.2
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1.1
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamScale ("Foam Scale", Range(0.1, 10)) = 6
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.25
        _GlintStrength ("Sun Glint", Range(0, 4)) = 1.2
        [Toggle(_SHORE_FOAM)] _ShoreFoam ("Shore Foam (needs Depth Texture)", Float) = 0
        _ShoreDistance ("Shore Foam Distance", Range(0.05, 3)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _SHORE_FOAM
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _HorizonColor;
                float _FresnelPower;
                float _WaveHeight;
                float _WaveFrequency;
                float _WaveSpeed;
                half4 _FoamColor;
                float _FoamScale;
                float _FoamAmount;
                float _GlintStrength;
                float _ShoreFoam;
                float _ShoreDistance;
            CBUFFER_END

            float SfuHash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float SfuValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                float n000 = SfuHash13(i);
                float n100 = SfuHash13(i + float3(1, 0, 0));
                float n010 = SfuHash13(i + float3(0, 1, 0));
                float n110 = SfuHash13(i + float3(1, 1, 0));
                float n001 = SfuHash13(i + float3(0, 0, 1));
                float n101 = SfuHash13(i + float3(1, 0, 1));
                float n011 = SfuHash13(i + float3(0, 1, 1));
                float n111 = SfuHash13(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                            lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
            }

            // Four octaves, normalised back to 0..1
            float SfuFractalNoise(float3 p)
            {
                float n = SfuValueNoise(p) * 0.5;
                n += SfuValueNoise(p * 2.03) * 0.25;
                n += SfuValueNoise(p * 4.01) * 0.125;
                n += SfuValueNoise(p * 8.05) * 0.0625;
                return n / 0.9375;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float wave : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // Two crossed sine waves and their slopes, for the normal
                float t = _Time.y * _WaveSpeed;
                float ax = positionWS.x * _WaveFrequency + t;
                float az = positionWS.z * _WaveFrequency * 0.8 + t * 0.9;
                float wave = sin(ax) * cos(az);
                float slopeX = cos(ax) * cos(az) * _WaveFrequency * _WaveHeight;
                float slopeZ = -sin(ax) * sin(az) * _WaveFrequency * 0.8 * _WaveHeight;
                positionWS.y += wave * _WaveHeight;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalize(float3(-slopeX, 1.0, -slopeZ));
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.wave = wave;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Deep colour looking down, horizon colour at grazing angles
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                half4 water = lerp(_DeepColor, _HorizonColor, fresnel);

                // Foam: drifting noise, boosted on the wave crests and faded in the troughs
                float2 drift = float2(1.0, 0.6) * _Time.y * 0.15;
                float noise = SfuFractalNoise(float3(input.positionWS.xz * _FoamScale + drift, _Time.y * 0.1));
                float crest = saturate(input.wave * 0.5 + 0.5);
                float foamMask = noise * lerp(0.6, 1.25, crest);
                float foam = smoothstep(1.0 - _FoamAmount, 1.02 - _FoamAmount, foamMask);

            #if defined(_SHORE_FOAM)
                // Foam where the water surface is close to the geometry behind it
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float shore = 1.0 - saturate((sceneEyeDepth - input.screenPos.w) / _ShoreDistance);
                foam = max(foam, step(0.5, shore * (0.5 + noise)));
            #endif

                // Sun glint from the main light
                Light mainLight = GetMainLight();
                float3 halfDirWS = normalize(mainLight.direction + viewDirWS);
                float glint = smoothstep(0.92, 0.95, pow(saturate(dot(normalWS, halfDirWS)), 64.0)) * _GlintStrength;

                float3 color = lerp(water.rgb, _FoamColor.rgb, foam) + glint * mainLight.color;
                float alpha = saturate(max(water.a, foam * _FoamColor.a) + glint);
                return half4(MixFog(color, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

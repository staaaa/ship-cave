Shader "Custom/WaterSurface_Gerstner_URP_Improved_Fixed"
{
    Properties
    {
        _Color ("Water Color", Color) = (0,0.4,0.7,0.5)
        _DepthColor ("Depth Color", Color) = (0,0.1,0.3,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.9
        _FresnelPower ("Fresnel Power", Range(0,10)) = 5

        // Foam
        _FoamColor ("Foam Color", Color) = (1,1,1,0.9)
        _FoamThreshold ("Foam Threshold", Range(-2,2)) = 0.5
        _FoamSpread ("Foam Spread", Range(0.01,2)) = 0.3
        _FoamNoiseScale ("Foam Noise Scale", Float) = 5.0
        _FoamSpeed ("Foam Speed", Float) = 0.5

        // Caustics
        _CausticsTex ("Caustics Texture", 2D) = "white" {}
        _CausticsScale ("Caustics Scale", Float) = 0.5
        _CausticsSpeed ("Caustics Speed", Float) = 0.2
        _CausticsStrength ("Caustics Strength", Range(0,2)) = 0.5

        // Reflection
        _ReflectionStrength ("Reflection Strength", Range(0,1)) = 0.8
        _ReflectionDistortion ("Reflection Distortion", Range(0,0.5)) = 0.05

        _WaterLevel ("Water Level", Float) = 0.0
        _WaveScale ("Wave Scale", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _DepthColor;
                float _Smoothness;
                float _FresnelPower;

                // Foam (deklaracje zgodne z Properties)
                float4 _FoamColor;
                float _FoamThreshold;
                float _FoamSpread;
                float _FoamNoiseScale;
                float _FoamSpeed;

                // Caustics
                float4 _CausticsTex_ST;
                float _CausticsScale;
                float _CausticsSpeed;
                float _CausticsStrength;

                // Reflection
                float _ReflectionStrength;
                float _ReflectionDistortion;

                // Water / global
                float _WaterLevel;
                float _WaveScale;
            CBUFFER_END

            TEXTURE2D(_CausticsTex);
            SAMPLER(sampler_CausticsTex);

            // --- arrays expected from script ---
            float4 _WaveDirections[32]; // (dir.x, dir.y, freq, amp)
            float4 _WaveParams[32];     // (phase, steepness, 0,0)
            int _WaveCount;

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float waveSum : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                float viewAngle : TEXCOORD5;
            };

            // simple hash / noise for foam texture
            float hash(float2 p) {
                return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453);
            }
            float noise(float2 p) {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1.0,0.0));
                float c = hash(i + float2(0.0,1.0));
                float d = hash(i + float2(1.0,1.0));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }

            // Safe Gerstner wave. Returns displacement and accumulates height.
            float3 GerstnerDisplace(float2 posXZ, out float height)
            {
                float3 offset = float3(0,0,0);
                height = 0;
                for (int i=0; i < _WaveCount; ++i)
                {
                    float2 dir = normalize(_WaveDirections[i].xy);
                    float freq = _WaveDirections[i].z;
                    float amp  = _WaveDirections[i].w * _WaveScale;
                    float phase = _WaveParams[i].x;
                    float steep = _WaveParams[i].y;

                    float k = max(0.00001, freq);
                    float theta = dot(dir, posXZ) * k + _Time.y * phase;
                    float s = sin(theta);
                    float c = cos(theta);
                    float Q = saturate(steep * 0.5);

                    offset.x += dir.x * (Q * amp * c);
                    offset.z += dir.y * (Q * amp * c);
                    offset.y += amp * s;

                    height += amp * s;
                }
                return offset;
            }

            // Approx normal computed via Gerstner partial derivatives (fast and stable)
            float3 GerstnerNormal(float2 posXZ)
            {
                float3 n = float3(0,1,0);
                for (int i=0; i < _WaveCount; ++i)
                {
                    float2 dir = normalize(_WaveDirections[i].xy);
                    float freq = _WaveDirections[i].z;
                    float amp  = _WaveDirections[i].w * _WaveScale;
                    float phase = _WaveParams[i].x;
                    float steep = _WaveParams[i].y;

                    float k = max(0.00001, freq);
                    float theta = dot(dir, posXZ) * k + _Time.y * phase;
                    float s = sin(theta);
                    float c = cos(theta);
                    float Q = saturate(steep * 0.5);

                    n.x += -dir.x * k * (Q * amp) * c;
                    n.z += -dir.y * k * (Q * amp) * c;
                }
                return normalize(n);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float h;
                float3 disp = GerstnerDisplace(posWS.xz, h);
                posWS += disp;
                float3 normalWS = GerstnerNormal(posWS.xz);

                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.positionWS = posWS;
                OUT.normalWS = normalWS;
                OUT.uv = IN.uv;
                OUT.waveSum = h;
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);

                float3 viewDir = GetWorldSpaceViewDir(posWS);
                OUT.viewAngle = abs(dot(normalize(viewDir), float3(0,1,0)));

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                #if UNITY_REVERSED_Z
                    float sceneRaw = SampleSceneDepth(screenUV);
                #else
                    float sceneRaw = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(screenUV));
                #endif
                float sceneDepth = LinearEyeDepth(sceneRaw, _ZBufferParams);
                float waterDepth = LinearEyeDepth(IN.positionCS.z, _ZBufferParams);
                float depthDiff = sceneDepth - waterDepth;

                float intersection = saturate(depthDiff / max(0.0001, (_FoamSpread*2.0)));
                float intersectionFoam = 1.0 - pow(intersection, 1.5);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float fresnel = pow(1.0 - saturate(dot(V, N)), _FresnelPower);

                float4 baseCol = lerp(_Color, _DepthColor, fresnel);

                float3 R = reflect(-V, N);
                float4 refl = SAMPLE_TEXTURECUBE(unity_SpecCube0, samplerunity_SpecCube0, R);
                refl.rgb = DecodeHDREnvironment(refl, unity_SpecCube0_HDR);
                baseCol.rgb = lerp(baseCol.rgb, refl.rgb, _ReflectionStrength * fresnel);

                float2 cUV = IN.positionWS.xz * _CausticsScale;
                cUV += _Time.y * _CausticsSpeed * float2(0.2, -0.1);
                float cVal = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, cUV).r;
                cVal = pow(cVal, 1.2);
                float depthFade = saturate(depthDiff / (1.5 + _CausticsScale));
                baseCol.rgb += cVal * _CausticsStrength * depthFade;

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndotl = saturate(dot(N, mainLight.direction));
                float3 lightCol = mainLight.color.rgb * (ndotl * 0.8 + 0.2) * mainLight.shadowAttenuation;
                baseCol.rgb *= lightCol;

                // foam
                float foamFromWaves = saturate((IN.waveSum - _FoamThreshold) / max(0.0001, _FoamSpread));
                float2 foamUV = IN.positionWS.xz * _FoamNoiseScale;
                foamUV += _Time.y * _FoamSpeed * float2(0.3, -0.2);
                float foamNoise = noise(foamUV) * noise(foamUV * 2.3 + 0.5);
                float foamMask = max(foamFromWaves * foamNoise, intersectionFoam);
                foamMask = saturate(foamMask);

                baseCol.rgb = lerp(baseCol.rgb, _FoamColor.rgb, foamMask * _FoamColor.a);

                baseCol.a = _Color.a;
                if (depthDiff < -0.01) {
                    if (depthDiff < -2.0) discard;
                }

                return half4(baseCol.rgb, baseCol.a);
            }

            ENDHLSL
        }
    }
    FallBack Off
}

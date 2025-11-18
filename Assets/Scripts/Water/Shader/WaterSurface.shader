Shader "Custom/WaterSurface_Gerstner"
{
    Properties
    {
        _ColorShallow ("Water Color Shallow", Color) = (0.5, 0.8, 1.0, 0.3)
        _ColorDeep ("Water Color Deep", Color) = (0.0, 0.2, 0.4, 0.9)
        _DepthRange ("Depth Range", Float) = 10.0
        _WaterLevel ("Water Level Y", Float) = 0.0
        
        [Header(Foam)]
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamThreshold ("Foam Threshold", Range(0, 1)) = 0.7
        _FoamSmoothness ("Foam Smoothness", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Maksymalna liczba fal (musi być stała w shaderze)
            #define MAX_WAVES 16

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorShallow;
                float4 _ColorDeep;
                float _DepthRange;
                float _WaterLevel;
                int _WaveCount;
                
                float4 _FoamColor;
                float _FoamThreshold;
                float _FoamSmoothness;
            CBUFFER_END

            // Dane fal z C#
            float4 _WaveDirections[MAX_WAVES]; // (dirX, dirY, frequency, phase)
            float4 _WaveParams[MAX_WAVES];     // (amplitude, steepness, waveLength, speed)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            // Funkcja obliczająca normalną Gerstner wave
            float3 GerstnerNormal(float3 positionWS, float time)
            {
                float3 normal = float3(0, 1, 0); // Start z normalną w górę
                
                for (int i = 0; i < _WaveCount && i < MAX_WAVES; i++)
                {
                    float2 direction = _WaveDirections[i].xy;
                    float frequency = _WaveDirections[i].z;
                    float phase = _WaveDirections[i].w;
                    
                    float amplitude = _WaveParams[i].x;
                    float steepness = _WaveParams[i].y;
                    
                    float projection = dot(direction, positionWS.xz);
                    float theta = projection * frequency + time * phase;
                    float cosTheta = cos(theta);
                    float sinTheta = sin(theta);
                    
                    float wa = frequency * amplitude;
                    float s = steepness * wa;
                    
                    // Oblicz gradient normalnej
                    normal.x -= direction.x * wa * cosTheta;
                    normal.z -= direction.y * wa * cosTheta;
                    normal.y -= s * sinTheta;
                }
                
                return normalize(normal);
            }
            float3 GerstnerWave(float3 positionWS, float time)
            {
                float3 result = positionWS;
                result.y = _WaterLevel; // Startuj od poziomu wody
                
                // Iteruj przez wszystkie fale
                for (int i = 0; i < _WaveCount && i < MAX_WAVES; i++)
                {
                    // Rozpakuj dane fali
                    float2 direction = _WaveDirections[i].xy;
                    float frequency = _WaveDirections[i].z;
                    float phase = _WaveDirections[i].w;
                    
                    float amplitude = _WaveParams[i].x;
                    float steepness = _WaveParams[i].y;
                    
                    // Oblicz fazę fali
                    float projection = dot(direction, positionWS.xz);
                    float theta = projection * frequency + time * phase;
                    float sinTheta = sin(theta);
                    float cosTheta = cos(theta);
                    
                    // Gerstner wave displacement
                    float steepnessFactor = steepness / (frequency * _WaveCount);
                    result.x += direction.x * steepnessFactor * amplitude * cosTheta;
                    result.z += direction.y * steepnessFactor * amplitude * cosTheta;
                    result.y += amplitude * sinTheta;
                }
                
                return result;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                // 1. Konwertuj pozycję do World Space
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                
                // 2. Oblicz pozycję z falami Gerstnera
                float3 wavePosition = GerstnerWave(positionWS, _Time.y);
                
                // 3. Oblicz normalną z falami
                OUT.normalWS = GerstnerNormal(positionWS, _Time.y);
                
                // 4. Użyj pozycji z falami do renderowania
                OUT.positionWS = wavePosition;
                OUT.positionHCS = TransformWorldToHClip(wavePosition);
                OUT.uv = IN.uv;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Oblicz głębokość na podstawie wysokości fragmentu
                float fragmentHeight = IN.positionWS.y;
                float depth = _WaterLevel - fragmentHeight;
                float normalizedDepth = saturate(depth / _DepthRange);
                
                // Interpoluj kolor wody (głębokość)
                float4 waterColor = lerp(_ColorShallow, _ColorDeep, normalizedDepth);
                
                // === PIANA OPARTA NA NACHYLENIU ===
                // Im bardziej pionowa normalna (y bliskie 1.0), tym mniej piany
                // Im bardziej skośna normalna (y bliskie 0.0), tym więcej piany
                float slopeFactor = saturate(1.0 - IN.normalWS.y);
                
                // Interpoluj między kolorem wody a pianą
                float4 finalColor = lerp(waterColor, _FoamColor, slopeFactor);
                
                return finalColor;
            }

            ENDHLSL
        }
    }
    FallBack Off
}
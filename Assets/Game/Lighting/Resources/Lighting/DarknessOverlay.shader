Shader "Hidden/Game/Lighting/DarknessOverlay"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            #define MAX_LIGHTS 32

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4x4 _InverseViewProjection;
            float _GameplayPlaneZ;
            float4 _DarknessColor;
            float _DarknessOpacity;
            int _LightCount;
            float4 _LightPositionRangeIntensity[MAX_LIGHTS];
            float4 _LightDirectionShapeSoftness[MAX_LIGHTS];
            float4 _LightAngleCosines[MAX_LIGHTS];

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;

                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0)
                {
                    output.uv.y = 1.0 - output.uv.y;
                }
                #endif

                return output;
            }

            bool TryGetGameplayPosition(float2 uv, out float2 gameplayPosition)
            {
                float2 ndc = uv * 2.0 - 1.0;
                float4 nearHomogeneous = mul(_InverseViewProjection, float4(ndc, -1.0, 1.0));
                float4 farHomogeneous = mul(_InverseViewProjection, float4(ndc, 1.0, 1.0));
                float3 nearWorld = nearHomogeneous.xyz / max(abs(nearHomogeneous.w), 0.000001) * sign(nearHomogeneous.w);
                float3 farWorld = farHomogeneous.xyz / max(abs(farHomogeneous.w), 0.000001) * sign(farHomogeneous.w);

                float denominator = farWorld.z - nearWorld.z;
                if (abs(denominator) <= 0.000001)
                {
                    gameplayPosition = 0.0;
                    return false;
                }

                float interpolation = (_GameplayPlaneZ - nearWorld.z) / denominator;
                gameplayPosition = lerp(nearWorld.xy, farWorld.xy, interpolation);
                return interpolation >= 0.0;
            }

            float EvaluateLight(int index, float2 gameplayPosition)
            {
                float4 positionRangeIntensity = _LightPositionRangeIntensity[index];
                float4 directionShapeSoftness = _LightDirectionShapeSoftness[index];
                float2 offset = gameplayPosition - positionRangeIntensity.xy;
                float distanceToLight = length(offset);
                float range = max(positionRangeIntensity.z, 0.0);

                if (range <= 0.0001 || distanceToLight > range)
                {
                    return 0.0;
                }

                float softness = clamp(directionShapeSoftness.w, 0.0, range);
                float radialInfluence = softness <= 0.0001
                    ? 1.0
                    : saturate((range - distanceToLight) / softness);

                float angularInfluence = 1.0;
                bool isSector = directionShapeSoftness.z > 0.5;
                if (isSector && distanceToLight > 0.0001)
                {
                    float2 lightDirection = normalize(directionShapeSoftness.xy);
                    float directionDot = dot(lightDirection, offset / distanceToLight);
                    float outerCosine = _LightAngleCosines[index].x;
                    float innerCosine = _LightAngleCosines[index].y;

                    if (directionDot < outerCosine)
                    {
                        return 0.0;
                    }

                    float cosineWidth = innerCosine - outerCosine;
                    angularInfluence = cosineWidth <= 0.0001
                        ? 1.0
                        : saturate((directionDot - outerCosine) / cosineWidth);
                }

                return radialInfluence * angularInfluence * max(positionRangeIntensity.w, 0.0);
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.uv);
                float2 gameplayPosition;
                float visibility = 0.0;

                if (TryGetGameplayPosition(input.uv, gameplayPosition))
                {
                    [loop]
                    for (int i = 0; i < MAX_LIGHTS; i++)
                    {
                        if (i >= _LightCount)
                        {
                            break;
                        }

                        visibility = max(visibility, EvaluateLight(i, gameplayPosition));
                    }
                }

                float darkness = saturate(_DarknessOpacity) * saturate(_DarknessColor.a) *
                    (1.0 - saturate(visibility));
                source.rgb = lerp(source.rgb, _DarknessColor.rgb, darkness);
                return source;
            }
            ENDCG
        }
    }

    Fallback Off
}

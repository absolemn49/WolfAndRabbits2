Shader "WolfAndRabbits/WarcraftStylizedFog"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.35
        _SpecularStrength("Specular Strength", Range(0, 2)) = 0.45
        _RimColor("Rim Glow Color", Color) = (0.65, 0.88, 1.0, 1)
        _RimPower("Rim Power", Range(0.5, 8.0)) = 3.2
        _RimStrength("Rim Strength", Range(0, 2)) = 0.35
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _UseVertexColor("Use Vertex Color", Range(0, 1)) = 0.0
        _WindSway("Wind Sway", Range(0, 0.5)) = 0.0
        _FogInfluence("Fog Influence", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Global Fog of War uniforms updated by FogOfWarController
            TEXTURE2D(_WRFogTex);
            SAMPLER(sampler_WRFogTex);
            float4 _WRMapBounds; // xy = minXZ, zw = sizeXZ
            float _WRFogEnabled;
            float4 _WRFogTint;   // light gray-blue tint outside vision

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _SpecularStrength;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half4 _EmissionColor;
                half _UseVertexColor;
                half _WindSway;
                half _FogInfluence;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS    : TEXCOORD2;
                half4 color       : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posOS = IN.positionOS.xyz;
                float3 posWS = TransformObjectToWorld(posOS);

                // Gentle breeze sway on foliage crowns
                if (_WindSway > 0.001)
                {
                    float heightFactor = saturate(posOS.y * 0.5);
                    float phase = _Time.y * 1.8 + posWS.x * 0.65 + posWS.z * 0.55;
                    posWS.x += sin(phase) * _WindSway * heightFactor;
                    posWS.z += cos(phase * 0.83) * _WindSway * 0.7 * heightFactor;
                }

                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 vCol = lerp(half3(1, 1, 1), IN.color.rgb, _UseVertexColor);
                half3 albedo = texCol.rgb * _BaseColor.rgb * vCol;

                half3 N = normalize(IN.normalWS);
                half3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Main directional light + real-time shadows
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 L = normalize(mainLight.direction);

                half NdotL = dot(N, L);
                // Stylized Warcraft 3 half-Lambert wrap lighting so silhouettes stay rich & readable
                half wrappedDiffuse = saturate(NdotL * 0.62 + 0.38);
                half shadowAtten = lerp(0.42, 1.0, mainLight.shadowAttenuation);
                half3 directDiffuse = mainLight.color * wrappedDiffuse * shadowAtten;

                // Stylized glossy specular highlight
                half3 H = normalize(L + V);
                half specPow = lerp(8.0, 96.0, _Smoothness);
                half spec = pow(saturate(dot(N, H)), specPow) * _SpecularStrength * mainLight.shadowAttenuation;
                half3 specularCol = mainLight.color * spec;

                // Hemispheric stylized ambient (cool forest shadow tint + warm sky fill)
                half3 skyAmbient = half3(0.30, 0.39, 0.48);
                half3 groundAmbient = half3(0.14, 0.18, 0.22);
                half3 ambient = lerp(groundAmbient, skyAmbient, saturate(N.y * 0.5 + 0.5));

                // Additional point lights (Forward and URP 17 Forward+ Cluster Light Loop)
                half3 addLightSum = half3(0, 0, 0);
                #ifdef _ADDITIONAL_LIGHTS
                    InputData inputData = (InputData)0;
                    inputData.positionWS = IN.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light addL = GetAdditionalLight(lightIndex, IN.positionWS);
                        half aNdotL = saturate(dot(N, addL.direction) * 0.65 + 0.35);
                        addLightSum += addL.color * (aNdotL * addL.distanceAttenuation * addL.shadowAttenuation);
                    LIGHT_LOOP_END
                #endif

                // Crisp rim glow for "lung linh" 3D stylized Warcraft feel
                half fresnel = pow(1.0 - saturate(dot(N, V)), _RimPower);
                half3 rim = _RimColor.rgb * fresnel * _RimStrength;

                half3 litColor = albedo * (ambient + directDiffuse + addLightSum) + specularCol + rim + _EmissionColor.rgb;

                // Smooth light-gray Fog of War (trees & grass stay clearly visible under a light gray veil)
                if (_WRFogEnabled > 0.5 && _FogInfluence > 0.01)
                {
                    float2 fogUV = (IN.positionWS.xz - _WRMapBounds.xy) / max(_WRMapBounds.zw, float2(1.0, 1.0));
                    half vis = SAMPLE_TEXTURE2D(_WRFogTex, sampler_WRFogTex, saturate(fogUV)).r;
                    half luma = dot(litColor, half3(0.299, 0.587, 0.114));
                    half3 fogTint = _WRFogTint.a > 0.01 ? _WRFogTint.rgb : half3(0.60, 0.64, 0.69);
                    // Blend subtly toward cool light-gray mist while preserving clear readable shapes
                    half3 fogged = lerp(litColor * 0.56, half3(luma, luma, luma) * 0.62, 0.65) * 0.85 + fogTint * 0.14;
                    litColor = lerp(litColor, lerp(fogged, litColor, vis), _FogInfluence);
                }

                return half4(litColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));
                OUT.positionCS = ApplyShadowClamping(posCS);
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Simple Lit"
}

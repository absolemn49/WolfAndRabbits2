Shader "WolfAndRabbits/WarcraftGlowUnlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [HDR] [MainColor] _BaseColor("Glow Color", Color) = (1, 0.9, 0.4, 0.85)
        _PulseSpeed("Pulse Speed", Range(0, 10)) = 0.0
        _PulseAmount("Pulse Amount", Range(0, 0.6)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "GlowPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                half4 col = texCol * _BaseColor * IN.color;
                col.rgb *= pulse;
                return col;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}

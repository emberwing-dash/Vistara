// Vistara mascot: flat two-tone cel shading with a per-material colour (the outline is an inverted-hull mesh exported with the model).
Shader "Vistara/MascotToon"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Shade ("Shadow brightness", Range(0.4, 1)) = 0.78
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Shade;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; float3 nrm : NORMAL; };
            struct Vary { float4 cs : SV_POSITION; float3 wn : TEXCOORD0; };

            Vary vert(Attr i)
            {
                Vary o;
                o.cs = TransformObjectToHClip(i.pos.xyz);
                o.wn = TransformObjectToWorldNormal(i.nrm);
                return o;
            }

            half4 frag(Vary i) : SV_Target
            {
                Light l = GetMainLight();
                half ndl = dot(normalize(i.wn), l.direction);
                half cel = step(-0.05h, ndl);
                half3 shade = lerp(_Shade.xxx * half3(0.92h, 0.9h, 1.0h), half3(1, 1, 1), cel);
                return half4(_Color.rgb * shade, 1);
            }
            ENDHLSL
        }
    }
}

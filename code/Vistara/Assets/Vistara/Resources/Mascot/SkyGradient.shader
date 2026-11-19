// Inside-out sky dome: vertical gradient bottom -> horizon -> top (set from script per category).
Shader "Vistara/SkyGradient"
{
    Properties
    {
        _Top ("Top", Color) = (0.25, 0.5, 0.95, 1)
        _Horizon ("Horizon", Color) = (0.85, 0.93, 1, 1)
        _Bottom ("Bottom", Color) = (0.3, 0.35, 0.4, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Top;
                float4 _Horizon;
                float4 _Bottom;
            CBUFFER_END
            struct Attr { float4 pos : POSITION; };
            struct Vary { float4 cs : SV_POSITION; float y : TEXCOORD0; };
            Vary vert(Attr i)
            {
                Vary o;
                o.cs = TransformObjectToHClip(i.pos.xyz);
                o.y = normalize(i.pos.xyz).y;
                return o;
            }
            half4 frag(Vary i) : SV_Target
            {
                half up = saturate(i.y * 1.4h);
                half dn = saturate(-i.y * 2.0h);
                half3 c = lerp(_Horizon.rgb, _Top.rgb, pow(up, 0.7h));
                c = lerp(c, _Bottom.rgb, dn);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}

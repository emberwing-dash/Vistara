// Stara's face parts: unlit colour with a vertical gradient (glossy iris) or a soft radial fade (blush).
// _Mode 0 = flat, 1 = vertical gradient (_Color at the top, _Color2 at the bottom), 2 = soft radial blush (alpha fades to the rim).
// _BMin / _BSize hold the mesh bounds (object space) so the gradient fits every part; set from script.
Shader "Vistara/StaraFace"
{
    Properties
    {
        _Color ("Top colour", Color) = (0.23, 0.12, 0.52, 1)
        _Color2 ("Bottom colour", Color) = (0.72, 0.52, 1.0, 1)
        _Mode ("Mode (0 flat, 1 gradient, 2 blush)", Float) = 1
        _Alpha ("Blush strength", Range(0, 1)) = 0.75
        _BMin ("Bounds min", Vector) = (-0.05, -0.05, -0.05, 0)
        _BSize ("Bounds size", Vector) = (0.1, 0.1, 0.1, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Face"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Color2;
                float _Mode;
                float _Alpha;
                float4 _BMin;
                float4 _BSize;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; };
            struct Vary { float4 cs : SV_POSITION; float3 op : TEXCOORD0; };

            Vary vert(Attr i)
            {
                Vary o;
                o.cs = TransformObjectToHClip(i.pos.xyz);
                o.op = i.pos.xyz;
                return o;
            }

            half4 frag(Vary i) : SV_Target
            {
                float3 p = (i.op - _BMin.xyz) / max(_BSize.xyz, float3(1e-4, 1e-4, 1e-4));
                if (_Mode < 0.5) return half4(_Color.rgb, 1);
                if (_Mode < 1.5)
                {
                    half g = saturate(p.y);                                   // vertical gradient across the part
                    return half4(lerp(_Color2.rgb, _Color.rgb, smoothstep(0.0h, 1.0h, g)), 1);
                }
                float2 d = (p.xy - 0.5) * 2.0;                                // blush: soft radial falloff
                half a = saturate(1.0h - dot(d, d)) * _Alpha;
                return half4(_Color.rgb, a * a * 1.8h);
            }
            ENDHLSL
        }
    }
}

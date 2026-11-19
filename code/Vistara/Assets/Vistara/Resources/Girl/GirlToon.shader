// Vistara guide girl: textured two-tone cel shading. _Recolor=1 swaps the dark tee for _TeeColor and the lavender sleeves for _SleeveColor (per-category outfit).
Shader "Vistara/GirlToon"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Shade ("Shadow brightness", Range(0.4, 1)) = 0.82
        _Recolor ("Recolour tee (0/1)", Float) = 0
        _TeeColor ("Tee colour", Color) = (0.2, 0.2, 0.25, 1)
        _SleeveColor ("Sleeve colour", Color) = (0.7, 0.7, 0.9, 1)
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

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float _Shade;
                float _Recolor;
                float4 _TeeColor;
                float4 _SleeveColor;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; float3 nrm : NORMAL; float2 uv : TEXCOORD0; };
            struct Vary { float4 cs : SV_POSITION; float3 wn : TEXCOORD0; float2 uv : TEXCOORD1; };

            Vary vert(Attr i)
            {
                Vary o;
                o.cs = TransformObjectToHClip(i.pos.xyz);
                o.wn = TransformObjectToWorldNormal(i.nrm);
                o.uv = i.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return o;
            }

            half4 frag(Vary i) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Tint.rgb;
                if (_Recolor > 0.5)
                {
                    half lum = dot(c, half3(0.299h, 0.587h, 0.114h));
                    half dark = 1.0h - smoothstep(0.03h, 0.12h, lum);
                    half sleeve = saturate((c.b - c.r - 0.03h) * 8.0h) * step(0.25h, lum);
                    c = lerp(c, _TeeColor.rgb * (0.85h + lum * 0.6h), dark);
                    c = lerp(c, _SleeveColor.rgb * (0.7h + lum * 0.4h), sleeve);
                }
                Light l = GetMainLight();
                half ndl = dot(normalize(i.wn), l.direction);
                half cel = smoothstep(-0.12h, 0.05h, ndl);
                half3 shade = lerp(_Shade.xxx * half3(0.93h, 0.9h, 1.0h), half3(1, 1, 1), cel);
                return half4(c * shade, 1);
            }
            ENDHLSL
        }
    }
}

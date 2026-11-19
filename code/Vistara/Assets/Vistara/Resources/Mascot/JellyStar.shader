// Vistara jelly: a translucent gel material for the star mascot.
// Gradient body (light top -> saturated bottom), soft wrapped cel lighting, glowing rim, fake back-light scattering, a glossy highlight streak,
// a dark back-face pass for depth, and a vertex wobble so the star jiggles like jelly (amplitude set from script via _Wobble).
Shader "Vistara/JellyStar"
{
    Properties
    {
        _TopColor ("Top colour", Color) = (0.74, 0.84, 0.84, 1)
        _BottomColor ("Bottom colour", Color) = (0.16, 0.45, 0.82, 1)
        _RimColor ("Rim colour", Color) = (1.0, 0.98, 0.82, 1)
        _InnerColor ("Inner glow", Color) = (1.0, 0.78, 0.25, 1)
        _Opacity ("Opacity", Range(0.4, 1)) = 0.94
        _RimPower ("Rim power", Range(0.5, 6)) = 2.4
        _GradMin ("Gradient min height", Float) = 0.0
        _GradMax ("Gradient max height", Float) = 1.05
        _Steps ("Light steps", Range(1, 6)) = 3
        _Sparkle ("Sparkle", Range(0, 2)) = 0.8
        _Wobble ("Wobble", Range(0, 1)) = 0.35
        _WobbleSpeed ("Wobble speed", Float) = 3.0
        _WobbleFreq ("Wobble frequency", Float) = 5.0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _TopColor;
            float4 _BottomColor;
            float4 _RimColor;
            float4 _InnerColor;
            float _Opacity;
            float _RimPower;
            float _GradMin;
            float _GradMax;
            float _Steps;
            float _Sparkle;
            float _Wobble;
            float _WobbleSpeed;
            float _WobbleFreq;
        CBUFFER_END

        struct Attr { float4 pos : POSITION; float3 nrm : NORMAL; };
        struct Vary { float4 cs : SV_POSITION; float3 wp : TEXCOORD0; float3 wn : TEXCOORD1; float oy : TEXCOORD2; };

        Vary JellyVert(Attr i)
        {
            Vary o;
            float t = _Time.y * _WobbleSpeed;
            float w = sin(t + i.pos.y * _WobbleFreq + i.pos.x * 2.0) * cos(t * 0.7 + i.pos.z * 3.0);
            float3 p = i.pos.xyz + i.nrm * (w * _Wobble * 0.018);
            p.y += sin(t * 1.3) * _Wobble * 0.004;
            o.oy = p.y;
            o.wp = TransformObjectToWorld(p);
            o.wn = TransformObjectToWorldNormal(i.nrm);
            o.cs = TransformWorldToHClip(o.wp);
            return o;
        }
        ENDHLSL

        // back faces first: a darker, thinner inside that gives the gel its depth
        Pass
        {
            Name "JellyBack"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex JellyVert
            #pragma fragment frag
            half4 frag(Vary i) : SV_Target
            {
                half3 c = _BottomColor.rgb * 0.62h;
                return half4(c, 0.55h * _Opacity);
            }
            ENDHLSL
        }

        Pass
        {
            Name "JellyFront"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex JellyVert
            #pragma fragment frag
            half4 frag(Vary i) : SV_Target
            {
                half3 N = normalize(i.wn);
                half3 V = normalize(_WorldSpaceCameraPos - i.wp);
                Light l = GetMainLight();
                half ndl = dot(N, l.direction);
                half wrap = saturate(ndl * 0.5h + 0.5h);                           // soft wrapped light
                half stepped = floor(wrap * _Steps + 0.5h) / max(_Steps, 1.0h);
                half lit = lerp(wrap, stepped, 0.55h);

                half g = saturate((i.oy - _GradMin) / max(_GradMax - _GradMin, 0.001h));
                half3 baseCol = lerp(_BottomColor.rgb, _TopColor.rgb, smoothstep(0.08h, 0.92h, g));

                half fres = pow(saturate(1.0h - dot(N, V)), _RimPower);            // glowing edge
                half back = pow(saturate(dot(V, -(l.direction + N * 0.45h))), 2.0h); // light shining through the jelly
                half3 col = baseCol * (0.62h + 0.5h * lit)
                          + _InnerColor.rgb * back * 0.45h
                          + _RimColor.rgb * fres * 0.85h
                          + _InnerColor.rgb * (1.0h - fres) * 0.10h;

                half3 H = normalize(l.direction + V);
                half spec = smoothstep(0.55h, 0.62h, pow(saturate(dot(N, H)), 56.0h)); // glossy streak
                half3 H2 = normalize(half3(-0.45h, 0.85h, 0.5h) + V);
                half spec2 = smoothstep(0.955h, 0.972h, dot(N, H2));                // fixed top-left sparkle
                col = lerp(col, half3(1, 1, 1), saturate(spec * 0.85h + spec2 * 0.9h));
                half sp = frac(sin(dot(floor(i.wp * 140.0 + floor(_Time.y * 3.0)), float3(12.9898, 78.233, 37.719))) * 43758.5453);   // tiny glitter
                col += step(0.992h, sp) * saturate(dot(N, H) * 1.2h + 0.2h) * _RimColor.rgb * _Sparkle;

                half a = saturate(_Opacity + fres * (1.0h - _Opacity) * 0.8h + (spec + spec2));
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}

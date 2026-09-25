// One shader for every 3D-overhaul mesh (animals, props, terrain). Albedo comes from a 16x1
// palette strip — each face's UVs point at one texel (see Tools/blender/*_gen.py) — so a mesh
// needs one material and recolouring is a texture swap (AnimalPalette for jerseys).
//
// The chosen art direction is toon + outline (picked in the Phase 0 look-dev, 2026-09):
// two flat tones split at the light terminator, a tinted shadow side, a rim highlight, and an
// inverted-hull outline pass. _OutlineWidth 0 disables the outline (terrain, thin court lines).
Shader "Volleyball/Stylized"
{
    Properties
    {
        [NoScaleOffset] _PaletteTex ("Palette", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.13, 0.09, 0.11, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.45, 0.35, 0.9, 1)
        _ShadowLift ("Shadow Lift", Range(0, 1)) = 0
        _GlowColor ("Glow (rgb colour, a strength)", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _OutlineColor;
            float4 _ShadowTint;
            float _OutlineWidth;
            float _ShadowLift;
            float4 _GlowColor;
        CBUFFER_END

        TEXTURE2D(_PaletteTex);
        SAMPLER(sampler_PaletteTex);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_PaletteTex, sampler_PaletteTex, i.uv).rgb * _BaseColor.rgb;
                float3 n = normalize(i.normalWS);

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float ndl = dot(n, light.direction);
                float shadow = light.shadowAttenuation * light.distanceAttenuation;
                half3 ambient = SampleSH(n);

                // two flat tones split at the terminator, coloured shadow side, rim light
                float d = step(0.02, ndl) * step(0.5, shadow);
                half3 shade = albedo * lerp(half3(1, 1, 1), _ShadowTint.rgb, 0.55) * (ambient * 0.75 + 0.12);
                half3 lit = albedo * (ambient * 0.25 + light.color * 0.85);
                shade = lerp(shade, lit, _ShadowLift * 0.5); // characters keep their colour in shade
                half3 col = lerp(shade, lit, d);
                float rim = step(0.72, 1.0 - saturate(dot(n, v))) * step(0.0, ndl);
                col += rim * 0.35 * light.color * albedo;
                col += _GlowColor.rgb * _GlowColor.a; // power-up glow (PowerUpGlow via CharacterView)

                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fogFactor : TEXCOORD0;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
                float4 cs = TransformWorldToHClip(positionWS);
                float3 ncs = mul((float3x3)UNITY_MATRIX_VP, normalWS);
                float2 dir = normalize(ncs.xy + 1e-5);
                dir.x *= _ScreenParams.y / _ScreenParams.x;
                // width in ~pixels at 1080p, independent of distance
                cs.xy += dir * (_OutlineWidth * 0.0099) * cs.w;
                o.positionCS = cs;
                o.fogFactor = ComputeFogFactor(cs.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (_OutlineWidth <= 0.0001) discard;
                return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1);
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 vert(Attributes i) : SV_POSITION
            {
                float3 p = TransformObjectToWorld(i.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - p);
            #else
                float3 ld = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(p, n, ld));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return cs;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}

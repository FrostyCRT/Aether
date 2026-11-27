// AJOUTÉ (2026-11-23, Couteaux de Lyra v2 - retour utilisateur : « on ne voit pas assez les reliefs [...] le plus beau
// graphiquement ») - métal « peint » au lieu du métal URP standard (qui rendait un gris uniforme vu du dessus) :
//  - facettes nettes (normales par face du modèle) en 3 tons doux selon la lumière : l'arête et les biseaux ressortent ;
//  - reflet façon chrome peint : ce que la facette « reflète » vient d'un ciel clair (haut) ou d'un sol chaud (bas), avec
//    une ligne d'horizon adoucie -> lecture immédiate du métal poli ;
//  - reflet spéculaire du soleil + éclat lumineux qui glisse périodiquement le long de la lame (_Glint) ;
//  - 2e passe : fin liseré sombre autour de la silhouette (trait d'encre des illustrations du jeu).
Shader "Aether/PaintedMetal"
{
    Properties
    {
        _BaseColor("Teinte du métal", Color) = (0.85, 0.88, 0.93, 1)
        _SkyColor("Reflet du ciel", Color) = (0.93, 0.97, 1, 1)
        _GroundColor("Reflet du sol", Color) = (0.42, 0.42, 0.45, 1)
        _ShadowColor("Côté à l'ombre", Color) = (0.38, 0.42, 0.5, 1)
        _Reflect("Part du reflet", Range(0, 1)) = 0.55
        _SpecPower("Netteté du reflet du soleil", Range(4, 300)) = 60
        _SpecStrength("Force du reflet du soleil", Range(0, 2)) = 0.8
        _Glint("Éclat qui glisse (0 = aucun)", Range(0, 2)) = 0
        _GlintSpeed("Vitesse de l'éclat", Float) = 0.9
        _GlintRange("Étendue de l'éclat (repère objet, avant)", Vector) = (-0.65, 0.05, 0, 0)
        _OutlineColor("Liseré", Color) = (0.12, 0.14, 0.2, 1)
        _OutlineWidth("Épaisseur du liseré", Range(0, 0.02)) = 0.006
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _SkyColor, _GroundColor, _ShadowColor, _OutlineColor;
            half _Reflect, _SpecPower, _SpecStrength, _Glint, _GlintSpeed, _OutlineWidth;
            float4 _GlintRange;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardPaintedMetal"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 positionOS : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.positionOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light sun = GetMainLight();
                half ndl = dot(n, sun.direction) * 0.5h + 0.5h;
                // 3 tons doux (peinture) : ombre / moyen / lumière
                half tone = smoothstep(0.3h, 0.45h, ndl) * 0.5h + smoothstep(0.62h, 0.75h, ndl) * 0.5h;
                half3 diffuse = lerp(_ShadowColor.rgb, _BaseColor.rgb, tone);
                // reflet chrome peint : ciel / sol séparés par une ligne d'horizon adoucie
                float3 r = reflect(-vdir, n);
                half sky = smoothstep(-0.05h, 0.12h, r.y);
                half3 env = lerp(_GroundColor.rgb, _SkyColor.rgb, sky);
                half3 col = lerp(diffuse, env * (0.75h + 0.25h * tone), _Reflect);
                // soleil
                float3 h = normalize(sun.direction + vdir);
                half spec = pow(saturate(dot(n, h)), _SpecPower) * _SpecStrength;
                // éclat qui glisse le long de la lame (de la garde vers la pointe, -Z local = avant)
                half glint = 0;
                if (_Glint > 0.001h)
                {
                    float ph = frac(_Time.y * _GlintSpeed);
                    float zc = lerp(_GlintRange.y, _GlintRange.x, saturate(ph * 1.6));      // pause entre deux passages
                    glint = (1.0h - smoothstep(0.0h, 0.06h, abs(i.positionOS.z - zc))) * step(ph, 0.625) * _Glint;
                }
                col += (spec + glint) * half3(1, 1, 1);
                col *= lerp(half3(1, 1, 1), sun.color, 0.35h);
                return half4(min(col, 1.6h), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                // normale ADOUCIE rangée dans la couleur des sommets (Blender, repère converti) : le liseré ne se fend pas aux
                // arêtes vives de la lame (les normales par face, elles, s'écartent et laissent des trous)
                float3 d = v.color.rgb * 2.0 - 1.0;
                float3 nOut = float3(-d.x, d.z, -d.y);
                if (dot(nOut, nOut) < 0.01) nOut = v.normalOS;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz + normalize(nOut) * _OutlineWidth);
                return o;
            }
            half4 frag(V i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        // ombre portée au sol (indice d'altitude : le couteau vole)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v);
                float3 pws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pws, nws, _LightDirection));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                return o;
            }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}

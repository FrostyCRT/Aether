// AJOUTÉ (2026-11-23, Orbe rebondissant - lot 1 des modèles Blender) - cristal « peint » fidèle à l'illustration de la carte :
//  - facettes nettes (normales par face de SM_CrystalOrb), chacune avec sa nuance bleu glacier / lavande (identité de facette
//    rangée dans l'alpha de la couleur des sommets) et 3 tons de lumière -> le volume taillé se lit tout de suite ;
//  - reflets irisés arc-en-ciel sur une partie des facettes, qui glissent quand l'orbe tourne ;
//  - arêtes des facettes en fins traits blancs (barycentriques rangées dans le RVB de la couleur des sommets, épaisseur en
//    pixels écran via fwidth : nettes à toutes les distances) ;
//  - liseré clair sur le pourtour (Fresnel), translucide : le cristal intérieur lumineux (_Mode 1) se voit au travers ;
//  - passe d'ombre portée (l'orbe flotte au-dessus du sol).
Shader "Aether/PaintedCrystal"
{
    Properties
    {
        _ColorA("Nuance 1 (bleu glacier)", Color) = (0.55, 0.76, 0.96, 1)
        _ColorB("Nuance 2 (lavande)", Color) = (0.7, 0.64, 0.96, 1)
        _DeepColor("Facettes à l'ombre", Color) = (0.2, 0.3, 0.6, 1)
        _SkyColor("Reflet du ciel", Color) = (0.92, 0.97, 1, 1)
        _EdgeColor("Arêtes", Color) = (1, 1, 1, 1)
        _EdgePixels("Épaisseur des arêtes (pixels)", Range(0, 4)) = 1.4
        _EdgeOpacity("Opacité des arêtes", Range(0, 1)) = 0.85
        _Iridescence("Reflets irisés", Range(0, 1)) = 0.55
        _IriShare("Part des facettes irisées", Range(0, 1)) = 0.35
        _Alpha("Opacité des facettes", Range(0, 1)) = 0.72
        _RimColor("Liseré du pourtour", Color) = (0.9, 0.96, 1, 1)
        _Glow("Intensité lumineuse", Range(0.3, 4)) = 1.1
        _Mode("Mode (0 enveloppe, 1 cœur)", Float) = 0
        _CoreWhite("Cœur : part de blanc", Range(0, 1)) = 1
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _ColorA, _ColorB, _DeepColor, _SkyColor, _EdgeColor, _RimColor;
            half _EdgePixels, _EdgeOpacity, _Iridescence, _IriShare, _Alpha, _Glow, _Mode, _ZWrite, _CoreWhite;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardCrystal"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 bary : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.bary = v.color;
                return o;
            }
            half3 Hue(half h) { return saturate(abs(frac(h + half3(0, 0.6667h, 0.3333h)) * 6 - 3) - 1); }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half ndv = saturate(dot(n, vdir));
                half fid = i.bary.a;
                Light sun = GetMainLight();
                half ndl = dot(n, sun.direction) * 0.5h + 0.5h;
                half tone = smoothstep(0.3h, 0.45h, ndl) * 0.5h + smoothstep(0.62h, 0.78h, ndl) * 0.5h;
                half3 baseC = lerp(_ColorA.rgb, _ColorB.rgb, fid);
                half3 col = lerp(_DeepColor.rgb, baseC, 0.35h + 0.65h * tone);
                // reflet du ciel sur les facettes tournées vers le haut
                float3 r = reflect(-vdir, n);
                col = lerp(col, _SkyColor.rgb, smoothstep(0.55h, 0.95h, r.y) * 0.45h);
                // irisation : une partie des facettes, teinte qui dépend de l'angle de vue (glisse quand l'orbe tourne)
                half iriMask = step(1.0h - _IriShare, frac(fid * 7.13h));
                half3 rainbow = lerp(half3(1, 1, 1), Hue(frac(fid * 3.1h + ndv * 1.6h)), 0.55h);
                col = lerp(col, col * 0.5h + rainbow * 0.6h, iriMask * _Iridescence);
                // soleil
                float3 h = normalize(sun.direction + vdir);
                col += pow(saturate(dot(n, h)), 80.0h) * 0.9h;
                // arêtes blanches (épaisseur constante à l'écran)
                float e = min(i.bary.r, min(i.bary.g, i.bary.b));
                float w = fwidth(e) * _EdgePixels;
                half edge = 1.0h - smoothstep(w * 0.5, w * 1.5 + 1e-5, e);
                // pourtour
                half rim = pow(1.0h - ndv, 3.0h);
                col = lerp(col, _RimColor.rgb, rim * 0.6h);
                half alpha;
                if (_Mode > 0.5h)
                {
                    // cœur : lumineux, plus clair au centre
                    col = lerp(baseC, half3(0.95h, 0.98h, 1.0h), (0.45h + 0.35h * ndv) * _CoreWhite) * 1.25h;
                    col = lerp(col, _EdgeColor.rgb, edge * 0.35h);
                    alpha = 0.9h;
                }
                else
                {
                    col = lerp(col, _EdgeColor.rgb, edge * _EdgeOpacity);
                    alpha = saturate(_Alpha + rim * 0.35h + edge * _EdgeOpacity * 0.5h);
                }
                return half4(col * _Glow, alpha);
            }
            ENDHLSL
        }

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

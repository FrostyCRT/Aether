// AJOUTÉ (2026-11-26, Lotus de la Source - auto-critique en jeu : les pétales faisaient « clipart », un aplat rose identique
// sur 30 pétales) - pétale PEINT, mat (aucune lumière propre, comme le Cerf) :
//  - dégradé base -> pointe (_ColorA -> _ColorB) selon t (couleur des sommets R) ;
//  - nervures fines parallèles + nervure centrale (position en travers, couleur des sommets G), qui s'estompent vers la
//    pointe et les bords ;
//  - ombre de contact à la base (les pétales s'emboîtent) ; bords plus clairs, comme de la soie qui laisse passer le jour ;
//  - légère variation de teinte et de valeur d'un pétale à l'autre (identité du pétale, couleur des sommets B) ;
//  - 3 tons de lumière à ombre rosée, liseré sombre fin couleur prune, ombre portée.
Shader "Aether/LotusPetal"
{
    Properties
    {
        _ColorA("Base du pétale", Color) = (0.99, 0.93, 0.9, 1)
        _ColorB("Pointe du pétale", Color) = (0.86, 0.42, 0.6, 1)
        _ColorVar("Teinte de variation", Color) = (0.95, 0.55, 0.5, 1)
        _Variation("Variation entre pétales", Range(0, 0.5)) = 0.18
        _GradStart("Début du dégradé", Range(0, 1)) = 0.12
        _ShadowTint("Ombre (multiplie)", Color) = (0.74, 0.62, 0.74, 1)
        _VeinColor("Nervures (multiplie)", Color) = (0.86, 0.72, 0.8, 1)
        _VeinCount("Nombre de nervures", Range(2, 20)) = 9
        _VeinSharp("Finesse des nervures", Range(0.02, 0.4)) = 0.12
        _BaseShade("Ombre de contact à la base", Range(0, 1)) = 0.55
        _EdgeLight("Bords clairs", Range(0, 1)) = 0.35
        _CorruptColor("Corruption : teinte", Color) = (0.12, 0.07, 0.24, 1)
        _CorruptVein("Corruption : nervures (multiplie)", Color) = (2.2, 1.3, 2.8, 1)
        _OutlineColor("Liseré", Color) = (0.42, 0.2, 0.32, 1)
        _OutlineWidth("Épaisseur du liseré", Range(0, 0.05)) = 0.006
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _ColorA, _ColorB, _ColorVar, _ShadowTint, _VeinColor, _OutlineColor, _CorruptColor, _CorruptVein;
            half _Variation, _GradStart, _VeinCount, _VeinSharp, _BaseShade, _EdgeLight, _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLotus"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half4 color : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = TransformObjectToWorldNormal(v.normalOS); o.color = v.color;
                return o;
            }
            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS); if (!front) n = -n;
                Light sun = GetMainLight();
                half t = saturate(i.color.r); half s = i.color.g * 2.0h - 1.0h; half pid = i.color.b;
                // dégradé + variation d'un pétale à l'autre
                half g = pow(saturate((t - _GradStart) / (1.0h - _GradStart)), 0.85h);
                half3 col = lerp(_ColorA.rgb, _ColorB.rgb, g);
                col = lerp(col, col * _ColorVar.rgb * 1.15h, (pid - 0.5h) * 2.0h * _Variation);
                col *= 1.0h + (frac(pid * 7.31h) - 0.5h) * _Variation * 0.5h;
                // nervures : lignes parallèles (en travers) + nervure centrale, estompées vers la pointe et les bords
                half sv = abs(frac(s * _VeinCount * 0.5h + 0.5h) - 0.5h) * 2.0h;
                half vein = (1.0h - smoothstep(0.0h, _VeinSharp, sv)) * (1.0h - smoothstep(0.55h, 0.95h, t)) * (1.0h - smoothstep(0.7h, 1.0h, abs(s)));
                half mid = (1.0h - smoothstep(0.0h, 0.06h, abs(s))) * (1.0h - smoothstep(0.7h, 1.0h, t));
                // corruption (couleur des sommets A) : la teinte sombre gagne depuis la pointe, les nervures virent au violet
                half cm = saturate(i.color.a * (0.35h + 0.65h * t) * 1.15h);
                col = lerp(col, _CorruptColor.rgb, cm * 0.9h);
                half veinAmt = saturate(vein * 0.6h + mid * 0.8h) * (1.0h + cm * 0.6h);
                col *= lerp(half3(1, 1, 1), lerp(_VeinColor.rgb, _CorruptVein.rgb, cm), saturate(veinAmt));
                // bords clairs (soie) et ombre de contact à la base
                col = lerp(col, lerp(col, half3(1, 0.97, 0.96), 0.6h), smoothstep(0.72h, 1.0h, abs(s)) * _EdgeLight);
                col *= lerp(1.0h - _BaseShade, 1.0h, smoothstep(0.0h, 0.3h, t));
                // lumière en 3 tons, ombre rosée
                half ndl = dot(n, sun.direction) * 0.5h + 0.5h;
                half tone = smoothstep(0.28h, 0.42h, ndl) * 0.5h + smoothstep(0.6h, 0.74h, ndl) * 0.5h;
                col *= lerp(_ShadowTint.rgb, half3(1, 1, 1), tone);
                col *= lerp(half3(1, 1, 1), sun.color, 0.3h);
                return half4(col, 1);
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
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz + normalize(v.normalOS) * _OutlineWidth); return o; }
            half4 frag(V i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ColorMask 0 Cull Off
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
                float4 cs = TransformWorldToHClip(ApplyShadowBias(TransformObjectToWorld(v.positionOS.xyz), TransformObjectToWorldNormal(v.normalOS), _LightDirection));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs; return o;
            }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}

// AJOUTÉ (2026-11-22, Boule de feu v3 - retour utilisateur : « c'est trop cartoon [...] les flammes se rentrent dedans entre
// elles [...] on arrive à distinguer l'orbe des flammes [...] chaque détail doit être parfait ») - feu peint « fondu » :
//  - 3 enveloppes de comète CONCENTRIQUES (SM_FireballFlame : cœur, flamme, voile), avant = -Z local, traîne vers +Z : une
//    seule forme continue, aucune pièce séparée, donc ni orbe distinct ni flammes qui se traversent ;
//  - translucide en prémultiplié (Blend One OneMinusSrcAlpha) : les couches se fondent en lumière au lieu de se couper ;
//  - dégradé continu blanc chaud -> jaune -> orange -> rouge profond, bords qui s'estompent (pas d'aplats ni de trait) ;
//  - langues de flamme dessinées par un bruit tourbillonnant (domain warp), étiré dans le sens du vol, qui coule vers
//    l'arrière ; contours qui ondulent légèrement ; aucun hasard : même aspect à chaque tir.
Shader "Aether/SoftFire"
{
    Properties
    {
        _WhiteColor("Cœur (blanc chaud)", Color) = (1, 0.96, 0.84, 1)
        _YellowColor("Jaune", Color) = (1, 0.8, 0.32, 1)
        _OrangeColor("Orange", Color) = (1, 0.46, 0.08, 1)
        _RedColor("Rouge profond", Color) = (0.72, 0.1, 0.03, 1)
        _HeatBase("Chaleur de l'enveloppe", Range(0, 2)) = 1
        _TailLength("Longueur de la traîne (repère objet)", Float) = 1.3
        _TailFade("Refroidissement le long de la traîne", Range(0, 3)) = 1
        _RimFade("Estompe des bords", Range(0, 3)) = 1
        _NoiseScale("Taille des flammes", Float) = 3
        _NoiseAmp("Force des flammes", Range(0, 3)) = 1
        _Warp("Tourbillon des flammes", Range(0, 2)) = 0.6
        _FlowSpeed("Écoulement vers l'arrière", Float) = 1.8
        _Wobble("Ondulation des contours", Range(0, 0.3)) = 0.04
        _Cut("Seuil de disparition", Range(0, 1)) = 0.25
        _Soft("Douceur des bords de flamme", Range(0.01, 1)) = 0.25
        _Opacity("Opacité", Range(0, 1)) = 1
        _CenterSeeThrough("Opacité au centre de la tête", Range(0, 1)) = 1
        _Glow("Intensité lumineuse", Range(0.3, 4)) = 1.1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardSoftFire"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WhiteColor, _YellowColor, _OrangeColor, _RedColor;
                float _HeatBase, _TailLength, _TailFade, _RimFade, _NoiseScale, _NoiseAmp, _Warp, _FlowSpeed, _Wobble;
                float _Cut, _Soft, _Opacity, _Glow, _CenterSeeThrough;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float hash31(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float vnoise3(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash31(i), hash31(i + float3(1, 0, 0)), f.x), lerp(hash31(i + float3(0, 1, 0)), hash31(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash31(i + float3(0, 0, 1)), hash31(i + float3(1, 0, 1)), f.x), lerp(hash31(i + float3(0, 1, 1)), hash31(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }
            float fbm3(float3 p)
            {
                float s = 0, a = 0.5;
                [unroll] for (int k = 0; k < 4; k++) { s += vnoise3(p) * a; p = p * 2.07 + 13.1; a *= 0.5; }
                return s / 0.9375;
            }
            float Tail(float3 p) { return saturate(p.z / max(_TailLength, 1e-3)); }
            // bruit étiré dans le sens du vol, qui coule vers l'arrière, avec un tourbillon (domain warp) pour des langues courbes
            float Flame(float3 p)
            {
                float3 q = p * float3(_NoiseScale, _NoiseScale, _NoiseScale * 0.7) - float3(0, 0, _Time.y * _FlowSpeed);
                float3 w = float3(fbm3(q + float3(1.7, 9.2, 0)), fbm3(q + float3(8.3, 2.8, 0)), 0) - 0.5;
                return fbm3(q + w * _Warp * 2.0);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 p = v.positionOS.xyz;
                float t = Tail(p);
                float w = vnoise3(p * float3(2.5, 2.5, 1.0) - float3(0, 0, _Time.y * _FlowSpeed)) - 0.5;
                p += v.normalOS * w * _Wobble * (0.3 + t * 1.7);
                VertexPositionInputs vp = GetVertexPositionInputs(p);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.positionOS = p;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half3 Ramp(float h)   // 0 = rouge profond (bord froid) ... 1 = blanc chaud
            {
                half3 c = lerp(_RedColor.rgb, _OrangeColor.rgb, smoothstep(0.0, 0.35, h));
                c = lerp(c, _YellowColor.rgb, smoothstep(0.35, 0.7, h));
                return lerp(c, _WhiteColor.rgb, smoothstep(0.7, 1.0, h));
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float ndv = saturate(dot(n, normalize(GetWorldSpaceViewDir(i.positionWS))));
                float t = Tail(i.positionOS);
                float f = Flame(i.positionOS) - 0.5;
                // chaleur : plus froide vers l'arrière et sur les bords vus de profil, flammes plus marquées vers la traîne
                float heat = _HeatBase - pow(t, 1.4) * _TailFade - pow(1.0 - ndv, 2.0) * _RimFade + f * _NoiseAmp * (0.3 + t * 0.9 + (1.0 - ndv) * 0.9);   // flammèches aussi sur le pourtour de la tête
                float a = smoothstep(_Cut, _Cut + _Soft, heat) * _Opacity;
                // voile plus transparent au centre de la tête (vu de face) : les couches plus chaudes transparaissent -> profondeur
                a *= lerp(1.0, _CenterSeeThrough, ndv * ndv * (1.0 - t));
                half3 col = Ramp(saturate((heat - _Cut) / max(1.0 - _Cut, 1e-3)));
                return half4(col * _Glow * a, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

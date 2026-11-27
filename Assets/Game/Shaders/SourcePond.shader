// AJOUTÉ (2026-11-26, Lotus de la Source - auto-critique en jeu : la fleur était posée sur l'herbe sans ancrage) - le
// BASSIN DE LA SOURCE sous le lotus. Eau peinte, mate (aucune émission : pas de « lueur magique »), sur un quad à plat :
//  - dégradé bleu profond (centre, sous la fleur) -> turquoise (bord), opacité qui s'efface sur une rive irrégulière ;
//  - liseré de rive clair (écume douce) ; ondes concentriques qui partent du cœur ; petits reflets qui scintillent.
// Coordonnées : uv du quad (0..1), centre en 0,5.
Shader "Aether/SourcePond"
{
    Properties
    {
        _DeepColor("Eau profonde (centre)", Color) = (0.08, 0.26, 0.4, 1)
        _ShallowColor("Eau claire (bord)", Color) = (0.32, 0.66, 0.74, 1)
        _FoamColor("Rive", Color) = (0.86, 0.96, 0.98, 1)
        _RippleColor("Ondes", Color) = (0.55, 0.85, 0.92, 1)
        _Alpha("Opacité", Range(0, 1)) = 0.88
        _Radius("Rayon de l'eau (fraction)", Range(0.2, 0.5)) = 0.46
        _Wobble("Irrégularité de la rive", Range(0, 0.08)) = 0.025
        _FoamWidth("Largeur de la rive", Range(0.002, 0.05)) = 0.012
        _RippleCount("Nombre d'ondes", Range(2, 30)) = 9
        _RippleSpeed("Vitesse des ondes", Range(0, 2)) = 0.18
        _RippleStrength("Force des ondes", Range(0, 1)) = 0.35
        _Sparkle("Reflets qui scintillent", Range(0, 1)) = 0.5
        _RipplePhase("Phase des ondes (pilotée par le code)", Float) = 0
        _CorruptDir("Corruption : direction (uv)", Vector) = (-0.8, -0.6, 0, 0)
        _CorruptColor("Corruption : encre", Color) = (0.1, 0.06, 0.2, 1)
        _CorruptStrength("Corruption : force", Range(0, 1)) = 0.85
        _ZoneMin("Zone de jeu : min (x, z monde)", Vector) = (-100000, -100000, 0, 0)
        _ZoneMax("Zone de jeu : max (x, z monde)", Vector) = (100000, 100000, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Pond"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor, _FoamColor, _RippleColor;
                half _Alpha, _Radius, _Wobble, _FoamWidth, _RippleCount, _RippleSpeed, _RippleStrength, _Sparkle, _RipplePhase, _CorruptStrength;
                float4 _CorruptDir; half4 _CorruptColor; float4 _ZoneMin, _ZoneMax;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; o.positionWS = TransformObjectToWorld(v.positionOS.xyz); return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            half4 frag(V i) : SV_Target
            {
                float2 d = i.uv - 0.5; float r = length(d); float a = atan2(d.y, d.x);
                // rive irrégulière (somme de sinus sur l'angle, lentement animée)
                float edge = _Radius + _Wobble * (sin(a * 5.0 + 1.3) * 0.6 + sin(a * 11.0 + _Time.y * 0.3) * 0.3 + sin(a * 23.0) * 0.15);
                float inside = 1.0 - smoothstep(edge - 0.004, edge + 0.004, r);
                // l'eau s'arrête aux murs de l'arène (le décor est au-delà) : coupe nette + rive le long du mur
                float wall = min(min(i.positionWS.x - _ZoneMin.x, _ZoneMax.x - i.positionWS.x), min(i.positionWS.z - _ZoneMin.y, _ZoneMax.y - i.positionWS.z));
                inside *= step(0.0, wall);
                if (inside <= 0.001) discard;
                float k = saturate(r / edge);
                half3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, smoothstep(0.25, 1.0, k));
                // ondes concentriques qui s'éloignent du cœur, plus marquées loin du centre
                float rip = sin((k * _RippleCount - (_Time.y * _RippleSpeed + _RipplePhase) * _RippleCount) * 6.2831);   // _RipplePhase : accélère / inverse les ondes (charge, implosion)
                rip = smoothstep(0.75, 1.0, rip) * smoothstep(0.3, 0.7, k) * (1.0 - smoothstep(0.85, 0.98, k));
                col = lerp(col, _RippleColor.rgb, rip * _RippleStrength);
                // petits reflets qui scintillent (cellules aléatoires, clignotement lent)
                float2 cell = floor(i.uv * 90.0); float h = hash(cell);
                float2 f = frac(i.uv * 90.0) - 0.5;
                float tw = 0.5 + 0.5 * sin(_Time.y * (1.0 + h * 2.0) + h * 40.0);
                float spark = step(0.93, h) * (1.0 - smoothstep(0.05, 0.18, length(f))) * tw * smoothstep(0.35, 0.6, k);
                col = lerp(col, half3(1, 1, 1), spark * _Sparkle);
                // encre de corruption : filaments sombres qui s'étendent dans l'eau du côté corrompu
                float2 cd = normalize(_CorruptDir.xy + 1e-5); float side = dot(d / max(r, 1e-4), cd);
                float fil = 0.5 + 0.5 * sin(a * 31.0 + sin(k * 9.0 + _Time.y * 0.4) * 2.5) * sin(a * 13.0 - k * 6.0);
                float ink = smoothstep(0.05, 0.75, side) * smoothstep(0.45, 0.75, k) * (0.45 + 0.55 * fil) * (1.0 - smoothstep(0.97, 1.0, k));   // visible au-delà des pétales, jusqu'à la rive
                col = lerp(col, _CorruptColor.rgb, saturate(ink) * _CorruptStrength);
                // rive claire juste à l'intérieur du bord
                float foam = 1.0 - smoothstep(0.0, _FoamWidth, abs(r - (edge - _FoamWidth * 1.2)));
                col = lerp(col, _FoamColor.rgb, foam * 0.85);
                float wallFoam = 1.0 - smoothstep(0.0, 0.35, wall);
                col = lerp(col, _FoamColor.rgb, wallFoam * 0.85);
                Light sun = GetMainLight(); col *= lerp(half3(1, 1, 1), sun.color, 0.3);
                return half4(col, _Alpha * inside * lerp(0.92, 1.0, foam));
            }
            ENDHLSL
        }
    }
    FallBack Off
}

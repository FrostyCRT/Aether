// AJOUTÉ (2026-11-22, lot 1 des modèles Blender, retour utilisateur : « le tir de base n'est pas un éclat, c'est un
// projectile d'eau [...] c'est de l'eau donc pas de lueur magique ») - eau stylisée « peinte », fidèle à l'icône du
// Double Tir : bleu profond sur les bords, plus clair et plus transparent au centre (effet de Fresnel), reflet du soleil
// net et un second reflet « peint » fixé à l'écran (en haut à gauche), comme les éclats blancs de l'illustration.
// AUCUNE émission : la couleur ne dépasse jamais l'éclairage, donc le bloom ne fait pas briller l'eau comme de la magie.
// Transparent, une seule passe, pas d'ombre projetée (petit objet qui file). Sert à la goutte, au tourbillon intérieur,
// aux bulles et aux gouttelettes (couleurs différentes par matériau).
Shader "Aether/StylizedWater"
{
    Properties
    {
        _ShallowColor("Centre (eau peu épaisse)", Color) = (0.48, 0.78, 1.0, 1)
        _DeepColor("Bords (eau épaisse)", Color) = (0.06, 0.27, 0.78, 1)
        _AlphaCenter("Opacité au centre", Range(0, 1)) = 0.55
        _AlphaRim("Opacité sur les bords", Range(0, 1)) = 0.92
        _RimPower("Largeur du bord", Range(0.5, 6)) = 1.8
        _SpecColor2("Couleur des reflets", Color) = (1, 1, 1, 1)
        _SpecPower("Netteté du reflet du soleil", Range(4, 400)) = 90
        _SpecStrength("Force du reflet du soleil", Range(0, 2)) = 1.0
        _PaintedDir("Direction du reflet peint (vue)", Vector) = (-0.45, 0.6, 0.66, 0)
        _PaintedPower("Netteté du reflet peint", Range(4, 400)) = 140
        _PaintedStrength("Force du reflet peint", Range(0, 2)) = 0.9
        _LightInfluence("Part de l'éclairage de la scène", Range(0, 1)) = 0.55
        _OutlineColor("Contour (trait d'encre)", Color) = (0.03, 0.12, 0.42, 1)
        _OutlineStart("Début du contour", Range(0.5, 1)) = 0.78
        _Shading("Ombre du côté opposé à la lumière", Range(0, 1)) = 0.45
        _SwirlColor("Tourbillon : couleur", Color) = (0.62, 0.88, 1, 1)
        _SwirlStrength("Tourbillon : force (0 = aucun)", Range(0, 1)) = 0
        _SwirlFreq("Tourbillon : nombre de cercles", Range(4, 80)) = 34
        _SwirlRadius("Tourbillon : rayon (fraction de la taille)", Range(0.05, 1)) = 0.32
        _SwirlSpeed("Tourbillon : vitesse", Range(-20, 20)) = 7
        _SwirlCenterOS("Tourbillon : centre (repère objet)", Vector) = (0, 0, 0.17, 0)
        _RimHighlight("Croissant de reflet sur le bord", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SpecColor2;
                half _AlphaCenter, _AlphaRim, _RimPower, _SpecPower, _SpecStrength, _PaintedPower, _PaintedStrength, _LightInfluence, _OutlineStart, _Shading;
                half4 _SwirlColor;
                half _SwirlStrength, _SwirlFreq, _SwirlRadius, _SwirlSpeed, _RimHighlight;
                float4 _SwirlCenterOS;
                half4 _OutlineColor;
                float4 _PaintedDir;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half ndv = saturate(dot(n, v));
                half fres = pow(1.0h - ndv, _RimPower);

                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, fres);

                Light sun = GetMainLight();
                half ndl = saturate(dot(n, sun.direction) * 0.5h + 0.5h);          // éclairage enveloppant, doux
                half3 lit = lerp(half3(1, 1, 1), sun.color * (0.55h + 0.45h * ndl), _LightInfluence);
                col *= lit;
                // volume (retour utilisateur : « on a l'impression que c'est de la 2D ») : côté opposé à la lumière nettement
                // plus sombre, en dégradé doux (deux tons, comme une illustration peinte)
                half shade = smoothstep(0.15h, 0.75h, ndl);
                col *= lerp(1.0h - _Shading, 1.0h, shade);

                // tourbillon de l'illustration (Double Tir) : cercles en spirale plus clairs, centrés sur le corps rond de la goutte
                // et toujours face à la caméra (comme peints sur l'image), qui tournent avec le temps
                if (_SwirlStrength > 0.001h)
                {
                    float3 centerWS = TransformObjectToWorld(_SwirlCenterOS.xyz);
                    float3 objX = float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20);
                    float sizeWS = max(length(objX), 1e-4);
                    float3 d = i.positionWS - centerWS;
                    float3 camRight = UNITY_MATRIX_V[0].xyz, camUp = UNITY_MATRIX_V[1].xyz;
                    float2 p = float2(dot(d, camRight), dot(d, camUp)) / sizeWS;
                    float r = length(p) / _SwirlRadius;
                    float ang = atan2(p.y, p.x);
                    // (2026-11-22, retour utilisateur : la spirale qui tourne « fait foreuse ») : cercles concentriques à peine
                    // ondulés, qui respirent lentement - un reflet dans l'eau, pas une vis
                    float bands = sin(r * _SwirlFreq * 0.25 + sin(ang * 2.0) * 0.35 + _Time.y * _SwirlSpeed * 0.15);
                    half ringMask = smoothstep(0.55h, 0.9h, bands) * (1.0h - smoothstep(0.75h, 1.05h, r)) * smoothstep(0.08h, 0.2h, r);
                    col = lerp(col, _SwirlColor.rgb, ringMask * _SwirlStrength);
                }

                // reflet du soleil
                float3 h = normalize(sun.direction + v);
                half spec = pow(saturate(dot(n, h)), _SpecPower) * _SpecStrength;
                // reflet peint : direction fixe dans le repère de la caméra (toujours en haut à gauche de la goutte)
                float3 pd = normalize(mul((float3x3)UNITY_MATRIX_I_V, _PaintedDir.xyz));
                float3 h2 = normalize(pd + v);
                half painted = pow(saturate(dot(n, h2)), _PaintedPower) * _PaintedStrength;
                // croissant de reflet le long du bord éclairé (l'épais liseré blanc en haut à gauche de l'illustration)
                half rimBand = smoothstep(0.35h, 0.6h, 1.0h - ndv) * (1.0h - smoothstep(0.78h, 0.92h, 1.0h - ndv));
                half facing = saturate(dot(n, pd) * 1.4h - 0.15h);
                half crescent = rimBand * facing * facing * _RimHighlight;
                half s = saturate(spec + painted + crescent);

                // contour sombre au ras de la silhouette, comme le trait d'encre des illustrations du jeu
                half edge = smoothstep(_OutlineStart, 0.98h, 1.0h - ndv);
                col = lerp(col, _OutlineColor.rgb, edge);
                // reflets blancs (pas teintés par la lumière jaune du soleil : l'eau reflète un ciel clair)
                col = lerp(col, _SpecColor2.rgb, s);
                col = min(col, 1.0h);                                                // jamais plus clair que blanc : pas de bloom
                half alpha = saturate(lerp(_AlphaCenter, _AlphaRim, fres) + s + edge);
                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

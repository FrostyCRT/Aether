// AJOUTE (2026-09-20) - shader d'interface "VENT" pour les fonds du menu principal : déforme légèrement l'image
// dans des ZONES précises (cime de l'arbre, lierre, bannières, herbes, ciel...) pour que le décor peint bouge au
// vent, sans calque supplémentaire. Chaque zone est une ellipse (centre + rayons en UV) avec une amplitude et une
// vitesse propres ; les personnages ne sont dans aucune zone (sauf un pan de cape, réglé à quelques pixels).
//
// _Gust (0..1) : rafale en cours (pilotée par MenuBackgroundController, la même que celle des feuilles) : le décor
// se penche vers la gauche, dans le sens du vent. _Sway (0..1) : intensité globale (0 = image immobile, réglage
// "Effets d'ambiance des menus" désactivé). Copie de UI/Default : masque, découpe et pochoir conservés.
Shader "Aether/UI/WindSway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Gust ("Gust", Range(0, 1)) = 0
        _Sway ("Sway", Range(0, 1)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _Gust;
            float _Sway;
            float _WindCount;
            float4 _WindA[12];   // xy : centre (UV), zw : rayons (UV)
            float4 _WindB[12];   // x : amplitude en U, y : amplitude en V (UV), z : vitesse, w : fréquence spatiale
            float _ProtectCount;
            float4 _ProtectA[4]; // zones PROTÉGÉES (personnage) : xy centre (UV), zw rayons (UV) : aucun déplacement à l'intérieur

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 off = float2(0, 0);
                float t = _Time.y;

                for (int i = 0; i < 12; i++)
                {
                    if (i >= (int)_WindCount) break;

                    float2 d = (uv - _WindA[i].xy) / _WindA[i].zw;
                    float w = saturate(1.0 - dot(d, d));
                    w = w * w;                                              // bords très doux : aucune arête visible
                    if (w <= 0.0) continue;

                    float ph = dot(uv, float2(1.0, 0.7)) * _WindB[i].w;
                    float s1 = sin(t * _WindB[i].z + ph * 6.2831);
                    float s2 = sin(t * _WindB[i].z * 2.3 + ph * 11.0 + 1.7) * 0.35;
                    float s = s1 + s2;

                    // rafale : tout se penche vers la gauche ; en rafale, le balancement s'amplifie un peu
                    float lean = -_Gust * 0.9;
                    float sway = s * (0.35 + 0.35 * _Gust);
                    off.x += w * _WindB[i].x * (lean + sway);
                    off.y += w * _WindB[i].y * (0.6 * s2 + 0.4 * s1) * (0.5 + 0.5 * _Gust);
                }

                // un personnage ne doit jamais bouger avec le décor qui l'entoure : fondu à 0 à l'intérieur de sa zone
                for (int j = 0; j < 4; j++)
                {
                    if (j >= (int)_ProtectCount) break;
                    float2 pd = (uv - _ProtectA[j].xy) / _ProtectA[j].zw;
                    off *= smoothstep(0.55, 1.0, dot(pd, pd));
                }

                off *= _Sway;
                half4 color = (tex2D(_MainTex, uv - off) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}

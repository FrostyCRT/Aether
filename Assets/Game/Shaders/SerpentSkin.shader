// AJOUTÉ (2026-11-25, boss 3 « Serpent de la Faille ») - peau peinte du serpent : écailles d'obsidienne (texture) éclairées
// en 3 tons comme Aether/PaintedMetal, liseré bleu-violet sur le pourtour, et veines rouges incandescentes (masque) qui
// pulsent lentement (corruption vivante, au-dessus du seuil du bloom). Compatible maillage animé (skinning fait avant).
// v2 (2026-11-25) : carte de normales (relief de chaque écaille, plaques ventrales).
Shader "Aether/SerpentSkin"
{
    Properties
    {
        _BaseMap("Écailles (albédo)", 2D) = "white" {}
        _BaseColor("Teinte", Color) = (1, 1, 1, 1)
        [Normal] _NormalMap("Relief des écailles (normales)", 2D) = "bump" {}
        _NormalStrength("Force du relief", Range(0, 2)) = 1
        _ShadowTint("Côté à l'ombre", Color) = (0.32, 0.32, 0.5, 1)
        _VeinMap("Masque des veines", 2D) = "black" {}
        [HDR] _VeinColor("Veines", Color) = (1.8, 0.16, 0.08, 1)
        _VeinPulse("Vitesse de la pulsation", Float) = 1.1
        _VeinMin("Pulsation : minimum", Range(0, 1)) = 0.5
        _RimColor("Liseré", Color) = (0.32, 0.26, 0.55, 1)
        _RimPower("Finesse du liseré", Range(1, 8)) = 3
        _SpecStrength("Reflet du soleil", Range(0, 2)) = 0.45
        _SpecPower("Netteté du reflet", Range(4, 200)) = 36
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ForwardSerpent"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_VeinMap); SAMPLER(sampler_VeinMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _VeinMap_ST;
                half4 _BaseColor, _ShadowTint, _VeinColor, _RimColor;
                half _VeinPulse, _VeinMin, _RimPower, _SpecStrength, _SpecPower, _NormalStrength;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 uv : TEXCOORD2; float4 tangentWS : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.normalWS = nrm.normalWS;
                o.tangentWS = float4(nrm.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = v.uv;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n0 = normalize(i.normalWS);
                float3 t0 = normalize(i.tangentWS.xyz);
                float3 b0 = cross(n0, t0) * i.tangentWS.w;
                half3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw), _NormalStrength);
                float3 n = normalize(tn.x * t0 + tn.y * b0 + tn.z * n0);
                float3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light sun = GetMainLight();
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb * _BaseColor.rgb;
                half ndl = dot(n, sun.direction) * 0.5h + 0.5h;
                half tone = smoothstep(0.3h, 0.45h, ndl) * 0.5h + smoothstep(0.62h, 0.78h, ndl) * 0.5h;
                half3 col = albedo * lerp(_ShadowTint.rgb, half3(1, 1, 1), tone) * lerp(half3(1, 1, 1), sun.color, 0.35h) * 1.6h;
                float3 h = normalize(sun.direction + vdir);
                col += pow(saturate(dot(n, h)), _SpecPower) * _SpecStrength * (0.4h + 0.6h * tone);
                half rim = pow(1.0h - saturate(dot(n, vdir)), _RimPower);
                col += _RimColor.rgb * rim;
                half vein = SAMPLE_TEXTURE2D(_VeinMap, sampler_VeinMap, i.uv * _VeinMap_ST.xy + _VeinMap_ST.zw).r;
                // pulsation lente qui parcourt le corps (décalée le long de la longueur, v)
                half pulse = lerp(_VeinMin, 1.0h, 0.5h + 0.5h * sin(_Time.y * _VeinPulse * 6.2831h - i.uv.y * 2.0h));
                col = lerp(col, _VeinColor.rgb * pulse, vein);
                return half4(col, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}

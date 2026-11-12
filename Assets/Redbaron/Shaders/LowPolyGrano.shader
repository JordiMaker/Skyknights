// Redbaron · low poly con grano de material (Built-in Render Pipeline).
// El grano se calcula en espacio de objeto, así no "nada" cuando la unidad se mueve, y no necesita UV ni texturas.
// Es el mismo grano que el del hangar: pintura moteada, metal cepillado, veta de madera, tela, piedra o liso.
Shader "Redbaron/LowPolyGrano"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Suavidad", Range(0,1)) = 0.2
        _Metallic ("Metálico", Range(0,1)) = 0
        [Enum(Pintura,0,Metal,1,Madera,2,Tela,3,Piedra,4,Liso,5)] _Tipo ("Tipo de material", Float) = 0
        _Grano ("Intensidad del grano", Range(0,2)) = 1
        _Escala ("Escala del grano", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        fixed4 _Color;
        half _Glossiness, _Metallic, _Tipo, _Grano, _Escala;

        struct Input { float3 objPos; };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex.xyz;
        }

        float gHash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
        float gNoise(float3 x)
        {
            float3 i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(gHash(i), gHash(i + float3(1,0,0)), f.x), lerp(gHash(i + float3(0,1,0)), gHash(i + float3(1,1,0)), f.x), f.y),
                        lerp(lerp(gHash(i + float3(0,0,1)), gHash(i + float3(1,0,1)), f.x), lerp(gHash(i + float3(0,1,1)), gHash(i + float3(1,1,1)), f.x), f.y), f.z);
        }
        float gFbm(float3 p) { float a = 0.5, s = 0.0; for (int k = 0; k < 4; k++) { s += a * gNoise(p); p *= 2.03; a *= 0.5; } return s; }

        // x = factor de color, y = cambio de rugosidad (positivo = más mate)
        float2 Grano(float3 p, int t)
        {
            if (t == 1) { float s = gFbm(p * float3(3, 32, 3)); return float2(0.84 + 0.28 * s + 0.05 * (gNoise(p * 40) - 0.5), 0.3 * (0.5 - s)); }
            if (t == 2) { float w = sin((p.x + p.z) * 22 + gFbm(p * 3) * 6); return float2(0.92 + 0.12 * w + 0.12 * (gFbm(p * 6) - 0.5), 0.05); }
            if (t == 3) { float s = gFbm(p * float3(14, 4, 4)); return float2(0.86 + 0.2 * s + 0.06 * (gNoise(p * 45) - 0.5), 0.08); }
            if (t == 4) { return float2(0.8 + 0.32 * gFbm(p * 4) + 0.1 * (gNoise(p * 28) - 0.5), 0.06); }
            if (t == 5) { return float2(1, 0); }
            return float2(0.87 + 0.18 * gFbm(p * 2.4) + 0.09 * (gNoise(p * 26) - 0.5), 0.1 + 0.12 * (gFbm(p * 3.5) - 0.5));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 g = Grano(IN.objPos * _Escala, (int)round(_Tipo));
            float f = lerp(1.0, g.x, _Grano);
            o.Albedo = _Color.rgb * f;
            o.Metallic = _Metallic;
            o.Smoothness = saturate(_Glossiness - g.y * _Grano);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

// Shader para el terreno, la pared de roca y las estructuras generadas por Redbaron.
// Built-in Render Pipeline. Usa el color de vértice como albedo; las caras planas
// vienen de la malla (cada triángulo tiene sus propios vértices).
Shader "Redbaron/LowPolyVertexColor"
{
    Properties
    {
        _Tint ("Tinte", Color) = (1,1,1,1)
        _Glossiness ("Suavidad", Range(0,1)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Back

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        fixed4 _Tint;
        half _Glossiness;

        struct Input
        {
            float4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Vertex colours are authored in sRGB; convert them when the project uses Linear colour space.
            #if defined(UNITY_COLORSPACE_GAMMA)
            o.Albedo = IN.color.rgb * _Tint.rgb;
            #else
            o.Albedo = GammaToLinearSpace(IN.color.rgb) * _Tint.rgb;
            #endif
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

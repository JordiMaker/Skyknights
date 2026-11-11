// Stand-in for the third-party "Hologram Effect Shader" that Quick RTS expects for its
// building blueprints (PlaceableHologram / NoPlaceableHologram materials).
// Its .meta carries the GUID those materials reference, so they pick it up automatically,
// and it reads the same property names (_Color, _HoloColor, _HoloDirection, _HoloDistance,
// _HoloSpeed, _HoloValue) so the colours set in the materials are kept.
Shader "Quick RTS/Hologram Lite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (0.47, 0.74, 0.82, 1)
        _HoloColor ("Hologram Color", Color) = (0.28, 0.47, 0.56, 1)
        _HoloDirection ("Scan Direction (world)", Vector) = (0, 1, 0, 0)
        _HoloDistance ("Line Spacing", Float) = 0.335
        _HoloSpeed ("Scroll Speed", Float) = 0.12
        _HoloValue ("Line Strength", Range(0, 1)) = 1
        _Opacity ("Opacity", Range(0, 1)) = 0.4
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _HoloColor;
            float4 _HoloDirection;
            float _HoloDistance;
            float _HoloSpeed;
            float _HoloValue;
            float _Opacity;
            float _RimPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = _HoloDirection.xyz;
                dir = dot(dir, dir) > 1e-6 ? normalize(dir) : float3(0, 1, 0);
                float spacing = max(_HoloDistance, 0.01);

                // Soft scan lines that scroll along the direction.
                float band = frac((dot(i.worldPos, dir) - _Time.y * _HoloSpeed) / spacing);
                float scan = smoothstep(0.0, 0.15, band) * (1.0 - smoothstep(0.45, 0.6, band));

                // Fresnel rim so the silhouette reads clearly over the terrain.
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float rim = pow(1.0 - saturate(dot(normalize(i.worldNormal), viewDir)), _RimPower);

                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed3 col = tex.rgb * _Color.rgb + _HoloColor.rgb * (scan * _HoloValue + rim);
                float alpha = saturate(_Opacity + rim * 0.5 + scan * _HoloValue * 0.25) * _Color.a;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}

Shader "TomCatRunner/UI/SDF Rounded Rect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _CornerRadius ("Corner Radius", Range(0, 1)) = 0.1
        _CornerRadiusTopLeft ("Corner Radius Top Left", Range(0, 1)) = 0
        _CornerRadiusTopRight ("Corner Radius Top Right", Range(0, 1)) = 0
        _CornerRadiusBottomLeft ("Corner Radius Bottom Left", Range(0, 1)) = 0
        _CornerRadiusBottomRight ("Corner Radius Bottom Right", Range(0, 1)) = 0
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0
        _BorderColor ("Border Color", Color) = (1,1,1,1)
        _ShadowDistance ("Shadow Distance", Range(0, 0.1)) = 0
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.5)
        _ShadowSoftness ("Shadow Softness", Range(0, 1)) = 0.5
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 0
        _GlowColor ("Glow Color", Color) = (1,1,1,1)
        _PixelSnap ("Pixel Snap", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "PreviewType"="Plane" }
        LOD 100

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _CornerRadius;
            float _CornerRadiusTopLeft;
            float _CornerRadiusTopRight;
            float _CornerRadiusBottomLeft;
            float _CornerRadiusBottomRight;
            float _BorderWidth;
            fixed4 _BorderColor;
            float _ShadowDistance;
            fixed4 _ShadowColor;
            float _ShadowSoftness;
            float _GlowIntensity;
            fixed4 _GlowColor;
            float _PixelSnap;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            // SDF 圆角矩形
            float sdRoundedRect(float2 uv, float2 size, float4 radii)
            {
                uv = abs(uv - 0.5) * 2.0;
                size = size * 2.0 - radii * 2.0;
                float2 d = abs(uv) - size;
                float2 outside = max(d, 0.0);
                float2 inside = min(max(d.x, d.y), 0.0);
                float corner = length(outside) + min(inside.x, inside.y) - min(radii.x, min(radii.y, min(radii.z, radii.w)));
                return corner * 0.5;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = float2(1, 1);
                float4 radii = float4(
                    _CornerRadius + _CornerRadiusTopLeft,
                    _CornerRadius + _CornerRadiusTopRight,
                    _CornerRadius + _CornerRadiusBottomRight,
                    _CornerRadius + _CornerRadiusBottomLeft
                );

                // SDF 距离
                float dist = sdRoundedRect(i.uv - 0.5, size, radii);

                // 阴影
                float shadowDist = dist + _ShadowDistance;
                float shadow = 1.0 - smoothstep(-_ShadowSoftness, _ShadowSoftness, shadowDist);
                fixed4 shadowCol = _ShadowColor * shadow;

                // 主形状
                float shape = 1.0 - smoothstep(-0.005, 0.005, dist);

                // 边框
                float borderDist = dist - _BorderWidth;
                float border = smoothstep(-0.005, 0.005, borderDist) * (1.0 - shape);
                fixed4 borderCol = _BorderColor * border;

                // 发光
                float glow = _GlowIntensity * exp(-abs(dist) * 20.0);
                fixed4 glowCol = _GlowColor * glow;

                // 纹理采样
                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;

                // 合成
                fixed4 result = tex * shape + borderCol + glowCol + shadowCol;

                // 像素对齐
                if (_PixelSnap > 0.5)
                {
                    result.rgb = round(result.rgb * 255.0) / 255.0;
                }

                return result;
            }
            ENDCG
        }
    }
}
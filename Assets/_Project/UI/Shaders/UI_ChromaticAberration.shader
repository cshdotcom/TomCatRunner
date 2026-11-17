Shader "TomCatRunner/UI/Chromatic Aberration"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _AberrationStrength ("Aberration Strength", Range(0, 0.1)) = 0.01
        _AberrationCenter ("Aberration Center", Vector) = (0.5, 0.5, 0, 0)
        _RadialMode ("Radial Mode", Float) = 1
        _IntensityR ("Red Channel Intensity", Range(0, 2)) = 1.0
        _IntensityG ("Green Channel Intensity", Range(0, 2)) = 1.0
        _IntensityB ("Blue Channel Intensity", Range(0, 2)) = 1.0
        _OffsetR ("Red Offset", Vector) = (0, 0, 0, 0)
        _OffsetG ("Green Offset", Vector) = (0, 0, 0, 0)
        _OffsetB ("Blue Offset", Vector) = (0, 0, 0, 0)
        _Vignette ("Vignette", Range(0, 1)) = 0.3
        _VignetteColor ("Vignette Color", Color) = (0,0,0,1)
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.1
        _ScanlineScale ("Scanline Scale", Float) = 800.0
        _TimeScale ("Time Scale", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "PreviewType"="Plane" }
        LOD 150

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _AberrationStrength;
            float2 _AberrationCenter;
            float _RadialMode;
            float _IntensityR;
            float _IntensityG;
            float _IntensityB;
            float2 _OffsetR;
            float2 _OffsetG;
            float2 _OffsetB;
            float _Vignette;
            fixed4 _VignetteColor;
            float _ScanlineIntensity;
            float _ScanlineScale;
            float _TimeScale;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_TARGET
            {
                float2 uv = i.uv;
                float2 center = _AberrationCenter;
                float2 offset = uv - center;
                float dist = length(offset);
                float time = _Time.y * _TimeScale;

                // 计算色差偏移
                float2 aberrationOffset = offset * _AberrationStrength;

                // 径向模式：距离中心越远色差越强
                if (_RadialMode > 0.5)
                {
                    aberrationOffset *= dist * 2.0;
                }

                // 动态抖动
                float jitter = sin(time * 10.0 + dist * 50.0) * 0.0005;
                aberrationOffset += float2(jitter, -jitter);

                // 采样三个通道
                fixed4 r = tex2D(_MainTex, uv + aberrationOffset * _IntensityR + _OffsetR);
                fixed4 g = tex2D(_MainTex, uv + aberrationOffset * _IntensityG + _OffsetG);
                fixed4 b = tex2D(_MainTex, uv + aberrationOffset * _IntensityB + _OffsetB);

                // 合成
                fixed4 result;
                result.r = r.r * _IntensityR;
                result.g = g.g * _IntensityG;
                result.b = b.b * _IntensityB;
                result.a = (r.a + g.a + b.a) / 3.0;

                // 晕影
                float vignette = 1.0 - smoothstep(0.5, 1.0, dist * 2.0) * _Vignette;
                result.rgb = lerp(_VignetteColor.rgb, result.rgb, vignette);

                // 扫描线
                float scanline = sin(uv.y * _ScanlineScale + time * 5.0) * _ScanlineIntensity;
                result.rgb += scanline;

                // 颜色校正
                result.rgb *= _Color.rgb;
                result.a *= _Color.a;

                return result;
            }
            ENDCG
        }
    }
}
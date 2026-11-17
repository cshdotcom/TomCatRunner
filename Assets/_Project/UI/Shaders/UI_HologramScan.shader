Shader "TomCatRunner/UI/Hologram Scan"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0, 1, 1, 1)
        _ScanColor ("Scan Line Color", Color) = (0, 1, 1, 1)
        _ScanWidth ("Scan Line Width", Range(0, 0.2)) = 0.02
        _ScanSpeed ("Scan Speed", Float) = 1.0
        _ScanDirection ("Scan Direction", Float) = 1.0
        _Intensity ("Intensity", Range(0, 5)) = 2.0
        _FlickerIntensity ("Flicker Intensity", Range(0, 1)) = 0.1
        _FlickerSpeed ("Flicker Speed", Float) = 10.0
        _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.2
        _NoiseScale ("Noise Scale", Float) = 100.0
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0.05
        _GlitchSpeed ("Glitch Speed", Float) = 2.0
        _EdgeGlow ("Edge Glow", Range(0, 2)) = 1.0
        _EdgeColor ("Edge Color", Color) = (0, 0.5, 1, 1)
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.3
        _DissolveTex ("Dissolve Texture", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.1)) = 0.01
        _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "PreviewType"="Plane" }
        LOD 200

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
            sampler2D _DissolveTex;
            fixed4 _Color;
            fixed4 _ScanColor;
            float _ScanWidth;
            float _ScanSpeed;
            float _ScanDirection;
            float _Intensity;
            float _FlickerIntensity;
            float _FlickerSpeed;
            float _NoiseIntensity;
            float _NoiseScale;
            float _GlitchIntensity;
            float _GlitchSpeed;
            float _EdgeGlow;
            fixed4 _EdgeColor;
            float _PulseSpeed;
            float _PulseIntensity;
            float _DissolveAmount;
            float _DissolveEdgeWidth;
            fixed4 _DissolveEdgeColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // 随机噪声
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            // 扫描线
            float scanline(float2 uv, float time)
            {
                float y = uv.y;
                if (_ScanDirection < 0) y = 1.0 - y;

                float scanPos = frac(y - time * _ScanSpeed);
                float line = 1.0 - smoothstep(0.0, _ScanWidth, abs(scanPos - 0.5));
                return line;
            }

            // 边缘发光
            float edgeGlow(float2 uv)
            {
                float2 dist = abs(uv - 0.5) * 2.0;
                float edge = 1.0 - max(dist.x, dist.y);
                return pow(edge, 3.0) * _EdgeGlow;
            }

            // 溶解效果
            float dissolve(float2 uv, float time)
            {
                float noise = tex2D(_DissolveTex, uv * 0.1 + time * 0.01).r;
                float dissolve = step(_DissolveAmount, noise);
                float edge = step(_DissolveAmount - _DissolveEdgeWidth, noise) - dissolve;
                return dissolve + edge;
            }

            fixed4 frag(v2f i) : SV_TARGET
            {
                float2 uv = i.uv;
                float time = _Time.y;
                fixed4 tex = tex2D(_MainTex, i.uv);

                // 基础全息颜色
                fixed4 baseColor = tex * _Color * _Intensity;

                // 扫描线
                float scan = scanline(uv, time);
                fixed4 scanLineCol = _ScanColor * scan * _Intensity * 2.0;

                // 边缘发光
                float edge = edgeGlow(uv);
                fixed4 edgeCol = _EdgeColor * edge;

                // 脉冲
                float pulse = sin(time * _PulseSpeed) * 0.5 + 0.5;
                pulse = 1.0 + (pulse - 0.5) * _PulseIntensity * 2.0;

                // 闪烁
                float flicker = 1.0 + (rand(float2(time * _FlickerSpeed, 0.0)) - 0.5) * _FlickerIntensity * 2.0;

                // 噪声纹理
                float noise = rand(uv * _NoiseScale + time * 0.1) * _NoiseIntensity;

                // 故障效果
                float glitch = 0.0;
                if (frac(time * _GlitchSpeed * 10.0) < _GlitchIntensity)
                {
                    float glitchOffset = (rand(float2(time * _GlitchSpeed, uv.y * 100.0)) - 0.5) * 0.1;
                    glitch = glitchOffset;
                    uv.x += glitch;
                    // 重新采样
                    tex = tex2D(_MainTex, uv);
                    baseColor = tex * _Color * _Intensity;
                }

                // 溶解
                float dissolveVal = dissolve(uv, time);
                float dissolveEdge = step(_DissolveAmount - _DissolveEdgeWidth, tex2D(_DissolveTex, uv * 0.1).r) - dissolveVal;

                // 合成
                fixed4 result = baseColor * pulse * flicker;
                result += scanLineCol;
                result += edgeCol;
                result.rgb += noise;
                result *= dissolveVal;
                result.rgb += _DissolveEdgeColor.rgb * dissolveEdge * 2.0;

                // 保持原始 Alpha 用于透明度混合
                result.a = tex.a * dissolveVal * _Color.a;

                return result;
            }
            ENDCG
        }
    }
}
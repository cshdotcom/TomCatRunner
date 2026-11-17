Shader "TomCatRunner/UI/Edge Glow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 2.0
        _GlowWidth ("Glow Width", Range(0, 0.5)) = 0.1
        _GlowFalloff ("Glow Falloff", Range(0.1, 10)) = 2.0
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.5
        _Animated ("Animated", Float) = 1
        _RotationSpeed ("Rotation Speed", Float) = 30.0
        _WaveCount ("Wave Count", Int) = 3
        _WaveSpeed ("Wave Speed", Float) = 2.0
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.1)) = 0.02
        _InnerGlow ("Inner Glow", Float) = 0
        _InnerGlowColor ("Inner Glow Color", Color) = (1,1,1,1)
        _InnerGlowWidth ("Inner Glow Width", Range(0, 0.2)) = 0.05
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.01)) = 0
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
            fixed4 _GlowColor;
            float _GlowIntensity;
            float _GlowWidth;
            float _GlowFalloff;
            float _PulseSpeed;
            float _PulseIntensity;
            float _Animated;
            float _RotationSpeed;
            int _WaveCount;
            float _WaveSpeed;
            float _WaveAmplitude;
            float _InnerGlow;
            fixed4 _InnerGlowColor;
            float _InnerGlowWidth;
            float _ChromaticAberration;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // SDF 圆角矩形距离场
            float sdRoundedRect(float2 uv, float2 size, float radius)
            {
                uv = abs(uv - 0.5) * 2.0;
                size = size * 2.0 - radius * 2.0;
                float2 d = abs(uv) - size;
                return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)) - radius;
            }

            // 旋转 UV
            float2 rotateUV(float2 uv, float angle)
            {
                float c = cos(angle);
                float s = sin(angle);
                float2 centered = uv - 0.5;
                float2 rotated = float2(
                    centered.x * c - centered.y * s,
                    centered.x * s + centered.y * c
                );
                return rotated + 0.5;
            }

            // 波浪扭曲
            float2 waveDistort(float2 uv, float time)
            {
                float2 centered = uv - 0.5;
                float angle = atan2(centered.y, centered.x);
                float dist = length(centered);

                float wave = sin(angle * _WaveCount - time * _WaveSpeed) * _WaveAmplitude;
                float2 dir = normalize(centered);
                return uv + dir * wave;
            }

            fixed4 frag(v2f i) : SV_TARGET
            {
                float2 uv = i.uv;
                float time = _Time.y;
                fixed4 tex = tex2D(_MainTex, uv);

                // 基础颜色
                fixed4 baseColor = tex * _Color;

                // 动画
                if (_Animated > 0.5)
                {
                    // 旋转发光
                    float rotationAngle = time * _RotationSpeed * 0.0174533; // 度转弧度
                    uv = rotateUV(uv, rotationAngle);

                    // 波浪扭曲
                    uv = waveDistort(uv, time);
                }

                // 脉冲
                float pulse = 1.0;
                if (_PulseSpeed > 0)
                {
                    pulse = 1.0 + sin(time * _PulseSpeed) * 0.5 * _PulseIntensity;
                }

                // 计算边缘距离 (SDF)
                float edgeDist = sdRoundedRect(uv, float2(1, 1), 0.1);

                // 外发光
                float outerGlow = exp(-abs(edgeDist) / _GlowWidth * _GlowFalloff) * _GlowIntensity * pulse;
                outerGlow = saturate(outerGlow);

                // 内发光
                float innerGlow = 0.0;
                if (_InnerGlow > 0.5)
                {
                    float innerDist = sdRoundedRect(uv, float2(1, 1) * (1.0 - _InnerGlowWidth * 2.0), 0.05);
                    innerGlow = exp(-abs(innerDist) / _InnerGlowWidth * 3.0) * _InnerGlow;
                    innerGlow = saturate(innerGlow);
                }

                // 色差
                fixed4 glowColor = _GlowColor;
                if (_ChromaticAberration > 0)
                {
                    float2 caOffset = float2(_ChromaticAberration, 0);
                    fixed4 r = tex2D(_MainTex, uv + caOffset);
                    fixed4 b = tex2D(_MainTex, uv - caOffset);
                    glowColor = fixed4(r.r, glowColor.g, b.b, glowColor.a);
                }

                // 合成发光
                fixed4 outerGlowCol = glowColor * outerGlow;
                fixed4 innerGlowCol = _InnerGlowColor * innerGlow;

                // 结果
                fixed4 result = baseColor + outerGlowCol + innerGlowCol;

                // 预乘 Alpha
                result.rgb *= result.a;

                return result;
            }
            ENDCG
        }
    }
}
Shader "TomCatRunner/UI/Fluid Noise"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Float) = 50.0
        _NoiseSpeed ("Noise Speed", Float) = 0.5
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.3
        _DistortionStrength ("Distortion Strength", Range(0, 0.5)) = 0.1
        _FlowDirection ("Flow Direction", Vector) = (1, 0, 0, 0)
        _FlowSpeed ("Flow Speed", Float) = 0.2
        _ColorA ("Color A", Color) = (0.2, 0.6, 1, 1)
        _ColorB ("Color B", Color) = (0.8, 0.2, 1, 1)
        _ColorC ("Color C", Color) = (0.2, 1, 0.6, 1)
        _BlendingMode ("Blending Mode", Int) = 0
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _UseMask ("Use Mask", Float) = 0
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
            sampler2D _NoiseTex;
            sampler2D _MaskTex;
            fixed4 _Color;
            float _NoiseScale;
            float _NoiseSpeed;
            float _NoiseStrength;
            float _DistortionStrength;
            float2 _FlowDirection;
            float _FlowSpeed;
            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _ColorC;
            int _BlendingMode;
            float _UseMask;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // 单纯形噪声 2D
            float3 permute(float3 x) { return fmod(((x*34.0)+1.0)*x, 289.0); }
            float2 permute(float2 x) { return fmod(((x*34.0)+1.0)*x, 289.0); }

            float snoise(float2 v)
            {
                const float4 C = float4(0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439);
                float2 i  = floor(v + dot(v, C.yy));
                float2 x0 = v - i + dot(i, C.xx);
                float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
                float4 x12 = x0.xyxy + C.xxzz;
                x12.xy -= i1;
                i = fmod(i, 289.0);
                float3 p = permute(permute(i.y + float3(0.0, i1.y, 1.0)) + i.x + float3(0.0, i1.x, 1.0));
                float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
                m = m*m;
                m = m*m;
                float3 x = 2.0 * frac(p * C.www) - 1.0;
                float3 h = abs(x) - 0.5;
                float3 ox = floor(x + 0.5);
                float3 a0 = x - ox;
                m *= 1.79284291400159 - 0.85373472095314 * (a0*a0 + h*h);
                float3 g;
                g.x  = a0.x  * x0.x  + h.x  * x0.y;
                g.yz = a0.yz * x12.xz + h.yz * x12.yw;
                return 130.0 * dot(m, g);
            }

            // FBM 噪声
            float fbm(float2 uv, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * snoise(uv * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                return value;
            }

            // 域扭曲
            float2 domainWarp(float2 uv, float time)
            {
                float2 warp = float2(
                    fbm(uv * 0.5 + time * _FlowDirection * _FlowSpeed, 3),
                    fbm(uv * 0.5 + time * _FlowDirection * _FlowSpeed + 100.0, 3)
                ) * _DistortionStrength;
                return uv + warp;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _Time.y * _NoiseSpeed;
                float2 uv = i.uv * _NoiseScale;

                // 域扭曲产生流体效果
                uv = domainWarp(uv, time);

                // 多层噪声
                float n1 = fbm(uv + time * 0.1, 4);
                float n2 = fbm(uv * 2.0 - time * 0.05, 3);
                float n3 = fbm(uv * 0.5 + time * 0.2, 2);

                // 组合噪声
                float noise = (n1 + n2 * 0.5 + n3 * 0.25) * _NoiseStrength;

                // 三色渐变映射
                fixed4 color1 = lerp(_ColorA, _ColorB, noise);
                fixed4 color2 = lerp(_ColorB, _ColorC, noise);
                fixed4 color3 = lerp(_ColorC, _ColorA, noise);

                fixed4 fluidColor;
                if (_BlendingMode == 0) // Additive
                    fluidColor = color1 + color2 + color3;
                else if (_BlendingMode == 1) // Multiply
                    fluidColor = color1 * color2 * color3;
                else // Screen
                    fluidColor = 1.0 - (1.0 - color1) * (1.0 - color2) * (1.0 - color3);

                // 采样主纹理
                fixed4 tex = tex2D(_MainTex, i.uv);

                // 遮罩
                float mask = 1.0;
                if (_UseMask > 0.5)
                {
                    mask = tex2D(_MaskTex, i.uv).r;
                }

                // 合成
                fixed4 result = tex * _Color + fluidColor * (1.0 - tex.a) * mask;
                result.a = tex.a * mask;

                return result;
            }
            ENDCG
        }
    }
}
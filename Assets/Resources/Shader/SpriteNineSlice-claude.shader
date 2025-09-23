Shader "Custom/SpriteNineSlice_claude"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Borders("Borders (L,R pixels)", Vector) = (32,32,0,0)
        _SegmentCount("Snake Segment Count", Float) = 11
        [Toggle] _DebugMode("Debug Mode", Float) = 0
    }

        SubShader
        {
            Tags
            {
                "Queue" = "Transparent"
                "RenderType" = "Transparent"
            }

            Cull Off
            Lighting Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                float4 _MainTex_TexelSize;
                float4 _Color;
                float4 _Borders;
                float _SegmentCount;
                float _DebugMode;

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

                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    o.color = v.color * _Color;
                    return o;
                }

                float2 LineRendererNineSliceRemap(float2 uv)
                {
                    float2 result = uv;

                    // 计算边界UV位置（基于分段数）
                    float segmentSize = 1.0 / _SegmentCount;
                    float headBoundary = segmentSize;           // 第1段是蛇头
                    float tailBoundary = 1.0 - segmentSize;     // 最后1段是蛇尾

                    // 纹理边框大小（UV坐标系）
                    float leftBorderTexUV = _Borders.x * _MainTex_TexelSize.x;     // 蛇头纹理区域
                    float rightBorderTexUV = _Borders.y * _MainTex_TexelSize.x;    // 蛇尾纹理区域
                    float centerTexUV = 1.0 - leftBorderTexUV - rightBorderTexUV;  // 身体纹理区域

                    // X轴重新映射
                    if (uv.x < headBoundary)
                    {
                        // 蛇头区域：映射到纹理的左边框
                        float t = uv.x / headBoundary;
                        result.x = t * leftBorderTexUV;
                    }
                    else if (uv.x > tailBoundary)
                    {
                        // 蛇尾区域：映射到纹理的右边框
                        float t = (uv.x - tailBoundary) / segmentSize;
                        result.x = (1.0 - rightBorderTexUV) + t * rightBorderTexUV;
                    }
                    else
                    {
                        // 身体区域：循环平铺纹理的中间部分
                        float centerUV = (uv.x - headBoundary) / (tailBoundary - headBoundary);

                        // 计算平铺次数
                        float bodySegments = _SegmentCount - 2.0; // 减去头尾两段
                        float tileCount = bodySegments; // 每个身体段对应一个完整的纹理平铺

                        // 循环平铺
                        float tiledU = fmod(centerUV * tileCount, 1.0);
                        result.x = leftBorderTexUV + tiledU * centerTexUV;
                    }

                    // Y轴保持不变（不做九宫格处理）
                    result.y = uv.y;

                    return result;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    // 详细调试模式
                    if (_DebugMode > 0.5)
                    {
                        float2 uv = i.uv;

                        // 计算边界
                        float segmentSize = 1.0 / _SegmentCount;
                        float headBoundary = segmentSize;
                        float tailBoundary = 1.0 - segmentSize;

                        // 根据分段显示不同颜色
                        if (uv.x < headBoundary)
                        {
                            // 蛇头区域 - 绿色
                            return fixed4(0, 1, 0, 1);
                        }
                        else if (uv.x > tailBoundary)
                        {
                            // 蛇尾区域 - 蓝色
                            return fixed4(0, 0, 1, 1);
                        }
                        else
                        {
                            // 身体区域 - 红色，显示平铺效果
                            float centerUV = (uv.x - headBoundary) / (tailBoundary - headBoundary);
                            float bodySegments = _SegmentCount - 2.0;

                            // 显示段落条纹
                            float segmentIndex = floor(centerUV * bodySegments);
                            float segmentPattern = fmod(segmentIndex, 2.0);

                            // 红色渐变 + 条纹效果
                            return fixed4(1, segmentPattern, centerUV, 1);
                        }
                    }

                    if (_DebugMode > 1.5)
                    {
                        // 显示重新映射后的UV坐标
                        float2 remappedUV = LineRendererNineSliceRemap(i.uv);
                        return fixed4(remappedUV.x, remappedUV.y, 0, 1);
                    }

                    // 正常渲染
                    float2 remappedUV = LineRendererNineSliceRemap(i.uv);
                    fixed4 texColor = tex2D(_MainTex, remappedUV);
                    return texColor * i.color;
                }
                ENDCG
            }
        }
            FallBack "Sprites/Default"
}
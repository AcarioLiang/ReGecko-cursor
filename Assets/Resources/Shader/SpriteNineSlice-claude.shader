Shader "Custom/SpriteNineSlice_claude"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Borders("Borders (L,R,B,T pixels)", Vector) = (16,16,16,16)
        [Toggle] _DebugMode("Debug Mode", Float) = 0
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
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
                // LineRenderer 在 Stretch 模式下的 UV：
                // u = 沿线长度方向 (0-1)
                // v = 沿线宽度方向 (0-1)
                
                // 对于蛇身体，我们希望：
                // - 沿长度方向(u)：两端不拉伸，中间拉伸
                // - 沿宽度方向(v)：保持原始纹理比例
                
                float2 result = uv;
                
                // 计算归一化边框阈值
                float leftBorder = _Borders.x * _MainTex_TexelSize.x;    // 左边框
                float rightBorder = _Borders.y * _MainTex_TexelSize.x;   // 右边框
                float bottomBorder = _Borders.z * _MainTex_TexelSize.y;  // 下边框  
                float topBorder = _Borders.w * _MainTex_TexelSize.y;     // 上边框
                
                // 沿长度方向(U)的九宫格处理
                float uLeft = leftBorder;
                float uRight = 1.0 - rightBorder;
                
                if (uv.x < uLeft)
                {
                    // 左端区域：映射到纹理左边框
                    result.x = (uv.x / uLeft) * leftBorder;
                }
                else if (uv.x > uRight)
                {
                    // 右端区域：映射到纹理右边框
                    float t = (uv.x - uRight) / (1.0 - uRight);
                    result.x = (1.0 - rightBorder) + t * rightBorder;
                }
                else
                {
                    // 中间区域：映射到纹理中心部分
                    float t = (uv.x - uLeft) / (uRight - uLeft);
                    result.x = leftBorder + t * (1.0 - leftBorder - rightBorder);
                }
                
                // 沿宽度方向(V)的九宫格处理
                float vBottom = bottomBorder;
                float vTop = 1.0 - topBorder;
                
                if (uv.y < vBottom)
                {
                    // 底部区域
                    result.y = (uv.y / vBottom) * bottomBorder;
                }
                else if (uv.y > vTop)
                {
                    // 顶部区域
                    float t = (uv.y - vTop) / (1.0 - vTop);
                    result.y = (1.0 - topBorder) + t * topBorder;
                }
                else
                {
                    // 中间区域
                    float t = (uv.y - vBottom) / (vTop - vBottom);
                    result.y = bottomBorder + t * (1.0 - bottomBorder - topBorder);
                }
                
                return result;
            }

            fixed4 frag(v2f i) : SV_Target
			{
				// 详细调试模式
				if (_DebugMode > 1.5) // 新的测试模式
				{
					float2 remappedUV = LineRendererNineSliceRemap(i.uv);
					
					// 生成测试图案：边框是纯色，中心是棋盘格
					float leftBorder = _Borders.x * _MainTex_TexelSize.x;
					float rightBorder = _Borders.y * _MainTex_TexelSize.x;
					float bottomBorder = _Borders.z * _MainTex_TexelSize.y;
					float topBorder = _Borders.w * _MainTex_TexelSize.y;
					
					// 判断采样点在纹理中的位置
					if (remappedUV.x < leftBorder || remappedUV.x > (1.0 - rightBorder) ||
						remappedUV.y < bottomBorder || remappedUV.y > (1.0 - topBorder))
					{
						// 边框区域：显示纯红色
						return fixed4(1, 0, 0, 1);
					}
					else
					{
						// 中心区域：显示棋盘格图案
						float2 centerUV = remappedUV;
						centerUV.x = (centerUV.x - leftBorder) / (1.0 - leftBorder - rightBorder);
						centerUV.y = (centerUV.y - bottomBorder) / (1.0 - bottomBorder - topBorder);
						
						// 创建棋盘格
						float checker = step(0.5, fmod(centerUV.x * 8.0, 1.0)) + 
									   step(0.5, fmod(centerUV.y * 8.0, 1.0));
						checker = fmod(checker, 2.0);
						
						return fixed4(checker, checker, 1, 1); // 蓝白棋盘格
					}
				}
				
				if (_DebugMode > 0.5)
				{
					float leftBorder = _Borders.x * _MainTex_TexelSize.x;
					float rightBorder = _Borders.y * _MainTex_TexelSize.x;
					float bottomBorder = _Borders.z * _MainTex_TexelSize.y;
					float topBorder = _Borders.w * _MainTex_TexelSize.y;
					
					float uLeft = leftBorder;
					float uRight = 1.0 - rightBorder;
					float vBottom = bottomBorder;
					float vTop = 1.0 - topBorder;
					
					float2 uv = i.uv;
					float2 remappedUV = LineRendererNineSliceRemap(uv);
					
					// 显示重映射后的 UV 作为颜色
					// 这样可以看到重映射是否正确
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
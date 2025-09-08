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
                // LineRenderer �� Stretch ģʽ�µ� UV��
                // u = ���߳��ȷ��� (0-1)
                // v = ���߿�ȷ��� (0-1)
                
                // ���������壬����ϣ����
                // - �س��ȷ���(u)�����˲����죬�м�����
                // - �ؿ�ȷ���(v)������ԭʼ�������
                
                float2 result = uv;
                
                // �����һ���߿���ֵ
                float leftBorder = _Borders.x * _MainTex_TexelSize.x;    // ��߿�
                float rightBorder = _Borders.y * _MainTex_TexelSize.x;   // �ұ߿�
                float bottomBorder = _Borders.z * _MainTex_TexelSize.y;  // �±߿�  
                float topBorder = _Borders.w * _MainTex_TexelSize.y;     // �ϱ߿�
                
                // �س��ȷ���(U)�ľŹ�����
                float uLeft = leftBorder;
                float uRight = 1.0 - rightBorder;
                
                if (uv.x < uLeft)
                {
                    // �������ӳ�䵽������߿�
                    result.x = (uv.x / uLeft) * leftBorder;
                }
                else if (uv.x > uRight)
                {
                    // �Ҷ�����ӳ�䵽�����ұ߿�
                    float t = (uv.x - uRight) / (1.0 - uRight);
                    result.x = (1.0 - rightBorder) + t * rightBorder;
                }
                else
                {
                    // �м�����ӳ�䵽�������Ĳ���
                    float t = (uv.x - uLeft) / (uRight - uLeft);
                    result.x = leftBorder + t * (1.0 - leftBorder - rightBorder);
                }
                
                // �ؿ�ȷ���(V)�ľŹ�����
                float vBottom = bottomBorder;
                float vTop = 1.0 - topBorder;
                
                if (uv.y < vBottom)
                {
                    // �ײ�����
                    result.y = (uv.y / vBottom) * bottomBorder;
                }
                else if (uv.y > vTop)
                {
                    // ��������
                    float t = (uv.y - vTop) / (1.0 - vTop);
                    result.y = (1.0 - topBorder) + t * topBorder;
                }
                else
                {
                    // �м�����
                    float t = (uv.y - vBottom) / (vTop - vBottom);
                    result.y = bottomBorder + t * (1.0 - bottomBorder - topBorder);
                }
                
                return result;
            }

            fixed4 frag(v2f i) : SV_Target
			{
				// ��ϸ����ģʽ
				if (_DebugMode > 1.5) // �µĲ���ģʽ
				{
					float2 remappedUV = LineRendererNineSliceRemap(i.uv);
					
					// ���ɲ���ͼ�����߿��Ǵ�ɫ�����������̸�
					float leftBorder = _Borders.x * _MainTex_TexelSize.x;
					float rightBorder = _Borders.y * _MainTex_TexelSize.x;
					float bottomBorder = _Borders.z * _MainTex_TexelSize.y;
					float topBorder = _Borders.w * _MainTex_TexelSize.y;
					
					// �жϲ������������е�λ��
					if (remappedUV.x < leftBorder || remappedUV.x > (1.0 - rightBorder) ||
						remappedUV.y < bottomBorder || remappedUV.y > (1.0 - topBorder))
					{
						// �߿�������ʾ����ɫ
						return fixed4(1, 0, 0, 1);
					}
					else
					{
						// ����������ʾ���̸�ͼ��
						float2 centerUV = remappedUV;
						centerUV.x = (centerUV.x - leftBorder) / (1.0 - leftBorder - rightBorder);
						centerUV.y = (centerUV.y - bottomBorder) / (1.0 - bottomBorder - topBorder);
						
						// �������̸�
						float checker = step(0.5, fmod(centerUV.x * 8.0, 1.0)) + 
									   step(0.5, fmod(centerUV.y * 8.0, 1.0));
						checker = fmod(checker, 2.0);
						
						return fixed4(checker, checker, 1, 1); // �������̸�
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
					
					// ��ʾ��ӳ���� UV ��Ϊ��ɫ
					// �������Կ�����ӳ���Ƿ���ȷ
					return fixed4(remappedUV.x, remappedUV.y, 0, 1);
				}

				// ������Ⱦ
				float2 remappedUV = LineRendererNineSliceRemap(i.uv);
				fixed4 texColor = tex2D(_MainTex, remappedUV);
				return texColor * i.color;
			}
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
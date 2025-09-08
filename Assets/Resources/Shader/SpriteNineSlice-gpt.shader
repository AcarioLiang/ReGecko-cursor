Shader "Custom/SpriteNineSlice_gpt"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[PerRendererData]_Color("Tint", Color) = (1,1,1,1)
		[PerRendererData]_RendererColor("Renderer Color", Color) = (1,1,1,1)
		[PerRendererData]_Flip("Flip", Vector) = (1,1,1,1)
		[MaterialToggle] PixelSnap("Pixel snap", Float) = 0
		_Borders("Borders (L,R,B,T pixels)", Vector) = (0,0,0,0)
	}

		SubShader
		{
			Tags
			{
				"Queue" = "Transparent"
				"IgnoreProjector" = "True"
				"RenderType" = "Transparent"
				"CanUseSpriteAtlas" = "True"
			}

			Cull Off
			Lighting Off
			ZWrite Off
			Blend One OneMinusSrcAlpha

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 2.0

				// ���� Sprites/Default �Ĺؼ�����
				#pragma multi_compile _ PIXELSNAP_ON
				#pragma multi_compile _ ETC1_EXTERNAL_ALPHA

				#include "UnityCG.cginc"

				sampler2D _MainTex;
				float4 _MainTex_TexelSize;	// x=1/width, y=1/height, z=width, w=height
				float4 _MainTex_ST;

				float4 _Color;
				float4 _RendererColor;
				float4 _Flip;				// �� SpriteRenderer ���ã��ɺ��Ի�ʹ��
				float  PixelSnap;

				float4 _Borders;			// (L,R,B,T) in pixels

				#ifdef ETC1_EXTERNAL_ALPHA
				sampler2D _AlphaTex;
				#endif

				struct appdata_t
				{
					float4 vertex   : POSITION;
					float4 color    : COLOR;
					float2 texcoord : TEXCOORD0;
				};

				struct v2f
				{
					float4 vertex   : SV_POSITION;
					fixed4 color : COLOR;
					float2 texcoord : TEXCOORD0;
				};

				inline float2 ApplyFlip(float2 uv, float4 flip)
				{
					// �� Sprites/Default һ�µ� Flip ֧�֣���δ�õ��ɺ���Ӱ�죩
					uv = (flip.x < 0.0) ? float2(1.0 - uv.x, uv.y) : uv;
					uv = (flip.y < 0.0) ? float2(uv.x, 1.0 - uv.y) : uv;
					return uv;
				}

				v2f vert(appdata_t v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
					// ��ѡ��֧�� Flip
					o.texcoord = ApplyFlip(o.texcoord, _Flip);
					o.color = v.color * _Color * _RendererColor;

					#ifdef PIXELSNAP_ON
					o.vertex = UnityPixelSnap(o.vertex);
					#endif
					return o;
				}

				// �� 0..1 �� uv ӳ�䵽�Ź����еĶ�Ӧ��Դ����uv��
				// _Borders �����ظ�����ʹ�� _MainTex_TexelSize תΪ��һ���ż���
				inline float2 NineSliceRemap(float2 uv)
				{
					// ��һ���ľŹ�����ֵ
					const float uL = _Borders.x * _MainTex_TexelSize.x;          // ��
					const float uR = 1.0 - _Borders.y * _MainTex_TexelSize.x;    // ��
					const float vB = _Borders.z * _MainTex_TexelSize.y;          // ��
					const float vT = 1.0 - _Borders.w * _MainTex_TexelSize.y;    // ��

					// ��ֹ����
					const float eps = 1e-5;

					float2 suv = uv;

					// X �� remap�����Σ����� / ���� / ���У�
					if (uv.x < uL)
					{
						// ���У���ʹ��Դͼ��߿� [0, uL]������ɵ�����
						float t = (uL > eps) ? saturate(uv.x / uL) : 0.0;
						suv.x = lerp(0.0, uL, t);
					}
					else if (uv.x > uR)
					{
						// ���У���ʹ��Դͼ�ұ߿� [uR, 1]
						float denom = max(1.0 - uR, eps);
						float t = saturate((uv.x - uR) / denom);
						suv.x = lerp(uR, 1.0, t);
					}
					else
					{
						// ���У���ʹ��Դͼ���� [uL, uR]
						float denom = max(uR - uL, eps);
						float t = saturate((uv.x - uL) / denom);
						suv.x = lerp(uL, uR, t);
					}

					// Y �� remap�����Σ����� / ���� / ���У�
					if (uv.y < vB)
					{
						// ���У���ʹ��Դͼ�±߿� [0, vB]
						float t = (vB > eps) ? saturate(uv.y / vB) : 0.0;
						suv.y = lerp(0.0, vB, t);
					}
					else if (uv.y > vT)
					{
						// ���У���ʹ��Դͼ�ϱ߿� [vT, 1]
						float denom = max(1.0 - vT, eps);
						float t = saturate((uv.y - vT) / denom);
						suv.y = lerp(vT, 1.0, t);
					}
					else
					{
						// ���У���ʹ��Դͼ���� [vB, vT]
						float denom = max(vT - vB, eps);
						float t = saturate((uv.y - vB) / denom);
						suv.y = lerp(vB, vT, t);
					}

					return suv;
				}

				fixed4 SampleSpriteTexture(float2 uv)
				{
					// �Ź���ӳ���Ĳ���
					float2 suv = NineSliceRemap(uv);
					fixed4 c = tex2D(_MainTex, suv);
				#ifdef ETC1_EXTERNAL_ALPHA
					// ETC1 �ⲿ Alpha ֧�֣��� Sprites/Default һ�£�
					fixed4 alpha = tex2D(_AlphaTex, suv);
					c.a = alpha.r;
				#endif
					return c;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					fixed4 c = SampleSpriteTexture(i.texcoord) * i.color;
				// Ԥ��/�����ϰ������ Blend ���ã������� Sprites/Default һ�µ�͸������
				return c;
			}
			ENDCG
		}
		}
			Fallback "Sprites/Default"
}
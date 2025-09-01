// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "KT/Blend Unlit Satellites"{
	Properties {
		_Blend("Blend", Range(0,1)) = 0
		_ShinyBlend("ShinyBlend", Range(0,1)) = 0
		_MainTex("Main Tex (Blend 0) (RGB)", 2D) = "white" {}
		_SecondTex("Second Tex (Blend 1) (RGB)", 2D) = "white" {}
		_MainShinyTex("Main Shiny Tex (Blend 0) (RGB)", 2D) = "white" {}
		_SecondShinyTex("Main Shiny Tex (Blend 0) (RGB)", 2D) = "white" {}
		_OffsetSpeedX("Offset Speed X", Range(-10, 10))= 1
		_OffsetSpeedY("Offset Speed X", Range(-10, 10))= 1
	}

	SubShader {
		Tags { "RenderType" = "Opaque" }
		LOD 100
		Lighting Off

		Pass {
			CGPROGRAM

	#pragma vertex vert
	#pragma fragment frag
	#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
			};

			fixed _OffsetSpeedX;
			fixed _OffsetSpeedY;
			half _Blend;
			half _ShinyBlend;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			sampler2D _SecondTex;
			float4 _SecondTex_ST;
			sampler2D _MainShinyTex;
			sampler2D _SecondShinyTex;


			v2f vert(appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed _X = _OffsetSpeedX * _Time;
				fixed _Y = _OffsetSpeedY * _Time;
				fixed2 offsetUVs = fixed2(_X, _Y);
				fixed4 tex1Col = tex2D(_MainTex, i.texcoord + offsetUVs);
				fixed4 tex2Col = tex2D(_SecondTex, i.texcoord + offsetUVs);
				fixed4 tex1ColShiny = tex2D(_MainShinyTex, i.texcoord + offsetUVs);
				fixed4 tex2ColShiny = tex2D(_SecondShinyTex, i.texcoord + offsetUVs);

				fixed4 c;
				c.rgba = (tex1Col * (1 - _Blend)) + (tex2Col * _Blend);
				fixed4 s;
				s.rgba = (tex1ColShiny * (1 - _Blend)) + (tex2ColShiny * _Blend);
				fixed4 r;
				r.rgba = (c * (1 - _ShinyBlend)) + (s * _ShinyBlend);

				return r;
			}
			ENDCG
		}
	}
}

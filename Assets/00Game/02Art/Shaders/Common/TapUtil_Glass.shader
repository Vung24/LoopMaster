Shader "TapUtil/Glass" {
	Properties {
		_Color ("Main Color", Vector) = (1,1,1,1)
		_NormalTex ("NormalTex", 2D) = "bump" {}
		[HDR] _SpecularColor ("SpecularColor", Vector) = (1,1,1,1)
		_MatCapTex ("MapCapTex", 2D) = "white" {}
		_RefMatCapTex ("RefMapCapTex", 2D) = "white" {}
		_ThicknessTex ("Thickness", 2D) = "white" {}
		_ReflectionIntensity ("_ReflectionIntensity", Range(0, 1)) = 0.1
		_EdgeThickness ("Edge Thickness", Range(0, 1)) = 0.2
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 _Color;

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return _Color; // RGBA
			}

			ENDHLSL
		}
	}
}
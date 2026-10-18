Shader "TapUtil/AutoPaint" {
	Properties {
		_BaseColor ("BaseColor", Vector) = (1,0.8333334,0,0)
		_NormalMap ("NormalMap", 2D) = "bump" {}
		_Metallic ("Metallic", Range(0, 1)) = 1
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
		[HDR] _fresnelcolor ("fresnelcolor", Vector) = (0.9963261,1,0.4858491,0)
		_fresnel ("fresnel", Vector) = (0,0.07,9.87,0)
		[HDR] _EmissionColor ("EmissionColor", Vector) = (0.9604942,1,0.05098039,0)
		_Emission ("Emission", Float) = 1
		_VoronoiScale ("VoronoiScale", Float) = 150
		[HideInInspector] _texcoord ("", 2D) = "white" {}
		[ToggleOff] _SpecularHighlights ("Specular Highlights", Float) = 1
		[ToggleOff] _GlossyReflections ("Reflections", Float) = 1
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
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

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
	//CustomEditor "AmplifyShaderEditor.MaterialInspector"
}
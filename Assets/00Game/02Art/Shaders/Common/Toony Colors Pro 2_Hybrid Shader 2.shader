Shader "Toony Colors Pro 2/Hybrid Shader 2" {
	Properties {
		[Enum(Front, 2, Back, 1, Both, 0)] _Cull ("Render Face", Float) = 2
		[TCP2ToggleNoKeyword] _ZWrite ("Depth Write", Float) = 1
		[Toggle(_ALPHATEST_ON)] _UseAlphaTest ("Alpha Clipping", Float) = 0
		_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
		_BaseColor ("Color", Vector) = (1,1,1,1)
		_BaseMap ("Albedo", 2D) = "white" {}
		[TCP2ColorNoAlpha] _HColor ("Highlight Color", Vector) = (1,1,1,1)
		[TCP2ColorNoAlpha] _SColor ("Shadow Color", Vector) = (0.2,0.2,0.2,1)
		[Toggle(TCP2_SHADOW_LIGHT_COLOR)] _ShadowColorLightAtten ("Main Light affects Shadow Color", Float) = 1
		[Toggle(TCP2_SHADOW_TEXTURE)] _UseShadowTexture ("Enable Shadow Albedo Texture", Float) = 0
		[NoScaleOffset] _ShadowBaseMap ("Shadow Albedo", 2D) = "gray" {}
		[TCP2MaterialKeywordEnumNoPrefix(Default,_,Crisp,TCP2_RAMP_CRISP,Bands,TCP2_RAMP_BANDS,Bands Crisp,TCP2_RAMP_BANDS_CRISP,Texture,TCP2_RAMPTEXT)] _RampType ("Ramp Type", Float) = 0
		[TCP2Gradient] _Ramp ("Ramp Texture (RGB)", 2D) = "gray" {}
		_RampScale ("Scale", Float) = 1
		_RampOffset ("Offset", Float) = 0
		[PowerSlider(0.415)] _RampThreshold ("Threshold", Range(0.01, 1)) = 0.75
		_RampSmoothing ("Smoothing", Range(0, 1)) = 0.1
		[IntRange] _RampBands ("Bands Count", Range(1, 20)) = 4
		_RampBandsSmoothing ("Bands Smoothing", Range(0, 1)) = 0.1
		[TCP2HeaderToggle(_NORMALMAP)] _UseNormalMap ("Normal Mapping", Float) = 0
		_BumpMap ("Normal Map", 2D) = "bump" {}
		_BumpScale ("Scale", Range(-1, 1)) = 1
		[TCP2HeaderToggle(TCP2_SPECULAR)] _UseSpecular ("Specular", Float) = 0
		[TCP2MaterialKeywordEnumNoPrefix(GGX,_,Stylized,TCP2_SPECULAR_STYLIZED,Crisp,TCP2_SPECULAR_CRISP)] _SpecularType ("Type", Float) = 0
		[TCP2ColorNoAlpha] [HDR] _SpecularColor ("Color", Vector) = (0.75,0.75,0.75,1)
		[PowerSlider(5.0)] _SpecularToonSize ("Size", Range(0.001, 1)) = 0.25
		_SpecularToonSmoothness ("Smoothing", Range(0, 1)) = 0.05
		_SpecularRoughness ("Roughness", Range(0, 1)) = 0.5
		[Enum(Disabled,0,Albedo Alpha,1,Custom R,2,Custom G,3,Custom B,4,Custom A,5)] _SpecularMapType ("Specular Map#Specular Map (A)", Float) = 0
		[NoScaleOffset] _SpecGlossMap ("Specular Texture", 2D) = "white" {}
		[TCP2HeaderToggle(_EMISSION)] _UseEmission ("Emission", Float) = 0
		[Enum(No Texture,5,R,0,G,1,B,2,A,3,RGB,4)] _EmissionChannel ("Texture Channel", Float) = 4
		_EmissionMap ("Texture#Texture (A)", 2D) = "white" {}
		[TCP2ColorNoAlpha(HDR)] _EmissionColor ("Color", Vector) = (1,1,0,1)
		[TCP2HeaderToggle(TCP2_RIM_LIGHTING)] _UseRim ("Rim Lighting", Float) = 0
		[TCP2ColorNoAlpha] [HDR] _RimColor ("Color", Vector) = (0.8,0.8,0.8,0.5)
		_RimMin ("Min", Range(0, 2)) = 0.5
		_RimMax ("Max", Range(0, 2)) = 1
		[Toggle(TCP2_RIM_LIGHTING_LIGHTMASK)] _UseRimLightMask ("Light-based Mask", Float) = 1
		[TCP2HeaderToggle(TCP2_MATCAP)] _UseMatCap ("MatCap", Float) = 0
		[Enum(Additive,0,Replace,1)] _MatCapType ("MatCap Blending", Float) = 0
		[NoScaleOffset] _MatCapTex ("Texture", 2D) = "black" {}
		[TCP2ColorNoAlpha] [HDR] _MatCapColor ("Color", Vector) = (1,1,1,1)
		[Toggle(TCP2_MATCAP_MASK)] _UseMatCapMask ("Enable Mask", Float) = 0
		[NoScaleOffset] _MatCapMask ("Mask Texture#Mask Texture (A)", 2D) = "black" {}
		[Enum(R,0,G,1,B,2,A,3)] _MatCapMaskChannel ("Texture Channel", Float) = 0
		_IndirectIntensity ("Strength", Range(0, 1)) = 1
		[TCP2ToggleNoKeyword] _SingleIndirectColor ("Single Indirect Color", Float) = 0
		[TCP2HeaderToggle(TCP2_REFLECTIONS)] _UseReflections ("Indirect Specular (Environment Reflections)", Float) = 0
		[TCP2ColorNoAlpha] _ReflectionColor ("Color", Vector) = (1,1,1,1)
		_ReflectionSmoothness ("Smoothness", Range(0, 1)) = 0.5
		[TCP2Enum(Disabled,0,Albedo Alpha (Smoothness),1,Custom R (Smoothness),2,Custom G (Smoothness),3,Custom B (Smoothness),4,Custom A (Smoothness),5,Albedo Alpha (Mask),6,Custom R (Mask),7,Custom G (Mask),8,Custom B (Mask),9,Custom A (Mask),10)] _ReflectionMapType ("Reflection Map", Float) = 0
		[NoScaleOffset] _ReflectionTex ("Reflection Texture#Reflection Texture (A)", 2D) = "white" {}
		[Toggle(TCP2_REFLECTIONS_FRESNEL)] _UseFresnelReflections ("Fresnel Reflections", Float) = 1
		_FresnelMin ("Fresnel Min", Range(0, 2)) = 0
		_FresnelMax ("Fresnel Max", Range(0, 2)) = 1.5
		[TCP2HeaderToggle(TCP2_OCCLUSION)] _UseOcclusion ("Occlusion", Float) = 0
		_OcclusionStrength ("Strength", Range(0, 1)) = 1
		[NoScaleOffset] _OcclusionMap ("Texture#Texture (A)", 2D) = "white" {}
		[Enum(Albedo Alpha,0,Custom R,1,Custom G,2,Custom B,3,Custom A,4)] _OcclusionChannel ("Texture Channel", Float) = 0
		[TCP2HeaderToggle] _UseOutline ("Outline", Float) = 0
		[HDR] _OutlineColor ("Color", Vector) = (0,0,0,1)
		[TCP2MaterialKeywordEnumNoPrefix(Disabled,_,Vertex Shader,TCP2_OUTLINE_TEXTURED_VERTEX,Pixel Shader,TCP2_OUTLINE_TEXTURED_FRAGMENT)] _OutlineTextureType ("Texture", Float) = 0
		_OutlineTextureLOD ("Texture LOD", Range(0, 8)) = 5
		_OutlineWidth ("Width", Range(0, 10)) = 1
		[TCP2MaterialKeywordEnumNoPrefix(Disabled,_,Constant,TCP2_OUTLINE_CONST_SIZE,Minimum,TCP2_OUTLINE_MIN_SIZE,Min Max,TCP2_OUTLINE_MIN_MAX_SIZE)] _OutlinePixelSizeType ("Pixel Size", Float) = 0
		_OutlineMinWidth ("Minimum Width (Pixels)", Float) = 1
		_OutlineMaxWidth ("Maximum Width (Pixels)", Float) = 1
		[TCP2MaterialKeywordEnumNoPrefix(Normal, _, Vertex Colors, TCP2_COLORS_AS_NORMALS, Tangents, TCP2_TANGENT_AS_NORMALS, UV1, TCP2_UV1_AS_NORMALS, UV2, TCP2_UV2_AS_NORMALS, UV3, TCP2_UV3_AS_NORMALS, UV4, TCP2_UV4_AS_NORMALS)] _NormalsSource ("Outline Normals Source", Float) = 0
		[TCP2MaterialKeywordEnumNoPrefix(Full XYZ, TCP2_UV_NORMALS_FULL, Compressed XY, _, Compressed ZW, TCP2_UV_NORMALS_ZW)] _NormalsUVType ("UV Data Type", Float) = 0
		[TCP2MaterialKeywordEnumNoPrefix(Disabled,_,Main Directional Light,TCP2_OUTLINE_LIGHTING_MAIN,All Lights,TCP2_OUTLINE_LIGHTING_ALL,Indirect Only, TCP2_OUTLINE_LIGHTING_INDIRECT)] _OutlineLightingTypeURP ("Lighting", Float) = 0
		[TCP2MaterialKeywordEnumNoPrefix(Disabled,_,Main Directional Light,TCP2_OUTLINE_LIGHTING_MAIN,Indirect Only, TCP2_OUTLINE_LIGHTING_INDIRECT)] _OutlineLightingType ("Lighting", Float) = 0
		_DirectIntensityOutline ("Direct Strength", Range(0, 1)) = 1
		_IndirectIntensityOutline ("Indirect Strength", Range(0, 1)) = 0
		[ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadowsOff ("Receive Shadows", Float) = 1
		[HideInInspector] _RenderingMode ("rendering mode", Float) = 0
		[HideInInspector] _SrcBlend ("blending source", Float) = 1
		[HideInInspector] _DstBlend ("blending destination", Float) = 0
		[HideInInspector] _UseMobileMode ("Mobile mode", Float) = 0
	}
	SubShader {
		Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
		LOD 200
		Cull [_Cull]
		ZWrite [_ZWrite]

		Pass
		{
			Tags { "LightMode" = "ForwardBase" }
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fwdbase
			#pragma shader_feature_local _RECEIVE_SHADOWS_OFF

			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"

			sampler2D _BaseMap;
			float4 _BaseMap_ST;
			fixed4 _BaseColor;
			fixed4 _HColor;
			fixed4 _SColor;
			half _RampThreshold;
			half _RampSmoothing;
			half _IndirectIntensity;
			half _ReceiveShadowsOff;
			half _UseAlphaTest;
			half _Cutoff;
			half _UseSpecular;
			half _SpecularType;
			half _SpecularRoughness;
			half _SpecularToonSize;
			half _SpecularToonSmoothness;
			half3 _SpecularColor;
            sampler2D _EmissionMap;
            half _UseEmission;
            half _EmissionChannel;
            half3 _EmissionColor;

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
				float2 uv1 : TEXCOORD1;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 worldNormal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				UNITY_LIGHTING_COORDS(3, 4)
			};

			v2f vert(appdata v)
			{
				v2f output;
				UNITY_INITIALIZE_OUTPUT(v2f, output);
				output.pos = UnityObjectToClipPos(v.vertex);
				output.uv = TRANSFORM_TEX(v.uv, _BaseMap);
				output.worldNormal = UnityObjectToWorldNormal(v.normal);
				output.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				UNITY_TRANSFER_LIGHTING(output, v.uv1);
				return output;
			}

			fixed4 frag(v2f input) : SV_Target
			{
				fixed4 albedo = tex2D(_BaseMap, input.uv) * _BaseColor;
				if (_UseAlphaTest > 0.5h) clip(albedo.a - _Cutoff);
				UNITY_LIGHT_ATTENUATION(attenuation, input, input.worldPos);
				#if defined(_RECEIVE_SHADOWS_OFF)
				attenuation = 1.0h;
				#else
				if (_ReceiveShadowsOff < 0.5h) attenuation = 1.0h;
				#endif
				half3 normal = normalize(input.worldNormal);
				half3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPos));
				half normalDotLight = saturate(dot(normal, lightDirection));
				half smoothing = max(_RampSmoothing, 0.02h);
				half toon = smoothstep(_RampThreshold - smoothing, _RampThreshold + smoothing, normalDotLight);
				half3 toonLight = lerp(_SColor.rgb, _HColor.rgb, toon) * _LightColor0.rgb * attenuation;
				half3 ambient = ShadeSH9(half4(normal, 1.0h)) * _IndirectIntensity;
				half3 lighting = max(toonLight + ambient, half3(0, 0, 0));
				half3 specular = half3(0, 0, 0);
				if (_UseSpecular > 0.5h && normalDotLight > 0)
				{
					half3 viewDirection = normalize(UnityWorldSpaceViewDir(input.worldPos));
					half3 halfDirection = normalize(lightDirection + viewDirection);
					half normalDotView = max(saturate(dot(normal, viewDirection)), 0.001h);
					half normalDotHalf = saturate(dot(normal, halfDirection));
					half viewDotHalf = saturate(dot(viewDirection, halfDirection));
					float roughness = max(_SpecularRoughness, 0.04h);
					float alpha = roughness * roughness;
					float alphaSquared = alpha * alpha;
					float denominator = normalDotHalf * normalDotHalf * (alphaSquared - 1.0) + 1.0;
					float distribution = alphaSquared / max(UNITY_PI * denominator * denominator, 0.000001);
					float k = (roughness + 1.0) * (roughness + 1.0) * 0.125;
					float visibility = normalDotView / (normalDotView * (1.0 - k) + k);
					visibility *= normalDotLight / (normalDotLight * (1.0 - k) + k);
					half3 fresnel = _SpecularColor + (1.0h - _SpecularColor) * pow(1.0h - viewDotHalf, 5.0h);
					specular = fresnel * (distribution * visibility / max(4.0h * normalDotView, 0.001h));
					if (_SpecularType > 0.5h)
					{
						half threshold = 1.0h - _SpecularToonSize;
						half smoothing = max(_SpecularToonSmoothness, 0.001h);
						half highlight = _SpecularType > 1.5h ? step(threshold, normalDotHalf) :
							smoothstep(threshold - smoothing, threshold + smoothing, normalDotHalf);
						 specular = _SpecularColor * highlight * normalDotLight;
					}
					specular *= _LightColor0.rgb * attenuation;
				}
				                half3 emission = half3(0, 0, 0);
                if (_UseEmission > 0.5h)
                {
                    half4 emissionSample = tex2D(_EmissionMap, input.uv);
                    half3 emissionMask = emissionSample.rgb;
                    if (_EmissionChannel < 0.5h) emissionMask = emissionSample.rrr;
                    else if (_EmissionChannel < 1.5h) emissionMask = emissionSample.ggg;
                    else if (_EmissionChannel < 2.5h) emissionMask = emissionSample.bbb;
                    else if (_EmissionChannel < 3.5h) emissionMask = emissionSample.aaa;
                    else if (_EmissionChannel > 4.5h) emissionMask = half3(1, 1, 1);
                    emission = emissionMask * _EmissionColor;
                }
                return fixed4(albedo.rgb * lighting + specular + emission, albedo.a);
			}

			ENDCG
		}
		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			ZWrite On
			ZTest LEqual
			CGPROGRAM
			#pragma vertex shadowVert
			#pragma fragment shadowFrag
			#pragma multi_compile_shadowcaster
			#include "UnityCG.cginc"
			sampler2D _BaseMap;
			float4 _BaseMap_ST;
			fixed4 _BaseColor;
			half _UseAlphaTest;
			half _Cutoff;
			struct shadowOutput { V2F_SHADOW_CASTER; float2 uv : TEXCOORD1; };
			shadowOutput shadowVert(appdata_base v)
			{
				shadowOutput o;
				UNITY_INITIALIZE_OUTPUT(shadowOutput, o);
				o.uv = TRANSFORM_TEX(v.texcoord, _BaseMap);
				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
				return o;
			}
			float4 shadowFrag(shadowOutput i) : SV_Target
			{
				if (_UseAlphaTest > 0.5h) clip(tex2D(_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
	//CustomEditor "ToonyColorsPro.ShaderGenerator.MaterialInspector_Hybrid"
}

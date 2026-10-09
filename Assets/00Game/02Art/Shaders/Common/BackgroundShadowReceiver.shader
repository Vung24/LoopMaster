Shader "LoopMaster/Background Shadow Receiver"
{
    Properties
    {
        _ShadowOpacity ("Shadow Opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        // Opaque depth lets Unity resolve directional shadows at the board surface.
        // Multiplicative color preserves the background already drawn by BgCamera.
        Tags { "Queue" = "Geometry-1" "RenderType" = "Opaque" }
        Blend DstColor Zero
        ColorMask RGB
        ZWrite On
        Cull Off
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            half _ShadowOpacity;
            struct appdata { float4 vertex : POSITION; float2 uv1 : TEXCOORD1; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                UNITY_LIGHTING_COORDS(1, 2)
            };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_LIGHTING(o, v.uv1);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                half shadow = UNITY_SHADOW_ATTENUATION(i, i.worldPos);
                half multiplier = lerp(1.0h, shadow, _ShadowOpacity);
                return fixed4(multiplier, multiplier, multiplier, 1);
            }
            ENDCG
        }
        // Also supplies the depth texture used by screen-space shadow resolution.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Blend Off
            ColorMask RGBA
            CGPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            struct shadowOutput { V2F_SHADOW_CASTER; };
            shadowOutput shadowVert(appdata_base v)
            {
                shadowOutput o;
                UNITY_INITIALIZE_OUTPUT(shadowOutput, o);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
            float4 shadowFrag(shadowOutput i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback Off
}

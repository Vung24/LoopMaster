Shader "Hidden/LoopMaster/RoadPreview"
{
    Properties { _Color ("Color", Color) = (1, 1, 1, 1) }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct v2f { float4 position : SV_POSITION; float3 normal : TEXCOORD0; };
            v2f vert(appdata_base input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.normal = UnityObjectToWorldNormal(input.normal);
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                float lighting = .4 + .6 * saturate(dot(normalize(input.normal), normalize(float3(-.4, 1, -.5))));
                return fixed4(_Color.rgb * lighting, 1);
            }
            ENDCG
        }
    }
}

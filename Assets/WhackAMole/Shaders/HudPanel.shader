// 화면 고정 HUD 뒤에 까는 반투명 단색 판 – 항상 맨 위(ZTest Always), 글자보다 먼저 그림
Shader "WhackAMole/HudPanel"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0.5)
    }
    SubShader
    {
        Tags { "Queue" = "Overlay-1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Lighting Off Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return _Color; }
            ENDCG
        }
    }
}

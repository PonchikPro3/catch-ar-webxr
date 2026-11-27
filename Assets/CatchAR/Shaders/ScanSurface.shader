Shader "CatchAR/Scan Surface"
{
    Properties { _Color("Tint",Color)=(1,.6,.4,.2) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 vertex:SV_POSITION; };
            fixed4 _Color;
            v2f vert(float4 v:POSITION) {v2f o;o.vertex=UnityObjectToClipPos(v);return o;}
            fixed4 frag(v2f i):SV_Target {return _Color;}
            ENDCG
        }
    }
}

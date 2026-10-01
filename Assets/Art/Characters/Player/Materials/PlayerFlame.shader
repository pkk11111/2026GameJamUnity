Shader "pawgatory/PlayerFlame"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            sampler2D _MainTex; float4 _Color;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            fixed4 frag(v2f i):SV_Target { float2 p=i.uv*2-1; float d=length(p); float edge=0.78+0.07*sin(atan2(p.y,p.x)*9+_Time.y*14); float a=1-smoothstep(edge-0.20,edge,d); float3 c=lerp(float3(1,0.22,0.02),float3(1,0.95,0.48),saturate(1-d*1.8)); return fixed4(c,a*i.color.a); }
            ENDCG
        }
    }
}

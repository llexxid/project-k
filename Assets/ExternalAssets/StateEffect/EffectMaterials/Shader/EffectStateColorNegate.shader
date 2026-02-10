Shader "EffectStateShader/ColorNegate"
{
Properties
{
[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
_Color("_Color", COLOR) = (0,0,0,1)

}

SubShader
{

Tags {"Queue" = "Transparent" "IgnoreProjector" = "true" "RenderType" = "Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
ZWrite Off Blend SrcAlpha OneMinusSrcAlpha Cull Off


Pass
{

CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma fragmentoption ARB_precision_hint_fastest
#include "UnityCG.cginc"

        struct appdata_t{
        float4 vertex   : POSITION;
        float4 color    : COLOR;
        float2 texcoord : TEXCOORD0;
        };

struct v2f
{
float2 texcoord  : TEXCOORD0;
float4 vertex   : SV_POSITION;
float4 color    : COLOR;
};

sampler2D _MainTex;
float4 _Color;

        v2f vert(appdata_t IN)
        {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
        }


        float4 vex(float4 txt, float4 color)
        {
                float3 vex = dot(txt.rgb, float3(.222, .707, .071));
                vex.rgb *= color.rgb;
                txt.rgb = lerp(txt.rgb,vex.rgb,color.a);
                return txt;
        }
        float4 frag (v2f i) : COLOR
        {   
                float4 Res,ColorRes;
                float4 _MainTex_Res;
                 _MainTex_Res = tex2D(_MainTex, i.texcoord);
                ColorRes = vex(_MainTex_Res,_Color); Res = ColorRes;
                Res.rgb *= i.color.rgb;
                Res.a = Res.a  * i.color.a;
                return Res;
        }

ENDCG
}
}
Fallback "Sprites/Default"
}

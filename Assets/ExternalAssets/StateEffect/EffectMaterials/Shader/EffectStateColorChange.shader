Shader "EffectStateShader/ChangeColor"
{
Properties
{
[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
_ChangeColor_1("_ChangeColor_1", COLOR) = (0.09398364,0.2928491,0.6037736,1)

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


struct appdata{
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
float4 _ChangeColor_1;

            v2f vert(appdata v2)
            {
                    v2f v3;
                    v3.vertex = UnityObjectToClipPos(v2.vertex);
                    v3.texcoord = v2.texcoord;
                    v3.color = v2.color;
                    return v3;
            }


            float4 ChangeColor(float4 txt, float4 color)
            {
            txt.rgb += color.rgb;
            return txt;
            }
            float4 frag (v2f i) : COLOR
            {
            float4 _MainTex_Res = tex2D(_MainTex, i.texcoord);
            float4 ChangeColor_1 = ChangeColor(_MainTex_Res,_ChangeColor_1);
            float4 Res = ChangeColor_1;
            Res.rgb *= i.color.rgb;
            Res.a = Res.a  * 1.0f * i.color.a;
            return Res;
            }

ENDCG
}
}
Fallback "Sprites/Default"
}

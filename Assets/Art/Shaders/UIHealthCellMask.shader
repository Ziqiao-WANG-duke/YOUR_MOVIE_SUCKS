// 生命值血条专用：把「满血」贴图按格遮罩，只显示前 _Filled 格，
// 底下再叠一张「空血」贴图当底 -> 得到 N/8 格的显示效果。
Shader "YourMovieSucks/UIHealthCellMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CellsStart ("Cells Start (U)", Float) = 0.291
        _CellsEnd ("Cells End (U)", Float) = 0.6924
        _CellCount ("Cell Count", Float) = 8
        _Filled ("Filled Cells", Float) = 8
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "UIHealthCellMask"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _CellsStart, _CellsEnd, _CellCount, _Filled;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;

                // 血格区域内的像素：超出 _Filled 的格子直接透明（露出底下的「空血」层）
                float u = (i.texcoord.x - _CellsStart) / max(_CellsEnd - _CellsStart, 0.0001);
                float inside = step(_CellsStart, i.texcoord.x) * step(i.texcoord.x, _CellsEnd);
                float index = floor(saturate(u) * max(_CellCount, 1.0));
                float keep = step(index + 0.5, _Filled);
                col.a *= lerp(1.0, keep, inside);

                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}

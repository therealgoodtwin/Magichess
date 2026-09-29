// Full-screen wipe drawn by ScreenWipe. _Progress 0 = nothing covered,
// 1 = whole screen covered. _Style picks the shape of the wipe.
Shader "Hidden/ScreenWipe"
{
    Properties
    {
        _Color("Color", Color) = (0, 0, 0, 1)
        _Progress("Progress", Range(0, 1)) = 0
        _Style("Style (0 Circle, 1 Diamond, 2 Blinds)", Float) = 0
        _Aspect("Aspect", Float) = 1.7778
        _MainTex("Unused", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Progress;
            float _Style;
            float _Aspect;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = (i.uv - 0.5) * float2(_Aspect, 1);
                float covered;

                if (_Style < 0.5)
                {
                    // Circle: a hole that closes down to nothing. The
                    // corner distance is the radius that covers the screen.
                    float maxRadius = length(float2(_Aspect, 1) * 0.5);
                    float radius = maxRadius * (1 - _Progress);
                    covered = step(radius, length(centered));
                }
                else if (_Style < 1.5)
                {
                    // Diamond: same idea with a Manhattan-distance shape.
                    float maxRadius = (_Aspect + 1) * 0.5;
                    float radius = maxRadius * (1 - _Progress);
                    covered = step(radius, abs(centered.x) + abs(centered.y));
                }
                else
                {
                    // Blinds: vertical bars that each grow until they meet.
                    float bar = frac(i.uv.x * 12);
                    covered = step(abs(bar - 0.5) * 2, saturate(_Progress * 1.1));
                }

                return fixed4(_Color.rgb, _Color.a * covered);
            }
            ENDCG
        }
    }
}

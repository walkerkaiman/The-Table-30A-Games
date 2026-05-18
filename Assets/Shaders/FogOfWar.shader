// FogOfWar.shader — Simple torch-illumination fog for Treasure Hunter.
// Built-In Render Pipeline, unlit transparent overlay.
//
// Concept: the mesh fills the entire dungeon. For each fragment we compute the
// minimum signed distance to any of the up-to-8 player torches. Inside a torch's
// radius the fragment is fully transparent (unfogged); outside it is opaque fog.
// A soft falloff ring between the two makes the torch edge look like lamplight.
//
// There is NO persistent "seen" memory — as soon as a player moves away, the
// previously-lit area goes dark again. This matches the design brief: each
// player's torch is their only light source.
//
// _Torches[i]: xy = world position, z = radius, w = enabled (0 or 1)
// _FogColor:   base fog colour (alpha drives the max opacity).
// _SoftEdge:   world-unit width of the soft edge falloff at the torch boundary.
Shader "TreasureHunter/FogOfWar"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0, 0, 0, 1)
        _SoftEdge ("Soft Edge Width", Float) = 0.75
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define MAX_TORCHES 8

            float4 _Torches[MAX_TORCHES]; // xy=world pos, z=radius, w=enabled
            float4 _FogColor;
            float  _SoftEdge;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 worldXY  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldXY = wp.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // signedDist < 0 inside torch, > 0 outside
                float minSigned = 1e9;

                [unroll]
                for (int t = 0; t < MAX_TORCHES; t++)
                {
                    // Branch on enabled; multiply keeps inactive slots from clobbering minSigned
                    // but we still need MAX_TORCHES considered each frame.
                    float enabled = _Torches[t].w;
                    float2 diff = i.worldXY - _Torches[t].xy;
                    float dist = sqrt(dot(diff, diff));
                    float sd = dist - _Torches[t].z;
                    // Disabled torches get large positive distance so they don't reveal anything.
                    sd = lerp(1e9, sd, enabled);
                    minSigned = min(minSigned, sd);
                }

                // t=0 right at the torch edge, t=1 at softEdge past it.
                float softEdge = max(_SoftEdge, 0.001);
                float lightT = saturate(minSigned / softEdge);
                // Also fully illuminate anywhere inside a torch (minSigned < 0).
                lightT = saturate((minSigned < 0.0) ? 0.0 : lightT);

                float fogAlpha = _FogColor.a * lightT;
                return fixed4(_FogColor.rgb, fogAlpha);
            }
            ENDCG
        }
    }
}

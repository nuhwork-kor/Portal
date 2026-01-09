Shader "Portal/StencilMask"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-10" "RenderType"="Opaque" }

        Pass
        {
            Name "StencilMask"
            ZWrite Off
            ZTest LEqual
            ColorMask 0

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }
        }
    }
}

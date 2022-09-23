/*
 *	Created by:  Peter @sHTiF Stefcek
 */

Shader "BinaryEgo/VoxelInstancedIndirect"
{
    Properties
    {
        _AmbientLight ("Ambient Light", Color) = (0,0,0)
        
        [Toggle(ENABLE_CULLING)]_EnableCulling("Enable Culling", Float) = 0
        [HideInInspector]_BoundSize("_BoundSize", Vector) = (1,1,0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline"}

        Pass
        {
            Cull Back
            ZTest Less
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #pragma multi_compile _ ENABLE_CULLING

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                half3 normalOS      : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                nointerpolation  half3 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float2 _BoundSize;
                float3 _AmbientLight;

                StructuredBuffer<float4> _colorBuffer;
                StructuredBuffer<float4x4> _matrixBuffer;
                StructuredBuffer<uint> _visibilityBuffer;
            CBUFFER_END

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

#if CULLING
                float4x4 instanceMatrix = _matrixBuffer[_visibilityBuffer[instanceID]];
#else
                float4x4 instanceMatrix = _matrixBuffer[instanceID];
#endif
                
                float3 positionWS = mul(instanceMatrix, IN.positionOS);
                half3 normalWS = normalize(mul(IN.normalOS, (float3x3)Inverse(instanceMatrix)));
                
                OUT.positionCS = TransformWorldToHClip(positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));

#if CULLING
                half3 albedo = _colorBuffer[_visibilityBuffer[instanceID]];
#else
                half3 albedo = _colorBuffer[instanceID];
#endif                

                half directDiffuse = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation * directDiffuse;

                OUT.color = (lighting + _AmbientLight) * albedo;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(IN.color,1);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            
            Cull Off
            Blend One Zero
            ZTest LEqual
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature CULLING
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                half3 normalOS      : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float2 _BoundSize;

                StructuredBuffer<float4> _colorBuffer;
                StructuredBuffer<float4x4> _matrixBuffer;
                StructuredBuffer<uint> _visibilityBuffer;
            CBUFFER_END

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

#if CULLING
                float4x4 instanceMatrix = _matrixBuffer[_visibilityBuffer[instanceID]];
#else
                float4x4 instanceMatrix = _matrixBuffer[instanceID];
#endif
                
                float3 positionWS = mul(instanceMatrix, IN.positionOS);
                
                OUT.positionCS = TransformWorldToHClip(positionWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return 1;
            }
            ENDHLSL
        }
    }
}
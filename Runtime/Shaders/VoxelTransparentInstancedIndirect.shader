/*
 *	Created by:  Peter @sHTiF Stefcek
 */

Shader "BinaryEgo/Voxelizer/VoxelTransparentInstancedIndirect"
{
    Properties
    {
        _AmbientLight ("Ambient Light", Color) = (0,0,0)
        _MainTex("Main Texture", 2D) = "white" {}
        _VoxelScale("Voxel Scale", Float) = 1
        
        [Toggle(ENABLE_CULLING)]_EnableCulling("Enable Culling", Float) = 0
        [Toggle(ENABLE_TEXTURE)]_EnableTexture("Enable Texture", Float) = 0
        [Toggle(ENABLE_BILLBOARD)] _EnableBillboard ("Enable Billboard", Float) = 0
        [HideInInspector]_BoundSize("_BoundSize", Vector) = (1,1,0)
    }

    SubShader
    {
        Tags { 
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalRenderPipeline"
            "Queue"          = "Transparent"
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            // #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #pragma multi_compile _ ENABLE_CULLING
            #pragma multi_compile _ ENABLE_TEXTURE
            #pragma multi_compile _ ENABLE_BILLBOARD

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct VertexInput
            {
                float4 positionOS   : POSITION;
                half3 normalOS      : NORMAL;
                float2 texcoord : TEXCOORD0;
            };

            struct FragmentInput
            {
                float4 positionCS  : SV_POSITION;
                nointerpolation  half3 color        : COLOR;
                float2 texcoord    : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float2 _BoundSize;
                float3 _AmbientLight;
                float _VoxelScale;
                TEXTURE2D(_MainTex);
                SAMPLER(sampler_MainTex);
                float4 _MainTex_ST;

                StructuredBuffer<float4> _colorBuffer;
                StructuredBuffer<float4x4> _matrixBuffer;
                StructuredBuffer<uint> _visibilityBuffer;
            CBUFFER_END

            FragmentInput vert(VertexInput IN, uint instanceID : SV_InstanceID)
            {
                FragmentInput OUT;

#if CULLING
                float4x4 instanceMatrix = _matrixBuffer[_visibilityBuffer[instanceID]];
#else
                float4x4 instanceMatrix = _matrixBuffer[instanceID];
#endif

                float4 position = float4(IN.positionOS.x*_VoxelScale, IN.positionOS.y*_VoxelScale, IN.positionOS.z*_VoxelScale, IN.positionOS.w);
                
#if ENABLE_BILLBOARD
                float4x4 v = unity_WorldToCamera;
                float3 right = normalize(v._m00_m01_m02);
                float3 up = normalize(v._m10_m11_m12);
                float3 forward = normalize(v._m20_m21_m22);
                float4x4 rotationMatrix = float4x4(right, 0,
    	            up, 0,
    	            forward, 0,
    	            0, 0, 0, 1);
                float4x4 rotationMatrixInverse = transpose(rotationMatrix);
                
                position = mul(rotationMatrixInverse, position);
#endif
                float3 positionWS = mul(instanceMatrix, position).xyz;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                
                half3 normalWS = normalize(mul(IN.normalOS, (float3x3)Inverse(instanceMatrix)));
#if CULLING
                half3 albedo = _colorBuffer[_visibilityBuffer[instanceID]].xyz;
#else
                half3 albedo = _colorBuffer[instanceID].xyz;
#endif                
                
#if ENABLE_BILLBOARD
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(instanceMatrix._m03_m13_m23));
                half3 lighting =  mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
#else
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half directDiffuse = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation * directDiffuse;
#endif

                OUT.color = (lighting + _AmbientLight) * albedo;
                OUT.texcoord    = IN.texcoord;

                return OUT;
            }

            half4 frag(FragmentInput IN) : SV_Target
            {
#if ENABLE_TEXTURE
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.texcoord * _MainTex_ST.xy + _MainTex_ST.zw);
                half3 color = texColor * IN.color;
                
                return half4(color, texColor.a);
#else
                return half4(IN.color, 1);
#endif
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
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma multi_compile _ ENABLE_BILLBOARD

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
            float _VoxelScale;

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
                
                float4 position = float4(IN.positionOS.x*_VoxelScale, IN.positionOS.y*_VoxelScale, IN.positionOS.z*_VoxelScale, IN.positionOS.w);

#if ENABLE_BILLBOARD
                float4x4 v = unity_WorldToCamera;
                float3 right = normalize(v._m00_m01_m02);
                float3 up = normalize(v._m10_m11_m12);
                float3 forward = normalize(v._m20_m21_m22);
                float4x4 rotationMatrix = float4x4(right, 0,
    	            up, 0,
    	            forward, 0,
    	            0, 0, 0, 1);
                float4x4 rotationMatrixInverse = transpose(rotationMatrix);
                
                position = mul(rotationMatrixInverse, position);

                float3 positionWS = instanceMatrix._m03_m13_m23;
#else
                float3 positionWS = mul(instanceMatrix, position).xyz;
#endif
                                
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
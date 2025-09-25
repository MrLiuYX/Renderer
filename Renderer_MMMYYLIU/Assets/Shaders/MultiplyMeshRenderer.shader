Shader "Unlit/MultiplyMeshRenderer"
{
    Properties
    {
        _DiffuseAtlas ("Diffuse Atlas", 2DArray) = "" {}
        _MatrixAtlas ("Martix", 2DArray) = "" {}
        _DataTex ("DataTex", 2D) = "white" {}
        _pixelSize ("Pixel Size", float) = 0
        _AnimationTime("AnimationTime", int) = 0
        _instanceIdOffset("InstanceIdOffset", int) = 0
    }
    SubShader
    {
        Pass
        {
            //blend SrcAlpha OneMinusSrcAlpha
            Name "MultiplyMeshRenderer"
            ZTest less
            ZWrite on
			HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma multi_compile_instancing
            // #pragma instancing_options procedural:setup 
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

             struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : Normal;
                float4 uv_vid_diffusePage : TEXCOORD1;
                float4 boneWeights : TEXCOORD2;
                float4 bonesIndexs : TEXCOORD3;
                float4 diffuseRect : TEXCOORD4;
            };

            struct v2f
            {
            	float4 vertex : SV_POSITION;
                float3 uv : TEXCOORD5;
                float3 normal : TEXCOORD6;
                int debug : TEXCOORD7;
            };
            
            //C#
            float _pixelSize;

            TEXTURE2D_ARRAY(_DiffuseAtlas);
            SAMPLER(sampler_DiffuseAtlas);

            sampler2D _DataTex;
            float4 _DataTex_TexelSize;

            TEXTURE2D_ARRAY(_MatrixAtlas);
            SAMPLER(sampler_MatrixAtlas);

            int _AnimationTime;
            int _instanceIdOffset;

            float DegreesToRadians(float degrees)
            {
                return degrees * PI / 180.0;
            }

            // void setup()
            // {

            // }

            float4x4 GetBoneMatrix(int4 matrixUV, float matrixPage, float boneIndex, float animationRow)
            {
                uint texW, texH, layers;
                _MatrixAtlas.GetDimensions(texW, texH, layers);
                float invW = 1.0 / texW;
                float invH = 1.0 / texH;
                int   slice = (int)matrixPage;

                int x0Pix = matrixUV.x + (boneIndex * 4.0 + 0.0);
                int x1Pix = matrixUV.x + (boneIndex * 4.0 + 1.0);
                int x2Pix = matrixUV.x + (boneIndex * 4.0 + 2.0);
                int x3Pix = matrixUV.x + (boneIndex * 4.0 + 3.0);
                int y = animationRow % matrixUV.w;

                float4 r0 = SAMPLE_TEXTURE2D_ARRAY_LOD(_MatrixAtlas, sampler_MatrixAtlas, float2(x0Pix * invW+invW / 2, y * invH + invH / 2), slice, 0);
                float4 r1 = SAMPLE_TEXTURE2D_ARRAY_LOD(_MatrixAtlas, sampler_MatrixAtlas, float2(x1Pix * invW+invW / 2, y * invH + invH / 2), slice, 0);
                float4 r2 = SAMPLE_TEXTURE2D_ARRAY_LOD(_MatrixAtlas, sampler_MatrixAtlas, float2(x2Pix * invW+invW / 2, y * invH + invH / 2), slice, 0);
                float4 r3 = SAMPLE_TEXTURE2D_ARRAY_LOD(_MatrixAtlas, sampler_MatrixAtlas, float2(x3Pix * invW+invW / 2, y * invH + invH / 2), slice, 0);

                return float4x4(r0,r1,r2,r3);
            }

            v2f vert (appdata v, uint id : SV_InstanceID)
            {
                v2f o;
                int debugInstanceId = id + _instanceIdOffset;
                id += _instanceIdOffset;

                id *= _pixelSize;
                float vid = v.uv_vid_diffusePage.z;

                float normalizeLen = _DataTex_TexelSize.x;
                //Pos Vstart
                float4 data1 = tex2Dlod(_DataTex, float4(id % _DataTex_TexelSize.w, id / _DataTex_TexelSize.w, 0, 0) * normalizeLen);    
                
                //Rot Vend
                float4 data2 = tex2Dlod(_DataTex, float4((id + 1) % _DataTex_TexelSize.w, (id + 1) / _DataTex_TexelSize.w, 0, 0) * normalizeLen);  
                data2.xyz = data2.xyz * PI / 180;
                
                if (vid < data1.w || vid >= data2.w)
                {
                    o.vertex = 0;
                    return o;
                }
                
                //Scale Show
                float4 data3 = tex2Dlod(_DataTex, float4((id + 2) % _DataTex_TexelSize.w, (id + 2) / _DataTex_TexelSize.w, 0, 0) * normalizeLen);  
                
                if (data3.w < 0.5)
                {
                    o.vertex = 0;
                    return o;
                }

                //MatrixUV
                int4 data4 = tex2Dlod(_DataTex, float4((id + 3) % (int)_DataTex_TexelSize.w, (id + 3) / (int)_DataTex_TexelSize.w, 0, 0) * normalizeLen);  

                //MatrixPage AnimationRow None None
                float4 data5 = tex2Dlod(_DataTex, float4((id + 4) % _DataTex_TexelSize.w, (id + 4) / _DataTex_TexelSize.w, 0, 0) * normalizeLen);  

                float4 skinnedPos = float4(0, 0, 0, 0);
                float3 skinnedNormal = float3(0, 0, 0);
                float4 pos = v.vertex;
                float3 normal = v.normal;
    
                float4x4 boneMatrix;
                //Bone 0
                 //data5.y = _AnimationTime;
                boneMatrix = GetBoneMatrix(data4, data5.x, v.bonesIndexs.x, data5.y);
                skinnedPos += mul(boneMatrix, pos) * v.boneWeights.x;
                skinnedNormal += mul((float3x3)boneMatrix, normal) * v.boneWeights.x;

                //Bone 1
                boneMatrix = GetBoneMatrix(data4, data5.x, v.bonesIndexs.y, data5.y);
                skinnedPos += mul(boneMatrix, pos) * v.boneWeights.y;
                skinnedNormal += mul((float3x3)boneMatrix, normal) * v.boneWeights.y;

                //Bone 2
				boneMatrix = GetBoneMatrix(data4, data5.x, v.bonesIndexs.z, data5.y);
				skinnedPos += mul(boneMatrix, pos) * v.boneWeights.z;
				skinnedNormal += mul((float3x3)boneMatrix, normal) * v.boneWeights.z;

                //Bone 3
                boneMatrix = GetBoneMatrix(data4, data5.x, v.bonesIndexs.w, data5.y);
                skinnedPos += mul(boneMatrix, pos) * v.boneWeights.w;
                skinnedNormal += mul((float3x3)boneMatrix, normal) * v.boneWeights.w;
                
                float4 vertex = skinnedPos;
                //vertex = v.vertex;
                normal = skinnedNormal;

                //�ߴ�
                float4x4 M_Scale = float4x4
                   (
                       data3.x,0,0,0,
                       0,data3.y,0,0,
                       0,0,data3.z,0,
                       0,0,0,1
                   );
                vertex = mul(M_Scale,vertex);

                //��ת
                float4x4 M_rotateX = float4x4
                   (
                   1,0,0,0,
                   0,cos(data2.x),-sin(data2.x),0,
                   0,sin(data2.x),cos(data2.x),0,
                   0,0,0,1
                   );
                float4x4 M_rotateY = float4x4
                   (
                   cos(data2.y),0,sin(data2.y),0,
                   0,1,0,0,
                   -sin(data2.y),0,cos(data2.y),0,
                   0,0,0,1
                   );
                float4x4 M_rotateZ = float4x4
                   (
                       cos(data2.z),-sin(data2.z),0,0,
                       sin(data2.z),cos(data2.z),0,0,
                       0,0,1,0,
                       0,0,0,1
                   );

                vertex = mul(M_rotateX,vertex);
                vertex = mul(M_rotateY,vertex);
                vertex = mul(M_rotateZ,vertex);

                float4 worldPos = float4(TransformObjectToWorld(vertex + data1.xyz), 0);
                o.vertex = TransformWorldToHClip(worldPos);

                normal = mul((float3x3)M_rotateX, normal);
                normal = mul((float3x3)M_rotateY, normal);
                normal = mul((float3x3)M_rotateZ, normal);
                normal = normalize(normal);  

                uint width, height, layers;
                _DiffuseAtlas.GetDimensions(width, height, layers);
                float uvXStart = v.diffuseRect.x / width;
                float uvYStart = v.diffuseRect.y / height;
                float uvXLength = v.diffuseRect.z / width;
                float uvYLength = v.diffuseRect.w / height;

                o.uv = float3(
                    uvXStart + v.uv_vid_diffusePage.x * uvXLength
                    , uvYStart + v.uv_vid_diffusePage.y * uvYLength
                    ,  v.uv_vid_diffusePage.w);

                o.normal = normal;
                o.debug = debugInstanceId;
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {   
                half4 col = _DiffuseAtlas.Sample(sampler_DiffuseAtlas, i.uv);
                Light mainLight = GetMainLight();
                float3 dir = normalize(mainLight.direction);
                col = col * max(0.2, dot(dir, i.normal));   
                return col;
                // return 1;
                // return i.debug == 56 ? half4(1,0,0,0.5) : half4(half3(1,1,1), 0.5);
                // return i.debug == 1023 ? half4(1,0,0,0.5) : half4(half3(1,1,1), 0.5);
            }
            ENDHLSL
        }
    }
}

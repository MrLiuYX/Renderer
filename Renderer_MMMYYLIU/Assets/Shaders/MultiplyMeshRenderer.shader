Shader "Unlit/MultiplyMeshRenderer"
{
    Properties
    {
        _DiffuseAtlas ("Diffuse Atlas", 2DArray) = "" {}
        _DataTex ("DataTex", 2D) = "white" {}
        _pixelSize ("Pixel Size", float) = 0
    }
    SubShader
    {
        Pass
        {
            // Tags{"RenderType" = "Transparent" "Queue"="Transparent" }    
            // Blend  SrcAlpha OneMinusSrcAlpha
            Name "MultiplyMeshRenderer"
            ZTest less
            ZWrite on
			HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma multi_compile_instancing 
            #define PI 3.14159265359
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : Normal;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
            	float4 vertex : SV_POSITION;
                float3 uv : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float debug : TEXCOORD6;
            };
            
            //C#
            float _pixelSize;
            TEXTURE2D_ARRAY(_DiffuseAtlas);
            SAMPLER(sampler_DiffuseAtlas);
            sampler2D _DataTex;
            float4 _DataTex_TexelSize;


            float DegreesToRadians(float degrees)
            {
                return degrees * PI / 180.0;
            }


            v2f vert (appdata v, uint id : SV_INSTANCEID, uint vid : SV_VertexID)
            {
                v2f o;

                //三个像素点
                id *= _pixelSize;

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
                
                //Color pageIndex
                float4 data4 = tex2Dlod(_DataTex, float4((id + 3) % _DataTex_TexelSize.w, (id + 3) / _DataTex_TexelSize.w, 0, 0) * normalizeLen); 

                //DiffuseUV
                float4 data5 = tex2Dlod(_DataTex, float4((id + 4) % _DataTex_TexelSize.w, (id + 4) / _DataTex_TexelSize.w, 0, 0) * normalizeLen); 
                
                float4 vertex = v.vertex;

                //尺寸
                float4x4 M_Scale = float4x4
                   (
                       data3.x,0,0,0,
                       0,data3.y,0,0,
                       0,0,data3.z,0,
                       0,0,0,1
                   );
                vertex = mul(M_Scale,vertex);

                //旋转
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

                float4 worldPos = float4(TransformObjectToWorld(vertex + float4(data1.xyz, 0)), 0);
                o.vertex = TransformWorldToHClip(worldPos);

                 // 需要同样旋转法线
                float3 normal = v.normal;
                normal = mul((float3x3)M_rotateX, normal);
                normal = mul((float3x3)M_rotateY, normal);
                normal = mul((float3x3)M_rotateZ, normal);
                normal = normalize(normal);  // 确保法线归一化

                uint width, height, layers;
                _DiffuseAtlas.GetDimensions(width, height, layers);

                float uvXStart = data5.x / width;
                float uvYStart = data5.y / width;
                float uvXLength = data5.z / width;
                float uvYLength = data5.w / width;

                o.uv = float3(
                    uvXStart + v.uv.x * uvXLength
                    , uvYStart + v.uv.y * uvYLength
                    ,  data4.w);

                o.normal = normal;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {   
                half4 col = _DiffuseAtlas.Sample(sampler_DiffuseAtlas, i.uv);
                Light mainLight = GetMainLight();
                float3 dir = normalize(mainLight.direction);
                col = col * max(0.2, dot(dir, i.normal));   
                return col;
            }
            ENDHLSL
        }
    }
}

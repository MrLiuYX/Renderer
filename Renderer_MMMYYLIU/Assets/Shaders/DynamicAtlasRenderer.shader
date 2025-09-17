Shader "Unlit/DynamicAtlasRenderer"
{
    Properties
    {
        _DataTex ("DataTex", 2D) = "white" {}
        _dynamicAtlas ("Dynamic Atlas", 2DArray) = "" {}
        _dynamicSize ("Dynamic Atlas Size", float) = 0
        _pixelSize ("Pixel Size", float) = 0
    }
    SubShader
    {
        Pass
        {
            Tags{"RenderType" = "Transparent" "Queue"="Transparent" }    
            Blend  SrcAlpha OneMinusSrcAlpha
            Name "DynamicAtlasRenderer"
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float3 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 debug : TEXCOORD1;
                float3 color : TEXCOORD2;
            };

            //C#
            float _pixelSize;
            float _dynamicSize;
            TEXTURE2D_ARRAY(_dynamicAtlas);
            SAMPLER(sampler_dynamicAtlas);

            //Shader
            sampler2D _DataTex;            
            float4 _DataTex_TexelSize;

            float DegreesToRadians(float degrees)
            {
                return degrees * PI / 180.0;
            }


            v2f vert (appdata v, uint id : SV_INSTANCEID)
            {
                v2f o;

                //三个像素点
                id *= _pixelSize;
                float normalizeLen = _DataTex_TexelSize.x;
                //Pos uvx
                float4 data1 = tex2Dlod(_DataTex, float4(id % _DataTex_TexelSize.w, id / _DataTex_TexelSize.w, 0, 0) * normalizeLen);    
                
                //Rot uvy
                float4 data2 = tex2Dlod(_DataTex, float4((id + 1) % _DataTex_TexelSize.w, (id + 1) / _DataTex_TexelSize.w, 0, 0) * normalizeLen);  
                data2.xyz = data2.xyz * PI / 180;
                
                //Scale width
                float4 data3 = tex2Dlod(_DataTex, float4((id + 2) % _DataTex_TexelSize.w, (id + 2) / _DataTex_TexelSize.w, 0, 0) * normalizeLen);  
                
                //Color height
                float4 data4 = tex2Dlod(_DataTex, float4((id + 3) % _DataTex_TexelSize.w, (id + 3) / _DataTex_TexelSize.w, 0, 0) * normalizeLen); 
                
                //TextureIndex IsShow None None
                float4 data5 = tex2Dlod(_DataTex, float4((id + 4) % _DataTex_TexelSize.w, (id + 4) / _DataTex_TexelSize.w, 0, 0) * normalizeLen);   
                o.debug = data5;
                if(data5.y < 0.5)
                {
                    return o;
                }
                
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

                //  // 需要同样旋转法线
                //float3 normal = v.normal;
                //normal = mul((float3x3)M_rotateX, normal);
                //normal = mul((float3x3)M_rotateY, normal);
                //normal = mul((float3x3)M_rotateZ, normal);
                //normal = normalize(normal);  // 确保法线归一化

                float4 worldPos = float4(TransformObjectToWorld(vertex + float4(data1.xyz, 0)), 0);
                o.vertex = TransformWorldToHClip(worldPos);

                float uvXStart = data1.w / _dynamicSize;
                float uvYStart = data2.w / _dynamicSize;
                float uvXLength = data3.w / _dynamicSize;
                float uvYLength = data4.w / _dynamicSize;

                o.uv = float3(
                    uvXStart + v.uv.x * uvXLength
                    , uvYStart + v.uv.y * uvYLength
                    , data5.x);
                o.color = data4.xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {   
                if(i.debug.y < 0.5)
                {
                    discard;
                    return 0;
                }

                half4 col = _dynamicAtlas.Sample(sampler_dynamicAtlas, i.uv);
                if(col.a < 0.5) discard;
                col *= half4(i.color, 1);
                return col;
            }
            ENDHLSL
        }
    }
}

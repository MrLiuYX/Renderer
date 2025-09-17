
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct MultiplyMeshRendererCommonData : IMultiplyMeshRendererData
{
    public float3 Position;
    public float VertexStartIndex;
    public float3 Rotation;
    public float VertexEndIndex;
    public float3 Scale;
    public float IsShow;
    public float3 Color;
    public float PageIndex;
    public float4 DiffuseUV;

    public void SetActive(bool state)
    {
        IsShow = state ? 1 : 0;
    }

    public void SetColor(float3 color)
    {
        Color = color;
    }

    public void SetDiffusePageIndex(int index)
    {
        PageIndex = index;
    }

    public void SetDiffuseRect(Rect rect)
    {
        DiffuseUV = rect.ToFloat4();
    }

    public void SetPosition(float3 pos)
    {
        Position = pos;
    }

    public void SetRotation(float3 rot)
    {
         Rotation = rot;
    }

    public void SetScale(float3 scale)
    {
        Scale = scale;  
    }

    public void SetVertexEnd(int index)
    {
        VertexEndIndex = index;
    }

    public void SetVertexStart(int index)
    {
        VertexStartIndex = index;
    }
}
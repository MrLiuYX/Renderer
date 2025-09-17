using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct DynamicAtlasRendererCommonData : IDynamicAtlasRendererData
{
    public float3 Position;
    public float UVX;

    public float3 Rotation;
    public float UVY;

    public float3 Scale;
    public float UVWidth;

    public float3 Color;
    public float UVHeight;

    public float TexIndex;
    public float IsShow;    //0 hide 1show
    public float2 None;

    public void SetActive(bool s)
    {
        IsShow = s ? 1 : 0;
    }

    public void SetColor(float3 color)
    {
        Color = color;
    }

    public void SetPageIndex(int index)
    {
        TexIndex = index;
    }

    public void SetPosition(float3 pos)
    {
        Position = pos;
    }

    public void SetRect(Rect rect)
    {
        UVX = rect.x;
        UVY = rect.y;
        UVWidth = rect.width;
        UVHeight = rect.height;
    }

    public void SetRotation(float3 rot)
    {
        Rotation = rot;
    }

    public void SetScale(float3 scale)
    {
        Scale = scale;
    }
}
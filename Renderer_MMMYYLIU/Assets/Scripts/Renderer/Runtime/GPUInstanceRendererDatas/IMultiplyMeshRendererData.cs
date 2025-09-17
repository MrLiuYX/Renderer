using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public interface IMultiplyMeshRendererData : IGPUInstanceRendererData
{
    public void SetVertexStart(int index);

    public void SetVertexEnd(int index);

    public void SetDiffuseRect(Rect rect);

    public void SetDiffusePageIndex(int index);
}
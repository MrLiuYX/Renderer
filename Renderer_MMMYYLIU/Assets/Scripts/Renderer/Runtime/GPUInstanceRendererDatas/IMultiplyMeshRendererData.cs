using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public interface IMultiplyMeshRendererData : IGPUInstanceRendererData
{
    public void SetMartixRect(Rect rect);

    public void SetMatrixPage(int page);

    public void SetVertexStart(int index);

    public void SetVertexEnd(int index);
}
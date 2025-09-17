using Unity.Mathematics;
using UnityEngine;

public interface IGPUInstanceRendererData
{
    public void SetActive(bool state);
    public void SetColor(float3 color);
    public void SetPosition(float3 pos);

    /// <summary>
    /// 设置旋转
    /// </summary>
    /// <param name="rot">欧拉角</param>
    public void SetRotation(float3 rot);

    public void SetScale(float3 scale);
}
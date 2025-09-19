using System;
using UnityEngine;

[Serializable]
public struct EntityAnimationConfigComponentData
{
    /// <summary>
    /// 贴图宽度 其实用不到
    /// </summary>
    [SerializeField]
    public int Width;
    /// <summary>
    /// 贴图长度 
    /// </summary>
    [SerializeField]
    public int Height;
}
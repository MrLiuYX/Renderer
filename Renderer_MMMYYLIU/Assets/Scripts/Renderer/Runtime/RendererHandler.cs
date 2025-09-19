
using Native;

/// <summary>
/// 单个渲染控制器
/// </summary>
public struct RendererHandler<T> where T : unmanaged, IGPUInstanceRendererData
{
    public int Index;
    public T Data;

    public static RendererHandler<T> Create()
    {
        var instance = new RendererHandler<T>();
        return instance;
    }
}
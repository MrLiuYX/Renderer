
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class Extension
{

    public static float4 ToFloat4(this Rect rect)
    {
        return new float4(rect.x, rect.y, rect.width, rect.height);
    }

    public static T AddOrGetComponent<T>(this GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
        {
            component = go.AddComponent<T>();
        }
        return component;
    }

    public static bool SizeCompare(this Vector2 size, Vector2 compareSize)
    {
        return size.x >= compareSize.x && size.y >= compareSize.y;
    }

    public static float Area(this Vector2 size)
    {
        return size.x * size.y;
    }

    public static void UniqueEnqueue(this Queue<int> queue, int num)
    {
        if (queue.Contains(num)) return;
        queue.Enqueue(num);
    }
}
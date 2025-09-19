using Native;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

public delegate void OnDynamicAtlasTextureChanged();
public class DynamicAtlas : IReference
{
    public const int MaxSize = 8192;
    private int InitSize = 512;
    private TextureFormat _format;
    private int _page = 0;
    private Texture2D[] _atlasTextures;
    private Dictionary<int, List<Rect>> _totalRect;
    private Dictionary<int, int> _size;
    private int _maxSize;
    private event OnDynamicAtlasTextureChanged _events;
    private Texture2DArray _texArray;
    private Dictionary<Texture2D, (Rect, int)> _textureDict;

    public int CurrentSize => _size[0];

    public static DynamicAtlas Create(int size = 512, TextureFormat format = TextureFormat.RGBA32)
    {
        var instance = ReferencePool.Acquire<DynamicAtlas>();
        instance._totalRect = DictionaryPool<int, List<Rect>>.Get();
        instance._size = DictionaryPool<int, int>.Get();
        instance._textureDict = DictionaryPool<Texture2D, (Rect, int)>.Get();
        instance.InitSize = size;
        instance._format = format;
        instance.CreateNewPage();
        return instance;
    }

    public void Clear()
    {
        foreach (var kv in _totalRect)
        {
            ListPool<Rect>.Release(kv.Value);
        }
        DictionaryPool<int, List<Rect>>.Release(_totalRect);
        DictionaryPool<int, int>.Release(_size);
        _events = null;
        GameObject.Destroy(_texArray);
        for (int i = 0; i < _atlasTextures.Length; i++)
        {
            GameObject.Destroy(_atlasTextures[i]);
        }
        DictionaryPool<Texture2D, (Rect, int)>.Release(_textureDict);
    }

    private bool CreateNewPage()
    {
        _page++;
        var currentIndex = _page - 1;
        _totalRect.Add(currentIndex, ListPool<Rect>.Get());
        _size.Add(currentIndex, math.max(InitSize, _maxSize));
        var tempTextures = new Texture2D[_page];
        for (int i = 0; i < currentIndex; i++)
        {
            tempTextures[i] = _atlasTextures[i];
        }
        tempTextures[currentIndex] = new Texture2D(math.max(InitSize, _maxSize), math.max(InitSize, _maxSize), _format, false);
        tempTextures[currentIndex].name = "DynamiceAtlas" + currentIndex;
        tempTextures[currentIndex].filterMode = FilterMode.Point;
        _atlasTextures = tempTextures;
        _totalRect[currentIndex].Add(new Rect(0, 0, math.max(InitSize, _maxSize), math.max(InitSize, _maxSize)));
        _maxSize = math.max(tempTextures[tempTextures.Length - 1].width, _maxSize);
        return true;
    }

    // 将新贴图添加到图集中，并返回UV坐标
    public bool AddTextureToAtlas(Texture2D newTexture, out Rect rect, out int pageIndex)
    {
        if(newTexture == null)
        {
            rect = new Rect();  
            pageIndex = 0;
            return false;
        }

        if(_textureDict.TryGetValue(newTexture, out var data))
        {
            rect = data.Item1;
            pageIndex = data.Item2;
            return true;
        }

        Vector2 texelSize = new Vector2(newTexture.width, newTexture.height);
        rect = new Rect();
        pageIndex = 0;
        if (texelSize.x >= MaxSize || texelSize.y >= MaxSize)
        {
            Debug.LogError($"Texture size is too large, tex:{newTexture.name}");
            return false;
        }

        var rectIndex = -1;
        var textureIndex = -1;
        //寻找空位
        foreach (var kv in _totalRect)
        {
            var rectArea = float.MaxValue;  // 找一个最小的面积填充
            for (int i = 0; i < kv.Value.Count; i++)
            {
                rect = kv.Value[i];

                if (rect.size.SizeCompare(texelSize) && rect.size.Area() < rectArea)
                {
                    rectIndex = i;
                    rectArea = rect.size.Area();
                }
            }

            if (rectIndex != -1)
            {
                textureIndex = kv.Key;
                break;
            }
        }

        if (textureIndex == -1 || rectIndex == -1)
        {
            if (!Expansion())
            {
                Debug.LogError("Can't Expansion");
                return false;
            }
            return AddTextureToAtlas(newTexture, out rect, out pageIndex);
        }

        var splitRect = _totalRect[textureIndex][rectIndex];
        _totalRect[textureIndex].RemoveAt(rectIndex);

        // 右边
        _totalRect[textureIndex].Add(new Rect(
            splitRect.x + texelSize.x
            , splitRect.y
            , splitRect.width - texelSize.x
            , texelSize.y));

        //右上
        _totalRect[textureIndex].Add(new Rect(
            splitRect.x + texelSize.x
            , splitRect.y + texelSize.y
            , splitRect.width - texelSize.x
            , splitRect.height - texelSize.y));

        //上
        _totalRect[textureIndex].Add(new Rect(
            splitRect.x
            , splitRect.y + texelSize.y
            , texelSize.x
            , splitRect.height - texelSize.y));

        // 图像压入操作开始
        
        var destX = (int)splitRect.x;
        var destY = (int)splitRect.y;
        var srcWidth = newTexture.width;
        var srcHeight = newTexture.height;
        var targetTexture = _atlasTextures[textureIndex];

        if (_format == TextureFormat.RGBAHalf || _format == TextureFormat.RGBAFloat)
        {
            if(_format == TextureFormat.RGBAHalf)
            {
                // 高精度纹理使用GetPixelData和SetPixelData
                var srcData = newTexture.GetPixelData<half4>(0);
                var dstData = targetTexture.GetPixelData<half4>(0);

                for (int row = 0, srcDataIndex = 0; row < srcHeight; row++)
                {
                    for (int dataCount = 0; dataCount < srcWidth; dataCount++, srcDataIndex++)
                    {
                        var index = targetTexture.width * row + destX + dataCount;
                        dstData[index] = srcData[srcDataIndex];
                    }
                }

                // 标记纹理数据已修改
                targetTexture.Apply(false);
            }
            else
            {
                // 高精度纹理使用GetPixelData和SetPixelData
                var srcData = newTexture.GetPixelData<float4>(0);
                var dstData = targetTexture.GetPixelData<float4>(0);

                for (int row = 0, srcDataIndex = 0; row < srcHeight; row++)
                {
                    for (int dataCount = 0; dataCount < srcWidth; dataCount++, srcDataIndex++)
                    {
                        var index = targetTexture.width * row + destX + dataCount;
                        dstData[index] = srcData[srcDataIndex];
                    }
                }

                // 标记纹理数据已修改
                targetTexture.Apply(false);
            }
        }
        else
        {
            // 普通纹理仍使用GetPixels32/SetPixels32
            var pixels = newTexture.GetPixels32();
            targetTexture.SetPixels32(
                destX,
                destY,
                srcWidth,
                srcHeight,
                pixels
            );
        }

        // 应用纹理修改
        targetTexture.Apply();

        ResizeTexture2DArray(textureIndex);

        rect = new Rect(
            splitRect.x,
            splitRect.y,
            texelSize.x,
            texelSize.y
        );
        pageIndex = textureIndex;
        _textureDict.Add(newTexture, (rect, pageIndex));
        _events?.Invoke();
        return true;
    }

    private bool Expansion()
    {
        for (int i = 0; i < _atlasTextures.Length; i++)
        {
            var tex = _atlasTextures[i];
            var size = _size[i];
            if (size < MaxSize)
            {
                if (_format == TextureFormat.RGBAHalf || _format == TextureFormat.RGBAFloat)
                {
                    var srcWidth = tex.width;
                    var srcHeight = tex.height;
                    if (_format == TextureFormat.RGBAHalf)
                    {
                        var srcData = tex.GetPixelData<half4>(0);
                        tex.Reinitialize(size << 1, size << 1);
                        var dstData = tex.GetPixelData<half4>(0);

                        for (int row = 0, srcDataIndex = 0; row < srcHeight; row++)
                        {
                            for (int dataCount = 0; dataCount < srcWidth; dataCount++, srcDataIndex++)
                            {
                                var index = tex.width * row  + dataCount;
                                dstData[index] = srcData[srcDataIndex];
                            }
                        }

                        // 标记纹理数据已修改
                        tex.Apply(false);
                    }
                    else
                    {
                        var srcData = tex.GetPixelData<float4>(0);
                        tex.Reinitialize(size << 1, size << 1);
                        var dstData = tex.GetPixelData<float4>(0);

                        for (int row = 0, srcDataIndex = 0; row < srcHeight; row++)
                        {
                            for (int dataCount = 0; dataCount < srcWidth; dataCount++, srcDataIndex++)
                            {
                                var index = tex.width * row + dataCount;
                                dstData[index] = srcData[srcDataIndex];
                            }
                        }

                        // 标记纹理数据已修改
                        tex.Apply(false);
                    }
                }
                else
                {
                    var pixels = tex.GetPixels32();
                    tex.Reinitialize(size << 1, size << 1);
                    tex.SetPixels32(0, 0, size, size, pixels);
                }
                //右
                _totalRect[i].Add(new Rect(size, 0, size, size));
                //右上
                _totalRect[i].Add(new Rect(size, size, size, size));
                //上
                _totalRect[i].Add(new Rect(0, size, size, size));
                _size[i] = size << 1;
                _maxSize = math.max(_maxSize, size << 1);
                return true;
            }
        }

        return CreateNewPage();
    }

    /// <summary>
    /// 获取所有纹理
    /// </summary>
    /// <returns></returns>
    public Texture2DArray GetTexture2DArray()
    {
        return _texArray ?? null;
    }

    private void ResizeTexture2DArray(int textureIndex)
    {
        //创建新尺寸需要全压
        if (_texArray == null || _texArray.width != MaxSize || _texArray.depth != _page)
        {
            if(_texArray != null) GameObject.Destroy(_texArray);
            _texArray = new Texture2DArray(_maxSize, _maxSize, _atlasTextures.Length, _format, false);
            _texArray.filterMode = FilterMode.Point;
            for (int i = 0; i < _atlasTextures.Length; i++)
            {
                _atlasTextures[i].Apply();
                Graphics.CopyTexture(_atlasTextures[i], 0, 0, _texArray, i, 0);
            }
            _texArray.Apply(false);
            return;
        }

        //替代旧贴图
        Graphics.CopyTexture(_atlasTextures[textureIndex], 0, 0, _texArray, textureIndex, 0);
    }

    public void RegisterTextureChangedEvent(OnDynamicAtlasTextureChanged @event, bool runOnce = false)
    {
        _events += @event;
        if (runOnce)
        {
            @event.Invoke();
        }
    }

    public void UnRegisterTextureChangedEvent(OnDynamicAtlasTextureChanged @event)
    {
        _events -= @event;
    }

    public static Texture2D MakeReadable(Texture2D source, bool mipmaps = false, bool linear = false)
    {
        var tex = new Texture2D(source.width, source.height, source.format, false);
        tex.filterMode = source.filterMode;
        Graphics.CopyTexture(source, tex);
        return tex;
    }
}

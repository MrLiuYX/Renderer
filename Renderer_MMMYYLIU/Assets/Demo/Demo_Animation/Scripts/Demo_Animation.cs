using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Demo_Animation : MonoBehaviour
{
    [Range(1, 10000)]
    public int EntityCount;
    [SerializeField]
    private ECSAnimationScriptObject[] _objects;
    private const int OperationCountFrame = 100;
    private int _currentCount;
    private MultiplyMeshRenderer<MultiplyMeshRendererCommonData> _renderer;
    private NativeList<RendererHandler<MultiplyMeshRendererCommonData>> _handler;
    private JobHandle _job;

    private void Start()
    {
        _renderer = RendererManager.Instance.GetOrCreateMultiplyRenderer<MultiplyMeshRendererCommonData>();
        _handler = new NativeList<RendererHandler<MultiplyMeshRendererCommonData>>(Allocator.Persistent);
        EntityCount = 100;
    }

    private void OnDestroy()
    {
        _handler.Dispose();
    }

    private void Update()
    {
        if (_currentCount < EntityCount)
        {
            var opCount = math.min(EntityCount - _currentCount, OperationCountFrame);
            for (int i = 0; i < opCount; i++, _currentCount++)
            {
                var handler = _renderer.AddData(_objects[_currentCount % _objects.Length]);
                _handler.Add(handler);
                handler.Data.SetPosition(new float3(_currentCount % 100, _currentCount / 100, 0));
                var matrix = _objects[_currentCount % _objects.Length].Matrix;
                handler.Data.SetAnimationRow(UnityEngine.Random.Range(0, matrix.height));
                _handler[_currentCount] = handler;
                _renderer.UpdateData(handler);
            }
        }
        else if (_currentCount > EntityCount)
        {
            var opCount = math.min(_currentCount - EntityCount, OperationCountFrame);
            for (int i = 0; i < opCount; i++, _currentCount--)
            {
                var last = _handler.Length - 1;
                _renderer.RemoveData(_handler[last]);
                _handler.RemoveAt(last);
            }
        }

        _job = new AnimationJob
        {
            Write = _handler,
        }
        .Schedule(_handler.Length, 1 << 6);
    }

    private void LateUpdate()
    {
        _job.Complete();
        for (int i = 0; i < _handler.Length; i++)
        {
            _renderer.UpdateData(_handler[i]);
        }
    }

    [BurstCompile]
    private struct AnimationJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeList<RendererHandler<MultiplyMeshRendererCommonData>> Write;

        [BurstCompile]
        public void Execute(int index)
        {
            var handler = Write[index];
            handler.Data.SetAnimationRow(1 * 0.25f + handler.Data.AnimationRow);
            Write[index] = handler;
        }
    }

    #region UI
    // 面板位置与尺寸（离左上角留出边距）
    private Rect _panelRect = new Rect(32, 32, 1100, 160);

    // 缓存样式 & 输入框文本
    private GUIStyle _box, _title, _value, _slider, _thumb;
    private string _inputCache;

    private void InitStyles()
    {
        if (_box != null) return;

        _box = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(16, 16, 16, 16)
        };

        _title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        _value = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleLeft
        };

        // 更“厚”的滑条与拇指（thumb）
        _slider = new GUIStyle(GUI.skin.horizontalSlider)
        {
            fixedHeight = 28
        };
        _thumb = new GUIStyle(GUI.skin.horizontalSliderThumb)
        {
            fixedHeight = 28,
            fixedWidth = 22
        };

        _inputCache = EntityCount.ToString();
    }

    private void OnGUI()
    {
        InitStyles();

        GUILayout.BeginArea(_panelRect, _box);

        // 标题 + 当前数值
        GUILayout.Label("Entities", _title);
        GUILayout.Space(4);

        // 一行：滑条 + 数值展示
        GUILayout.BeginHorizontal();

        // 用 Rect 版本的 Slider 才能指定自定义样式和高度
        var sliderRect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        EntityCount = Mathf.RoundToInt(
            GUI.HorizontalSlider(sliderRect, EntityCount, 0, 10000, _slider, _thumb)
        );

        GUILayout.Space(12);
        GUILayout.Label($"EntityCount：{EntityCount}", _value, GUILayout.Width(220));

        // 可手动输入
        GUI.SetNextControlName("EntityInput");
        _inputCache = GUILayout.TextField(_inputCache, GUILayout.Width(100));
        if (GUI.GetNameOfFocusedControl() != "EntityInput")
        {
            _inputCache = EntityCount.ToString();
        }
        else if (int.TryParse(_inputCache, out var typed))
        {
            EntityCount = Mathf.Clamp(typed, 0, 10000);
        }

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
    #endregion
}

using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class BoneMatrixUse : MonoBehaviour
{
    [Header("每个 bone 占 4 列，y=动画帧(像素行)")]
    [SerializeField] private Texture2D _matrix;   // RGBAHalf/RGBAFloat，Read/Write 开启，mip 关闭，Point 过滤
    [Range(0f, 1f)][SerializeField] private float _time = 0f; // 0~1 → 行索引
    [SerializeField] private bool _yBottomOrigin = true;       // 大多数 CPU 侧像素数据 y=0 为底部；若你的纹理是顶为0，改为 false

    MeshFilter _mf;
    Mesh _srcMesh;
    Mesh _runtimeMesh; // 只创建一次
    Vector3[] _srcVerts, _srcNormals;
    Vector3[] _dstVerts, _dstNormals;
    BoneWeight[] _weights;

    NativeArray<half4> _datas; // 来自 _matrix.GetPixelData<half4>(0)
    int _texW, _texH;

    void OnDisable()
    {
        // 这里 _datas 来自 Texture，不要 Dispose（由 Unity 管理）。纹理变更时要重新抓。
    }

    void Update()
    {
        if (_matrix == null) return;

        if (_mf == null)
        {
            _mf = GetComponent<MeshFilter>();
            if (_mf == null) return;
        }

        if (_srcMesh == null)
        {
            _srcMesh = _mf.sharedMesh;
            if (_srcMesh == null) return;

            // 缓存源数据
            _srcVerts = _srcMesh.vertices;
            _srcNormals = _srcMesh.normals;
            _weights = _srcMesh.boneWeights; // 需为 legacy BoneWeight；若为空可改用 GetAllBoneWeights API
            if (_weights == null || _weights.Length != _srcMesh.vertexCount)
            {
                Debug.LogError("Mesh 没有 legacy BoneWeight（boneWeights[]）。如使用新 API，请改用 GetAllBoneWeights + GetBonesPerVertex。");
                enabled = false; return;
            }

            _dstVerts = new Vector3[_srcVerts.Length];
            _dstNormals = (_srcNormals != null && _srcNormals.Length == _srcVerts.Length) ? new Vector3[_srcVerts.Length] : null;

            // 运行时 Mesh，只建一次，避免每帧分配
            _runtimeMesh = Instantiate(_srcMesh);
            _runtimeMesh.MarkDynamic();
            _mf.sharedMesh = _runtimeMesh;
        }

        // 纹理像素数据与尺寸
        if (!_datas.IsCreated || _matrix.width != _texW || _matrix.height != _texH)
        {
            _texW = _matrix.width;
            _texH = _matrix.height;

            // 仅示例：我们假设纹理是 RGBAHalf。若你用 RGBAFloat，把类型改为 float4 并对应 GetPixelData<float4>(0)。
            _datas = _matrix.GetPixelData<half4>(0);
        }

        // 帧对应的 y 行
        int y = Mathf.Clamp(Mathf.FloorToInt(_time * (_texH - 1)), 0, _texH - 1);
        if (!_yBottomOrigin)
            y = (_texH - 1) - y; // 顶部为 0 时翻转

        SkinCPU(y);

        // 应用
        _runtimeMesh.SetVertices(_dstVerts);
        if (_dstNormals != null) _runtimeMesh.SetNormals(_dstNormals);
        _runtimeMesh.RecalculateBounds(); // 顶点大改后建议重算包围盒
        // _runtimeMesh.RecalculateTangents(); // 如需要
    }

    void SkinCPU(int rowY)
    {
        var verts = _srcVerts;
        var norms = _srcNormals;
        var dstV = _dstVerts;
        var dstN = _dstNormals;
        var wts = _weights;

        for (int i = 0; i < verts.Length; i++)
        {
            var v = (float3)verts[i];
            var n = (float3)((norms != null && norms.Length == verts.Length) ? norms[i] : Vector3.up);

            BoneWeight bw = wts[i];

            float4x4 m0 = GetMatrix(bw.boneIndex0, rowY);
            float4x4 m1 = GetMatrix(bw.boneIndex1, rowY);
            float4x4 m2 = GetMatrix(bw.boneIndex2, rowY);
            float4x4 m3 = GetMatrix(bw.boneIndex3, rowY);

            float w0 = bw.weight0, w1 = bw.weight1, w2 = bw.weight2, w3 = bw.weight3;

            // 位置：列主序矩阵 * 列向量
            float3 skinnedPos =
                math.mul(m0, new float4(v, 1)).xyz * w0 +
                math.mul(m1, new float4(v, 1)).xyz * w1 +
                math.mul(m2, new float4(v, 1)).xyz * w2 +
                math.mul(m3, new float4(v, 1)).xyz * w3;

            // 法线：只取 3x3（去平移），如有非均匀缩放可考虑用逆转置
            float3x3 R0 = new float3x3(m0.c0.xyz, m0.c1.xyz, m0.c2.xyz);
            float3x3 R1 = new float3x3(m1.c0.xyz, m1.c1.xyz, m1.c2.xyz);
            float3x3 R2 = new float3x3(m2.c0.xyz, m2.c1.xyz, m2.c2.xyz);
            float3x3 R3 = new float3x3(m3.c0.xyz, m3.c1.xyz, m3.c2.xyz);

            float3 skinnedNormal =
                math.mul(R0, n) * w0 +
                math.mul(R1, n) * w1 +
                math.mul(R2, n) * w2 +
                math.mul(R3, n) * w3;

            dstV[i] = (Vector3)skinnedPos;
            if (dstN != null) dstN[i] = (Vector3)math.normalize(skinnedNormal);
        }
    }

    // ---------------------------------------------
    // 纹理布局：每个 bone 占用 4 列像素；一行(y)是一帧；每个像素是矩阵的一行（row）
    // CPU 端需要把“行向量 r0..r3”转成 float4x4 的“列”以匹配列主序乘法
    // ---------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float4x4 GetMatrix(int boneIndex, int y)
    {
        if (_matrix == null) return float4x4.identity;

        int xBase = boneIndex * 4;
        // 越界保护（你可以在 Start 时预校验 4 * boneCount <= width）
        int x0 = math.clamp(xBase + 0, 0, _texW - 1);
        int x1 = math.clamp(xBase + 1, 0, _texW - 1);
        int x2 = math.clamp(xBase + 2, 0, _texW - 1);
        int x3 = math.clamp(xBase + 3, 0, _texW - 1);
        int yy = math.clamp(y, 0, _texH - 1);

        // 行向量（r0..r3）—— 每个像素是一行的 4 个分量
        float4 r0 = math.float4(_datas[x0 + yy * _texW]);
        float4 r1 = math.float4(_datas[x1 + yy * _texW]);
        float4 r2 = math.float4(_datas[x2 + yy * _texW]);
        float4 r3 = math.float4(_datas[x3 + yy * _texW]);

        // 把行拼成“列”，得到与 HLSL/Shader 中一致的矩阵
        // 列0 = (r0.x, r1.x, r2.x, r3.x)，列1 = (r0.y, r1.y, r2.y, r3.y) ...
        return new float4x4(
            new float4(r0.x, r1.x, r2.x, r3.x),
            new float4(r0.y, r1.y, r2.y, r3.y),
            new float4(r0.z, r1.z, r2.z, r3.z),
            new float4(r0.w, r1.w, r2.w, r3.w)
        );
    }
}

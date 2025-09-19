using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class BoneMatrixUse : MonoBehaviour
{
    [Header("ÿ�� bone ռ 4 �У�y=����֡(������)")]
    [SerializeField] private Texture2D _matrix;   // RGBAHalf/RGBAFloat��Read/Write ������mip �رգ�Point ����
    [Range(0f, 1f)][SerializeField] private float _time = 0f; // 0~1 �� ������
    [SerializeField] private bool _yBottomOrigin = true;       // ����� CPU ���������� y=0 Ϊ�ײ�������������Ƕ�Ϊ0����Ϊ false

    MeshFilter _mf;
    Mesh _srcMesh;
    Mesh _runtimeMesh; // ֻ����һ��
    Vector3[] _srcVerts, _srcNormals;
    Vector3[] _dstVerts, _dstNormals;
    BoneWeight[] _weights;

    NativeArray<half4> _datas; // ���� _matrix.GetPixelData<half4>(0)
    int _texW, _texH;

    void OnDisable()
    {
        // ���� _datas ���� Texture����Ҫ Dispose���� Unity ������������ʱҪ����ץ��
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

            // ����Դ����
            _srcVerts = _srcMesh.vertices;
            _srcNormals = _srcMesh.normals;
            _weights = _srcMesh.boneWeights; // ��Ϊ legacy BoneWeight����Ϊ�տɸ��� GetAllBoneWeights API
            if (_weights == null || _weights.Length != _srcMesh.vertexCount)
            {
                Debug.LogError("Mesh û�� legacy BoneWeight��boneWeights[]������ʹ���� API������� GetAllBoneWeights + GetBonesPerVertex��");
                enabled = false; return;
            }

            _dstVerts = new Vector3[_srcVerts.Length];
            _dstNormals = (_srcNormals != null && _srcNormals.Length == _srcVerts.Length) ? new Vector3[_srcVerts.Length] : null;

            // ����ʱ Mesh��ֻ��һ�Σ�����ÿ֡����
            _runtimeMesh = Instantiate(_srcMesh);
            _runtimeMesh.MarkDynamic();
            _mf.sharedMesh = _runtimeMesh;
        }

        // ��������������ߴ�
        if (!_datas.IsCreated || _matrix.width != _texW || _matrix.height != _texH)
        {
            _texW = _matrix.width;
            _texH = _matrix.height;

            // ��ʾ�������Ǽ��������� RGBAHalf�������� RGBAFloat�������͸�Ϊ float4 ����Ӧ GetPixelData<float4>(0)��
            _datas = _matrix.GetPixelData<half4>(0);
        }

        // ֡��Ӧ�� y ��
        int y = Mathf.Clamp(Mathf.FloorToInt(_time * (_texH - 1)), 0, _texH - 1);
        if (!_yBottomOrigin)
            y = (_texH - 1) - y; // ����Ϊ 0 ʱ��ת

        SkinCPU(y);

        // Ӧ��
        _runtimeMesh.SetVertices(_dstVerts);
        if (_dstNormals != null) _runtimeMesh.SetNormals(_dstNormals);
        _runtimeMesh.RecalculateBounds(); // �����ĺ��������Χ��
        // _runtimeMesh.RecalculateTangents(); // ����Ҫ
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

            // λ�ã���������� * ������
            float3 skinnedPos =
                math.mul(m0, new float4(v, 1)).xyz * w0 +
                math.mul(m1, new float4(v, 1)).xyz * w1 +
                math.mul(m2, new float4(v, 1)).xyz * w2 +
                math.mul(m3, new float4(v, 1)).xyz * w3;

            // ���ߣ�ֻȡ 3x3��ȥƽ�ƣ������зǾ������ſɿ�������ת��
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
    // �����֣�ÿ�� bone ռ�� 4 �����أ�һ��(y)��һ֡��ÿ�������Ǿ����һ�У�row��
    // CPU ����Ҫ�ѡ������� r0..r3��ת�� float4x4 �ġ��С���ƥ��������˷�
    // ---------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float4x4 GetMatrix(int boneIndex, int y)
    {
        if (_matrix == null) return float4x4.identity;

        int xBase = boneIndex * 4;
        // Խ�籣����������� Start ʱԤУ�� 4 * boneCount <= width��
        int x0 = math.clamp(xBase + 0, 0, _texW - 1);
        int x1 = math.clamp(xBase + 1, 0, _texW - 1);
        int x2 = math.clamp(xBase + 2, 0, _texW - 1);
        int x3 = math.clamp(xBase + 3, 0, _texW - 1);
        int yy = math.clamp(y, 0, _texH - 1);

        // ��������r0..r3������ ÿ��������һ�е� 4 ������
        float4 r0 = math.float4(_datas[x0 + yy * _texW]);
        float4 r1 = math.float4(_datas[x1 + yy * _texW]);
        float4 r2 = math.float4(_datas[x2 + yy * _texW]);
        float4 r3 = math.float4(_datas[x3 + yy * _texW]);

        // ����ƴ�ɡ��С����õ��� HLSL/Shader ��һ�µľ���
        // ��0 = (r0.x, r1.x, r2.x, r3.x)����1 = (r0.y, r1.y, r2.y, r3.y) ...
        return new float4x4(
            new float4(r0.x, r1.x, r2.x, r3.x),
            new float4(r0.y, r1.y, r2.y, r3.y),
            new float4(r0.z, r1.z, r2.z, r3.z),
            new float4(r0.w, r1.w, r2.w, r3.w)
        );
    }
}

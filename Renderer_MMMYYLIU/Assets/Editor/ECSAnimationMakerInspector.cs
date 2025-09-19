using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static ECSAnimationScriptObject;

[CustomEditor(typeof(ECSAnimationMaker))]
public class ECSAnimationMakerInspector : Editor
{
    private enum BakeMode
    {
        NormalModel,
        Spine,
    }


    private GameObject _target;
    private SerializedProperty _makeDatas;

    private Renderer[] _allSkin;
    private List<GameObject> _subMesh;
    private const string ECSDataRootPath = "Assets/AssetBundleRes/Main/Prefabs/ECSScriptObject";
    private string ECSDataPath;
    private BakeMode _mode;

    public void OnEnable()
    {
        _subMesh = new List<GameObject>();
        _target = (serializedObject.targetObject as ECSAnimationMaker).gameObject;
        _makeDatas = serializedObject.FindProperty("MakeDatas");
        ECSDataPath = Path.Combine(ECSDataRootPath, $"ECS_{_target.name}.asset").Replace("\\", "/");
        if(!Directory.Exists(ECSDataRootPath))
        {
            Directory.CreateDirectory(ECSDataRootPath);
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.ApplyModifiedProperties();
        
        base.OnInspectorGUI();
        
        if (GUILayout.Button("Create"))
        {
            if (!CheckGOVaild()) return;
            if (!CheckDataVaild()) return;

            try
            {
                var directoryPath = Path.Combine("Assets/AssetBundleRes/Main/Prefabs/ECSRendererData", $"ECS_{_target.name}");
                if (Directory.Exists(directoryPath))
                {
                    if (EditorUtility.DisplayDialog("★Warning★", "You have same ecs data, do you want delete that?", "Ensure", "Cancel"))
                    {
                        Directory.Delete(directoryPath, true);
                        if (File.Exists(ECSDataPath)) File.Delete(ECSDataPath);
                        CreateECSObject(_target.name);
                    }
                }
                else
                {
                    CreateECSObject(_target.name);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogErrorFormat($"{e.StackTrace}");
            }
            finally
            {
                for (int i = 0; i < _subMesh.Count; i++)
                {
                    GameObject.DestroyImmediate(_subMesh[i]);
                }
                _subMesh.Clear();
            }
        }
    }

    public bool CheckGOVaild()
    {
        var temp = _target.GetComponentsInChildren<Renderer>().ToList();
        for (int i = 0; i < temp.Count; i++)
        {
            if(temp[i] is ParticleSystemRenderer 
                || !temp[i].enabled)
            {
                temp.RemoveAt(i--);
            }

            if (temp[i] is MeshRenderer meshRenderer)
            {
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh.subMeshCount > 1) // 检查是否有多个子网格
                {
                    temp.RemoveAt(i--);
                    // 处理每个子网格
                    for (int j = 0; j < meshFilter.sharedMesh.subMeshCount; j++)
                    {
                        // 创建新的 GameObject 用于每个子网格
                        var subMesh = new GameObject($"SubMesh{_subMesh.Count}");
                        subMesh.transform.SetParent(meshFilter.transform);
                        subMesh.transform.localPosition = Vector3.zero;
                        subMesh.transform.localScale = Vector3.one;
                        subMesh.transform.localRotation = Quaternion.identity;

                        // 创建并设置 MeshFilter 和 MeshRenderer 组件
                        var subMeshFilter = subMesh.AddComponent<MeshFilter>();
                        var subMeshRenderer = subMesh.AddComponent<MeshRenderer>();

                        // 设置新的 MeshFilter 和 MeshRenderer
                        subMeshFilter.sharedMesh = CreateSubMesh(meshFilter.sharedMesh, j); // 创建子网格
                        subMeshRenderer.sharedMaterial = meshRenderer.sharedMaterials[j]; // 设置材质
                        temp.Add(subMeshRenderer);
                        _subMesh.Add(subMesh);
                    }
                }
            }
            else if (temp[i] is SkinnedMeshRenderer skineMesh)
            {
                if (skineMesh.sharedMesh.subMeshCount > 1) // 检查是否有多个子网格
                {
                    temp.RemoveAt(i--);
                    // 处理每个子网格
                    for (int j = 0; j < skineMesh.sharedMesh.subMeshCount; j++)
                    {
                        // 创建新的 GameObject 用于每个子网格
                        var subMesh = new GameObject($"SubMesh{_subMesh.Count}");
                        subMesh.transform.SetParent(skineMesh.transform);
                        subMesh.transform.localPosition = Vector3.zero;
                        subMesh.transform.localScale = Vector3.one;
                        subMesh.transform.localRotation = Quaternion.identity;

                        // 创建并设置 MeshFilter 和 MeshRenderer 组件
                        var subMeshFilter = subMesh.AddComponent<MeshFilter>();
                        var subMeshRenderer = subMesh.AddComponent<MeshRenderer>();

                        // 设置新的 MeshFilter 和 MeshRenderer
                        subMeshFilter.sharedMesh = CreateSubMesh(skineMesh.sharedMesh, j); // 创建子网格
                        subMeshRenderer.sharedMaterial = skineMesh.sharedMaterials[j]; // 设置材质
                        temp.Add(subMeshRenderer);
                        _subMesh.Add(subMesh);
                    }
                }
            }
        }
        _allSkin = temp.ToArray();
        return _allSkin != null;
    }

    // 创建一个新的 Mesh 作为子网格
    private Mesh CreateSubMesh(Mesh originalMesh, int subMeshIndex)
    {
        // 创建一个新的 Mesh 用于子网格
        Mesh subMesh = new Mesh();

        // 提取子网格的顶点、法线和三角形数据
        subMesh.vertices = originalMesh.vertices;
        subMesh.normals = originalMesh.normals;
        subMesh.uv = originalMesh.uv;

        // 只复制子网格的三角形
        subMesh.triangles = originalMesh.GetTriangles(subMeshIndex);

        // 返回创建的子网格
        return subMesh;
    }

    public bool CheckDataVaild()
    {
        HashSet<int> ids = new HashSet<int>();

        for (int i = 0; i < _makeDatas.arraySize; i++)
        {
            var id = _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("Id");

            if (!ids.Add(id.intValue))
            {
                Debug.LogError($"[Element{i}] Id:{id.intValue} has already in make data.");
                return false;
            }

            var animation = _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("Animation");
            if (animation.objectReferenceValue == null)
            {
                Debug.LogError($"[Element{i}] not set animation clip.");
                return false;
            }
        }

        return true;
    }

    public void CreateECSObject(string targetName)
    {
        var name = $"ECS_{targetName.Replace(" ", "_")}";
        var go = new GameObject(name);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var tempPos = _target.transform.position;
        var tempRot = _target.transform.rotation;
        _target.transform.position = _target.transform.localPosition;
        _target.transform.rotation = _target.transform.localRotation;
        try
        {
            //生成ECS组件
            var authoring = new ECSAnimationScriptObject();
            authoring.Diffuse = new Texture2D[_allSkin.Length];
            authoring.MeshPath = new Mesh[_allSkin.Length];
            Texture2D waitMergeMatrixTexture = null;
            for (int i = 0; i < _allSkin.Length; i++)
            {
                int width = 0;
                int height = 0;
                string meshPath = "";
                string matPath = "";

                Bake(name, i, _allSkin[i], ref width, ref height, ref meshPath, ref matPath, ref waitMergeMatrixTexture);

                authoring.Diffuse[i] = _allSkin[i].sharedMaterial.mainTexture as Texture2D;

                authoring.MeshPath[i] = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            }

            if(waitMergeMatrixTexture != null)
            {
                var matrixTexturePath = Path.Combine("Assets/AssetBundleRes/Main/Prefabs/ECSRendererData", name, $"{name}_MatrixTex.asset");
                authoring.Matrix = AssetDatabase.LoadAssetAtPath<Texture2D>(matrixTexturePath);
            }

            List<EntityAnimationConfigBuffer> configBuffer = new List<EntityAnimationConfigBuffer>();
            for (int i = 0; i < _makeDatas.arraySize; i++)
            {
                configBuffer.Add(new EntityAnimationConfigBuffer()
                {
                    AnimationId = _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("Id").intValue,
                    AnimationLoop = _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("Loop").boolValue,
                    StartLine = _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("Start").intValue,
                    EndLine = _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("End").intValue,
                    TotalSec = _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("LenSec").floatValue,
                });
            }

            List<EntityAnimationEventConfigBuffer> eventConfigBuffer = new List<EntityAnimationEventConfigBuffer>();
            for (int i = 0; i < _makeDatas.arraySize; i++)
            {
                var events = _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("Events");
                for (int j = 0; j < events.arraySize; j++)
                {
                    eventConfigBuffer.Add(new EntityAnimationEventConfigBuffer()
                    {
                        AnimationId = _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("Id").intValue,
                        EventId = events.GetArrayElementAtIndex(j).FindPropertyRelative("EventId").intValue,
                        NormalizeTriggerTime = events.GetArrayElementAtIndex(j).FindPropertyRelative("EventTime").floatValue,
                    });
                }
            }

            authoring.animationConfigs = configBuffer;

            authoring.eventConfigs = eventConfigBuffer;

            AssetDatabase.CreateAsset(authoring, ECSDataPath);

            serializedObject.ApplyModifiedProperties();  // 应用修改

            //var prefabPath = Path.Combine("Assets/AssetBundleRes/Main/Prefabs/ECSBakePrefabs", $"{name}_Prefab.prefab");

            //PrefabUtility.SaveAsPrefabAsset(go, InternalCheckPathVaild(prefabPath));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Create fail : {e.Message}");
            throw;
        }
        finally
        {
            _target.transform.position = tempPos;
            _target.transform.rotation = tempRot;
            GameObject.DestroyImmediate(go);
        }

        EditorUtility.DisplayDialog("Create Success", $"Create ECS Animation Data Success, Path: Assets/AssetBundleRes/Main/Prefabs/ECSRendererData/{name}", "OK");
        Debug.Log($"Create Success {targetName}");
    }

    private void Bake(string name, int path, Renderer _sk, ref int width, ref int height, ref string meshPath, ref string matPath, ref Texture2D waitMergeMatrixTexture)
    {
        Mesh staticMesh;
        GameObject meshAdpater = null;
        Transform targetTs = null;
        var useMeshRenderer = false;
        Texture mainTex = null;
        var mainColor = Color.white;

        if (_sk as SkinnedMeshRenderer)
        {
            staticMesh = GameObject.Instantiate((_sk as SkinnedMeshRenderer).sharedMesh);
            targetTs = _sk.transform;
            mainTex = (_sk as SkinnedMeshRenderer).sharedMaterials[0].GetTexture("_MainTex");

            if(_mode == BakeMode.NormalModel)
            {
                try
                {
                    mainColor = (_sk as SkinnedMeshRenderer).sharedMaterials[0].color;
                }
                catch (Exception e)
                {
                    mainColor = Color.white;
                }
            }
        }
        else
        {
            meshAdpater = new GameObject("MeshAdpater");
            targetTs = _sk.transform;
            var shareMesh = GameObject.Instantiate(_sk.GetComponent<MeshFilter>().sharedMesh);
            mainTex = (_sk as MeshRenderer).sharedMaterials[0].GetTexture("_MainTex");
            if (_mode == BakeMode.NormalModel)
            {
                try
                {
                    mainColor = (_sk as SkinnedMeshRenderer).sharedMaterials[0].color;
                }
                catch (Exception e)
                {
                    mainColor = Color.white;
                }
            }
            _sk = meshAdpater.AddComponent<SkinnedMeshRenderer>();
            (_sk as SkinnedMeshRenderer).sharedMesh = shareMesh;
            staticMesh = shareMesh;
            useMeshRenderer = true;
        }

        var tempSk = (_sk as SkinnedMeshRenderer);

        width = _sk != null ? staticMesh.vertexCount : staticMesh.vertexCount;

        height = 0;

        for (int i = 0; i < _makeDatas.arraySize; i++)
        {
            var clip = (_makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("Animation").objectReferenceValue as AnimationClip);
            _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("Start").intValue = height;
            height += Mathf.CeilToInt(clip.length / ECSAnimationMaker.Split);
            _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("End").intValue = height;
        }

        if (height == 0) height = 1;

        var safeBones = new List<Transform>();
        for (int i = 0; i < tempSk.bones.Length; i++)
        {
            if (tempSk.bones[i] != null) safeBones.Add(tempSk.bones[i]);
        }
        tempSk.bones = safeBones.ToArray();
        //每个bones需要占用4个像素来存矩阵
        var matrixLen = tempSk.bones.Length * 4;
        var IsMeshRenderer = matrixLen == 0;
        matrixLen = matrixLen == 0 ? 4 : matrixLen;

        Texture2D matrixTexture = new Texture2D(matrixLen, height, TextureFormat.RGBAHalf, false);
        matrixTexture.filterMode = FilterMode.Point;
        NativeArray<ECSAnimationMaker.TexData> matrixTextureData = new NativeArray<ECSAnimationMaker.TexData>(matrixLen * height, Allocator.Temp);

        if (_sk as SkinnedMeshRenderer)
        {
            var bindPoses = new List<Matrix4x4>();
            staticMesh.GetBindposes(bindPoses);
            int index_bone = 0;
            for (int i = 0; i < _makeDatas.arraySize; i++)
            {
                var clip = (_makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("Animation").objectReferenceValue as AnimationClip);
                _makeDatas.GetArrayElementAtIndex(i).FindPropertyRelative("LenSec").floatValue = clip.length;
                var jMax = Mathf.CeilToInt(clip.length / ECSAnimationMaker.Split);

                for (float j = 0; j < jMax; j++)
                {
                    var samplePoint = Mathf.Clamp(j * ECSAnimationMaker.Split, 0, clip.length);
                    clip.SampleAnimation(_target, samplePoint);

                    if (IsMeshRenderer)
                    {
                        Matrix4x4 localToWorld = targetTs.localToWorldMatrix;

                        var matrix_index = index_bone++ * 4;
                        
                        matrixTextureData[matrix_index] = new ECSAnimationMaker.TexData()
                        {
                            r = (half)localToWorld.GetRow(0).x,
                            g = (half)localToWorld.GetRow(0).y,
                            b = (half)localToWorld.GetRow(0).z,
                            a = (half)localToWorld.GetRow(0).w,
                        };

                        matrixTextureData[matrix_index + 1] = new ECSAnimationMaker.TexData()
                        {
                            r = (half)localToWorld.GetRow(1).x,
                            g = (half)localToWorld.GetRow(1).y,
                            b = (half)localToWorld.GetRow(1).z,
                            a = (half)localToWorld.GetRow(1).w,
                        };

                        matrixTextureData[matrix_index + 2] = new ECSAnimationMaker.TexData()
                        {
                            r = (half)localToWorld.GetRow(2).x,
                            g = (half)localToWorld.GetRow(2).y,
                            b = (half)localToWorld.GetRow(2).z,
                            a = (half)localToWorld.GetRow(2).w,
                        };

                        matrixTextureData[matrix_index + 3] = new ECSAnimationMaker.TexData()
                        {
                            r = (half)localToWorld.GetRow(3).x,
                            g = (half)localToWorld.GetRow(3).y,
                            b = (half)localToWorld.GetRow(3).z,
                            a = (half)localToWorld.GetRow(3).w,
                        };
                    }

                    for (int len = 0; len < tempSk.bones.Length; index_bone++, len++)
                    {
                        var matrix_index = index_bone * 4;
                        var curMatrix = tempSk.bones[len].localToWorldMatrix * bindPoses[len];

                        matrixTextureData[matrix_index] = new ECSAnimationMaker.TexData()
                        {
                            r = (half)curMatrix.GetRow(0).x,
                            g = (half)curMatrix.GetRow(0).y,
                            b = (half)curMatrix.GetRow(0).z,
                            a = (half)curMatrix.GetRow(0).w,
                        };
                        matrixTextureData[matrix_index + 1] = new ECSAnimationMaker.TexData()
                        {
                            r = (half)curMatrix.GetRow(1).x,
                            g = (half)curMatrix.GetRow(1).y,
                            b = (half)curMatrix.GetRow(1).z,
                            a = (half)curMatrix.GetRow(1).w,
                        };
                        matrixTextureData[matrix_index + 2] = new ECSAnimationMaker.TexData()
                        {
                            r = (half)curMatrix.GetRow(2).x,
                            g = (half)curMatrix.GetRow(2).y,
                            b = (half)curMatrix.GetRow(2).z,
                            a = (half)curMatrix.GetRow(2).w,
                        };
                        matrixTextureData[matrix_index + 3] = new ECSAnimationMaker.TexData()
                        {
                            r = (half)curMatrix.GetRow(3).x,
                            g = (half)curMatrix.GetRow(3).y,
                            b = (half)curMatrix.GetRow(3).z,
                            a = (half)curMatrix.GetRow(3).w,
                        };

                    }
                }
            }

            if (_makeDatas.arraySize == 0)
            {
                if (IsMeshRenderer)
                {
                    Matrix4x4 localToWorld = targetTs.localToWorldMatrix;

                    var matrix_index = index_bone++ * 4;

                    matrixTextureData[matrix_index] = new ECSAnimationMaker.TexData()
                    {
                        r = (half)localToWorld.GetRow(0).x,
                        g = (half)localToWorld.GetRow(0).y,
                        b = (half)localToWorld.GetRow(0).z,
                        a = (half)localToWorld.GetRow(0).w,
                    };

                    matrixTextureData[matrix_index + 1] = new ECSAnimationMaker.TexData()
                    {
                        r = (half)localToWorld.GetRow(1).x,
                        g = (half)localToWorld.GetRow(1).y,
                        b = (half)localToWorld.GetRow(1).z,
                        a = (half)localToWorld.GetRow(1).w,
                    };

                    matrixTextureData[matrix_index + 2] = new ECSAnimationMaker.TexData()
                    {
                        r = (half)localToWorld.GetRow(2).x,
                        g = (half)localToWorld.GetRow(2).y,
                        b = (half)localToWorld.GetRow(2).z,
                        a = (half)localToWorld.GetRow(2).w,
                    };

                    matrixTextureData[matrix_index + 3] = new ECSAnimationMaker.TexData()
                    {
                        r = (half)localToWorld.GetRow(3).x,
                        g = (half)localToWorld.GetRow(3).y,
                        b = (half)localToWorld.GetRow(3).z,
                        a = (half)localToWorld.GetRow(3).w,
                    };
                }

                for (int len = 0; len < tempSk.bones.Length; index_bone++, len++)
                {
                    var matrix_index = index_bone * 4;
                    var curMatrix = tempSk.bones[len].localToWorldMatrix * bindPoses[len];
                    matrixTextureData[matrix_index] = new ECSAnimationMaker.TexData()
                    {
                        r = (half)curMatrix.GetRow(0).x,
                        g = (half)curMatrix.GetRow(0).y,
                        b = (half)curMatrix.GetRow(0).z,
                        a = (half)curMatrix.GetRow(0).w,
                    };
                    matrixTextureData[matrix_index + 1] = new ECSAnimationMaker.TexData()
                    {
                        r = (half)curMatrix.GetRow(1).x,
                        g = (half)curMatrix.GetRow(1).y,
                        b = (half)curMatrix.GetRow(1).z,
                        a = (half)curMatrix.GetRow(1).w,
                    };
                    matrixTextureData[matrix_index + 2] = new ECSAnimationMaker.TexData()
                    {
                        r = (half)curMatrix.GetRow(2).x,
                        g = (half)curMatrix.GetRow(2).y,
                        b = (half)curMatrix.GetRow(2).z,
                        a = (half)curMatrix.GetRow(2).w,
                    };
                    matrixTextureData[matrix_index + 3] = new ECSAnimationMaker.TexData()
                    {
                        r = (half)curMatrix.GetRow(3).x,
                        g = (half)curMatrix.GetRow(3).y,
                        b = (half)curMatrix.GetRow(3).z,
                        a = (half)curMatrix.GetRow(3).w,
                    };
                }
            }
        }

        matrixTexture.SetPixelData(matrixTextureData, 0);
        matrixTexture.Apply();

        //合并矩阵
        if (waitMergeMatrixTexture != null)
        {
            var tTexture = new Texture2D(matrixLen + waitMergeMatrixTexture.width, height, TextureFormat.RGBAHalf, false);
            tTexture.filterMode = FilterMode.Point;
            //tTexture.SetPixels32(0, 0, waitMergeMatrixTexture.width, waitMergeMatrixTexture.height, waitMergeMatrixTexture.GetPixels32());
            //tTexture.SetPixels32(waitMergeMatrixTexture.width, 0, matrixTexture.width, matrixTexture.height, matrixTexture.GetPixels32());
            var lastDatas = waitMergeMatrixTexture.GetPixelData<ECSAnimationMaker.TexData>(0);
            var curDatas = matrixTextureData;
            var mergeDatas = new NativeArray<ECSAnimationMaker.TexData>(lastDatas.Length + curDatas.Length, Allocator.Temp);
            for (int i = 0, index = 0, curDataIndex = 0, lastDataIndex = 0; i < height; i++)
            {
                for (int j = 0; j < matrixLen + waitMergeMatrixTexture.width; j++, index++)
                {
                    if(j >= waitMergeMatrixTexture.width)
                    {
                        //从curDatas取
                        mergeDatas[index] = curDatas[curDataIndex++];
                    }
                    else
                    {
                        //从lastDatas取
                        mergeDatas[index] = lastDatas[lastDataIndex++];
                    }
                }
            }
            tTexture.SetPixelData<ECSAnimationMaker.TexData>(mergeDatas, 0);
            mergeDatas.Dispose();
            GameObject.DestroyImmediate(matrixTexture);
            matrixTexture = tTexture;
            matrixTexture.Apply();
        }

        waitMergeMatrixTexture = matrixTexture;
        meshPath = Path.Combine("Assets/AssetBundleRes/Main/Prefabs/ECSRendererData", name, $"{name}_{path}_mesh.mesh");
        matPath = Path.Combine("Assets/AssetBundleRes/Main/Prefabs/ECSRendererData", name, $"{name}_{path}_mat.mat");
        var matrixTexturePath = Path.Combine("Assets/AssetBundleRes/Main/Prefabs/ECSRendererData", name, $"{name}_MatrixTex.asset");
        if (File.Exists(meshPath)) File.Delete(meshPath);
        if (File.Exists(matPath)) File.Delete(matPath);
        if (File.Exists(matrixTexturePath)) File.Delete(matrixTexturePath);

        AssetDatabase.Refresh();

        if (_sk as SkinnedMeshRenderer)
        {
            AssetDatabase.CreateAsset(staticMesh, InternalCheckPathVaild(meshPath));
        }


        serializedObject.ApplyModifiedProperties();

        AssetDatabase.CreateAsset(matrixTexture, InternalCheckPathVaild(matrixTexturePath));

        AssetDatabase.Refresh();

        MakeReadableInPlaceMenu.Do(AssetDatabase.LoadAssetAtPath<Texture2D>(matrixTexturePath));

        AssetDatabase.Refresh();

        GameObject.DestroyImmediate(meshAdpater);
    }

    private string InternalCheckPathVaild(string path)
    {
        if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
        }

        return path;
    }
}

public static class MakeReadableInPlaceMenu
{
    public static void Do(Texture2D target)
    {
        var readable = MakeReadableCopyMenu.CaptureToReadable(target);
        EditorUtility.CopySerialized(readable, target);
        EditorUtility.SetDirty(target);
        Debug.Log($"Made readable: {AssetDatabase.GetAssetPath(target)}");
    }
}
public static class MakeReadableCopyMenu
{
    // GPU→CPU：保持精度，用 Half 管线
    public static Texture2D CaptureToReadable(Texture src)
    {
        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0,
                                            RenderTextureFormat.ARGBHalf,
                                            linear ? RenderTextureReadWrite.Linear
                                                   : RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);

        var prev = RenderTexture.active;
        RenderTexture.active = rt;

        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBAHalf, false, linear);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        tex.Apply(false, false); // 保持“可读”
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }
}
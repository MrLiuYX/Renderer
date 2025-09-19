using System.IO;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static UnityEngine.GraphicsBuffer;

[CustomEditor(typeof(ECSAnimationMakerMonitor))]
public class ECSAnimationMakerMonitorInspector : Editor
{
    public SerializedProperty AnimationClips;
    public SerializedProperty AnimationIds;
    public ECSAnimationMakerMonitor Target;
    public int ChooseIndex;
    public float Slider;
    private float _percent;

    private GameObject Attach;

    public void OnEnable()
    {
        if (serializedObject == null) return;
        AnimationClips = serializedObject.FindProperty("AnimationClips");
        AnimationIds = serializedObject.FindProperty("AnimationIds");
        Target = target as ECSAnimationMakerMonitor;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        Attach = (GameObject)EditorGUILayout.ObjectField(Attach, typeof(GameObject));

        if (GUILayout.Button("Load"))
        {
            InternalLoad();
        }

        if (AnimationClips.arraySize == 0) return;

        ChooseIndex = Mathf.Clamp(EditorGUILayout.IntField(ChooseIndex), 0, AnimationClips.arraySize);
        var animation = AnimationClips.GetArrayElementAtIndex(ChooseIndex).objectReferenceValue as AnimationClip;
        Slider = EditorGUILayout.Slider(Slider, 0, animation.length);
        _percent = Slider / animation.length;
        GUILayout.Label($"NormalizeTime:{_percent}");
        GUILayout.Label($"Seconds:{Slider}");
        animation.SampleAnimation(Target.gameObject, Slider);
    }

    private void InternalLoad()
    {
        AnimationClips.ClearArray();
        var datas = Target.GetComponent<ECSAnimationMaker>().MakeDatas;
        for (int i = 0; i < datas.Count; i++)
        {
            AnimationClips.InsertArrayElementAtIndex(i);
            AnimationIds.InsertArrayElementAtIndex(i);

            var animation = AnimationClips.GetArrayElementAtIndex(i);
            animation.objectReferenceValue = datas[i].Animation;

            var animationId = AnimationIds.GetArrayElementAtIndex(i);
            animationId.intValue = datas[i].Id;
        }
        serializedObject.ApplyModifiedProperties(); 
    }
}
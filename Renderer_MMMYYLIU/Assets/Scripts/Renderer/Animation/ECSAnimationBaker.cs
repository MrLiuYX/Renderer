using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(ECSAnimationMakerMonitor))]
public class ECSAnimationMaker : MonoBehaviour
{
    public struct TexData
    {
        public half r;
        public half g;
        public half b;
        public half a;
    }

    public const float Split = 0.02f;

	[Serializable]
	public struct ECSAnimationMakeData
	{
		[SerializeField]
		public int Id;
        [SerializeField]
        public AnimationClip Animation;
        [SerializeField]
        public bool Loop;
        [SerializeField]
        public List<ECSAnimationMakeDataEvent> Events;

        [HideInInspector]
        public int Start;
        [HideInInspector]
        public int End;
        [HideInInspector]
        public float LenSec;
    }

    [Serializable]
    public struct ECSAnimationMakeDataEvent
    {
        [SerializeField]
        public int EventId;
        [SerializeField]
        public float EventTime;
    }

    [SerializeField]
    public string ShaderPath;

    [SerializeField]
    public List<ECSAnimationMakeData> MakeDatas;
}
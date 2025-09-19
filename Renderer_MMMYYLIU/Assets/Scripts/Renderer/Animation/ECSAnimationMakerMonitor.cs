

using System.Collections.Generic;
using UnityEngine;

public class ECSAnimationMakerMonitor : MonoBehaviour
{
    [SerializeField]
    public List<AnimationClip> AnimationClips;
    [SerializeField]
    [HideInInspector]
    public List<int> AnimationIds;
}
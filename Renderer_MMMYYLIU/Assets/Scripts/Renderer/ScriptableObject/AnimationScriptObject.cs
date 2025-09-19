using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class ECSAnimationScriptObject : ScriptableObject
{
    [SerializeField]
    public List<EntityAnimationConfigBuffer> animationConfigs;
    [SerializeField]
    public List<EntityAnimationEventConfigBuffer> eventConfigs;
    [SerializeField]
    public Texture2D[] Diffuse;
    [SerializeField]
    public Mesh[] MeshPath;
    [SerializeField]
    public Texture2D Matrix;
}
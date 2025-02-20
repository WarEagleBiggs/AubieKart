using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SceneFollow : MonoBehaviour
{

    private SceneView sceneCam;
    public bool follow;
    public Transform target;

    private void Start()
    {
        sceneCam = SceneView.lastActiveSceneView;
    }

    void Update()
    {
        if (follow)
        {
            sceneCam.LookAt(target.position);
        }
    }
}

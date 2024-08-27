using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ShaderGlobals : MonoBehaviour
{
    [SerializeField] Light mainLight;
    public Material oceanMaterial;
    public string propertyName;
    
    void Update()
    {
        oceanMaterial.SetVector(propertyName, mainLight.transform.forward);
    }
}


using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using Random = System.Random;

[System.Serializable]
public class WaveSectionNoVertexColor
{
    public int startBlendShapeIndex, animationShapesCount;
    public SkinnedMeshRenderer mesh;
    public float[] speedList;
    public int blendShapeIndexToWaveBreak, blendShapeToStartLeapFoam;
    public Vector3 breakPosLocal, leapFoamPosLocal;
    public UnityEvent<Vector3> CallWaveBreak;
    public UnityEvent<Vector3, int, int> CallStartLeapFoam;
    
    public IEnumerator PlayCoroutine()
    {
        float lerpAux = 0;
        int blendShapeIndex = startBlendShapeIndex;
        int speedIndex = 0;

        while ( blendShapeIndex < startBlendShapeIndex + animationShapesCount )
        {
            lerpAux += Time.deltaTime * speedList[ speedIndex ];
            mesh.SetBlendShapeWeight( blendShapeIndex, lerpAux );

            if (lerpAux >= 100)
            {
                if (blendShapeIndex == blendShapeToStartLeapFoam)
                {
                    int totalBlendsUntilBreak = blendShapeIndexToWaveBreak - blendShapeToStartLeapFoam;
                    CallStartLeapFoam.Invoke( leapFoamPosLocal, speedIndex + 1, totalBlendsUntilBreak );
                }
                
                if (blendShapeIndex == blendShapeIndexToWaveBreak)
                {
                    CallWaveBreak.Invoke( breakPosLocal );
                }

                lerpAux = 0;
                blendShapeIndex++;
                speedIndex++;
                speedIndex = Mathf.Clamp(speedIndex, 0, speedList.Length - 1);
            }
            
            yield return new WaitForEndOfFrame();
        }
    }

}



public class BlendShapesController : MonoBehaviour
{
    public SkinnedMeshRenderer waveMesh;
    public List<WaveSectionNoVertexColor> sectionList;
    public int animationShapesCountPerSection, verticesPerSection;
    
    public int blendShapeIndexToWaveBreak, blendShapeToStartLeapFoam;
    public GameObject breakVFX, leapMist, areaToBreakWave, areaForLeapFoam;
    public AnimationCurve leapFoamForward, leapFoamDownward;
    
    public float[] speedList;
    public int trigger;
    private int currentWaveTrigger, secondCurrentWaveTrigger;

    private Vector3 initialPos;
    private float currentSpeed;
    public float waveSpeedForward;

    public void OnWaveBreak( Vector3 pos )
    {
        GameObject go = Instantiate(breakVFX);
        Transform waveT = waveMesh.transform;
        go.transform.position = pos * waveT.localScale.x + waveT.position;
        go.transform.SetParent(waveT);
    }

    public void OnStartLeapFoam( Vector3 pos, int speedIndex, int totalBlendsForWaveBreak )
    {
        GameObject go = Instantiate(leapMist);
        Transform waveT = waveMesh.transform;
        go.transform.position = pos * waveT.localScale.x + waveT.position;
        go.transform.SetParent(waveT);
        StartCoroutine(LeapFoamFollowAnimation(go, speedIndex, totalBlendsForWaveBreak));
    }

    
    IEnumerator LeapFoamFollowAnimation( GameObject go, int speedIndex, int totalBlendsCountToBreak )
    {
        float lerpAux = 0;
        float evaluateTime = 0;
        Vector3 initialPosLocal = go.transform.localPosition;
        float lastEvaluateLoopValue = 0;
        
        while ( true )
        {
            lerpAux += Time.deltaTime * speedList[ speedIndex ] / 100f;
            evaluateTime = lastEvaluateLoopValue + lerpAux / totalBlendsCountToBreak;
            float x = -leapFoamForward.Evaluate(evaluateTime) / waveMesh.transform.localScale.x;
            float y = leapFoamDownward.Evaluate(evaluateTime) / waveMesh.transform.localScale.x;
            go.transform.localPosition = initialPosLocal + new Vector3( x, y, 0 ) ;

            if (lerpAux >= 1)
            {
                lastEvaluateLoopValue = evaluateTime;
                lerpAux = 0;
                speedIndex++;
            }

            if ( speedIndex >= speedIndex + totalBlendsCountToBreak )
                break;
            
            yield return new WaitForEndOfFrame();
        }
    }
        
    private void Start()
    {
        int blendShapeIndexPop = 0;
        int lastVerticeAux = 0;
        int bShapeWaveBreakAux = blendShapeIndexToWaveBreak;
        int bShapeLeapFoamAux = blendShapeToStartLeapFoam;
        sectionList = new List<WaveSectionNoVertexColor>();
        verticesPerSection = GetVerticesPerSection();

        Vector3[] vertex = waveMesh.sharedMesh.vertices;
        
        while ( blendShapeIndexPop < waveMesh.sharedMesh.blendShapeCount )
        {
            WaveSectionNoVertexColor es = new WaveSectionNoVertexColor();
            es.startBlendShapeIndex = blendShapeIndexPop;
            es.animationShapesCount = animationShapesCountPerSection;
            es.mesh = waveMesh;
            es.speedList = speedList;

            es.blendShapeToStartLeapFoam = bShapeLeapFoamAux;
            Vector3 leapFoamPos = areaForLeapFoam.transform.localPosition;
            leapFoamPos.z = vertex[lastVerticeAux].z;;
            es.leapFoamPosLocal = leapFoamPos;
            bShapeLeapFoamAux += animationShapesCountPerSection + 1;
            es.CallStartLeapFoam = new UnityEvent<Vector3, int, int>();
            es.CallStartLeapFoam.AddListener(OnStartLeapFoam);
            
            es.blendShapeIndexToWaveBreak = bShapeWaveBreakAux;
            Vector3 waveBreakPos = areaToBreakWave.transform.localPosition;
            waveBreakPos.z = vertex[lastVerticeAux].z;
            es.breakPosLocal = waveBreakPos;
            bShapeWaveBreakAux += animationShapesCountPerSection + 1;;
            es.CallWaveBreak = new UnityEvent<Vector3>();
            es.CallWaveBreak.AddListener(OnWaveBreak);

            lastVerticeAux += verticesPerSection;
            blendShapeIndexPop += animationShapesCountPerSection + 1;
            
            sectionList.Add(es);
        }

        initialPos = waveMesh.transform.localPosition;
        areaToBreakWave.gameObject.SetActive(false);
        areaForLeapFoam.gameObject.SetActive(false);

        CallMiddle();
    }

    int GetVerticesPerSection()
    {
        int vCountPerSection = 0;
        Vector3 lastVerPos = waveMesh.sharedMesh.vertices[0];
        for ( int i = 1; i < waveMesh.sharedMesh.vertexCount; i++ )
        {
            Vector3 vPos = waveMesh.sharedMesh.vertices[i];
            vCountPerSection++;
            if ( vPos.x < lastVerPos.x  )
                break;

            lastVerPos = vPos;
        }

        return vCountPerSection;
    }

    private void Update()
    {
        waveMesh.transform.localPosition += waveMesh.transform.right * currentSpeed * Time.deltaTime;
        
        if (Input.GetKeyDown(KeyCode.P))
        {
            StopAllCoroutines();
            Time.timeScale = 0;
        }
        
        if (Input.GetKeyDown(KeyCode.S))
        {
            Time.timeScale = 0.2f;
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            CallMiddle();
        }
    }

    void CallMiddle()
    {
        Time.timeScale = 1;
        ResetAll();
        waveMesh.transform.localPosition = initialPos;
        currentSpeed = waveSpeedForward;
        currentWaveTrigger = sectionList.Count / 2;
        secondCurrentWaveTrigger = currentWaveTrigger;
        StartCoroutine(sectionList[currentWaveTrigger].PlayCoroutine());
        StartCoroutine(MiddleWave());
        
    }

    IEnumerator MiddleWave()
    {
        while ( true )
        {
            if ( waveMesh.GetBlendShapeWeight(sectionList[currentWaveTrigger].startBlendShapeIndex ) >= 
                 trigger )
            {
                currentWaveTrigger++;
                secondCurrentWaveTrigger--;
                StartCoroutine(sectionList[secondCurrentWaveTrigger].PlayCoroutine());
                StartCoroutine(sectionList[currentWaveTrigger].PlayCoroutine());
            }

            yield return new WaitForEndOfFrame();
        }
    }

    void ResetAll()
    {
        StopAllCoroutines();
        for ( int i = 0; i < waveMesh.sharedMesh.blendShapeCount; i++ )
        {
            waveMesh.SetBlendShapeWeight(i, 0);
        }
    }

}

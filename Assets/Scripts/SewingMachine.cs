using System;
using UnityEngine;

public class SewingMachine : MonoBehaviour
{
    [Header("Elementy Maszyny")]
    public Transform needleObj; 
    public Transform needleTip; 
    public Transform leverObj; 
    public Transform wheelObj;
    public Transform onOffButton;
    
    public AudioSource machineSound;
    public AudioClip machineClip;

    public GameObject stitchPrefab;
    public float raycastOffset = 0.05f;
    public float raycastLength = 0.1f;
    public float minDistanceBetweenStitches = 0.005f;
    public LayerMask clothLayer;
    
    [Header("Parametry")]
    public float maxNeedleSpeed = 20f;
    public float needleAmplitude = 0.02f;

    [field: SerializeField] private float currentPressure = 0f;
    [field: SerializeField] private bool isOn = false;
    
    private Vector3 startNeedlePos;
    private Vector3 startLeverPos;
    private float cyclePosition = 0f;
    
    private readonly float OFFPOSITION = 30;
    
    private bool hasStitchedInThisCycle = false;
    private Vector3 lastStitchPosition;
    private bool isFirstStitch = true;

    void Start()
    {
        if(needleObj) startNeedlePos = needleObj.localPosition;
        if(leverObj) startLeverPos = leverObj.localPosition;
        
        UpdateOnOffButton();

        if (machineSound)
        {
            machineSound.clip = machineClip;
            machineSound.loop = true;
            machineSound.Stop();
        }
    }

    private void UpdateOnOffButton()
    {
        if (!onOffButton) return;
        
        var targetY = isOn ? -OFFPOSITION : OFFPOSITION;
        onOffButton.localRotation = Quaternion.Euler(onOffButton.localEulerAngles.x, targetY, onOffButton.localEulerAngles.z);
    }

    private void Update()
    {
        if (isOn && currentPressure > 0.01f)
        {
            var currentSpeed = maxNeedleSpeed * currentPressure;
            cyclePosition += Time.deltaTime * currentSpeed;
            var rawSine = Mathf.Sin(cyclePosition);
            var sineNormalized = (rawSine - 1f) * 0.5f;
            
            var newY = startNeedlePos.y + sineNormalized * needleAmplitude;
            needleObj.localPosition = new Vector3(startNeedlePos.x, newY, startNeedlePos.z);
            
            var newY2 = startLeverPos.y + sineNormalized * needleAmplitude;
            leverObj.localPosition = new Vector3(startLeverPos.x, newY2, startLeverPos.z);

            wheelObj.Rotate(Vector3.forward * (currentSpeed * 50f * Time.deltaTime));

            if (rawSine < -0.8f) 
            {
                if (!hasStitchedInThisCycle)
                {
                    TryToSew();
                    hasStitchedInThisCycle = true;
                }
            }
            else
            {
                hasStitchedInThisCycle = false;
            }
            
            if (!machineSound.isPlaying) machineSound.Play();
            
            machineSound.pitch = Mathf.Lerp(0.2f, 1.0f, currentPressure);
        }
        else
        {
            if (machineSound.isPlaying) machineSound.Stop();
        }
    }

    private void TryToSew()
    {

        Vector3 rayOrigin = needleTip.position + (Vector3.up * raycastOffset); 
        Vector3 rayDirection = Vector3.down; 

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, rayDirection, raycastLength, clothLayer);

        if (hits.Length > 0)
        {
            Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

            RaycastHit topHit = hits[0];

            float dist = Vector3.Distance(topHit.point, lastStitchPosition);

            if (isFirstStitch || dist > minDistanceBetweenStitches)
            {
                CreateStitch(topHit.point, topHit.transform);
            
                lastStitchPosition = topHit.point;
                isFirstStitch = false;

                if (hits.Length >= 2)
                {
                    GameObject topObj = hits[0].collider.gameObject;
                    GameObject bottomObj = hits[1].collider.gameObject;

                    if (topObj != bottomObj)
                    {
                        ConnectCloth(topObj, bottomObj);
                    }
                }
            }
        }
    }

    private void CreateStitch(Vector3 position, Transform clothTransform)
    {
        Vector3 spawnPos = position + (Vector3.up * 0.001f);
        
        GameObject newStitch = Instantiate(stitchPrefab, spawnPos, Quaternion.Euler(90, 0, 0));
        
        newStitch.transform.SetParent(clothTransform, true);
    }

    private void ConnectCloth(GameObject topCloth, GameObject bottomCloth)
    {
        Rigidbody topRb = topCloth.GetComponent<Rigidbody>();
        Rigidbody bottomRb = bottomCloth.GetComponent<Rigidbody>();

        if (topRb != null && bottomRb != null)
        {
            FixedJoint[] existingJoints = topCloth.GetComponents<FixedJoint>();
            foreach (var j in existingJoints)
            {
                if (j.connectedBody == bottomRb) return;
            }

            FixedJoint joint = topCloth.AddComponent<FixedJoint>();
            joint.connectedBody = bottomRb;
        
            joint.breakForce = Mathf.Infinity; 
            joint.breakTorque = Mathf.Infinity;
        
            joint.enableCollision = false; 

        }
    }
    
    public void TogglePower()
    {
        isOn = !isOn;
        
        UpdateOnOffButton();
        if (!isOn) currentPressure = 0f;
        
        var targetY = isOn ? -OFFPOSITION : OFFPOSITION;
        onOffButton.rotation = Quaternion.Euler(onOffButton.localEulerAngles.x, targetY, onOffButton.localEulerAngles.z);
    }

    public void SetPedalPressure(float pressure)
    {
        currentPressure = isOn ? pressure : 0f;
    }
    
}
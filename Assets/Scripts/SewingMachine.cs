using System;
using UnityEngine;

public class SewingMachine : MonoBehaviour
{
    [Header("Elementy Maszyny")]
    public Transform needleObj; 
    public Transform leverObj; 
    public Transform wheelObj;
    public Transform onOffButton;
    
    public AudioSource machineSound;
    public AudioClip machineClip;

    [Header("Parametry")]
    public float needleSpeed = 20f;
    public float needleAmplitude = 0.02f; 

    [field: SerializeField] private bool isRunning = false;
    private Vector3 startNeedlePos;
    private Vector3 startLeverPos;
    
    private readonly float OFFPOSITION = 30;

    void Start()
    {
        if(needleObj) startNeedlePos = needleObj.localPosition;
        if(leverObj) startLeverPos = leverObj.localPosition;
        if(onOffButton) onOffButton.rotation = Quaternion.Euler(onOffButton.localEulerAngles.x, OFFPOSITION, onOffButton.localEulerAngles.z);
        machineSound.clip = machineClip;
    }

    void Update()
    {
        if (isRunning && needleObj && wheelObj)
        {
            float newY = startNeedlePos.y + Mathf.Sin(Time.time * needleSpeed) * needleAmplitude;
            needleObj.localPosition = new Vector3(startNeedlePos.x, newY, startNeedlePos.z);
            leverObj.localPosition = new Vector3(startLeverPos.x, newY, startLeverPos.z);

            wheelObj.Rotate(Vector3.forward * needleSpeed * 50f * Time.deltaTime);
        }
    }
    
    public void TogglePower()
    {
        isRunning = !isRunning;
        if (isRunning && machineSound)
        {
            machineSound.loop = true;
            if (!machineSound.isPlaying) 
                machineSound.Play();
        }
        else machineSound?.Stop();
        
        var targetY = isRunning ? -OFFPOSITION : OFFPOSITION;
        onOffButton.rotation = Quaternion.Euler(onOffButton.localEulerAngles.x, targetY, onOffButton.localEulerAngles.z);
    }
    
    public bool IsMachineOn() => isRunning;
}
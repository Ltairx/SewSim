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
    public float maxNeedleSpeed = 20f;
    public float needleAmplitude = 0.02f;

    [field: SerializeField] private float currentPressure = 0f;
    [field: SerializeField] private bool isOn = false;
    
    private Vector3 startNeedlePos;
    private Vector3 startLeverPos;
    private float cyclePosition = 0f;
    
    private readonly float OFFPOSITION = 30;

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
            var sineNormalized = (Mathf.Sin(cyclePosition) - 1f) * 0.5f;
            
            var newY = startNeedlePos.y + sineNormalized * needleAmplitude;
            needleObj.localPosition = new Vector3(startNeedlePos.x, newY, startNeedlePos.z);
            
            var newY2 = startLeverPos.y + sineNormalized * needleAmplitude;
            leverObj.localPosition = new Vector3(startLeverPos.x, newY2, startLeverPos.z);

            wheelObj.Rotate(Vector3.forward * (currentSpeed * 50f * Time.deltaTime));

            if (!machineSound.isPlaying) machineSound.Play();
            
            machineSound.pitch = Mathf.Lerp(0.2f, 1.0f, currentPressure);
        }
        else
        {
            if (machineSound.isPlaying) machineSound.Stop();
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
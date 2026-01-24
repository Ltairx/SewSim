using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class LockFoot : MonoBehaviour
{
    public float lockThreshold = 0.95f;
    
    public string materialTag = "cloth";

    [SerializeField] private GameObject currentObjectInZone;
    [SerializeField] private bool isLocked = false;

    private Rigidbody targetRb;
    private XRGrabInteractable targetGrab;
    private bool wasKinematicBefore;


    private void OnTriggerEnter(Collider other)
    {
        if (!isLocked && other.CompareTag(materialTag))
        {
            currentObjectInZone = other.gameObject;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isLocked && other.gameObject == currentObjectInZone)
        {
            currentObjectInZone = null;
        }
    }

    public void UpdateLeverState(float leverValue)
    {
        if (leverValue >= lockThreshold && currentObjectInZone != null && !isLocked)
        {
            LockObject();
        }
        else if (leverValue < lockThreshold && isLocked)
        {
            UnlockObject();
        }
    }

    private void LockObject()
    {
        if (currentObjectInZone == null) return;

        targetRb = currentObjectInZone.GetComponent<Rigidbody>();
        targetGrab = currentObjectInZone.GetComponent<XRGrabInteractable>();

        if (targetRb && targetGrab)
        {

            targetGrab.enabled = false;

            wasKinematicBefore = targetRb.isKinematic;
            targetRb.isKinematic = true;

            // Opcjonalnie: wyrównaj pozycję/rotację idealnie do stołu (Snap)
            // currentObjectInZone.transform.rotation = Quaternion.identity; 
            
            isLocked = true;
        }
    }

    private void UnlockObject()
    {
        if (targetRb && targetGrab)
        {
            targetRb.isKinematic = wasKinematicBefore;

            targetGrab.enabled = true;

            isLocked = false;
        }
    }
}
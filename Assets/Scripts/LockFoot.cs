using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class LockFoot : MonoBehaviour
{
    public float lockThreshold = 0.95f;
    
    public string materialTag = "cloth";

    public GameObject currentObjectInZone;
    public bool isLocked = false;

    private Rigidbody _targetRb;
    private XRGrabInteractable _targetGrab;
    private bool _wasKinematicBefore;
    private RigidbodyConstraints _oldConstraints;
    private XRBaseInteractable.MovementType _oldMovementType;
    private bool _oldTrackRotation;
    private bool _oldThrowOnDetach;

    private float _lockedY;
    private float _lockedZ;
    private Quaternion _lockedRotation;

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

    private void LateUpdate()
    {
        if (isLocked && currentObjectInZone)
        {
            Vector3 currentPos = currentObjectInZone.transform.position;
            currentObjectInZone.transform.position = new Vector3(currentPos.x, _lockedY, _lockedZ);

            currentObjectInZone.transform.rotation = _lockedRotation;
        }
    }
    
    private void LockObject()
    {
        if (currentObjectInZone == null) return;

        _targetRb = currentObjectInZone.GetComponent<Rigidbody>();
        _targetGrab = currentObjectInZone.GetComponent<XRGrabInteractable>();

        if (_targetRb && _targetGrab)
        {
            _lockedY = currentObjectInZone.transform.position.y;
            _lockedZ = currentObjectInZone.transform.position.z;
            _lockedRotation = currentObjectInZone.transform.rotation;
            
            _wasKinematicBefore = _targetRb.isKinematic;
            _oldConstraints = _targetRb.constraints;
            _oldMovementType = _targetGrab.movementType;
            _oldTrackRotation = _targetGrab.trackRotation;
            _oldThrowOnDetach = _targetGrab.throwOnDetach;

            _targetRb.isKinematic = false;

            _targetRb.constraints = RigidbodyConstraints.FreezeAll & ~RigidbodyConstraints.FreezePositionX;

            _targetGrab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            _targetGrab.trackRotation = false;
            _targetGrab.throwOnDetach = false;
            
            isLocked = true;
        }
    }

    private void UnlockObject()
    {
        if (_targetRb && _targetGrab)
        {
            _targetRb.isKinematic = _wasKinematicBefore;
            _targetRb.constraints = _oldConstraints;

            _targetGrab.movementType = _oldMovementType;
            _targetGrab.trackRotation = _oldTrackRotation;
            _targetGrab.throwOnDetach = _oldThrowOnDetach;

            isLocked = false;
        }
    }
}
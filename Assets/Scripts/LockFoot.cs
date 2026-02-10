using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class LockFoot : MonoBehaviour
{
    public float lockThreshold = 0.95f;
    public string materialTag = "cloth";
    public List<GameObject> objectsInZone = new List<GameObject>();
    public bool isLocked = false;

    private class LockedObjectState
    {
        public GameObject obj;
        public Rigidbody rb;
        public XRGrabInteractable grab;
        
        public bool wasKinematic;
        public RigidbodyConstraints oldConstraints;
        public XRBaseInteractable.MovementType oldMovementType;
        public bool oldTrackRotation;
        public bool oldThrowOnDetach;

        public float lockedY;
        public float lockedZ;
        public Quaternion lockedRotation;
    }

    private List<LockedObjectState> activeLocks = new List<LockedObjectState>();

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(materialTag) && !objectsInZone.Contains(other.gameObject))
        {
            objectsInZone.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (objectsInZone.Contains(other.gameObject))
        {
            objectsInZone.Remove(other.gameObject);
        }
    }

    public void UpdateLeverState(float leverValue)
    {
        if (leverValue >= lockThreshold && objectsInZone.Count > 0 && !isLocked)
        {
            LockAllObjects();
        }
        else if (leverValue < lockThreshold && isLocked)
        {
            UnlockAllObjects();
        }
    }

    private void LateUpdate()
    {
        if (isLocked && activeLocks.Count > 0)
        {
            for (int i = activeLocks.Count - 1; i >= 0; i--)
            {
                var state = activeLocks[i];

                if (!state.obj) 
                {
                    activeLocks.RemoveAt(i);
                    continue;
                }

                Vector3 currentPos = state.obj.transform.position;
                state.obj.transform.position = new Vector3(currentPos.x, state.lockedY, state.lockedZ);
                state.obj.transform.rotation = state.lockedRotation;
            }
        }
    }
    
    private void LockAllObjects()
    {
        activeLocks.Clear();

        foreach (GameObject obj in objectsInZone)
        {
            if (obj == null) continue;

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            XRGrabInteractable grab = obj.GetComponent<XRGrabInteractable>();

            if (rb && grab)
            {
                LockedObjectState state = new LockedObjectState();
                state.obj = obj;
                state.rb = rb;
                state.grab = grab;

                state.lockedY = obj.transform.position.y;
                state.lockedZ = obj.transform.position.z;
                state.lockedRotation = obj.transform.rotation;

                state.wasKinematic = rb.isKinematic;
                state.oldConstraints = rb.constraints;
                state.oldMovementType = grab.movementType;
                state.oldTrackRotation = grab.trackRotation;
                state.oldThrowOnDetach = grab.throwOnDetach;

                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.FreezeAll & ~RigidbodyConstraints.FreezePositionX;

                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                grab.trackRotation = false;
                grab.throwOnDetach = false;

                activeLocks.Add(state);
            }
        }

        if (activeLocks.Count > 0)
        {
            isLocked = true;
        }
    }

    private void UnlockAllObjects()
    {
        foreach (var state in activeLocks)
        {
            if (state.obj != null && state.rb != null && state.grab != null)
            {
                state.rb.isKinematic = state.wasKinematic;
                state.rb.constraints = state.oldConstraints;

                state.grab.movementType = state.oldMovementType;
                state.grab.trackRotation = state.oldTrackRotation;
                state.grab.throwOnDetach = state.oldThrowOnDetach;
            }
        }

        activeLocks.Clear();
        isLocked = false;
    }
}
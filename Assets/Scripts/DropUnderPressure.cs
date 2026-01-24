using UnityEngine;

public class DropUnderPressure : MonoBehaviour
{
    public Vector3 moveOffset = new Vector3(0, -0.02f, 0); 

    private Vector3 startLocalPos;

    private void Start()
    {
        startLocalPos = transform.localPosition;
    }

    public void UpdatePosition(float percentage)
    {
        transform.localPosition = Vector3.Lerp(startLocalPos, startLocalPos + moveOffset, Mathf.Clamp01(percentage));
    }
}

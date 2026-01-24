using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(HingeJoint))]
public class LeverReader : MonoBehaviour
{
    public UnityEvent<float> onLeverMoved;
    public UnityEvent onLeverDown;

    private HingeJoint joint;
    private float minLimit;
    private float maxLimit;

    void Start()
    {
        joint = GetComponent<HingeJoint>();
        minLimit = joint.limits.min;
        maxLimit = joint.limits.max;
    }

    void Update()
    {
        var currentAngle = joint.angle;
        var percentage = Mathf.InverseLerp(minLimit, maxLimit, currentAngle);
        
        onLeverMoved.Invoke(percentage);
        
        if(percentage >= 0.9f) onLeverDown.Invoke();
    }
}
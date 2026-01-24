using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class InteractWithTrigger : MonoBehaviour
{
    [Header("Input Settings")]
    public InputActionReference triggerActionLeft;
    public InputActionReference triggerActionRight;

    [Header("Events")]
    public UnityEvent<float> onPressureChanged;

    private XRSimpleInteractable _interactable;
    private bool _isHovered = false;

    private void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        _interactable.hoverEntered.AddListener(OnHoverEnter);
        _interactable.hoverExited.AddListener(OnHoverExit);

        if (triggerActionLeft != null) triggerActionLeft.action.Enable();
        if (triggerActionRight != null) triggerActionRight.action.Enable();
    }

    private void OnDisable()
    {
        _interactable.hoverEntered.RemoveListener(OnHoverEnter);
        _interactable.hoverExited.RemoveListener(OnHoverExit);
    }

    private void OnHoverEnter(HoverEnterEventArgs args) => _isHovered = true;
    private void OnHoverExit(HoverExitEventArgs args)
    {
        _isHovered = false;
        onPressureChanged.Invoke(0f);
    }

    private void Update()
    {
        if (_isHovered)
        {
            var pressure = 0f;

            if (triggerActionLeft)
                pressure = Mathf.Max(pressure, triggerActionLeft.action.ReadValue<float>());
            
            if (triggerActionRight)
                pressure = Mathf.Max(pressure, triggerActionRight.action.ReadValue<float>());

            onPressureChanged.Invoke(pressure);
        }
    }
}
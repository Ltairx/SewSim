using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace UnityEngine.XR.Content.Interaction
{
    public class XRLever : XRBaseInteractable
    {
        public enum LeverAxis
        {
            X,
            Y,
            Z
        }

        const float k_LeverDeadZone = 0.1f;

        [Header("Lever Settings")]
        [SerializeField]
        [Tooltip("The object that is visually grabbed and manipulated")]
        Transform m_Handle = null;

        [SerializeField]
        [Tooltip("The axis around which the lever rotates")]
        LeverAxis m_RotationAxis = LeverAxis.X;

        [SerializeField]
        [Tooltip("The value of the lever")]
        bool m_Value = false;

        [SerializeField]
        [Tooltip("If enabled, the lever will snap to the value position when released")]
        bool m_LockToValue;

        [SerializeField]
        [Tooltip("Angle of the lever in the 'on' position")]
        [Range(-180.0f, 180.0f)]
        float m_MaxAngle = 90.0f;

        [SerializeField]
        [Tooltip("Angle of the lever in the 'off' position")]
        [Range(-180.0f, 180.0f)]
        float m_MinAngle = -90.0f;

        [Header("Events")]
        [SerializeField]
        UnityEvent m_OnLeverActivate = new UnityEvent();

        [SerializeField]
        UnityEvent m_OnLeverDeactivate = new UnityEvent();

        IXRSelectInteractor m_Interactor;

        // Public properties
        public Transform handle { get => m_Handle; set => m_Handle = value; }
        public bool value { get => m_Value; set => SetValue(value, true); }
        public bool lockToValue { get => m_LockToValue; set => m_LockToValue = value; }
        public float maxAngle { get => m_MaxAngle; set => m_MaxAngle = value; }
        public float minAngle { get => m_MinAngle; set => m_MinAngle = value; }
        public UnityEvent onLeverActivate => m_OnLeverActivate;
        public UnityEvent onLeverDeactivate => m_OnLeverDeactivate;

        void Start()
        {
            SetValue(m_Value, true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            selectEntered.AddListener(StartGrab);
            selectExited.AddListener(EndGrab);
        }

        protected override void OnDisable()
        {
            selectEntered.RemoveListener(StartGrab);
            selectExited.RemoveListener(EndGrab);
            base.OnDisable();
        }

        void StartGrab(SelectEnterEventArgs args)
        {
            m_Interactor = args.interactorObject;
        }

        void EndGrab(SelectExitEventArgs args)
        {
            SetValue(m_Value, true);
            m_Interactor = null;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            {
                if (isSelected)
                {
                    UpdateValue();
                }
            }
        }

        Vector3 GetLookDirection()
        {
            Vector3 direction = m_Interactor.GetAttachTransform(this).position - m_Handle.position;
            direction = transform.InverseTransformDirection(direction);

            switch (m_RotationAxis)
            {
                case LeverAxis.X: direction.x = 0; break;
                case LeverAxis.Y: direction.y = 0; break;
                case LeverAxis.Z: direction.z = 0; break;
            }

            return direction.normalized;
        }

        void UpdateValue()
        {
            var lookDirection = GetLookDirection();
            float lookAngle = 0f;

            switch (m_RotationAxis)
            {
                case LeverAxis.X:
                    lookAngle = Mathf.Atan2(lookDirection.z, lookDirection.y) * Mathf.Rad2Deg;
                    break;
                case LeverAxis.Y:
                    lookAngle = Mathf.Atan2(lookDirection.x, lookDirection.z) * Mathf.Rad2Deg;
                    break;
                case LeverAxis.Z:
                    lookAngle = Mathf.Atan2(lookDirection.y, lookDirection.x) * Mathf.Rad2Deg;
                    break;
            }

            if (m_MinAngle < m_MaxAngle)
                lookAngle = Mathf.Clamp(lookAngle, m_MinAngle, m_MaxAngle);
            else
                lookAngle = Mathf.Clamp(lookAngle, m_MaxAngle, m_MinAngle);

            var maxAngleDistance = Mathf.Abs(m_MaxAngle - lookAngle);
            var minAngleDistance = Mathf.Abs(m_MinAngle - lookAngle);

            if (m_Value)
                maxAngleDistance *= (1.0f - k_LeverDeadZone);
            else
                minAngleDistance *= (1.0f - k_LeverDeadZone);

            var newValue = (maxAngleDistance < minAngleDistance);

            SetHandleAngle(lookAngle);
            SetValue(newValue);
        }

        void SetValue(bool isOn, bool forceRotation = false)
        {
            if (m_Value == isOn)
            {
                if (forceRotation)
                    SetHandleAngle(m_Value ? m_MaxAngle : m_MinAngle);
                return;
            }

            m_Value = isOn;

            if (m_Value) m_OnLeverActivate.Invoke();
            else m_OnLeverDeactivate.Invoke();

            if (!isSelected && (m_LockToValue || forceRotation))
                SetHandleAngle(m_Value ? m_MaxAngle : m_MinAngle);
        }

        void SetHandleAngle(float angle)
        {
            if (m_Handle != null)
            {
                switch (m_RotationAxis)
                {
                    case LeverAxis.X:
                        m_Handle.localRotation = Quaternion.Euler(angle, 0.0f, 0.0f);
                        break;
                    case LeverAxis.Y:
                        m_Handle.localRotation = Quaternion.Euler(0.0f, angle, 0.0f);
                        break;
                    case LeverAxis.Z:
                        m_Handle.localRotation = Quaternion.Euler(0.0f, 0.0f, angle);
                        break;
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            var angleStartPoint = transform.position;
            if (m_Handle != null) angleStartPoint = m_Handle.position;

            const float k_AngleLength = 0.25f;

            Vector3 axisVector = Vector3.right;
            Vector3 upVector = Vector3.up;

            switch (m_RotationAxis)
            {
                case LeverAxis.X:
                    axisVector = Vector3.right;
                    upVector = Vector3.up;
                    break;
                case LeverAxis.Y:
                    axisVector = Vector3.up;
                    upVector = Vector3.forward;
                    break;
                case LeverAxis.Z:
                    axisVector = Vector3.forward;
                    upVector = Vector3.up;
                    break;
            }

            var maxRotation = Quaternion.AngleAxis(m_MaxAngle, axisVector);
            var minRotation = Quaternion.AngleAxis(m_MinAngle, axisVector);

            var angleMaxPoint = angleStartPoint + transform.TransformDirection(maxRotation * upVector) * k_AngleLength;
            var angleMinPoint = angleStartPoint + transform.TransformDirection(minRotation * upVector) * k_AngleLength;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(angleStartPoint, angleMaxPoint);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(angleStartPoint, angleMinPoint);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(angleStartPoint - transform.TransformDirection(axisVector) * 0.1f, angleStartPoint + transform.TransformDirection(axisVector) * 0.1f);
        }

        void OnValidate()
        {
            SetHandleAngle(m_Value ? m_MaxAngle : m_MinAngle);
        }
    }
}
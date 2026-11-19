using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;

public sealed class CiudadCameraLook : MonoBehaviour
{
    [SerializeField] private float stickSensitivity = 110f;
    [SerializeField] private float mouseSensitivity = 0.08f;
    [SerializeField] private float smoothing = 12f;
    [SerializeField] private float pitchLimit = 80f;

    private Camera playerCamera;
    private Transform cameraRig;
    private Transform pitchPivot;
    private Quaternion initialPivotRotation;
    private InputDevice rightController;
    private Vector2 smoothedStick;
    private float pitch;

    private void Update()
    {
        if (VRPauseMenu.IsMenuOpen)
        {
            smoothedStick = Vector2.zero;
            return;
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                return;
            }

            cameraRig = playerCamera.transform.root;
            pitchPivot = playerCamera.transform.parent;
            if (pitchPivot == null)
            {
                Debug.LogError("La cámara del jugador necesita un padre para controlar el pitch.", playerCamera);
                enabled = false;
                return;
            }

            initialPivotRotation = pitchPivot.localRotation;
        }

        if (TryReadControllerStick(out Vector2 stick))
        {
            stick = Vector2.ClampMagnitude(stick, 1f);
            smoothedStick = Vector2.Lerp(smoothedStick, stick, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            ApplyRotation(smoothedStick.x * stickSensitivity * Time.deltaTime, smoothedStick.y * stickSensitivity * Time.deltaTime);
            return;
        }

        smoothedStick = Vector2.zero;
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            ApplyRotation(mouseDelta.x * mouseSensitivity, mouseDelta.y * mouseSensitivity);
        }
    }

    private void LateUpdate()
    {
        if (pitchPivot != null)
        {
            pitchPivot.localRotation = initialPivotRotation * Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private bool TryReadControllerStick(out Vector2 stick)
    {
        if (Gamepad.current != null)
        {
            stick = Gamepad.current.rightStick.ReadValue();
            return true;
        }

        if (!rightController.isValid)
        {
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        if (rightController.isValid &&
            rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out stick))
        {
            return true;
        }

        stick = Vector2.zero;
        return false;
    }

    private void ApplyRotation(float yawDelta, float pitchDelta)
    {
        if (cameraRig != null)
        {
            cameraRig.Rotate(Vector3.up, yawDelta, Space.World);
        }

        pitch = Mathf.Clamp(pitch - pitchDelta, -pitchLimit, pitchLimit);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
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
    private bool visorVRRealConectado;

    public static bool EsVisorVRRealActivo()
    {
        if (Application.platform == RuntimePlatform.Android)
            return true;

        if (!XRSettings.isDeviceActive)
            return false;

        string loadedDevice = XRSettings.loadedDeviceName != null ? XRSettings.loadedDeviceName.ToLowerInvariant() : "";
        if (loadedDevice.Contains("mock") || loadedDevice.Contains("simulat"))
            return false;

        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (head.isValid)
        {
            string headName = head.name != null ? head.name.ToLowerInvariant() : "";
            if (!headName.Contains("simulat"))
                return true;
        }

        return XRSettings.isDeviceActive;
    }

    public static void DesactivarSimuladorSiHayVisorReal()
    {
        if (!EsVisorVRRealActivo()) return;

        // Desactivar cualquier simulador XR por nombre de tipo sin referenciar tipos obsoletos
        MonoBehaviour[] comportamientos = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        for (int i = 0; i < comportamientos.Length; i++)
        {
            MonoBehaviour mb = comportamientos[i];
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            if (typeName == "XRDeviceSimulator" || typeName == "XRInteractionSimulator")
            {
                mb.enabled = false;
                mb.gameObject.SetActive(false);
            }
        }

        if (Camera.main != null)
        {
            TrackedPoseDriver tpd = Camera.main.GetComponent<TrackedPoseDriver>();
            if (tpd != null && !tpd.enabled)
                tpd.enabled = true;
        }
    }

    private void Awake()
    {
        DesactivarSimuladorSiHayVisorReal();
    }

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
                return;

            cameraRig = playerCamera.transform.root;
            pitchPivot = playerCamera.transform.parent;
            if (pitchPivot != null)
                initialPivotRotation = pitchPivot.localRotation;
        }

        visorVRRealConectado = EsVisorVRRealActivo();
        if (visorVRRealConectado)
        {
            DesactivarSimuladorSiHayVisorReal();
        }

        if (TryReadControllerStick(out Vector2 stick))
        {
            stick = Vector2.ClampMagnitude(stick, 1f);
            smoothedStick = Vector2.Lerp(smoothedStick, stick, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            float yawDelta = smoothedStick.x * stickSensitivity * Time.deltaTime;
            float pitchDelta = visorVRRealConectado ? 0f : (smoothedStick.y * stickSensitivity * Time.deltaTime);
            ApplyRotation(yawDelta, pitchDelta);
            return;
        }

        smoothedStick = Vector2.zero;

        if (!visorVRRealConectado && Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            if (mouseDelta.sqrMagnitude > 0.0001f)
            {
                ApplyRotation(mouseDelta.x * mouseSensitivity, mouseDelta.y * mouseSensitivity);
            }
        }
    }

    private void LateUpdate()
    {
        if (!visorVRRealConectado && pitchPivot != null)
        {
            pitchPivot.localRotation = initialPivotRotation * Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private bool TryReadControllerStick(out Vector2 stick)
    {
        if (!rightController.isValid)
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (rightController.isValid &&
            rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick) &&
            stick.sqrMagnitude > 0.02f)
        {
            return true;
        }

        XRController xrRight = XRController.rightHand;
        if (xrRight != null)
        {
            foreach (InputControl ctrl in xrRight.allControls)
            {
                if (ctrl is UnityEngine.InputSystem.Controls.Vector2Control v2)
                {
                    string n = v2.name.ToLowerInvariant();
                    if (n.Contains("thumbstick") || n.Contains("primary2daxis") || n.Contains("joystick"))
                    {
                        Vector2 val = v2.ReadValue();
                        if (val.sqrMagnitude > 0.02f)
                        {
                            stick = val;
                            return true;
                        }
                    }
                }
            }
        }

        if (Gamepad.current != null)
        {
            Vector2 gpStick = Gamepad.current.rightStick.ReadValue();
            if (gpStick.sqrMagnitude > 0.02f)
            {
                stick = gpStick;
                return true;
            }
        }

        stick = Vector2.zero;
        return false;
    }

    private void ApplyRotation(float yawDelta, float pitchDelta)
    {
        if (cameraRig != null && Mathf.Abs(yawDelta) > 0.0001f)
        {
            if (visorVRRealConectado && playerCamera != null)
            {
                cameraRig.RotateAround(playerCamera.transform.position, Vector3.up, yawDelta);
            }
            else
            {
                cameraRig.Rotate(Vector3.up, yawDelta, UnityEngine.Space.World);
            }
        }

        if (!visorVRRealConectado)
        {
            pitch = Mathf.Clamp(pitch - pitchDelta, -pitchLimit, pitchLimit);
        }
    }
}

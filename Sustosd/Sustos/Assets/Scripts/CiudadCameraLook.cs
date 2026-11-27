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
    private bool characterControllerAjustado;
    private bool xriTurnProviderDetectado;
    private float proximoChequeoRig = 0f;

    // Por defecto en Meta Quest 2 usamos Giro por Grados (Snap Turn 45°) y velocidad de caminata interior suave (1.85 m/s) para CERO náusea
    public static bool GiroSnapAntiNauseaActivo { get; private set; } = true;

    public static void AlternarModoGiroConfortVR()
    {
        GiroSnapAntiNauseaActivo = !GiroSnapAntiNauseaActivo;
        AplicarConfiguracionConfortLocomocion(GiroSnapAntiNauseaActivo);
    }

    public static void AplicarConfiguracionConfortLocomocion(bool usarSnapTurnSinNausea)
    {
        GiroSnapAntiNauseaActivo = usarSnapTurnSinNausea;
        MonoBehaviour[] mbs = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        for (int i = 0; i < mbs.Length; i++)
        {
            MonoBehaviour mb = mbs[i];
            if (mb == null) continue;
            System.Type tipo = mb.GetType();
            string tn = tipo.Name;

            // 1. Limitar velocidad de caminata continua a ritmo humano interior (1.85 m/s) para evitar mareo vestibular
            if (tn.Contains("MoveProvider"))
            {
                var propSpeed = tipo.GetProperty("moveSpeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (propSpeed != null && propSpeed.CanRead && propSpeed.CanWrite && propSpeed.PropertyType == typeof(float))
                {
                    float actual = (float)propSpeed.GetValue(mb, null);
                    if (actual > 1.90f || actual < 0.8f)
                        propSpeed.SetValue(mb, 1.85f, null);
                }
            }

            // 2. Activar Snap Turn (45° instantáneo = cero mareo) o Giro Suave moderado (65°/s) según preferencia en Opciones
            var propSmoothTurn = tipo.GetProperty("smoothTurnEnabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (propSmoothTurn != null && propSmoothTurn.CanWrite && propSmoothTurn.PropertyType == typeof(bool))
            {
                propSmoothTurn.SetValue(mb, !usarSnapTurnSinNausea, null);
            }

            if (tn.Contains("ContinuousTurnProvider"))
            {
                var propTurnSpeed = tipo.GetProperty("turnSpeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (propTurnSpeed != null && propTurnSpeed.CanWrite && propTurnSpeed.PropertyType == typeof(float))
                    propTurnSpeed.SetValue(mb, 65f, null);
            }
            else if (tn.Contains("SnapTurnProvider"))
            {
                var propTurnAmount = tipo.GetProperty("turnAmount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (propTurnAmount != null && propTurnAmount.CanWrite && propTurnAmount.PropertyType == typeof(float))
                    propTurnAmount.SetValue(mb, 45f, null);
            }

            // 3. Asegurar activa la viñeta de confort periférica (TunnelingVignette) si existe en el rig
            if (tn.Contains("TunnelingVignette"))
            {
                mb.enabled = true;
                if (!mb.gameObject.activeSelf)
                    mb.gameObject.SetActive(true);
            }
        }
    }

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
        AsegurarLocomocionEscalerasYTurnVR();
    }

    private void AsegurarLocomocionEscalerasYTurnVR()
    {
        // En House_Colliders.fbx la rampa de las escaleras tiene ~52.4° de pendiente;
        // elevar slopeLimit a 62° y stepOffset a 0.42m garantiza subir/bajar sin atascos en Meta Quest 2.
        CharacterController[] controllers = FindObjectsByType<CharacterController>(FindObjectsInactive.Include);
        for (int i = 0; i < controllers.Length; i++)
        {
            CharacterController cc = controllers[i];
            if (cc == null) continue;
            if (cc.slopeLimit < 62f) cc.slopeLimit = 62f;
            if (cc.stepOffset < 0.42f) cc.stepOffset = 0.42f;
            cc.skinWidth = Mathf.Clamp(cc.skinWidth, 0.02f, 0.04f);
            cc.minMoveDistance = 0.0005f;
            characterControllerAjustado = true;
        }

        if (EsVisorVRRealActivo())
        {
            AplicarConfiguracionConfortLocomocion(GiroSnapAntiNauseaActivo);
        }

        // Detectar si el prefab de XR Origin ya tiene un TurnProvider activo de XRI para no duplicar el giro del stick derecho en Quest 2
        xriTurnProviderDetectado = false;
        MonoBehaviour[] mbs = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
        for (int i = 0; i < mbs.Length; i++)
        {
            MonoBehaviour mb = mbs[i];
            if (mb == null || !mb.enabled || !mb.gameObject.activeInHierarchy) continue;
            string tn = mb.GetType().Name;
            if (tn.Contains("TurnProvider"))
            {
                xriTurnProviderDetectado = true;
                break;
            }
        }
    }

    private void Update()
    {
        if (VRPauseMenu.IsMenuOpen)
        {
            smoothedStick = Vector2.zero;
            return;
        }

        if (!characterControllerAjustado || Time.unscaledTime >= proximoChequeoRig)
        {
            proximoChequeoRig = Time.unscaledTime + 4f;
            AsegurarLocomocionEscalerasYTurnVR();
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
            // Si XRI ya maneja el giro del stick derecho en Quest 2, no duplicar rotación
            if (xriTurnProviderDetectado)
            {
                smoothedStick = Vector2.zero;
                return;
            }
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

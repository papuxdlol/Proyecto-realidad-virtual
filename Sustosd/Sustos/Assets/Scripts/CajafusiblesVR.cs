using UnityEngine;
using UnityEngine.EventSystems;
using Keyboard = UnityEngine.InputSystem.Keyboard;
using Mouse = UnityEngine.InputSystem.Mouse;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(ObjetoAnomalia), typeof(XRSimpleInteractable))]
public class CajaFusiblesVR : MonoBehaviour
{
    public Transform palancaVisual;
    public Light luzIndicadora;
    public AudioSource sonidoPalanca;

    private ObjetoAnomalia anomaliaApagon;
    private XRSimpleInteractable interactable;
    private bool rayoEncima = false;
    private float tiempoTironManual = -10f;
    private int ultimaInteraccionFrame = -1;
    private float ultimoTiempoInteraccion = -1f;
    private bool triggerAnteriorVR = false;

    private static AudioClip clipPalancaProcedural;

    void Awake()
    {
        anomaliaApagon = GetComponent<ObjetoAnomalia>();
        interactable = GetComponent<XRSimpleInteractable>();

        if (palancaVisual == null)
        {
            Transform piv = transform.Find("Pivote_Palanca");
            if (piv != null) palancaVisual = piv;
        }

        if (luzIndicadora == null)
            luzIndicadora = GetComponentInChildren<Light>(true);

        if (sonidoPalanca == null)
        {
            sonidoPalanca = GetComponent<AudioSource>();
            if (sonidoPalanca == null)
                sonidoPalanca = gameObject.AddComponent<AudioSource>();
        }

        sonidoPalanca.playOnAwake = false;
        sonidoPalanca.spatialBlend = 1f;
        if (sonidoPalanca.clip == null)
            sonidoPalanca.clip = ObtenerClipPalanca();

        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null)
            bc = gameObject.AddComponent<BoxCollider>();

        // Agrandar ligeramente el BoxCollider hacia adelante para que ningún collider de pared lo tape
        bc.isTrigger = false;
        bc.size = new Vector3(1.5f, 1.4f, 2.4f);
        bc.center = new Vector3(0f, 0f, -0.3f);
        bc.enabled = true;

        if (interactable != null)
        {
            interactable.colliders.Clear();
            interactable.colliders.Add(bc);

            if (Application.isPlaying && interactable.interactionManager != null && interactable.isActiveAndEnabled)
            {
                XRInteractionManager mgr = interactable.interactionManager;
                mgr.UnregisterInteractable(interactable as IXRInteractable);
                mgr.RegisterInteractable(interactable as IXRInteractable);
            }
        }
    }

    void OnEnable()
    {
        if (interactable == null)
            interactable = GetComponent<XRSimpleInteractable>();

        if (interactable != null)
        {
            interactable.selectEntered.AddListener(AlTirarPalanca);
            interactable.activated.AddListener(AlActivarPalanca);
            interactable.hoverEntered.AddListener(OnHoverEnter);
            interactable.hoverExited.AddListener(OnHoverExit);
        }
    }

    void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(AlTirarPalanca);
            interactable.activated.RemoveListener(AlActivarPalanca);
            interactable.hoverEntered.RemoveListener(OnHoverEnter);
            interactable.hoverExited.RemoveListener(OnHoverExit);
        }
        rayoEncima = false;
    }

    private void OnHoverEnter(HoverEnterEventArgs args) => rayoEncima = true;
    private void OnHoverExit(HoverExitEventArgs args) => rayoEncima = interactable != null && interactable.isHovered;

    void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        RepararFusibles(null);
    }

    void Update()
    {
        if (rayoEncima || (interactable != null && interactable.isHovered))
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            {
                Mouse mouse = Mouse.current;
                Keyboard teclado = Keyboard.current;
                bool entradaPC =
                    (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                    (teclado != null && (teclado.gKey.wasPressedThisFrame || teclado.eKey.wasPressedThisFrame || teclado.fKey.wasPressedThisFrame));

                bool triggerVR = LeerTriggerVR();
                bool entradaVR = triggerVR && !triggerAnteriorVR;
                triggerAnteriorVR = triggerVR;

                if (entradaPC || entradaVR)
                    RepararFusibles(null);
            }
        }
        else
        {
            triggerAnteriorVR = false;
        }

        bool apagonActivo = anomaliaApagon != null && anomaliaApagon.estaActiva;
        bool animandoTiron = (Time.unscaledTime - tiempoTironManual) < 0.35f;

        if (palancaVisual != null)
        {
            float anguloObjetivo = (apagonActivo || animandoTiron) ? 45f : -35f;
            palancaVisual.localRotation = Quaternion.Lerp(
                palancaVisual.localRotation,
                Quaternion.Euler(anguloObjetivo, 0f, 0f),
                Time.deltaTime * 10f);
        }

        if (luzIndicadora != null)
        {
            luzIndicadora.color = apagonActivo ? Color.red : Color.green;
        }
    }

    private bool LeerTriggerVR()
    {
        InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (right.isValid && right.TryGetFeatureValue(CommonUsages.triggerButton, out bool rTrig) && rTrig)
            return true;

        InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (left.isValid && left.TryGetFeatureValue(CommonUsages.triggerButton, out bool lTrig) && lTrig)
            return true;

        return false;
    }

    private void AlTirarPalanca(SelectEnterEventArgs args) => RepararFusibles(args.interactorObject);
    private void AlActivarPalanca(ActivateEventArgs args) => RepararFusibles(args.interactorObject);

    public void RepararFusibles(IXRInteractor interactor)
    {
        if (ultimaInteraccionFrame == Time.frameCount) return;
        if (Time.unscaledTime - ultimoTiempoInteraccion < 0.25f) return;

        ultimaInteraccionFrame = Time.frameCount;
        ultimoTiempoInteraccion = Time.unscaledTime;
        tiempoTironManual = Time.unscaledTime;

        if (sonidoPalanca != null && sonidoPalanca.clip != null)
            sonidoPalanca.PlayOneShot(sonidoPalanca.clip);

        if (interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.75f, 0.2f);

        // Si había un apagón activo, al tocar la caja se restauran todas las luces de la casa
        if (anomaliaApagon != null && anomaliaApagon.estaActiva)
        {
            anomaliaApagon.RestaurarNormalidad();
            if (TabletSistemaVR.Instancia != null)
                TabletSistemaVR.Instancia.NotificarFusiblesReparados();
            Debug.Log("¡Caja de fusibles reparada manualmente en el Garaje!");
        }
        else
        {
            // Asegurar que todos los switches estén desbloqueados si se acciona la caja de fusibles
            foreach (var sw in FindObjectsByType<InterruptorLuz>(FindObjectsSortMode.None))
            {
                if (sw != null && sw.bloqueadaPorAnomalia)
                {
                    sw.bloqueadaPorAnomalia = false;
                    sw.CambiarEstado(true);
                }
            }
            Debug.Log("[Caja de Fusibles] Palanca verificada: energía estable en toda la casa.");
        }
    }

    private static AudioClip ObtenerClipPalanca()
    {
        if (clipPalancaProcedural == null)
        {
            int sampleRate = 22050;
            int samples = sampleRate / 6;
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-i / (samples * 0.28f));
                float clank = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.45f + (Random.value * 2f - 1f) * 0.45f;
                data[i] = clank * env;
            }
            clipPalancaProcedural = AudioClip.Create("LeverClank", samples, 1, sampleRate, false);
            clipPalancaProcedural.SetData(data, 0);
        }
        return clipPalancaProcedural;
    }
}

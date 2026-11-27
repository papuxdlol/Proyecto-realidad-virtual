using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
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
    public AudioClip clipFuseBoxReal;

    [Header("Estado de Energía General")]
    public bool energiaGeneralActiva = true;
    public float delayConmutacion = 0.18f;
    public float cooldownInteraccion = 0.50f;

    private ObjetoAnomalia anomaliaApagon;
    private XRSimpleInteractable interactable;
    private Coroutine rutinaConmutacion;
    private int ultimaInteraccionFrame = -1;
    private float ultimoTiempoInteraccion = -10f;
    private bool apagonEstabaActivo = false;

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

        // Calibrar el LED indicador para que sea un foco pequeño de tablero y no ilumine todo el garaje de verde
        if (luzIndicadora != null)
        {
            luzIndicadora.intensity = 0.35f;
            luzIndicadora.range = 0.85f;
            luzIndicadora.shadows = LightShadows.None;
        }

        if (sonidoPalanca == null)
        {
            sonidoPalanca = GetComponent<AudioSource>();
            if (sonidoPalanca == null)
                sonidoPalanca = gameObject.AddComponent<AudioSource>();
        }

        sonidoPalanca.playOnAwake = false;
        sonidoPalanca.spatialBlend = 1f;
        AudioClip mejorClip = ObtenerClipPalanca();
        if (mejorClip != null)
            sonidoPalanca.clip = mejorClip;

        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null)
            bc = gameObject.AddComponent<BoxCollider>();

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

        energiaGeneralActiva = true;
        InterruptorLuz.EnergiaGeneralCasaActiva = true;
    }

    void OnEnable()
    {
        if (interactable == null)
            interactable = GetComponent<XRSimpleInteractable>();

        if (interactable != null)
        {
            interactable.selectEntered.AddListener(AlTirarPalanca);
            interactable.activated.AddListener(AlActivarPalanca);
        }
    }

    void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(AlTirarPalanca);
            interactable.activated.RemoveListener(AlActivarPalanca);
        }
    }

    void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        RepararFusibles(null);
    }

    void Update()
    {
        bool apagonActivo = anomaliaApagon != null && anomaliaApagon.estaActiva;

        // Si se acaba de activar la anomalía de apagón en la caja, bajar la palanca automáticamente
        if (apagonActivo && !apagonEstabaActivo)
        {
            energiaGeneralActiva = false;
            InterruptorLuz.EnergiaGeneralCasaActiva = false;
            InteractivoMuebleYElectronicoVR.NotificarCambioEnergiaGeneral(false);

            InterruptorLuz[] todos = FindObjectsByType<InterruptorLuz>(FindObjectsInactive.Include);
            for (int i = 0; i < todos.Length; i++)
            {
                if (todos[i] != null)
                    todos[i].RefrescarSegunEnergiaGeneral();
            }
        }
        apagonEstabaActivo = apagonActivo;

        bool palancaAbajo = apagonActivo || !energiaGeneralActiva;

        if (palancaVisual != null)
        {
            float anguloObjetivo = palancaAbajo ? 45f : -35f;
            palancaVisual.localRotation = Quaternion.Lerp(
                palancaVisual.localRotation,
                Quaternion.Euler(anguloObjetivo, 0f, 0f),
                Time.deltaTime * 10f);
        }

        if (luzIndicadora != null)
        {
            luzIndicadora.color = palancaAbajo ? new Color(1f, 0.15f, 0.15f) : new Color(0.15f, 0.95f, 0.25f);
            luzIndicadora.intensity = 0.35f;
            luzIndicadora.range = 0.85f;
        }
    }

    private void AlTirarPalanca(SelectEnterEventArgs args) => RepararFusibles(args.interactorObject);
    private void AlActivarPalanca(ActivateEventArgs args) => RepararFusibles(args.interactorObject);

    public void RepararFusibles(IXRInteractor interactor)
    {
        if (ultimaInteraccionFrame == Time.frameCount) return;
        if (Time.unscaledTime - ultimoTiempoInteraccion < cooldownInteraccion) return;

        ultimaInteraccionFrame = Time.frameCount;
        ultimoTiempoInteraccion = Time.unscaledTime;

        if (sonidoPalanca != null)
        {
            AudioClip c = clipFuseBoxReal != null ? clipFuseBoxReal : sonidoPalanca.clip;
            if (c != null) sonidoPalanca.PlayOneShot(c, 0.90f);
        }

        if (interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.75f, 0.2f);

        if (rutinaConmutacion != null)
            StopCoroutine(rutinaConmutacion);

        rutinaConmutacion = StartCoroutine(RutinaConmutarCajaConDelay());
    }

    private IEnumerator RutinaConmutarCajaConDelay()
    {
        bool habiaApagonAnomalo = anomaliaApagon != null && anomaliaApagon.estaActiva;
        bool nuevoEstadoEnergia = habiaApagonAnomalo ? true : !energiaGeneralActiva;

        // Mover la palanca visual inmediatamente y esperar el delay mecánico antes de cortar/dar luz
        energiaGeneralActiva = nuevoEstadoEnergia;

        yield return new WaitForSeconds(delayConmutacion);

        InterruptorLuz.EnergiaGeneralCasaActiva = energiaGeneralActiva;
        InteractivoMuebleYElectronicoVR.NotificarCambioEnergiaGeneral(energiaGeneralActiva);

        if (habiaApagonAnomalo)
        {
            anomaliaApagon.RestaurarNormalidad();
            if (TabletSistemaVR.Instancia != null)
                TabletSistemaVR.Instancia.NotificarFusiblesReparados();
            Debug.Log("[Caja de Fusibles] ¡Apagón reparado! Energía restaurada en toda la casa.");
        }
        else
        {
            InterruptorLuz[] todos = FindObjectsByType<InterruptorLuz>(FindObjectsInactive.Include);
            foreach (InterruptorLuz sw in todos)
            {
                if (sw == null) continue;
                if (energiaGeneralActiva && sw.bloqueadaPorAnomalia)
                    sw.bloqueadaPorAnomalia = false;
                sw.RefrescarSegunEnergiaGeneral();
            }

            Debug.Log(energiaGeneralActiva
                ? "[Caja de Fusibles] Palanca ARRIBA: Energía general ENCENDIDA en toda la casa."
                : "[Caja de Fusibles] Palanca ABAJO: Energía general APAGADA en toda la casa.");
        }

        rutinaConmutacion = null;
    }

    private AudioClip ObtenerClipPalanca()
    {
        if (clipFuseBoxReal != null)
            return clipFuseBoxReal;

#if UNITY_EDITOR
        AudioClip realEd = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-fuse-box-klick-106954.mp3");
        if (realEd != null)
        {
            clipFuseBoxReal = realEd;
            return realEd;
        }
#endif
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

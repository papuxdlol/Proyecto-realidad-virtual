using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DefaultExecutionOrder(-100)]
public class PuertaAutoCierre : MonoBehaviour
{
    [Header("Configuración de Giro")]
    public float anguloAbierta = 90f;
    public float velocidadGiro = 160f;
    public float segundosParaCerrarse = 6f;

    [Header("Sonidos")]
    public AudioSource audioPuerta;
    public AudioClip sonidoAbrir;
    public AudioClip sonidoCerrar;
    public AudioClip sonidoAzotar;

    [HideInInspector] public bool estaAbierta = false;

    private Vector3 puntoBisagraMundial;
    private Vector3 posCerradaMundial;
    private Quaternion rotCerradaMundial;
    private float anguloActual = 0f;
    private Coroutine rutinaAutoCierre;
    private XRSimpleInteractable interactable;
    private BoxCollider hitboxPositivo;
    private int ultimaInteraccionFrame = -1;
    private float ultimoTiempoInteraccion = -10f;

    private static AudioClip clipPuertaProcedural;

    public static bool EsObjetoDeHouseCollidersOInvisible(Transform t)
    {
        if (t == null) return true;

        Transform actual = t;
        while (actual != null)
        {
            if (actual.name == "House_Colliders")
                return true;
            actual = actual.parent;
        }

        MeshRenderer mr = t.GetComponent<MeshRenderer>();
        if (mr == null || !mr.enabled)
            return true;

        return false;
    }

    void Awake()
    {
        // Si este script fue puesto por error en un clon de colisión invisible dentro de House_Colliders, desactivarlo
        if (EsObjetoDeHouseCollidersOInvisible(transform))
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Transform hb = transform.Find("Hitbox_VR_Positivo");
            if (hb != null) Destroy(hb.gameObject);

            Destroy(this);
            return;
        }

        posCerradaMundial = transform.position;
        rotCerradaMundial = transform.rotation;

        CalcularBisagraYHitboxPositivo();
        AsegurarAudioPuerta();
        DesactivarColliderEstaticoDuplicadoEnHouseColliders();
    }

    public void CalcularBisagraYHitboxPositivo()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        Bounds b = mr != null ? mr.bounds : new Bounds(transform.position, new Vector3(0.9f, 2.1f, 0.1f));

        // Tomamos el borde lateral más ancho como bisagra en espacio mundial
        if (b.extents.x > b.extents.z)
            puntoBisagraMundial = new Vector3(b.min.x, b.center.y, b.center.z);
        else
            puntoBisagraMundial = new Vector3(b.center.x, b.center.y, b.min.z);

        // 1. Desactivar cualquier BoxCollider en el objeto con escala negativa (-95.79)
        BoxCollider colliderDirecto = GetComponent<BoxCollider>();
        if (colliderDirecto != null)
        {
            colliderDirecto.enabled = false;
        }

        // 2. Crear hijo Hitbox_VR_Positivo con escala mundial (1, 1, 1)
        Transform hijoHitbox = transform.Find("Hitbox_VR_Positivo");
        if (hijoHitbox == null)
        {
            GameObject objHitbox = new GameObject("Hitbox_VR_Positivo");
            objHitbox.layer = 0;
            hijoHitbox = objHitbox.transform;
            hijoHitbox.SetParent(transform, false);
        }

        hijoHitbox.position = b.center;
        hijoHitbox.rotation = Quaternion.identity;

        Vector3 ls = transform.lossyScale;
        hijoHitbox.localScale = new Vector3(
            Mathf.Abs(ls.x) > 0.0001f ? 1f / ls.x : 1f,
            Mathf.Abs(ls.y) > 0.0001f ? 1f / ls.y : 1f,
            Mathf.Abs(ls.z) > 0.0001f ? 1f / ls.z : 1f
        );

        hitboxPositivo = hijoHitbox.GetComponent<BoxCollider>();
        if (hitboxPositivo == null)
            hitboxPositivo = hijoHitbox.gameObject.AddComponent<BoxCollider>();

        // Asegurar grosor mínimo de 20cm para que sobresalga a ambos lados de cualquier marco/pared
        Vector3 tamanoMundial = new Vector3(
            Mathf.Max(b.size.x, 0.20f),
            Mathf.Max(b.size.y, 0.40f),
            Mathf.Max(b.size.z, 0.20f)
        );
        hitboxPositivo.isTrigger = false;
        hitboxPositivo.center = Vector3.zero;
        hitboxPositivo.size = tamanoMundial;
        hitboxPositivo.enabled = true;

        // 3. Configurar XRSimpleInteractable y vincular el BoxCollider de escala positiva
        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null)
            interactable = gameObject.AddComponent<XRSimpleInteractable>();

        interactable.colliders.Clear();
        interactable.colliders.Add(hitboxPositivo);

        if (Application.isPlaying && interactable.interactionManager != null && interactable.isActiveAndEnabled)
        {
            XRInteractionManager mgr = interactable.interactionManager;
            mgr.UnregisterInteractable(interactable as IXRInteractable);
            mgr.RegisterInteractable(interactable as IXRInteractable);
        }
    }

    private void DesactivarColliderEstaticoDuplicadoEnHouseColliders()
    {
        if (hitboxPositivo == null) return;

        GameObject houseColliders = GameObject.Find("House_Colliders");
        if (houseColliders == null) return;

        Vector3 puertaCenter = hitboxPositivo.transform.position;
        string miNombre = gameObject.name.ToLower();

        foreach (Collider col in houseColliders.GetComponentsInChildren<Collider>(true))
        {
            if (col == null) continue;
            string colName = col.gameObject.name.ToLower();

            bool nombreDePuerta = (colName.StartsWith("door") || colName.StartsWith("closet_door")) && !colName.Contains("frame");
            if (nombreDePuerta || colName == miNombre)
            {
                col.enabled = false;
                PuertaAutoCierre puertaFalsa = col.GetComponent<PuertaAutoCierre>();
                if (puertaFalsa != null) Destroy(puertaFalsa);
                continue;
            }

            if (col.bounds.size.magnitude < 3.2f && Vector3.Distance(col.bounds.center, puertaCenter) < 0.25f)
            {
                col.enabled = false;
            }
        }
    }

    private void AsegurarAudioPuerta()
    {
        if (audioPuerta == null)
        {
            audioPuerta = GetComponent<AudioSource>();
            if (audioPuerta == null)
                audioPuerta = gameObject.AddComponent<AudioSource>();
        }

        audioPuerta.playOnAwake = false;
        audioPuerta.spatialBlend = 1f;
        audioPuerta.minDistance = 0.8f;
        audioPuerta.maxDistance = 12f;

        if (sonidoAbrir == null && audioPuerta.clip != null)
            sonidoAbrir = audioPuerta.clip;

        if (sonidoAbrir == null)
            sonidoAbrir = ObtenerClipPuerta();
        if (sonidoCerrar == null)
            sonidoCerrar = sonidoAbrir;
    }

    public Vector3 ObtenerPosicionMundial()
    {
        if (hitboxPositivo != null) return hitboxPositivo.transform.position;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        return mr != null ? mr.bounds.center : transform.position;
    }

    public Vector3 ObtenerPuntoMasCercano(Vector3 puntoOrigen)
    {
        if (hitboxPositivo != null && hitboxPositivo.enabled)
            return hitboxPositivo.bounds.ClosestPoint(puntoOrigen);

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
            return mr.bounds.ClosestPoint(puntoOrigen);

        return transform.position;
    }

    void OnEnable()
    {
        if (interactable == null)
            interactable = GetComponent<XRSimpleInteractable>();

        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelect);
            interactable.activated.AddListener(OnActivate);
        }
    }

    void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelect);
            interactable.activated.RemoveListener(OnActivate);
        }
    }

    private void OnSelect(SelectEnterEventArgs args) => ProcesarInteraccion(args.interactorObject);
    private void OnActivate(ActivateEventArgs args) => ProcesarInteraccion(args.interactorObject);

    void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        ProcesarInteraccion(null);
    }

    private void ProcesarInteraccion(IXRInteractor interactor)
    {
        if (interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.5f, 0.12f);

        AlternarPuerta();
    }

    public void AlternarPuerta()
    {
        // Protección anti-doble disparo en el mismo frame (p. ej. OnMouseDown/XRI + InteractorUniversalVR a la vez)
        if (ultimaInteraccionFrame == Time.frameCount) return;
        if (Time.unscaledTime - ultimoTiempoInteraccion < 0.30f) return;

        ultimaInteraccionFrame = Time.frameCount;
        ultimoTiempoInteraccion = Time.unscaledTime;

        if (!estaAbierta) AbrirPuerta();
        else CerrarPuerta(false);
    }

    public void AbrirPuerta()
    {
        if (estaAbierta) return;
        estaAbierta = true;
        if (audioPuerta && sonidoAbrir) audioPuerta.PlayOneShot(sonidoAbrir);

        if (rutinaAutoCierre != null) StopCoroutine(rutinaAutoCierre);
        rutinaAutoCierre = StartCoroutine(TemporizadorCierre());
    }

    // Usado cuando una anomalía Unknown abre la puerta sola: permanece abierta para que el jugador la descubra
    public void AbrirPuertaPorAnomalia()
    {
        if (rutinaAutoCierre != null)
        {
            StopCoroutine(rutinaAutoCierre);
            rutinaAutoCierre = null;
        }
        estaAbierta = true;
        if (audioPuerta && sonidoAbrir) audioPuerta.PlayOneShot(sonidoAbrir);
    }

    public void CerrarPuerta(bool esAzoteAnomalia)
    {
        if (!estaAbierta && Mathf.Abs(anguloActual) < 0.1f) return;
        estaAbierta = false;
        if (rutinaAutoCierre != null)
        {
            StopCoroutine(rutinaAutoCierre);
            rutinaAutoCierre = null;
        }

        if (audioPuerta)
        {
            AudioClip clip = (esAzoteAnomalia && sonidoAzotar != null) ? sonidoAzotar : sonidoCerrar;
            if (clip != null) audioPuerta.PlayOneShot(clip);
        }
    }

    private IEnumerator TemporizadorCierre()
    {
        yield return new WaitForSeconds(segundosParaCerrarse);
        CerrarPuerta(false);
    }

    void Update()
    {
        // Animación determinista de giro desde el estado cerrado (sin acumular deriva de RotateAround)
        float objetivo = estaAbierta ? anguloAbierta : 0f;
        if (Mathf.Abs(anguloActual - objetivo) > 0.05f)
        {
            anguloActual = Mathf.MoveTowards(anguloActual, objetivo, velocidadGiro * Time.deltaTime);
            AplicarAnguloDesdeCerrada(anguloActual);
        }
        else if (!estaAbierta && anguloActual != 0f)
        {
            anguloActual = 0f;
            AplicarAnguloDesdeCerrada(0f);
        }
    }

    private void AplicarAnguloDesdeCerrada(float angulo)
    {
        Quaternion rotGiro = Quaternion.AngleAxis(angulo, Vector3.up);
        Vector3 offsetDesdeBisagra = posCerradaMundial - puntoBisagraMundial;
        transform.position = puntoBisagraMundial + (rotGiro * offsetDesdeBisagra);
        transform.rotation = rotGiro * rotCerradaMundial;
    }

    private static AudioClip ObtenerClipPuerta()
    {
        if (clipPuertaProcedural == null)
        {
            int sampleRate = 22050;
            int samples = sampleRate / 4; // 250ms
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-i / (samples * 0.35f));
                float creak = Mathf.Sin(2f * Mathf.PI * (140f + 40f * Mathf.Sin(t * 25f)) * t) * 0.3f;
                float thud = (Random.value * 2f - 1f) * Mathf.Exp(-i / (samples * 0.1f)) * 0.4f;
                data[i] = (creak + thud) * env;
            }
            clipPuertaProcedural = AudioClip.Create("DoorSound", samples, 1, sampleRate, false);
            clipPuertaProcedural.SetData(data, 0);
        }
        return clipPuertaProcedural;
    }
}

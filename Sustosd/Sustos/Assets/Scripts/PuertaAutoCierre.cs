using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DefaultExecutionOrder(-100)]
public class PuertaAutoCierre : MonoBehaviour
{
    [Header("Configuración de Giro y Auto-Cierre")]
    public float anguloAbierta = 90f;
    public float velocidadGiro = 145f;
    public float velocidadGiroAnomalia = 48f;
    public float segundosParaCerrarse = 5.5f;
    public float delayAccion = 0.14f;
    public float cooldownInteraccion = 0.55f;

    [Header("Sonidos Reales de Puerta")]
    public AudioSource audioPuerta;
    public AudioClip sonidoAbrir;
    public AudioClip sonidoCerrar;
    public AudioClip sonidoAbrirAnomalia;
    public AudioClip sonidoAzotar;

    [HideInInspector] public bool estaAbierta = false;
    [HideInInspector] public bool abiertaPorAnomalia = false;
    private bool entreabiertaPorPreacher = false;

    private Vector3 puntoBisagraMundial;
    private Vector3 posCerradaMundial;
    private Quaternion rotCerradaMundial;
    private float anguloActual = 0f;
    private Coroutine rutinaAutoCierre;
    private Coroutine rutinaDelayAccion;
    private Coroutine rutinaTemblorEntidad;
    private XRSimpleInteractable interactable;
    private BoxCollider hitboxPositivo;
    private int ultimaInteraccionFrame = -1;
    private float ultimoTiempoInteraccion = -10f;
    private bool pendienteSonidoCierre = false;
    private bool cierreEsAzote = false;

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

    public static float ObtenerAnguloAperturaOptimo(string nombreObjeto)
    {
        if (string.IsNullOrEmpty(nombreObjeto)) return 90f;
        string n = nombreObjeto.Trim().ToLowerInvariant();

        switch (n)
        {
            // Piso 1
            case "door": return 90f;             // Entrada principal -> abre hacia el vestíbulo interior
            case "closet_door": return 90f;      // Clóset vestíbulo hoja derecha -> abre hacia afuera al vestíbulo
            case "closet_door_01": return -90f;  // Clóset vestíbulo hoja izquierda -> abre hacia afuera al vestíbulo
            case "closet_door_02": return 90f;   // Clóset pasillo sala -> abre hacia afuera al pasillo
            case "door_007": return 90f;         // Puerta Lavandería restaurada -> abre hacia adentro pegada al muro derecho
            case "door_008": return 90f;         // Puerta interior Garaje -> abre hacia el pasillo sin chocar con escalera
            case "door_009": return -90f;        // Puerta trasera Garaje -> abre hacia adentro del garaje sin chocar con macetas
            case "garden_gate": return -90f;     // Puerta jardín hoja derecha -> abre hacia el patio trasero
            case "garden_gate_01": return 90f;   // Puerta jardín hoja izquierda -> abre hacia el patio trasero

            // Piso 2
            case "door_001": return -90f;        // Recámara 2 (Frente) -> abre hacia adentro de la recámara
            case "door_002": return -90f;        // Clóset Recámara 1 -> abre hacia la recámara sin chocar con muros
            case "door_003": return -90f;        // Baño 1 Arriba -> abre hacia afuera al pasillo sin golpear el lavabo
            case "door_004": return -90f;        // Recámara 1 (Oeste) -> abre hacia el descanso sin atravesar muro de escalera
            case "door_005": return -90f;        // Baño 2 Arriba -> abre hacia adentro del baño
            case "door_006": return 90f;         // Recámara 3 (Sur) -> abre hacia adentro de la recámara
            case "closet_door_03": return 78f;   // Clóset Recámara 3 -> abre hacia la recámara deteniéndose antes de la cómoda
        }

        if (n.Contains("01") && (n.Contains("cabinet_door") || n.Contains("washbasin_door")))
            return -90f;

        return 90f;
    }

    void Awake()
    {
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

        string n = gameObject.name.Trim().ToLowerInvariant();
        anguloAbierta = ObtenerAnguloAperturaOptimo(n);

        // En House.fbx todas las puertas (Door*, closet_door*) tienen su pivote real de Blender en transform.position.
        // Solo garden_gate y garden_gate_01 tienen el origen en el centro geométrico de la malla.
        Vector2 pos2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 center2D = new Vector2(b.center.x, b.center.z);
        float anchoPuerta = Mathf.Max(b.size.x, b.size.z);

        if (n == "garden_gate")
        {
            puntoBisagraMundial = new Vector3(b.max.x, b.center.y, b.center.z);
        }
        else if (n == "garden_gate_01")
        {
            puntoBisagraMundial = new Vector3(b.min.x, b.center.y, b.center.z);
        }
        else if (Vector2.Distance(pos2D, center2D) > anchoPuerta * 0.18f)
        {
            puntoBisagraMundial = new Vector3(transform.position.x, b.center.y, transform.position.z);
        }
        else
        {
            if (b.extents.x > b.extents.z)
                puntoBisagraMundial = new Vector3(b.min.x, b.center.y, b.center.z);
            else
                puntoBisagraMundial = new Vector3(b.center.x, b.center.y, b.min.z);
        }

        BoxCollider colliderDirecto = GetComponent<BoxCollider>();
        if (colliderDirecto != null)
            colliderDirecto.enabled = false;

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

        Vector3 tamanoMundial = new Vector3(
            Mathf.Max(b.size.x, 0.22f),
            Mathf.Max(b.size.y, 0.40f),
            Mathf.Max(b.size.z, 0.22f)
        );
        hitboxPositivo.isTrigger = false;
        hitboxPositivo.center = Vector3.zero;
        hitboxPositivo.size = tamanoMundial;
        hitboxPositivo.enabled = true;

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
        string miNombre = gameObject.name.ToLowerInvariant();

        foreach (Collider col in houseColliders.GetComponentsInChildren<Collider>(true))
        {
            if (col == null) continue;
            string colName = col.gameObject.name.ToLowerInvariant();

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
        audioPuerta.maxDistance = 14f;

        if (sonidoAbrir == null && audioPuerta.clip != null)
            sonidoAbrir = audioPuerta.clip;

        if (sonidoAbrir == null)
            sonidoAbrir = ObtenerClipPuerta();
        if (sonidoCerrar == null)
            sonidoCerrar = sonidoAbrir;
        if (sonidoAbrirAnomalia == null)
            sonidoAbrirAnomalia = sonidoAbrir;

        // Nunca usar el sonido de metal pipe falling
        if (sonidoAzotar == null || sonidoAzotar.name.ToLowerInvariant().Contains("pipe"))
            sonidoAzotar = sonidoCerrar;
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
        Vector3 posRef = Camera.main != null
            ? ObtenerPuntoMasCercano(Camera.main.transform.position)
            : ObtenerPosicionMundial();

        if (!InteractorUniversalVR.EstaDentroDeRangoYLineaDeVision(posRef, 2.15f, transform))
            return;

        if (interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.55f, 0.14f);

        AlternarPuerta();
    }

    public void AlternarPuerta()
    {
        if (ultimaInteraccionFrame == Time.frameCount) return;
        if (Time.unscaledTime - ultimoTiempoInteraccion < cooldownInteraccion) return;

        ultimaInteraccionFrame = Time.frameCount;
        ultimoTiempoInteraccion = Time.unscaledTime;

        if (Application.isPlaying && gameObject.activeInHierarchy && delayAccion > 0.01f)
        {
            if (rutinaDelayAccion != null) StopCoroutine(rutinaDelayAccion);
            rutinaDelayAccion = StartCoroutine(RutinaAlternarConDelay());
        }
        else
        {
            EjecutarAlternanciaInmediata();
        }
    }

    private IEnumerator RutinaAlternarConDelay()
    {
        yield return new WaitForSeconds(delayAccion);
        EjecutarAlternanciaInmediata();
        rutinaDelayAccion = null;
    }

    private void EjecutarAlternanciaInmediata()
    {
        if (!estaAbierta) AbrirPuerta();
        else CerrarPuerta(false);
    }

    public void AbrirPuerta()
    {
        if (estaAbierta && !entreabiertaPorPreacher) return;
        estaAbierta = true;
        abiertaPorAnomalia = false;
        entreabiertaPorPreacher = false;
        pendienteSonidoCierre = false;

        if (audioPuerta != null && sonidoAbrir != null)
        {
            audioPuerta.volume = 0.85f;
            audioPuerta.pitch = Random.Range(0.95f, 1.04f);
            audioPuerta.PlayOneShot(sonidoAbrir);
        }

        if (rutinaAutoCierre != null) StopCoroutine(rutinaAutoCierre);
        rutinaAutoCierre = StartCoroutine(TemporizadorCierre());
    }

    public void AbrirPuertaPorAnomalia()
    {
        if (rutinaAutoCierre != null)
        {
            StopCoroutine(rutinaAutoCierre);
            rutinaAutoCierre = null;
        }
        estaAbierta = true;
        abiertaPorAnomalia = true;
        entreabiertaPorPreacher = false;
        pendienteSonidoCierre = false;

        if (audioPuerta != null)
        {
            AudioClip clipAnomalo = sonidoAbrirAnomalia != null ? sonidoAbrirAnomalia : sonidoAbrir;
            if (clipAnomalo != null)
            {
                audioPuerta.volume = 0.82f;
                audioPuerta.pitch = 0.92f;
                audioPuerta.PlayOneShot(clipAnomalo);
            }
        }
    }

    public void AbrirEntreabiertaPorPreacher()
    {
        if (rutinaAutoCierre != null)
        {
            StopCoroutine(rutinaAutoCierre);
            rutinaAutoCierre = null;
        }
        estaAbierta = true;
        abiertaPorAnomalia = true;
        entreabiertaPorPreacher = true;
        pendienteSonidoCierre = false;
        // Dejar la puerta del clóset ya entreabierta en silencio (sin rechinido de puerta Unknown) para no confundir con anomalía de puerta
        anguloActual = anguloAbierta * 0.42f;
        AplicarAnguloDesdeCerrada(anguloActual);
    }

    public bool CalcularPuntoAsomoPreacherDetrasDeRendija(Vector3 centroHabitacion, out Vector3 posPreacher, out float rotY)
    {
        posPreacher = posCerradaMundial;
        rotY = 0f;

        if (hitboxPositivo == null) return false;

        // Calcular el centro de la hoja cuando está cerrada y el borde de apertura opuesto a la bisagra
        Quaternion invRotActual = Quaternion.Inverse(transform.rotation);
        Vector3 offsetCentroLocal = invRotActual * (hitboxPositivo.transform.position - transform.position);
        Vector3 centroCerrado = posCerradaMundial + (rotCerradaMundial * offsetCentroLocal);

        Vector3 deBisagraACentro = centroCerrado - puntoBisagraMundial;
        deBisagraACentro.y = 0f;
        if (deBisagraACentro.sqrMagnitude < 0.001f)
            return false;

        Vector3 bordeRendija = puntoBisagraMundial + deBisagraACentro * 1.55f;
        Vector3 haciaHabitacion = centroHabitacion - centroCerrado;
        haciaHabitacion.y = 0f;
        if (haciaHabitacion.sqrMagnitude < 0.001f)
            haciaHabitacion = Vector3.forward;

        // Proyectar sobre la normal perpendicular a la hoja cerrada para mirar recto hacia afuera de la rendija del clóset
        Vector3 tangenteHoja = deBisagraACentro.normalized;
        Vector3 normalHoja = new Vector3(-tangenteHoja.z, 0f, tangenteHoja.x);
        if (Vector3.Dot(normalHoja, haciaHabitacion) < 0f)
            normalHoja = -normalHoja;

        posPreacher = bordeRendija - normalHoja * 0.30f;
        rotY = Quaternion.LookRotation(normalHoja, Vector3.up).eulerAngles.y;
        return true;
    }

    public void CerrarPuerta(bool esAzoteAnomalia)
    {
        if (!estaAbierta && Mathf.Abs(anguloActual) < 0.1f) return;
        estaAbierta = false;
        abiertaPorAnomalia = false;
        entreabiertaPorPreacher = false;

        if (rutinaAutoCierre != null)
        {
            StopCoroutine(rutinaAutoCierre);
            rutinaAutoCierre = null;
        }

        cierreEsAzote = esAzoteAnomalia;
        pendienteSonidoCierre = true;
    }

    private IEnumerator TemporizadorCierre()
    {
        yield return new WaitForSeconds(segundosParaCerrarse);
        CerrarPuerta(false);
    }

    /// <summary>
    /// Idea 8 (Bloque 2): Cuando hay una entidad humanoide detrás de esta puerta cerrada y el jugador pasa cerca,
    /// la puerta tiembla en su marco como si forcejearan la manija desde adentro.
    /// </summary>
    public void SacudirPuertaCerradaPorEntidad()
    {
        if (estaAbierta || !gameObject.activeInHierarchy) return;
        if (rutinaTemblorEntidad != null) StopCoroutine(rutinaTemblorEntidad);
        rutinaTemblorEntidad = StartCoroutine(RutinaTemblorPuertaCerrada());
    }

    private IEnumerator RutinaTemblorPuertaCerrada()
    {
        if (audioPuerta != null)
        {
            AudioClip clipForcejeo = sonidoCerrar != null ? sonidoCerrar : sonidoAbrirAnomalia;
            if (clipForcejeo != null)
            {
                audioPuerta.pitch = Random.Range(0.68f, 0.78f);
                audioPuerta.PlayOneShot(clipForcejeo, 0.52f);
            }
        }

        float duracion = 0.55f;
        float t = 0f;
        while (t < duracion && !estaAbierta)
        {
            t += Time.deltaTime;
            float sacudida = Mathf.Sin(t * 58f) * 1.65f * (1f - t / duracion);
            AplicarAnguloDesdeCerrada(sacudida);
            yield return null;
        }

        if (!estaAbierta)
            AplicarAnguloDesdeCerrada(0f);

        rutinaTemblorEntidad = null;
    }

    void Update()
    {
        float anguloMetaAbierta = entreabiertaPorPreacher ? (anguloAbierta * 0.42f) : anguloAbierta;
        float objetivo = estaAbierta ? anguloMetaAbierta : 0f;
        float vel = (estaAbierta && abiertaPorAnomalia) ? velocidadGiroAnomalia : velocidadGiro;

        if (Mathf.Abs(anguloActual - objetivo) > 0.05f)
        {
            anguloActual = Mathf.MoveTowards(anguloActual, objetivo, vel * Time.deltaTime);
            AplicarAnguloDesdeCerrada(anguloActual);

            if (!estaAbierta && pendienteSonidoCierre && Mathf.Abs(anguloActual) <= 5.0f)
            {
                pendienteSonidoCierre = false;
                ReproducirSonidoCierre();
            }
        }
        else if (!estaAbierta && anguloActual != 0f)
        {
            anguloActual = 0f;
            AplicarAnguloDesdeCerrada(0f);
            if (pendienteSonidoCierre)
            {
                pendienteSonidoCierre = false;
                ReproducirSonidoCierre();
            }
        }
    }

    private void ReproducirSonidoCierre()
    {
        if (audioPuerta == null) return;
        AudioClip clip = sonidoCerrar != null ? sonidoCerrar : sonidoAbrir;
        if (clip != null)
        {
            audioPuerta.volume = 0.85f;
            audioPuerta.pitch = Random.Range(0.96f, 1.04f);
            audioPuerta.PlayOneShot(clip);
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
            int samples = sampleRate / 4;
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

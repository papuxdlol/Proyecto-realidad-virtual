using UnityEngine;

/// <summary>
/// Evento Unknown (inspirado en el juego original):
/// Aparece una puerta donde antes solo había pared lisa. Cuando el jugador la mira,
/// la puerta se empieza a abrir lentamente sola hacia un pasillo/vacío totalmente oscuro.
/// Si el jugador la reporta como DESCONOCIDO (Unknown), desaparece.
/// Si el jugador intenta entrar/cruzar el umbral oscuro, salta el Screamer con una entidad y la Death Screen.
/// </summary>
public class PuertaAbismalUnknownVR : MonoBehaviour
{
    public ObjetoAnomalia anomaliaAsociada;
    public Transform pivoteHojaPuerta;
    public AudioSource audioRechinido;
    public AudioClip clipRechinido;

    private bool jugadorYaLaVio = false;
    private float anguloActual = 0f;
    private const float AnguloMaximoApertura = 82f;
    private const float VelocidadAperturaLenta = 22f;
    private Vector3 centroUmbralMundial;
    private Vector3 normalParedMundial = Vector3.forward;

    public void ConfigurarVisualesPuertaAbismal(AudioClip sonidoRechinido)
    {
        clipRechinido = sonidoRechinido;

        if (audioRechinido == null)
        {
            audioRechinido = GetComponent<AudioSource>();
            if (audioRechinido == null)
                audioRechinido = gameObject.AddComponent<AudioSource>();
        }
        audioRechinido.playOnAwake = false;
        audioRechinido.spatialBlend = 1f;
        audioRechinido.minDistance = 0.8f;
        audioRechinido.maxDistance = 11f;

        Shader shUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Shader shLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material matNegroAbismo = new Material(shUnlit) { name = "Mat_Abismo_Negro_Unknown", color = Color.black };
        Material matMarcoOscuro = new Material(shLit) { name = "Mat_Marco_Puerta_Unknown", color = new Color(0.10f, 0.09f, 0.09f, 1f) };
        Material matMaderaPuerta = new Material(shLit) { name = "Mat_Hoja_Puerta_Unknown", color = new Color(0.32f, 0.20f, 0.12f, 1f) };

        // Limpiar hijos anteriores si se reconstruye
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
            else DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // Dimensiones arquitectónicas estándar de las puertas de la casa (ancho 0.98m, alto 2.18m)
        const float ancho = 0.98f;
        const float alto = 2.18f;

        // 1. Hueco / Pasillo totalmente negro abismal pegado al muro
        GameObject abismo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        abismo.name = "Pasillo_Abismal_Oscuro";
        Collider colAbismo = abismo.GetComponent<Collider>();
        if (colAbismo != null)
        {
            if (Application.isPlaying) Destroy(colAbismo);
            else DestroyImmediate(colAbismo);
        }
        abismo.transform.SetParent(transform, false);
        abismo.transform.localPosition = new Vector3(0f, alto * 0.5f, 0.012f);
        abismo.transform.localRotation = Quaternion.identity;
        abismo.transform.localScale = new Vector3(ancho, alto, 0.02f);
        abismo.GetComponent<MeshRenderer>().sharedMaterial = matNegroAbismo;

        // 2. Marco perimetral oscuro (jamba izquierda, jamba derecha, dintel superior)
        CrearPieza(transform, "Marco_Izq", new Vector3(-ancho * 0.5f - 0.03f, alto * 0.5f, 0.025f), new Vector3(0.06f, alto + 0.06f, 0.06f), matMarcoOscuro);
        CrearPieza(transform, "Marco_Der", new Vector3( ancho * 0.5f + 0.03f, alto * 0.5f, 0.025f), new Vector3(0.06f, alto + 0.06f, 0.06f), matMarcoOscuro);
        CrearPieza(transform, "Marco_Sup", new Vector3(0f, alto + 0.03f, 0.025f), new Vector3(ancho + 0.12f, 0.06f, 0.06f), matMarcoOscuro);

        // 3. Pivote en la bisagra izquierda (-ancho * 0.5f) para que la hoja se abra lentamente dejando ver la oscuridad
        GameObject pivoteObj = new GameObject("Pivote_Hoja_Puerta_Unknown");
        pivoteObj.transform.SetParent(transform, false);
        pivoteObj.transform.localPosition = new Vector3(-ancho * 0.5f, 0f, 0.032f);
        pivoteObj.transform.localRotation = Quaternion.identity;
        pivoteHojaPuerta = pivoteObj.transform;

        // Intentar copiar el material de madera real de Door_001 si existe en escena
        GameObject door001 = GameObject.Find("Door_001");
        if (door001 != null)
        {
            MeshRenderer mrRef = door001.GetComponent<MeshRenderer>();
            if (mrRef != null && mrRef.sharedMaterial != null)
                matMaderaPuerta = mrRef.sharedMaterial;
        }

        CrearPieza(pivoteHojaPuerta, "Hoja_Puerta_Unknown", new Vector3(ancho * 0.5f, alto * 0.5f, 0f), new Vector3(ancho, alto, 0.035f), matMaderaPuerta);
        // Manija metálica
        CrearPieza(pivoteHojaPuerta, "Manija_Puerta_Unknown", new Vector3(ancho - 0.10f, 1.02f, 0.035f), new Vector3(0.11f, 0.03f, 0.05f), matMarcoOscuro);
    }

    private static void CrearPieza(Transform padre, string nombre, Vector3 posLocal, Vector3 escalaLocal, Material mat)
    {
        GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubo.name = nombre;
        Collider c = cubo.GetComponent<Collider>();
        if (c != null)
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
        cubo.transform.SetParent(padre, false);
        cubo.transform.localPosition = posLocal;
        cubo.transform.localRotation = Quaternion.identity;
        cubo.transform.localScale = escalaLocal;
        cubo.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    public void ReiniciarYPosicionarEnPared(Vector3 puntoParedEnPiso, Vector3 normalPared)
    {
        normalParedMundial = normalPared.normalized;
        transform.position = puntoParedEnPiso;
        transform.rotation = Quaternion.LookRotation(normalParedMundial, Vector3.up);
        centroUmbralMundial = puntoParedEnPiso + Vector3.up * 1.1f + normalParedMundial * 0.15f;

        jugadorYaLaVio = false;
        anguloActual = 0f;
        if (pivoteHojaPuerta != null)
            pivoteHojaPuerta.localRotation = Quaternion.identity;
    }

    void OnEnable()
    {
        jugadorYaLaVio = false;
        anguloActual = 0f;
        if (pivoteHojaPuerta != null)
            pivoteHojaPuerta.localRotation = Quaternion.identity;
    }

    void Update()
    {
        if (VRPauseMenu.IsMenuOpen || FinalTurnoVR.EstaEnSecuenciaFinal) return;
        if (Camera.main == null) return;

        Transform cam = Camera.main.transform;
        Vector3 posCam = cam.position;

        // Verificar que el jugador esté en el mismo piso que esta puerta
        if (Mathf.Abs(posCam.y - centroUmbralMundial.y) > 2.1f) return;

        float dist = Vector3.Distance(posCam, centroUmbralMundial);

        // 1. Cuando el jugador mira hacia la puerta (< 6.8m y dentro de su campo de visión), empieza a abrirse lentamente
        if (!jugadorYaLaVio && dist < 6.8f)
        {
            Vector3 haciaPuerta = (centroUmbralMundial - posCam).normalized;
            float anguloMirada = Vector3.Angle(cam.forward, haciaPuerta);
            if (anguloMirada < 42f && InteractorUniversalVR.TieneLineaDeVisionDirecta(posCam, centroUmbralMundial, transform))
            {
                jugadorYaLaVio = true;
                if (audioRechinido != null && clipRechinido != null)
                {
                    audioRechinido.pitch = 0.72f;
                    audioRechinido.PlayOneShot(clipRechinido, 0.75f);
                }
            }
        }

        // 2. Animar la apertura lenta hacia el pasillo oscuro
        if (jugadorYaLaVio && pivoteHojaPuerta != null && anguloActual < AnguloMaximoApertura)
        {
            anguloActual = Mathf.MoveTowards(anguloActual, AnguloMaximoApertura, VelocidadAperturaLenta * Time.deltaTime);
            // Abrir hacia afuera (+Y local o -Y local) para no meterse dentro del muro
            pivoteHojaPuerta.localRotation = Quaternion.Euler(0f, -anguloActual, 0f);
        }

        // 3. Si la puerta ya se está abriendo y el jugador intenta entrar al umbral oscuro (< 1.12m en horizontal),
        // salta inmediatamente una entidad humanoide con el Screamer y la Death Screen VHS
        if (jugadorYaLaVio && anguloActual > 22f)
        {
            float distXZ = Vector2.Distance(new Vector2(posCam.x, posCam.z), new Vector2(centroUmbralMundial.x, centroUmbralMundial.z));
            if (distXZ < 1.12f)
            {
                FinalTurnoVR gestorFinal = FinalTurnoVR.Instancia;
                if (gestorFinal == null)
                    gestorFinal = FindAnyObjectByType<FinalTurnoVR>(FindObjectsInactive.Include);

                if (gestorFinal != null)
                {
                    TipoAnomalia entidadUmbral = TabletSistemaVR.Instancia != null
                        ? TabletSistemaVR.Instancia.ultimoTipoHumanoideAparecido
                        : TipoAnomalia.PureForm;

                    gestorFinal.ActivarDerrota(
                        entidadUmbral,
                        "Intentaste cruzar el umbral de una Puerta Imposible (Anomalía Desconocida).");
                }
            }
        }
    }
}

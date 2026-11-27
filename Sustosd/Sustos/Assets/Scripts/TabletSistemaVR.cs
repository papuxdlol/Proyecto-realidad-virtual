using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class TabletSistemaVR : MonoBehaviour
{
    public static TabletSistemaVR Instancia;

    [Header("1. Configuración del Turno (11:55 PM -> 06:00 AM)")]
    public float duracionInspeccionInicial = 40f;
    public float duracionHoraEnSegundos = 50f;
    private bool enFaseInspeccion = true;
    private float tiempoRestanteInspeccion = 40f;
    private float relojInterno = 0f;
    private int horaActual = 0;

    [Header("2. Generador Automático de Anomalías (Progresión por Hora)")]
    public float segundosEntreAnomalias = 22f;
    private float temporizadorAnomalias = 10f;
    public List<ObjetoAnomalia> todasLasAnomalias = new List<ObjetoAnomalia>();

    [Header("3. Sonidos de la Tablet")]
    public AudioSource audioFeedback;
    private AudioSource audioProcesandoSource;
    private AudioSource audioGlitchSource;
    public AudioClip sonidoAcierto;
    public AudioClip sonidoFallo;
    public AudioClip sonidoProcesando;
    public AudioClip sonidoGlitch;
    private AudioClip sonidoClickUI;

    [Header("4. Estadísticas del Turno")]
    public int reportesFallidos = 0;
    public TipoAnomalia ultimoTipoHumanoideAparecido = TipoAnomalia.PureForm;

    public bool EstaEnInspeccion => enFaseInspeccion;
    public int HoraActual => horaActual;
    public int AnomaliasCorregidas => anomaliasCorregidas;

    private Canvas tabletCanvas;
    private GameObject panelAnomalias;
    private Light luzResplandorTablet;

    private Text textoRelojHeader;
    private Text textoEstadoReporte;
    private Button botonEnviarReporte;
    private Image imagenBotonEnviar;
    private Text textoBotonEnviar;

    private Dictionary<string, Image> botonesCuartos = new Dictionary<string, Image>();
    private Dictionary<int, Image> botonesTipos = new Dictionary<int, Image>();

    private string cuartoElegido = "LivingRoom";
    private int tipoElegido = 0;
    private int anomaliasCorregidas = 0;
    private bool fusiblesReparadosReciente = false;
    private bool procesandoReporte = false;
    private bool glitchActivo = false;
    private Coroutine rutinaProcesando;
    private Coroutine rutinaGlitch;
    private string ultimoCuartoGenerado = "";
    private int ultimoTipoGenerado = -1;
    private readonly HashSet<string> categoriasYaMostradasEnTurno = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ObjetoAnomalia, float> tiempoActivacionPorAnomalia = new Dictionary<ObjetoAnomalia, float>();
    private readonly Dictionary<string, int> incidentesPorCuarto = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private float sumaTiemposReaccion = 0f;
    private int totalTiemposReaccion = 0;
    private bool evento3AMDisparado = false;
    private float ultimoSegundoInspeccionMostrado = -1f;
    private int ultimoConteoActivasUI = -1;
    private InputDevice rightController;
    private InputDevice leftController;

    private readonly Color colorRosaNormal = new Color(0.85f, 0.55f, 0.58f, 1f);
    private readonly Color colorVerdeSeleccionado = new Color(0.45f, 0.95f, 0.55f, 1f);
    private readonly Color colorBotonOscuro = new Color(0.12f, 0.14f, 0.18f, 0.95f);
    private readonly Color colorBotonEnviarNormal = new Color(0.15f, 0.42f, 0.2f, 1f);
    private readonly Color colorBotonInspeccion = new Color(0.14f, 0.34f, 0.52f, 1f);
    private readonly Color colorBotonProcesando = new Color(0.48f, 0.38f, 0.12f, 1f);

    private static readonly string[,] HabitacionesCasa = new string[,]
    {
        { "LivingRoom", "SALA" },
        { "Kitchen",    "COCINA" },
        { "Garage",     "GARAJE" },
        { "Laundry",    "LAVANDERÍA" },
        { "Bathroom",   "BAÑOS" },
        { "Bedroom1",   "RECÁMARA 1" },
        { "Bedroom2",   "RECÁMARA 2" },
        { "Bedroom3",   "RECÁMARA 3" }
    };

    private static readonly string[] NombresTiposEspanol = new string[]
    {
        "CUADROS / PINTURA",
        "DESPLAZAMIENTO",
        "OBJETO DUPLICADO",
        "DEFECTUOSO",
        "FORMA COMPLETA",
        "MONJE",
        "FALLO ELÉCTRICO",
        "TULPA",
        "CADÁVER",
        "DESCONOCIDO"
    };

    public static string ObtenerNombreCuartoEspanol(string idCuarto)
    {
        if (string.IsNullOrEmpty(idCuarto)) return "SALA";
        string c = idCuarto.Trim();
        if (string.Equals(c, "Basement", StringComparison.OrdinalIgnoreCase))
            return "GARAJE";
        if (string.Equals(c, "Bedroom", StringComparison.OrdinalIgnoreCase))
            return "RECÁMARA 1";

        for (int i = 0; i < HabitacionesCasa.GetLength(0); i++)
        {
            if (string.Equals(HabitacionesCasa[i, 0], c, StringComparison.OrdinalIgnoreCase))
                return HabitacionesCasa[i, 1];
        }
        return c.ToUpperInvariant();
    }

    public static string ObtenerNombreTipoEspanol(int indiceTipo)
    {
        if (indiceTipo >= 0 && indiceTipo < NombresTiposEspanol.Length)
            return NombresTiposEspanol[indiceTipo];
        return ((TipoAnomalia)indiceTipo).ToString().ToUpperInvariant();
    }

    public static string ObtenerNombreTipoEspanol(TipoAnomalia tipo)
    {
        return ObtenerNombreTipoEspanol((int)tipo);
    }

    private static bool EsRecamara(string cuarto)
    {
        if (string.IsNullOrEmpty(cuarto)) return false;
        return cuarto.Trim().StartsWith("Bedroom", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CoincideCuartoExacto(ObjetoAnomalia anomalia, string cuartoReporte)
    {
        if (anomalia == null) return false;
        MeshRenderer mr = anomalia.GetComponent<MeshRenderer>();
        Vector3 posReal = mr != null ? mr.bounds.center : anomalia.transform.position;
        string cuartoAnom = ObjetoAnomalia.NormalizarCuartoPorPosicion(posReal, anomalia.nombreCuarto, anomalia.gameObject.name);
        string rep = string.Equals(cuartoReporte, "Basement", StringComparison.OrdinalIgnoreCase) ? "Garage" : cuartoReporte;

        return string.Equals(cuartoAnom, rep, StringComparison.OrdinalIgnoreCase);
    }

    private void Awake()
    {
        Instancia = this;

        InteractorUniversalVR interactorEnTablet = GetComponent<InteractorUniversalVR>();
        if (interactorEnTablet != null)
            Destroy(interactorEnTablet);

        AnomalyTabletMenu menuAntiguo = GetComponentInChildren<AnomalyTabletMenu>(true);
        if (menuAntiguo != null)
            Destroy(menuAntiguo);

        EnsureUiEventSystem();
        AsegurarAudioTablet();
        AjustarColliderTabletParaNoBloquearUI();
    }

    private void Start()
    {
        RefrescarListaAnomalias();
        ConstruirInterfazEnTablet();
        AsegurarResplandorRealistaTablet();
        ConfigurarRayosUIMandos();
        SeleccionarCuartoUI("LivingRoom");
        SeleccionarTipoUI(0);

        enFaseInspeccion = duracionInspeccionInicial > 0.5f;
        tiempoRestanteInspeccion = Mathf.Max(0f, duracionInspeccionInicial);
        ActualizarEstadoBotonPrincipal();
        ActualizarRelojUI();

        if (enFaseInspeccion && textoEstadoReporte != null)
        {
            textoEstadoReporte.text = "Tiempo de gracia";
            textoEstadoReporte.color = new Color(0.65f, 0.88f, 1f, 1f);
        }
    }

    private void AsegurarResplandorRealistaTablet()
    {
        Transform existente = transform.Find("Luz_Resplandor_Tablet");
        if (existente != null)
        {
            luzResplandorTablet = existente.GetComponent<Light>();
        }
        else
        {
            GameObject luzObj = new GameObject("Luz_Resplandor_Tablet");
            luzObj.transform.SetParent(transform, false);
            luzObj.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            luzResplandorTablet = luzObj.AddComponent<Light>();
        }

        if (luzResplandorTablet != null)
        {
            luzResplandorTablet.type = LightType.Point;
            luzResplandorTablet.color = new Color(0.72f, 0.84f, 1f, 1f);
            luzResplandorTablet.intensity = 0.28f;
            luzResplandorTablet.range = 1.15f;
            luzResplandorTablet.shadows = LightShadows.None;
            luzResplandorTablet.renderMode = LightRenderMode.ForcePixel;
            luzResplandorTablet.enabled = true;
        }
    }

    public void RefrescarListaAnomalias()
    {
        todasLasAnomalias.Clear();
        ObjetoAnomalia[] halladas = FindObjectsByType<ObjetoAnomalia>(FindObjectsInactive.Include);
        for (int i = 0; i < halladas.Length; i++)
        {
            ObjetoAnomalia a = halladas[i];
            if (a == null) continue;
            // Excluir por completo escrituras en las paredes
            if (a.gameObject.name.ToLowerInvariant().Contains("mensajepared"))
            {
                a.estaActiva = false;
                if (a.posiblesVariantes != null) a.posiblesVariantes.Clear();
                a.gameObject.SetActive(false);
                continue;
            }

            if (!Application.isPlaying || enFaseInspeccion)
                a.estaActiva = false;

            MeshRenderer mr = a.GetComponent<MeshRenderer>();
            Vector3 posReal = mr != null ? mr.bounds.center : a.transform.position;
            a.nombreCuarto = ObjetoAnomalia.NormalizarCuartoPorPosicion(
                posReal,
                a.nombreCuarto,
                a.gameObject.name);
            todasLasAnomalias.Add(a);
        }
    }

    private void AsegurarAudioTablet()
    {
        if (audioFeedback == null)
        {
            audioFeedback = GetComponent<AudioSource>();
            if (audioFeedback == null)
                audioFeedback = gameObject.AddComponent<AudioSource>();
        }

        audioFeedback.playOnAwake = false;
        audioFeedback.spatialBlend = 0.5f;

        if (audioProcesandoSource == null)
        {
            audioProcesandoSource = gameObject.AddComponent<AudioSource>();
            audioProcesandoSource.playOnAwake = false;
            audioProcesandoSource.spatialBlend = 0.5f;
            audioProcesandoSource.loop = false;
        }

        if (audioGlitchSource == null)
        {
            audioGlitchSource = gameObject.AddComponent<AudioSource>();
            audioGlitchSource.playOnAwake = false;
            audioGlitchSource.spatialBlend = 0.5f;
            audioGlitchSource.loop = false;
        }

#if UNITY_EDITOR
        if (sonidoAcierto == null)
            sonidoAcierto = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_tablet_acierto.wav");
        if (sonidoFallo == null)
            sonidoFallo = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_tablet_fallo.wav");
        if (sonidoProcesando == null)
            sonidoProcesando = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_tablet_procesando.wav");
        if (sonidoGlitch == null)
            sonidoGlitch = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/glitchstarting.mp3");
#endif
        if (sonidoClickUI == null)
            sonidoClickUI = CrearTonoProcedural("TabletClick", 880f, 880f, 0.035f, 0.35f);
        if (sonidoAcierto == null)
            sonidoAcierto = CrearTonoProcedural("TabletSuccess", 520f, 1040f, 0.22f, 0.55f);
        if (sonidoFallo == null)
            sonidoFallo = CrearTonoProcedural("TabletError", 260f, 140f, 0.28f, 0.55f);
        if (sonidoProcesando == null)
            sonidoProcesando = CrearTonoProcedural("TabletProcessing", 740f, 920f, 0.12f, 0.28f);
        if (sonidoGlitch == null)
            sonidoGlitch = CrearRuidoGlitchProcedural("TabletGlitch", 0.36f, 0.30f);
    }

    private void AjustarColliderTabletParaNoBloquearUI()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.center = new Vector3(bc.center.x, -0.0025f, bc.center.z);
        }
    }

    private void ConfigurarRayosUIMandos()
    {
        foreach (var ray in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include))
        {
            if (ray == null) continue;
            ray.enableUIInteraction = true;
            ray.blockUIOnInteractableSelection = false;
            ray.allowHoveredActivate = true;
        }
    }

    private void Update()
    {
        if (VRPauseMenu.IsMenuOpen || FinalTurnoVR.EstaEnSecuenciaFinal) return;

        // Fase 1: Inspección previa de la casa (11:55 PM -> 12:00 AM, cero anomalías)
        if (enFaseInspeccion)
        {
            tiempoRestanteInspeccion -= Time.deltaTime;
            int segRestantes = Mathf.CeilToInt(Mathf.Max(0f, tiempoRestanteInspeccion));
            if (segRestantes != Mathf.RoundToInt(ultimoSegundoInspeccionMostrado))
            {
                ultimoSegundoInspeccionMostrado = segRestantes;
                ActualizarRelojUI();
            }

            if (tiempoRestanteInspeccion <= 0f)
                FinalizarFaseInspeccion();

            return;
        }

        // Fase nocturna (12:00 AM -> 06:00 AM)
        if (horaActual < 6)
        {
            relojInterno += Time.deltaTime;
            if (relojInterno >= duracionHoraEnSegundos)
            {
                relojInterno = 0f;
                horaActual++;
                ActualizarRelojUI();

                // Idea 6 (Bloque 2): Al dar las 03:00 AM ("La Hora Muerta"), disparar caída general de tensión eléctrica
                if (horaActual == 3 && !evento3AMDisparado)
                {
                    evento3AMDisparado = true;
                    if (TensionYAmbienteVR.Instancia != null)
                        TensionYAmbienteVR.Instancia.DispararEventoHoraMuerta3AM();
                }

                if (horaActual >= 6)
                {
                    AsegurarGestorFinalTurno().ActivarVictoria(anomaliasCorregidas, reportesFallidos);
                    return;
                }
            }

            float intervaloActual = ObtenerIntervaloAnomaliasPorHora();
            temporizadorAnomalias += Time.deltaTime;
            if (temporizadorAnomalias >= intervaloActual)
            {
                temporizadorAnomalias = 0f;
                GenerarAnomaliaAleatoria();
            }

            // Mantener el indicador de nivel de anomalías sincronizado al instante y parpadear en estado Crítico
            int activas = ContarAnomaliasActivas();
            if (activas != ultimoConteoActivasUI && !glitchActivo)
            {
                ActualizarRelojUI();
            }
            if (!glitchActivo && textoRelojHeader != null && activas >= 3)
            {
                float pulso = Mathf.PingPong(Time.unscaledTime * (activas >= 4 ? 5.2f : 3.0f), 1f);
                textoRelojHeader.color = Color.Lerp(new Color(0.58f, 0.10f, 0.10f, 1f), new Color(1f, 0.24f, 0.24f, 1f), pulso);
            }
        }
    }

    public void FinalizarFaseInspeccion()
    {
        if (!enFaseInspeccion) return;
        enFaseInspeccion = false;
        tiempoRestanteInspeccion = 0f;
        horaActual = 0;
        relojInterno = 0f;
        temporizadorAnomalias = 10f; // Primera anomalía llega ~12s después de iniciar el turno

        ActualizarEstadoBotonPrincipal();
        ActualizarRelojUI();

        if (textoEstadoReporte != null)
        {
            textoEstadoReporte.text = "Turno iniciado";
            textoEstadoReporte.color = colorVerdeSeleccionado;
        }
        if (audioFeedback != null && sonidoClickUI != null)
            audioFeedback.PlayOneShot(sonidoClickUI, 0.8f);
        EnviarHapticAmbosMandos(0.35f, 0.12f);
    }

    private float ObtenerIntervaloAnomaliasPorHora()
    {
        // Idea 5: Progresión de dificultad por hora (12-2 AM: 22s, 2-4 AM: 17s, 4-6 AM: 13s)
        if (horaActual < 2) return Mathf.Max(16f, segundosEntreAnomalias);
        if (horaActual < 4) return Mathf.Max(13f, segundosEntreAnomalias - 5f);
        return Mathf.Max(10f, segundosEntreAnomalias - 9f);
    }

    private bool EsTipoPrioritarioParaHora(TipoAnomalia tipo)
    {
        if (horaActual < 2)
        {
            // 12:00 AM - 2:00 AM: Memoria y observación ambiental
            return tipo == TipoAnomalia.Displacement ||
                   tipo == TipoAnomalia.Mimic ||
                   tipo == TipoAnomalia.Imagery ||
                   tipo == TipoAnomalia.Electrical;
        }
        if (horaActual < 4)
        {
            // 2:00 AM - 4:00 AM: Entran entidades y fenómenos raros
            return tipo == TipoAnomalia.Preacher ||
                   tipo == TipoAnomalia.Tulpa ||
                   tipo == TipoAnomalia.Corpse ||
                   tipo == TipoAnomalia.Unknown ||
                   tipo == TipoAnomalia.Electrical ||
                   tipo == TipoAnomalia.Displacement;
        }
        // 4:00 AM - 6:00 AM: Alta tensión (Flawed, PureForm, Tulpa, Preacher y Apagones)
        return tipo == TipoAnomalia.Flawed ||
               tipo == TipoAnomalia.PureForm ||
               tipo == TipoAnomalia.Preacher ||
               tipo == TipoAnomalia.Tulpa ||
               tipo == TipoAnomalia.Electrical ||
               tipo == TipoAnomalia.Unknown;
    }

    public static bool EsEntidadHumanoide(TipoAnomalia tipo)
    {
        return tipo == TipoAnomalia.Flawed ||
               tipo == TipoAnomalia.PureForm ||
               tipo == TipoAnomalia.Preacher ||
               tipo == TipoAnomalia.Corpse ||
               tipo == TipoAnomalia.Tulpa;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }

    // =========================================================================
    // GESTOR DE ANOMALÍAS INTEGRADO (COBERTURA GARANTIZADA 1x POR TIPO + SIN REPETIR CUARTO, SIN ESCRITURAS EN PARED)
    // =========================================================================
    private static readonly string[] CategoriasHoraTemprana = { "Imagery", "Displacement", "Mimic", "Electrical_Luz", "Unknown_Puerta" };
    private static readonly string[] CategoriasHoraMedia    = { "Unknown_PuertaAbismal", "Preacher", "Tulpa", "Corpse", "Unknown_LuzDia", "Electrical_Apagon", "Imagery" };
    private static readonly string[] CategoriasHoraTardia   = { "Flawed", "PureForm", "Unknown_PuertaAbismal", "Electrical_Apagon", "Tulpa", "Preacher", "Corpse", "Unknown_LuzDia" };
    private static readonly string[] TodasLasCategoriasDemo =
    {
        "Imagery", "Displacement", "Mimic", "Electrical_Luz", "Electrical_Apagon",
        "Unknown_Puerta", "Unknown_PuertaAbismal", "Unknown_LuzDia",
        "Preacher", "Tulpa", "Corpse", "Flawed", "PureForm"
    };

    private static bool PerteneceACategoria(ObjetoAnomalia a, string categoria, out TipoAnomalia tipoAsociado)
    {
        tipoAsociado = TipoAnomalia.Displacement;
        if (a == null || a.posiblesVariantes == null || a.posiblesVariantes.Count == 0) return false;

        string objNombre = a.gameObject.name.ToLowerInvariant();
        bool esCajaFusibles = a.GetComponent<CajaFusiblesVR>() != null || objNombre.Contains("fusible");

        for (int i = 0; i < a.posiblesVariantes.Count; i++)
        {
            VarianteAnomalia v = a.posiblesVariantes[i];
            if (v == null) continue;
            string desc = v.nombreDescriptivo != null ? v.nombreDescriptivo.ToLowerInvariant() : "";

            switch (categoria)
            {
                case "Imagery":
                    if (v.tipo == TipoAnomalia.Imagery) { tipoAsociado = TipoAnomalia.Imagery; return true; }
                    break;
                case "Displacement":
                    if (v.tipo == TipoAnomalia.Displacement) { tipoAsociado = TipoAnomalia.Displacement; return true; }
                    break;
                case "Mimic":
                    if (v.tipo == TipoAnomalia.Mimic) { tipoAsociado = TipoAnomalia.Mimic; return true; }
                    break;
                case "Electrical_Apagon":
                    if (v.tipo == TipoAnomalia.Electrical && (esCajaFusibles || desc.Contains("apag") || desc.Contains("fusible")))
                    {
                        tipoAsociado = TipoAnomalia.Electrical;
                        return true;
                    }
                    break;
                case "Electrical_Luz":
                    if (v.tipo == TipoAnomalia.Electrical && !esCajaFusibles && !desc.Contains("apag") && !desc.Contains("fusible"))
                    {
                        tipoAsociado = TipoAnomalia.Electrical;
                        return true;
                    }
                    break;
                case "Unknown_PuertaAbismal":
                    if (v.tipo == TipoAnomalia.Unknown && (objNombre.Contains("puertaabismal") || desc.Contains("abismo") || desc.Contains("imposible")))
                    {
                        tipoAsociado = TipoAnomalia.Unknown;
                        return true;
                    }
                    break;
                case "Unknown_LuzDia":
                    if (v.tipo == TipoAnomalia.Unknown && (desc.Contains("luz de d") || objNombre.Contains("luzdia") || objNombre.Contains("ventana")))
                    {
                        tipoAsociado = TipoAnomalia.Unknown;
                        return true;
                    }
                    break;
                case "Unknown_Pared":
                    return false;
                case "Unknown_Puerta":
                    if (v.tipo == TipoAnomalia.Unknown && (v.puertaQueSeAbreSola != null || desc.Contains("puerta")))
                    {
                        tipoAsociado = TipoAnomalia.Unknown;
                        return true;
                    }
                    break;
                case "Preacher":
                    if (v.tipo == TipoAnomalia.Preacher) { tipoAsociado = TipoAnomalia.Preacher; return true; }
                    break;
                case "Tulpa":
                    if (v.tipo == TipoAnomalia.Tulpa) { tipoAsociado = TipoAnomalia.Tulpa; return true; }
                    break;
                case "Corpse":
                    if (v.tipo == TipoAnomalia.Corpse) { tipoAsociado = TipoAnomalia.Corpse; return true; }
                    break;
                case "Flawed":
                    if (v.tipo == TipoAnomalia.Flawed) { tipoAsociado = TipoAnomalia.Flawed; return true; }
                    break;
                case "PureForm":
                    if (v.tipo == TipoAnomalia.PureForm) { tipoAsociado = TipoAnomalia.PureForm; return true; }
                    break;
            }
        }
        return false;
    }

    public void GenerarAnomaliaAleatoria()
    {
        if (todasLasAnomalias.Count == 0)
            RefrescarListaAnomalias();

        // 1. Identificar qué cuartos ya tienen una anomalía activa para NO duplicar anomalías en el mismo cuarto
        HashSet<string> cuartosOcupados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < todasLasAnomalias.Count; i++)
        {
            ObjetoAnomalia a = todasLasAnomalias[i];
            if (a != null && a.estaActiva && !string.IsNullOrEmpty(a.nombreCuarto))
                cuartosOcupados.Add(a.nombreCuarto.Trim());
        }

        List<ObjetoAnomalia> inactivas = todasLasAnomalias.FindAll(a =>
        {
            if (a == null || a.estaActiva || a.posiblesVariantes == null || a.posiblesVariantes.Count == 0)
                return false;
            string nLow = a.gameObject.name.ToLowerInvariant();
            bool seReubica = nLow.StartsWith("anomalia_flawed") || nLow.StartsWith("anomalia_pureform") ||
                             nLow.StartsWith("anomalia_preacher") || nLow.StartsWith("anomalia_corpse") ||
                             nLow.Contains("puertaabismal");
            // Las entidades móviles eligen habitación desocupada al activarse, así que no bloquearlas por su cuarto anterior
            if (seReubica) return cuartosOcupados.Count < 7;
            return !cuartosOcupados.Contains(a.nombreCuarto != null ? a.nombreCuarto.Trim() : "");
        });

        if (inactivas.Count == 0) return;

        Transform cam = Camera.main != null ? Camera.main.transform : transform;
        string cuartoActualJugador = ObjetoAnomalia.NormalizarCuartoPorPosicion(cam.position, "LivingRoom");
        bool hayApagon = !InterruptorLuz.EnergiaGeneralCasaActiva;

        // 2. Excluir estrictamente el cuarto actual del jugador y cualquier objeto en su línea de visión para que NUNCA aparezcan en el aire frente a él
        List<ObjetoAnomalia> preferidas = inactivas.FindAll(a =>
        {
            string nLow = a.gameObject.name.ToLowerInvariant();
            bool seReubica = nLow.StartsWith("anomalia_flawed") || nLow.StartsWith("anomalia_pureform") ||
                             nLow.StartsWith("anomalia_preacher") || nLow.StartsWith("anomalia_corpse") ||
                             nLow.Contains("puertaabismal");
            if (seReubica) return true;

            MeshRenderer mr = a.GetComponent<MeshRenderer>();
            Vector3 pObj = mr != null ? mr.bounds.center : a.transform.position;
            return !string.Equals(a.nombreCuarto, ultimoCuartoGenerado, StringComparison.OrdinalIgnoreCase) &&
                   !ObjetoAnomalia.PuntoVisibleOMismoCuartoJugador(cam, pObj, a.nombreCuarto);
        });
        List<ObjetoAnomalia> segurasFueraDeVista = inactivas.FindAll(a =>
        {
            string nLow = a.gameObject.name.ToLowerInvariant();
            bool seReubica = nLow.StartsWith("anomalia_flawed") || nLow.StartsWith("anomalia_pureform") ||
                             nLow.StartsWith("anomalia_preacher") || nLow.StartsWith("anomalia_corpse") ||
                             nLow.Contains("puertaabismal");
            if (seReubica) return true;
            MeshRenderer mr = a.GetComponent<MeshRenderer>();
            Vector3 pObj = mr != null ? mr.bounds.center : a.transform.position;
            return !ObjetoAnomalia.PuntoVisibleOMismoCuartoJugador(cam, pObj, a.nombreCuarto);
        });
        List<ObjetoAnomalia> poolSecundario = segurasFueraDeVista.Count > 0 ? segurasFueraDeVista : inactivas;

        // 3. Sistema de Cobertura Garantizada: asegurar que salga al menos 1 VEZ cada tipo y variación clave durante el turno
        string[] ordenHora = horaActual < 2 ? CategoriasHoraTemprana : (horaActual < 4 ? CategoriasHoraMedia : CategoriasHoraTardia);
        ObjetoAnomalia elegida = null;
        TipoAnomalia? tipoForzado = null;
        string categoriaElegida = null;

        // 3a. Buscar primero entre las categorías PENDIENTES propias de la hora actual
        if (!IntentarElegirDeCategorias(ordenHora, true, hayApagon, preferidas, poolSecundario, out elegida, out tipoForzado, out categoriaElegida))
        {
            // 3b. Si ya salieron todas las de esta hora, buscar CUALQUIER categoría pendiente del turno completo
            if (!IntentarElegirDeCategorias(TodasLasCategoriasDemo, true, hayApagon, preferidas, poolSecundario, out elegida, out tipoForzado, out categoriaElegida))
            {
                // 3c. Si ya salieron todas al menos 1 vez, usar RNG balanceado por categoría
                IntentarElegirDeCategorias(ordenHora, false, hayApagon, preferidas, poolSecundario, out elegida, out tipoForzado, out categoriaElegida);
                if (elegida == null)
                    IntentarElegirDeCategorias(TodasLasCategoriasDemo, false, hayApagon, preferidas, poolSecundario, out elegida, out tipoForzado, out categoriaElegida);
            }
        }

        // Fallback de seguridad
        if (elegida == null)
        {
            List<ObjetoAnomalia> fallbackPool = preferidas.Count > 0 ? preferidas : poolSecundario;
            elegida = fallbackPool[UnityEngine.Random.Range(0, fallbackPool.Count)];
        }

        // Activar anomalía con transición eléctrica suave (y micro-corte de 0.11s si por algún motivo extremo estuviera en línea de visión)
        InterruptorLuz.EjecutarTransicionAparicionAnomalia(this, elegida, tipoForzado);

        tiempoActivacionPorAnomalia[elegida] = Time.time;
        if (!string.IsNullOrEmpty(elegida.nombreCuarto))
        {
            string cKey = elegida.nombreCuarto.Trim();
            incidentesPorCuarto[cKey] = (incidentesPorCuarto.TryGetValue(cKey, out int prev) ? prev : 0) + 1;
        }

        if (!string.IsNullOrEmpty(categoriaElegida))
            categoriasYaMostradasEnTurno.Add(categoriaElegida);

        ultimoCuartoGenerado = elegida.nombreCuarto;
        ultimoTipoGenerado = (int)elegida.tipoActivoActual;
        if (EsEntidadHumanoide(elegida.tipoActivoActual))
            ultimoTipoHumanoideAparecido = elegida.tipoActivoActual;

        ActualizarRelojUI();
        Debug.Log($"[ANOMALÍA ACTIVADA | {horaActual:00}:00 AM | Cat: {categoriaElegida ?? "RNG"}] Cuarto: {ObtenerNombreCuartoEspanol(elegida.nombreCuarto)} ({elegida.nombreCuarto}) | Tipo: {ObtenerNombreTipoEspanol(elegida.tipoActivoActual)} | Objeto: {elegida.name}");

        EvaluarDerrotaPorAcumulacionOFallos();
    }

    private bool IntentarElegirDeCategorias(
        string[] listaCategorias,
        bool soloPendientes,
        bool hayApagon,
        List<ObjetoAnomalia> preferidas,
        List<ObjetoAnomalia> todasInactivas,
        out ObjetoAnomalia elegida,
        out TipoAnomalia? tipoForzado,
        out string categoriaElegida)
    {
        elegida = null;
        tipoForzado = null;
        categoriaElegida = null;

        List<string> candidatasCat = new List<string>();
        for (int i = 0; i < listaCategorias.Length; i++)
        {
            string cat = listaCategorias[i];
            if (soloPendientes && categoriasYaMostradasEnTurno.Contains(cat)) continue;
            if (hayApagon && (cat == "Electrical_Luz" || cat == "Electrical_Apagon")) continue;
            candidatasCat.Add(cat);
        }

        if (candidatasCat.Count == 0) return false;

        // Mezclar orden para que la demostración mantenga factor sorpresa
        for (int i = candidatasCat.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            string tmp = candidatasCat[i];
            candidatasCat[i] = candidatasCat[j];
            candidatasCat[j] = tmp;
        }

        for (int c = 0; c < candidatasCat.Count; c++)
        {
            string cat = candidatasCat[c];
            TipoAnomalia tipoTemp = TipoAnomalia.Displacement;

            // Primero buscar en cuartos donde no está el jugador; si es entidad única (ej. Tulpa/Fusibles/Humanoide), aceptar de todasInactivas
            List<ObjetoAnomalia> matchesPref = new List<ObjetoAnomalia>();
            for (int i = 0; i < preferidas.Count; i++)
            {
                if (PerteneceACategoria(preferidas[i], cat, out tipoTemp))
                    matchesPref.Add(preferidas[i]);
            }

            if (matchesPref.Count > 0)
            {
                elegida = matchesPref[UnityEngine.Random.Range(0, matchesPref.Count)];
                PerteneceACategoria(elegida, cat, out tipoTemp);
                tipoForzado = tipoTemp;
                categoriaElegida = cat;
                return true;
            }

            List<ObjetoAnomalia> matchesAll = new List<ObjetoAnomalia>();
            for (int i = 0; i < todasInactivas.Count; i++)
            {
                if (PerteneceACategoria(todasInactivas[i], cat, out tipoTemp))
                    matchesAll.Add(todasInactivas[i]);
            }

            if (matchesAll.Count > 0)
            {
                elegida = matchesAll[UnityEngine.Random.Range(0, matchesAll.Count)];
                PerteneceACategoria(elegida, cat, out tipoTemp);
                tipoForzado = tipoTemp;
                categoriaElegida = cat;
                return true;
            }
        }

        return false;
    }

    public void NotificarFusiblesReparados()
    {
        fusiblesReparadosReciente = true;
        anomaliasCorregidas++;
        if (textoEstadoReporte != null)
        {
            textoEstadoReporte.text = "Energía restablecida";
            textoEstadoReporte.color = colorVerdeSeleccionado;
        }
        ActualizarRelojUI();
    }

    private ObjetoAnomalia BuscarAnomaliaReportada(string cuartoReporte, TipoAnomalia tipoBuscado)
    {
        if (todasLasAnomalias.Count == 0)
            RefrescarListaAnomalias();

        ObjetoAnomalia enc = todasLasAnomalias.Find(a =>
            a != null &&
            a.estaActiva &&
            CoincideCuartoExacto(a, cuartoReporte) &&
            a.tipoActivoActual == tipoBuscado);

        if (enc == null && EsRecamara(cuartoReporte))
        {
            enc = todasLasAnomalias.Find(a =>
                a != null &&
                a.estaActiva &&
                EsRecamara(ObjetoAnomalia.NormalizarCuartoPorPosicion(a.transform.position, a.nombreCuarto, a.gameObject.name)) &&
                a.tipoActivoActual == tipoBuscado);
        }
        return enc;
    }

    private void AlPulsarBotonPrincipal()
    {
        if (enFaseInspeccion)
        {
            FinalizarFaseInspeccion();
            return;
        }

        if (procesandoReporte) return;

        if (rutinaProcesando != null)
            StopCoroutine(rutinaProcesando);

        rutinaProcesando = StartCoroutine(RutinaProcesarReporte(cuartoElegido, tipoElegido));
    }

    private IEnumerator RutinaProcesarReporte(string cuartoCongelado, int tipoCongelado)
    {
        procesandoReporte = true;
        ActualizarEstadoBotonPrincipal();

        // Si el reporte coincide con el Flawed de traje sentado, disparar Sit To Stand para que se levante en los últimos momentos antes del corte!
        ObjetoAnomalia preMatch = BuscarAnomaliaReportada(cuartoCongelado, (TipoAnomalia)tipoCongelado);
        bool disparoSitToStand = false;

        Color colorProcesando = new Color(1f, 0.85f, 0.32f, 1f);
        string[] puntos = new string[] { ".", "..", "...", ".", "..", "..." };

        // Si el clip de procesando es la secuencia completa extraída (~1.9s), reproducirla en su AudioSource dedicado para nunca cortarse con clicks ni con Acierto/Fallo
        AudioSource srcProc = audioProcesandoSource != null ? audioProcesandoSource : audioFeedback;
        bool clipSecuenciaCompleta = sonidoProcesando != null && sonidoProcesando.length > 0.65f;
        if (clipSecuenciaCompleta && srcProc != null)
        {
            srcProc.Stop();
            srcProc.clip = sonidoProcesando;
            srcProc.loop = false;
            srcProc.volume = 0.76f;
            srcProc.time = 0f;
            srcProc.Play();
        }

        for (int i = 0; i < puntos.Length; i++)
        {
            if (!disparoSitToStand && i == 1 && preMatch != null && preMatch.EsFlawedSentadoActivo())
            {
                disparoSitToStand = true;
                preMatch.DispararSitToStandPorReporte();
            }

            string textoAnimado = "Enviando" + puntos[i];
            if (textoEstadoReporte != null && !glitchActivo)
            {
                textoEstadoReporte.text = textoAnimado;
                textoEstadoReporte.color = colorProcesando;
            }
            if (textoBotonEnviar != null)
                textoBotonEnviar.text = textoAnimado;

            if (i % 2 == 0)
            {
                if (!clipSecuenciaCompleta && srcProc != null && sonidoProcesando != null)
                    srcProc.PlayOneShot(sonidoProcesando, 0.55f);
                EnviarHapticPulso(0.12f, 0.05f);
            }

            yield return new WaitForSeconds(disparoSitToStand ? 0.34f : 0.29f);
        }

        // Desvanecer suavemente el audio de procesando (0.16s) en vez de cortarlo en seco mientras entra el sonido de Acierto o Fallo
        if (clipSecuenciaCompleta && srcProc != null && srcProc.isPlaying)
        {
            StartCoroutine(DesvanecerFuenteTablet(srcProc, 0.16f));
        }

        procesandoReporte = false;
        rutinaProcesando = null;
        ActualizarEstadoBotonPrincipal();
        EnviarReporteAlGestor(cuartoCongelado, tipoCongelado);
    }

    private static IEnumerator DesvanecerFuenteTablet(AudioSource src, float duracion)
    {
        if (src == null || !src.isPlaying) yield break;
        float v0 = src.volume;
        float t = 0f;
        while (t < duracion && src != null && src.isPlaying)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duracion);
            src.volume = v0 * (0.5f * (1f + Mathf.Cos(p * Mathf.PI)));
            yield return null;
        }
        if (src != null)
        {
            src.Stop();
            src.volume = v0;
        }
    }

    private void EnviarReporteAlGestor(string cuartoReporte, int tipoReporte)
    {
        TipoAnomalia tipoBuscado = (TipoAnomalia)tipoReporte;
        ObjetoAnomalia anomaliaEncontrada = BuscarAnomaliaReportada(cuartoReporte, tipoBuscado);

        // Si el jugador ya subió la palanca de fusibles en el Garaje y reporta GARAJE + FALLO ELÉCTRICO
        if (anomaliaEncontrada == null &&
            fusiblesReparadosReciente &&
            (string.Equals(cuartoReporte, "Garage", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(cuartoReporte, "Basement", StringComparison.OrdinalIgnoreCase)) &&
            tipoBuscado == TipoAnomalia.Electrical)
        {
            fusiblesReparadosReciente = false;
            textoEstadoReporte.text = "Reporte validado";
            textoEstadoReporte.color = colorVerdeSeleccionado;
            if (audioFeedback && sonidoAcierto) audioFeedback.PlayOneShot(sonidoAcierto);
            EnviarHapticPulso(0.65f, 0.18f);
            ActualizarRelojUI();
            return;
        }

        if (anomaliaEncontrada != null)
        {
            string cuartoRealEsp = ObtenerNombreCuartoEspanol(anomaliaEncontrada.nombreCuarto);
            string tipoEsp = ObtenerNombreTipoEspanol(tipoBuscado);
            if (tiempoActivacionPorAnomalia.TryGetValue(anomaliaEncontrada, out float tInicio))
            {
                sumaTiemposReaccion += Mathf.Max(1f, Time.time - tInicio);
                totalTiemposReaccion++;
                tiempoActivacionPorAnomalia.Remove(anomaliaEncontrada);
            }

            anomaliasCorregidas++;
            textoEstadoReporte.text = "Reporte validado";
            textoEstadoReporte.color = colorVerdeSeleccionado;
            if (audioFeedback && sonidoAcierto) audioFeedback.PlayOneShot(sonidoAcierto);
            EnviarHapticPulso(0.65f, 0.18f);

            // Corte oscuro envolvente de cámara + luz: actualiza el contador de la Tablet DESPUÉS de desactivar la anomalía en la oscuridad!
            InterruptorLuz.EjecutarMicroCorteYRestaurar(this, anomaliaEncontrada, ActualizarRelojUI);
            Debug.Log($"[REPORTE EXITOSO] Se corrigió {anomaliaEncontrada.name} en {cuartoRealEsp} ({tipoEsp}).");
        }
        else
        {
            reportesFallidos++;
            textoEstadoReporte.text = "Sin coincidencias";
            textoEstadoReporte.color = new Color(1f, 0.35f, 0.35f, 1f);
            if (audioFeedback && sonidoFallo) audioFeedback.PlayOneShot(sonidoFallo);
            EnviarHapticPulso(0.3f, 0.1f);
            ActualizarRelojUI();

            EvaluarDerrotaPorAcumulacionOFallos();
        }
    }

    private FinalTurnoVR AsegurarGestorFinalTurno()
    {
        if (FinalTurnoVR.Instancia != null) return FinalTurnoVR.Instancia;
        FinalTurnoVR existente = FindAnyObjectByType<FinalTurnoVR>(FindObjectsInactive.Include);
        if (existente != null) return existente;
        return gameObject.AddComponent<FinalTurnoVR>();
    }

    private TipoAnomalia ObtenerEntidadResponsableScreamer()
    {
        for (int i = 0; i < todasLasAnomalias.Count; i++)
        {
            ObjetoAnomalia a = todasLasAnomalias[i];
            if (a != null && a.estaActiva && EsEntidadHumanoide(a.tipoActivoActual))
                return a.tipoActivoActual;
        }
        return EsEntidadHumanoide(ultimoTipoHumanoideAparecido) ? ultimoTipoHumanoideAparecido : TipoAnomalia.Flawed;
    }

    private void EvaluarDerrotaPorAcumulacionOFallos()
    {
        if (FinalTurnoVR.EstaEnSecuenciaFinal || enFaseInspeccion || horaActual >= 6) return;

        FinalTurnoVR gestorFinal = AsegurarGestorFinalTurno();
        int activas = ContarAnomaliasActivas();

        if (activas >= gestorFinal.maxAnomaliasActivasParaPerder)
        {
            gestorFinal.ActivarDerrota(
                ObtenerEntidadResponsableScreamer(),
                $"Colapso de vigilancia: se acumularon {activas} anomalías activas sin contener.");
        }
        else if (reportesFallidos >= gestorFinal.maxReportesFallidosParaPerder)
        {
            gestorFinal.ActivarDerrota(
                ObtenerEntidadResponsableScreamer(),
                $"Fallo cognitivo crítico: acumulaste {reportesFallidos} reportes erróneos.");
        }
    }

    public int ContarAnomaliasActivas()
    {
        int c = 0;
        for (int i = 0; i < todasLasAnomalias.Count; i++)
        {
            if (todasLasAnomalias[i] != null && todasLasAnomalias[i].estaActiva)
                c++;
        }
        return c;
    }

    public string ObtenerResumenExpedienteCognitivo()
    {
        string horaStr = enFaseInspeccion ? "11:55 PM" : (horaActual == 0 ? "12:00 AM" : $"0{Mathf.Min(horaActual, 6)}:00 AM");
        string cuartoCritico = "SALA";
        int maxInc = 0;
        foreach (var kv in incidentesPorCuarto)
        {
            if (kv.Value > maxInc)
            {
                maxInc = kv.Value;
                cuartoCritico = ObtenerNombreCuartoEspanol(kv.Key);
            }
        }
        string tiempoMedioStr = totalTiemposReaccion > 0
            ? $"{(sumaTiemposReaccion / totalTiemposReaccion):F1} s"
            : "N/A";

        return $"Hora del registro: {horaStr}  |  Sector crítico: {cuartoCritico}  |  Reacción media: {tiempoMedioStr}";
    }

    private void EnviarHapticPulso(float amplitud, float duracion)
    {
        if (!rightController.isValid)
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightController.isValid)
            rightController.SendHapticImpulse(0u, amplitud, duracion);
    }

    public void EnviarHapticAmbosMandos(float amplitud, float duracion)
    {
        if (!rightController.isValid)
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!leftController.isValid)
            leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        if (rightController.isValid)
            rightController.SendHapticImpulse(0u, amplitud, duracion);
        if (leftController.isValid)
            leftController.SendHapticImpulse(0u, amplitud, duracion);
    }

    private void ActualizarEstadoBotonPrincipal()
    {
        if (botonEnviarReporte == null) return;

        if (enFaseInspeccion)
        {
            botonEnviarReporte.interactable = true;
            if (imagenBotonEnviar != null) imagenBotonEnviar.color = colorBotonInspeccion;
            if (textoBotonEnviar != null) textoBotonEnviar.text = "Iniciar turno";
        }
        else if (procesandoReporte)
        {
            botonEnviarReporte.interactable = false;
            if (imagenBotonEnviar != null) imagenBotonEnviar.color = colorBotonProcesando;
            if (textoBotonEnviar != null) textoBotonEnviar.text = "Enviando.";
        }
        else
        {
            botonEnviarReporte.interactable = true;
            if (imagenBotonEnviar != null) imagenBotonEnviar.color = colorBotonEnviarNormal;
            if (textoBotonEnviar != null) textoBotonEnviar.text = "Enviar reporte";
        }
    }

    public void ActivarGlitchVisual(float duracion = 0.45f)
    {
        if (glitchActivo || !gameObject.activeInHierarchy) return;
        if (rutinaGlitch != null) StopCoroutine(rutinaGlitch);
        rutinaGlitch = StartCoroutine(RutinaGlitchTablet(duracion));
    }

    private IEnumerator RutinaGlitchTablet(float duracion)
    {
        glitchActivo = true;
        string headerPrevio = textoRelojHeader != null ? textoRelojHeader.text : "";
        Color colorHeaderPrevio = textoRelojHeader != null ? textoRelojHeader.color : colorRosaNormal;
        string estadoPrevio = textoEstadoReporte != null ? textoEstadoReporte.text : "";
        Color colorEstadoPrevio = textoEstadoReporte != null ? textoEstadoReporte.color : Color.white;

        AudioSource srcGlitch = audioGlitchSource != null ? audioGlitchSource : audioFeedback;
        if (srcGlitch != null && sonidoGlitch != null)
        {
            srcGlitch.Stop();
            srcGlitch.clip = sonidoGlitch;
            srcGlitch.loop = false;
            srcGlitch.volume = 0.42f;
            srcGlitch.time = 0f;
            srcGlitch.Play();
            StartCoroutine(DesvanecerGlitchTrasDuracion(srcGlitch, duracion + 0.12f, 0.22f));
        }
        EnviarHapticPulso(0.18f, 0.08f);

        string[] textosCorruptos = new string[]
        {
            "ERR#R // S3ÑAL !NTERFER!DA",
            "##:## AM [PR3S3NC!A C3RC4]",
            "0x4F_FALL0_S3NS0R_VR"
        };

        float fin = Time.unscaledTime + duracion;
        int paso = 0;
        while (Time.unscaledTime < fin)
        {
            if (textoRelojHeader != null)
            {
                textoRelojHeader.text = textosCorruptos[paso % textosCorruptos.Length];
                textoRelojHeader.color = (paso % 2 == 0) ? new Color(1f, 0.25f, 0.35f, 1f) : new Color(0.35f, 0.95f, 1f, 1f);
            }
            if (textoEstadoReporte != null && !procesandoReporte)
            {
                textoEstadoReporte.text = "Interferencia";
                textoEstadoReporte.color = new Color(0.45f, 0.95f, 1f, 1f);
            }
            if (luzResplandorTablet != null)
                luzResplandorTablet.intensity = (paso % 2 == 0) ? 0.06f : 0.42f;

            paso++;
            yield return new WaitForSecondsRealtime(0.09f);
        }

        if (luzResplandorTablet != null)
            luzResplandorTablet.intensity = 0.28f;

        glitchActivo = false;
        rutinaGlitch = null;

        if (textoRelojHeader != null)
        {
            textoRelojHeader.text = headerPrevio;
            textoRelojHeader.color = colorHeaderPrevio;
        }
        if (textoEstadoReporte != null && !procesandoReporte)
        {
            textoEstadoReporte.text = estadoPrevio;
            textoEstadoReporte.color = colorEstadoPrevio;
        }
        ActualizarRelojUI();
    }

    private static IEnumerator DesvanecerGlitchTrasDuracion(AudioSource src, float espera, float fade)
    {
        yield return new WaitForSecondsRealtime(espera);
        if (src != null && src.isPlaying)
        {
            float v0 = src.volume;
            float t = 0f;
            while (t < fade && src != null && src.isPlaying)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / fade);
                src.volume = v0 * (0.5f * (1f + Mathf.Cos(p * Mathf.PI)));
                yield return null;
            }
            if (src != null)
            {
                src.Stop();
                src.volume = v0;
            }
        }
    }

    private void ActualizarRelojUI()
    {
        if (textoRelojHeader == null || glitchActivo) return;

        if (enFaseInspeccion)
        {
            int seg = Mathf.CeilToInt(Mathf.Max(0f, tiempoRestanteInspeccion));
            int minutoPrevio = Mathf.Clamp(55 + Mathf.FloorToInt((1f - (tiempoRestanteInspeccion / Mathf.Max(1f, duracionInspeccionInicial))) * 5f), 55, 59);
            textoRelojHeader.text = $"11:{minutoPrevio:00} PM  ·  Tiempo de gracia ({seg}s)";
            textoRelojHeader.color = new Color(0.62f, 0.86f, 1f, 1f);
            ultimoConteoActivasUI = 0;
            return;
        }

        string horaTexto = (horaActual == 0) ? "12:00 AM" : $"0{horaActual}:00 AM";
        if (horaActual >= 6)
        {
            textoRelojHeader.text = "06:00 AM  ·  Fin del turno";
            textoRelojHeader.color = colorVerdeSeleccionado;
        }
        else
        {
            int activas = ContarAnomaliasActivas();
            ultimoConteoActivasUI = activas;

            if (activas >= 3)
            {
                textoRelojHeader.text = $"{horaTexto}  ·  Anomalías: Crítico";
                textoRelojHeader.color = new Color(1f, 0.22f, 0.22f, 1f);
            }
            else if (activas == 2)
            {
                textoRelojHeader.text = $"{horaTexto}  ·  Anomalías: Detectable";
                textoRelojHeader.color = new Color(1f, 0.52f, 0.30f, 1f);
            }
            else if (activas == 1)
            {
                textoRelojHeader.text = $"{horaTexto}  ·  Anomalías: Leve";
                textoRelojHeader.color = new Color(0.98f, 0.80f, 0.42f, 1f);
            }
            else
            {
                textoRelojHeader.text = $"{horaTexto}  ·  Anomalías: Ninguna";
                textoRelojHeader.color = colorRosaNormal;
            }
        }
    }

    // =========================================================================
    // CONSTRUCCIÓN AUTOMÁTICA DE LA PANTALLA EN ESPAÑOL (8 HABITACIONES + 10 TIPOS)
    // =========================================================================
    private void ConstruirInterfazEnTablet()
    {
        tabletCanvas = GetComponentInChildren<Canvas>(true);

        if (tabletCanvas == null)
        {
            GameObject canvasObj = new GameObject("Pantalla de reportes",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);
            tabletCanvas = canvasObj.GetComponent<Canvas>();

            canvasObj.transform.localPosition = new Vector3(0f, 0.003f, 0f);
            canvasObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            canvasObj.transform.localScale = Vector3.one * 0.000076444565f;
        }
        else
        {
            Vector3 lp = tabletCanvas.transform.localPosition;
            tabletCanvas.transform.localPosition = new Vector3(lp.x, Mathf.Max(lp.y, 0.003f), lp.z);
            tabletCanvas.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        tabletCanvas.gameObject.layer = 5;
        tabletCanvas.renderMode = RenderMode.WorldSpace;
        tabletCanvas.worldCamera = Camera.main;
        tabletCanvas.sortingOrder = 100;

        GraphicRaycaster gr = tabletCanvas.GetComponent<GraphicRaycaster>();
        if (gr == null) gr = tabletCanvas.gameObject.AddComponent<GraphicRaycaster>();
        gr.ignoreReversedGraphics = false;

        TrackedDeviceGraphicRaycaster tdgr = tabletCanvas.GetComponent<TrackedDeviceGraphicRaycaster>();
        if (tdgr == null) tdgr = tabletCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        tdgr.ignoreReversedGraphics = false;
        tdgr.checkFor3DOcclusion = false;

        RectTransform canvasRect = tabletCanvas.GetComponent<RectTransform>();
        if (canvasRect.sizeDelta.x <= 0f || canvasRect.sizeDelta.y <= 0f)
            canvasRect.sizeDelta = new Vector2(900f, 600f);

        Vector2 canvasSize = canvasRect.sizeDelta;
        bool portrait = canvasSize.y > canvasSize.x;
        float designWidth = portrait ? 680f : 900f;
        float designHeight = portrait ? 1020f : 600f;
        float uiScale = Mathf.Min(canvasSize.x / designWidth, canvasSize.y / designHeight);

        for (int i = tabletCanvas.transform.childCount - 1; i >= 0; i--)
            Destroy(tabletCanvas.transform.GetChild(i).gameObject);

        botonesCuartos.Clear();
        botonesTipos.Clear();

        CreateImage(tabletCanvas.transform, "Fondo_Pantalla", canvasSize, Vector2.zero, new Color(0.04f, 0.05f, 0.07f, 0.98f)).raycastTarget = false;

        // 1. BARRA SUPERIOR (Reloj + Botón de Pausa)
        float headerHeight = (portrait ? 76f : 56f) * uiScale;
        float headerY = (designHeight * 0.5f - (portrait ? 38f : 28f)) * uiScale;
        GameObject barraSuperior = CreateRect("Barra_Superior", tabletCanvas.transform,
            new Vector2(designWidth * uiScale, headerHeight), new Vector2(0f, headerY));
        CreateImage(barraSuperior.transform, "Bg_Header", new Vector2(designWidth * uiScale, headerHeight),
            Vector2.zero, new Color(0.09f, 0.1f, 0.14f, 1f)).raycastTarget = false;

        float relojX = portrait ? -125f : -125f;
        float pausaX = portrait ? 205f : 315f;
        Vector2 relojSize = portrait ? new Vector2(400f, 56f) : new Vector2(540f, 46f);
        Vector2 pausaSize = portrait ? new Vector2(210f, 52f) : new Vector2(195f, 42f);
        int headerFontSize = Mathf.RoundToInt((portrait ? 24f : 20f) * uiScale);
        textoRelojHeader = CreateText(barraSuperior.transform, "Reloj_Turno", "12:00 AM  ·  Anomalías: Ninguna",
            new Vector2(relojX * uiScale, 0f), relojSize * uiScale, headerFontSize,
            TextAnchor.MiddleLeft, colorRosaNormal);

        CreateButton(barraSuperior.transform, "Btn_Pausa_Header", "PAUSA",
            new Vector2(pausaX * uiScale, 0f), pausaSize * uiScale,
            new Color(0.55f, 0.14f, 0.16f, 1f), Color.white,
            Mathf.RoundToInt((portrait ? 24f : 18f) * uiScale), AlternarPausaEnTablet);

        // 2. PANEL DE REPORTE DE ANOMALÍAS (8 Habitaciones Reales de la Casa)
        panelAnomalias = CreateRect("App_Reporte_Anomalias", tabletCanvas.transform, canvasSize, Vector2.zero);

        float roomLabelY = portrait ? 442f : 212f;
        CreateText(panelAnomalias.transform, "Lbl_Cuartos", "Habitación",
            new Vector2(0f, roomLabelY * uiScale), new Vector2((portrait ? 640f : 860f) * uiScale, 32f * uiScale),
            Mathf.RoundToInt((portrait ? 22f : 20f) * uiScale), TextAnchor.MiddleCenter, Color.white);

        int numCuartos = HabitacionesCasa.GetLength(0); // 8 habitaciones
        int colsCuartos = portrait ? 2 : 4;
        float roomColumnSpacing = portrait ? 315f : 212f;
        float roomButtonWidth = portrait ? 298f : 202f;
        float roomButtonHeight = portrait ? 52f : 44f;
        float roomRowSpacing = portrait ? 60f : 52f;
        float roomFirstRowY = portrait ? 395f : 162f;
        int roomFontSize = Mathf.RoundToInt((portrait ? 21f : 18f) * uiScale);

        for (int i = 0; i < numCuartos; i++)
        {
            int fila = i / colsCuartos;
            int col = i % colsCuartos;
            float offsetCentro = (colsCuartos - 1) * 0.5f;
            float posX = (col - offsetCentro) * roomColumnSpacing;
            float posY = roomFirstRowY - (fila * roomRowSpacing);

            string idCuarto = HabitacionesCasa[i, 0];
            string nombreMostrar = HabitacionesCasa[i, 1];

            Button bCuarto = CreateButton(panelAnomalias.transform, "Room_" + idCuarto, nombreMostrar,
                new Vector2(posX * uiScale, posY * uiScale),
                new Vector2(roomButtonWidth, roomButtonHeight) * uiScale,
                colorBotonOscuro, colorRosaNormal, roomFontSize,
                () => SeleccionarCuartoUI(idCuarto));

            botonesCuartos[idCuarto] = bCuarto.GetComponent<Image>();
        }

        // 3. LOS 10 TIPOS DE ANOMALÍA EN ESPAÑOL
        float typeLabelY = portrait ? 160f : 44f;
        float typeColumnX = portrait ? 158f : 215f;
        float typeFirstRowY = portrait ? 105f : 0f;
        float typeRowSpacing = portrait ? 66f : 42f;
        Vector2 typeButtonSize = (portrait ? new Vector2(302f, 56f) : new Vector2(415f, 38f)) * uiScale;
        int typeFontSize = Mathf.RoundToInt((portrait ? 20f : 18f) * uiScale);

        CreateText(panelAnomalias.transform, "Lbl_Tipos", "Tipo",
            new Vector2(0f, typeLabelY * uiScale), new Vector2((portrait ? 640f : 860f) * uiScale, 32f * uiScale),
            Mathf.RoundToInt((portrait ? 22f : 20f) * uiScale), TextAnchor.MiddleCenter, Color.white);

        for (int i = 0; i < 10; i++)
        {
            int indiceCopia = i;
            int col = i / 5;
            int fila = i % 5;
            float posX = (col == 0) ? -typeColumnX : typeColumnX;
            float posY = typeFirstRowY - (fila * typeRowSpacing);

            Button bTipo = CreateButton(panelAnomalias.transform, "Type_" + i, NombresTiposEspanol[i],
                new Vector2(posX * uiScale, posY * uiScale), typeButtonSize,
                colorBotonOscuro, colorRosaNormal, typeFontSize,
                () => SeleccionarTipoUI(indiceCopia));

            botonesTipos[indiceCopia] = bTipo.GetComponent<Image>();
        }

        float statusY = portrait ? -235f : -218f;
        float reportY = portrait ? -330f : -269f;
        Vector2 statusSize = (portrait ? new Vector2(640f, 44f) : new Vector2(850f, 32f)) * uiScale;
        Vector2 reportSize = (portrait ? new Vector2(620f, 92f) : new Vector2(700f, 46f)) * uiScale;
        textoEstadoReporte = CreateText(panelAnomalias.transform, "Txt_Estado_Reporte", "Turno iniciado",
            new Vector2(0f, statusY * uiScale), statusSize,
            Mathf.RoundToInt((portrait ? 22f : 17f) * uiScale), TextAnchor.MiddleCenter, Color.white);

        botonEnviarReporte = CreateButton(panelAnomalias.transform, "Btn_Enviar_Reporte", "Enviar reporte",
            new Vector2(0f, reportY * uiScale), reportSize,
            colorBotonEnviarNormal, Color.white,
            Mathf.RoundToInt((portrait ? 30f : 21f) * uiScale),
            AlPulsarBotonPrincipal);
        imagenBotonEnviar = botonEnviarReporte.GetComponent<Image>();
        textoBotonEnviar = botonEnviarReporte.GetComponentInChildren<Text>();
    }

    private void SeleccionarCuartoUI(string cuarto)
    {
        cuartoElegido = cuarto;
        foreach (var par in botonesCuartos)
        {
            Text txt = par.Value.GetComponentInChildren<Text>();
            bool esSeleccionado = (par.Key == cuarto);
            par.Value.color = esSeleccionado ? new Color(0.15f, 0.35f, 0.2f, 1f) : colorBotonOscuro;
            if (txt != null) txt.color = esSeleccionado ? colorVerdeSeleccionado : colorRosaNormal;
        }

        if (audioFeedback && sonidoClickUI) audioFeedback.PlayOneShot(sonidoClickUI);
        EnviarHapticPulso(0.25f, 0.05f);

        if (textoEstadoReporte != null && !procesandoReporte && !glitchActivo && !enFaseInspeccion)
        {
            textoEstadoReporte.text = $"{ObtenerNombreCuartoEspanol(cuartoElegido)} · {ObtenerNombreTipoEspanol(tipoElegido)}";
            textoEstadoReporte.color = Color.white;
        }
    }

    private void SeleccionarTipoUI(int indiceTipo)
    {
        tipoElegido = indiceTipo;
        foreach (var par in botonesTipos)
        {
            Text txt = par.Value.GetComponentInChildren<Text>();
            bool esSeleccionado = (par.Key == indiceTipo);
            par.Value.color = esSeleccionado ? new Color(0.15f, 0.35f, 0.2f, 1f) : colorBotonOscuro;
            if (txt != null) txt.color = esSeleccionado ? colorVerdeSeleccionado : colorRosaNormal;
        }

        if (audioFeedback && sonidoClickUI) audioFeedback.PlayOneShot(sonidoClickUI);
        EnviarHapticPulso(0.25f, 0.05f);

        if (textoEstadoReporte != null && !procesandoReporte && !glitchActivo && !enFaseInspeccion)
        {
            textoEstadoReporte.text = $"{ObtenerNombreCuartoEspanol(cuartoElegido)} · {ObtenerNombreTipoEspanol(tipoElegido)}";
            textoEstadoReporte.color = Color.white;
        }
    }

    public void AlternarPausaEnTablet()
    {
        if (VRPauseMenu.Instance == null)
        {
            Debug.LogWarning("No se encontró el menú de pausa VR para mostrarlo frente al jugador.", this);
            return;
        }

        VRPauseMenu.Instance.ToggleMenu();
    }

    private void EnsureUiEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject evObj = new GameObject("EventSystem VR", typeof(EventSystem), typeof(XRUIInputModule));
            eventSystem = evObj.GetComponent<EventSystem>();
        }
        XRUIInputModule inputModule = eventSystem.GetComponent<XRUIInputModule>();
        if (inputModule == null)
        {
            foreach (BaseInputModule mod in eventSystem.GetComponents<BaseInputModule>())
                mod.enabled = false;
            inputModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();
        }
        inputModule.enableXRInput = true;
        inputModule.enableMouseInput = true;
        inputModule.enableTouchInput = true;
        inputModule.enableBuiltinActionsAsFallback = true;
    }

    private GameObject CreateRect(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.GetComponent<RectTransform>();
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return obj;
    }

    private Image CreateImage(Transform parent, string name, Vector2 size, Vector2 pos, Color col)
    {
        GameObject obj = CreateRect(name, parent, size, pos);
        obj.AddComponent<CanvasRenderer>();
        Image img = obj.AddComponent<Image>();
        img.color = col;
        return img;
    }

    private Text CreateText(Transform parent, string name, string content, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color col)
    {
        GameObject obj = CreateRect(name, parent, size, pos);
        obj.AddComponent<CanvasRenderer>();
        Text t = obj.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = content;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = col;
        t.raycastTarget = false;
        return t;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bgCol, Color txtCol, int fontSize, UnityEngine.Events.UnityAction onClick)
    {
        Image img = CreateImage(parent, name, size, pos, bgCol);
        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(onClick);

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.9f, 1f);
        cb.pressedColor = new Color(0.6f, 0.95f, 0.6f, 1f);
        btn.colors = cb;

        CreateText(img.transform, "Texto", label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, txtCol);
        return btn;
    }

    private static AudioClip CrearTonoProcedural(string nombre, float freqInicio, float freqFin, float duracion, float volumen)
    {
        int sampleRate = 22050;
        int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duracion));
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float progreso = (float)i / samples;
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(freqInicio, freqFin, progreso);
            float env = Mathf.Sin(progreso * Mathf.PI);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * volumen;
        }
        AudioClip clip = AudioClip.Create(nombre, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CrearRuidoGlitchProcedural(string nombre, float duracion, float volumen)
    {
        int sampleRate = 22050;
        int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duracion));
        float[] data = new float[samples];
        float lp = 0f;
        for (int i = 0; i < samples; i++)
        {
            float progreso = (float)i / samples;
            float t = (float)i / sampleRate;
            float env = Mathf.Sin(progreso * Mathf.PI);
            float rafaga = (Mathf.FloorToInt(t * 28f) % 3 != 1) ? 1f : 0.25f;
            lp = lp * 0.65f + (UnityEngine.Random.value * 2f - 1f) * 0.35f;
            float zumbido = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.16f;
            data[i] = (lp * 0.55f + zumbido) * env * rafaga * volumen;
        }
        AudioClip clip = AudioClip.Create(nombre, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

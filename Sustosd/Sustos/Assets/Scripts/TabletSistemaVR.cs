using System;
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

    [Header("1. Configuración del Turno (00:00 a 06:00 AM)")]
    public float duracionHoraEnSegundos = 50f;
    private float relojInterno = 0f;
    private int horaActual = 0;

    [Header("2. Generador Automático de Anomalías")]
    public float segundosEntreAnomalias = 18f;
    private float temporizadorAnomalias = 12f;
    public List<ObjetoAnomalia> todasLasAnomalias = new List<ObjetoAnomalia>();

    [Header("3. Sonidos Opcionales de la Tablet")]
    public AudioSource audioFeedback;
    public AudioClip sonidoAcierto;
    public AudioClip sonidoFallo;
    private AudioClip sonidoClickUI;

    private Canvas tabletCanvas;
    private GameObject panelAnomalias;

    private Text textoRelojHeader;
    private Text textoEstadoReporte;

    private Dictionary<string, Image> botonesCuartos = new Dictionary<string, Image>();
    private Dictionary<int, Image> botonesTipos = new Dictionary<int, Image>();

    private string cuartoElegido = "LivingRoom";
    private int tipoElegido = 0;
    private int anomaliasCorregidas = 0;
    private bool fusiblesReparadosReciente = false;
    private InputDevice rightController;

    private readonly Color colorRosaNormal = new Color(0.85f, 0.55f, 0.58f, 1f);
    private readonly Color colorVerdeSeleccionado = new Color(0.45f, 0.95f, 0.55f, 1f);
    private readonly Color colorBotonOscuro = new Color(0.12f, 0.14f, 0.18f, 0.95f);

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
        "HUMANO DEFORME",
        "SOMBRA (OJOS BLANCOS)",
        "SUSURROS (MONJE)",
        "FALLO ELÉCTRICO",
        "REFLEJO EN ESPEJO",
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
        string cuartoAnom = ObjetoAnomalia.NormalizarCuartoPorPosicion(anomalia.transform.position, anomalia.nombreCuarto);
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
        ConfigurarRayosUIMandos();
        SeleccionarCuartoUI("LivingRoom");
        SeleccionarTipoUI(0);
    }

    public void RefrescarListaAnomalias()
    {
        todasLasAnomalias.Clear();
        todasLasAnomalias.AddRange(FindObjectsByType<ObjetoAnomalia>(FindObjectsInactive.Include));
        for (int i = 0; i < todasLasAnomalias.Count; i++)
        {
            if (todasLasAnomalias[i] != null)
            {
                todasLasAnomalias[i].nombreCuarto = ObjetoAnomalia.NormalizarCuartoPorPosicion(
                    todasLasAnomalias[i].transform.position,
                    todasLasAnomalias[i].nombreCuarto);
            }
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

        if (sonidoClickUI == null)
            sonidoClickUI = CrearTonoProcedural("TabletClick", 880f, 880f, 0.035f, 0.35f);
        if (sonidoAcierto == null)
            sonidoAcierto = CrearTonoProcedural("TabletSuccess", 520f, 1040f, 0.22f, 0.55f);
        if (sonidoFallo == null)
            sonidoFallo = CrearTonoProcedural("TabletError", 260f, 140f, 0.28f, 0.55f);
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
        if (!VRPauseMenu.IsMenuOpen && horaActual < 6)
        {
            relojInterno += Time.deltaTime;
            if (relojInterno >= duracionHoraEnSegundos)
            {
                relojInterno = 0f;
                horaActual++;
                ActualizarRelojUI();
            }

            temporizadorAnomalias += Time.deltaTime;
            if (temporizadorAnomalias >= segundosEntreAnomalias)
            {
                temporizadorAnomalias = 0f;
                GenerarAnomaliaAleatoria();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }

    // =========================================================================
    // GESTOR DE ANOMALÍAS INTEGRADO
    // =========================================================================
    public void GenerarAnomaliaAleatoria()
    {
        if (todasLasAnomalias.Count == 0)
            RefrescarListaAnomalias();

        List<ObjetoAnomalia> inactivas = todasLasAnomalias.FindAll(a =>
            a != null && !a.estaActiva && a.posiblesVariantes != null && a.posiblesVariantes.Count > 0);

        if (inactivas.Count == 0) return;

        Transform cam = Camera.main != null ? Camera.main.transform : transform;
        List<ObjetoAnomalia> enLugaresCerrados = inactivas.FindAll(a =>
            Vector3.Distance(cam.position, a.transform.position) > 3.2f ||
            Vector3.Angle(cam.forward, (a.transform.position - cam.position).normalized) > 65f);

        List<ObjetoAnomalia> pool = enLugaresCerrados.Count > 0 ? enLugaresCerrados : inactivas;
        int indice = UnityEngine.Random.Range(0, pool.Count);
        ObjetoAnomalia elegida = pool[indice];
        elegida.ActivarAnomalia();

        if (elegida.tipoActivoActual == TipoAnomalia.Unknown && elegida.GetComponent<PuertaAutoCierre>() != null)
        {
            ObjetoAnomalia interna = pool.Find(a =>
                a != elegida &&
                !a.estaActiva &&
                CoincideCuartoExacto(a, elegida.nombreCuarto) &&
                a.GetComponent<PuertaAutoCierre>() == null);

            if (interna != null)
            {
                interna.ActivarAnomalia();
                Debug.Log($"[ANOMALÍA EN CUARTO ABIERTO POR UNKNOWN] Cuarto: {ObtenerNombreCuartoEspanol(interna.nombreCuarto)} | Tipo: {ObtenerNombreTipoEspanol(interna.tipoActivoActual)} | Objeto: {interna.name}");
            }
        }

        ActualizarRelojUI();
        Debug.Log($"[ANOMALÍA ACTIVADA] Cuarto: {ObtenerNombreCuartoEspanol(elegida.nombreCuarto)} | Tipo: {ObtenerNombreTipoEspanol(elegida.tipoActivoActual)} | Objeto: {elegida.name}");
    }

    public void NotificarFusiblesReparados()
    {
        fusiblesReparadosReciente = true;
        anomaliasCorregidas++;
        if (textoEstadoReporte != null)
        {
            textoEstadoReporte.text = "¡APAGÓN REPARADO EN GARAJE!";
            textoEstadoReporte.color = colorVerdeSeleccionado;
        }
        ActualizarRelojUI();
    }

    private void EnviarReporteAlGestor()
    {
        TipoAnomalia tipoBuscado = (TipoAnomalia)tipoElegido;
        string cuartoEsp = ObtenerNombreCuartoEspanol(cuartoElegido);
        string tipoEsp = ObtenerNombreTipoEspanol(tipoBuscado);

        if (todasLasAnomalias.Count == 0)
            RefrescarListaAnomalias();

        // 1. Buscar coincidencia exacta de Habitación + Tipo de Anomalía activa
        ObjetoAnomalia anomaliaEncontrada = todasLasAnomalias.Find(a =>
            a != null &&
            a.estaActiva &&
            CoincideCuartoExacto(a, cuartoElegido) &&
            a.tipoActivoActual == tipoBuscado);

        // 2. Si el jugador seleccionó cualquiera de las 3 recámaras y acertó el tipo en otra recámara del piso 2, aceptarlo también
        if (anomaliaEncontrada == null && EsRecamara(cuartoElegido))
        {
            anomaliaEncontrada = todasLasAnomalias.Find(a =>
                a != null &&
                a.estaActiva &&
                EsRecamara(ObjetoAnomalia.NormalizarCuartoPorPosicion(a.transform.position, a.nombreCuarto)) &&
                a.tipoActivoActual == tipoBuscado);
        }

        // 3. Si el jugador ya subió la palanca de fusibles en el Garaje y reporta GARAJE + FALLO ELÉCTRICO
        if (anomaliaEncontrada == null &&
            fusiblesReparadosReciente &&
            (string.Equals(cuartoElegido, "Garage", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(cuartoElegido, "Basement", StringComparison.OrdinalIgnoreCase)) &&
            tipoBuscado == TipoAnomalia.Electrical)
        {
            fusiblesReparadosReciente = false;
            textoEstadoReporte.text = $"¡ANOMALÍA CORREGIDA! ({anomaliasCorregidas} resueltas)";
            textoEstadoReporte.color = colorVerdeSeleccionado;
            if (audioFeedback && sonidoAcierto) audioFeedback.PlayOneShot(sonidoAcierto);
            EnviarHapticPulso(0.65f, 0.18f);
            ActualizarRelojUI();
            return;
        }

        if (anomaliaEncontrada != null)
        {
            string cuartoRealEsp = ObtenerNombreCuartoEspanol(anomaliaEncontrada.nombreCuarto);
            try
            {
                anomaliaEncontrada.RestaurarNormalidad();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Tablet] Aviso al restaurar anomalía {anomaliaEncontrada.name}: {ex.Message}");
            }

            anomaliasCorregidas++;
            textoEstadoReporte.text = $"¡CORREGIDA! ({cuartoRealEsp} - {tipoEsp})";
            textoEstadoReporte.color = colorVerdeSeleccionado;
            if (audioFeedback && sonidoAcierto) audioFeedback.PlayOneShot(sonidoAcierto);
            EnviarHapticPulso(0.65f, 0.18f);
            ActualizarRelojUI();
            Debug.Log($"[REPORTE EXITOSO] Se corrigió {anomaliaEncontrada.name} en {cuartoRealEsp} ({tipoEsp}).");
        }
        else
        {
            ObjetoAnomalia otraEnMismoCuarto = todasLasAnomalias.Find(a =>
                a != null &&
                a.estaActiva &&
                CoincideCuartoExacto(a, cuartoElegido));

            int totalActivas = ContarAnomaliasActivas();
            if (totalActivas == 0)
            {
                textoEstadoReporte.text = "SIN ANOMALÍAS ACTIVAS EN ESTE MOMENTO.";
            }
            else if (otraEnMismoCuarto != null)
            {
                textoEstadoReporte.text = $"FALLO: En {cuartoEsp} no es {tipoEsp}.";
            }
            else
            {
                textoEstadoReporte.text = $"ERROR: Sin {tipoEsp} en {cuartoEsp}.";
            }

            textoEstadoReporte.color = new Color(1f, 0.35f, 0.35f, 1f);
            if (audioFeedback && sonidoFallo) audioFeedback.PlayOneShot(sonidoFallo);
            EnviarHapticPulso(0.3f, 0.1f);
        }
    }

    private int ContarAnomaliasActivas()
    {
        int c = 0;
        for (int i = 0; i < todasLasAnomalias.Count; i++)
        {
            if (todasLasAnomalias[i] != null && todasLasAnomalias[i].estaActiva)
                c++;
        }
        return c;
    }

    private void EnviarHapticPulso(float amplitud, float duracion)
    {
        if (!rightController.isValid)
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightController.isValid)
            rightController.SendHapticImpulse(0u, amplitud, duracion);
    }

    private void ActualizarRelojUI()
    {
        if (textoRelojHeader == null) return;
        string horaTexto = (horaActual == 0) ? "12:00 AM" : $"0{horaActual}:00 AM";
        if (horaActual >= 6)
        {
            textoRelojHeader.text = "06:00 AM - ¡TURNO COMPLETADO!";
            textoRelojHeader.color = colorVerdeSeleccionado;
        }
        else
        {
            int activas = ContarAnomaliasActivas();
            textoRelojHeader.text = activas > 0 ? $"{horaTexto}  [ALERTA: {activas}]" : horaTexto;
            textoRelojHeader.color = activas > 0 ? new Color(1f, 0.45f, 0.45f, 1f) : colorRosaNormal;
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

        float relojX = portrait ? -150f : -170f;
        float pausaX = portrait ? 190f : 300f;
        Vector2 relojSize = portrait ? new Vector2(340f, 56f) : new Vector2(420f, 46f);
        Vector2 pausaSize = portrait ? new Vector2(240f, 52f) : new Vector2(220f, 42f);
        int headerFontSize = Mathf.RoundToInt((portrait ? 28f : 22f) * uiScale);
        textoRelojHeader = CreateText(barraSuperior.transform, "Reloj_Turno", "12:00 AM",
            new Vector2(relojX * uiScale, 0f), relojSize * uiScale, headerFontSize,
            TextAnchor.MiddleLeft, colorRosaNormal);

        CreateButton(barraSuperior.transform, "Btn_Pausa_Header", "PAUSA [A]",
            new Vector2(pausaX * uiScale, 0f), pausaSize * uiScale,
            new Color(0.55f, 0.14f, 0.16f, 1f), Color.white,
            Mathf.RoundToInt((portrait ? 24f : 18f) * uiScale), AlternarPausaEnTablet);

        // 2. PANEL DE REPORTE DE ANOMALÍAS (8 Habitaciones Reales de la Casa)
        panelAnomalias = CreateRect("App_Reporte_Anomalias", tabletCanvas.transform, canvasSize, Vector2.zero);

        float roomLabelY = portrait ? 442f : 212f;
        CreateText(panelAnomalias.transform, "Lbl_Cuartos", "1. SELECCIONA HABITACIÓN DE LA CASA:",
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

        CreateText(panelAnomalias.transform, "Lbl_Tipos", "2. SELECCIONA TIPO DE ANOMALÍA:",
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
        textoEstadoReporte = CreateText(panelAnomalias.transform, "Txt_Estado_Reporte", "Sistema listo para reportar.",
            new Vector2(0f, statusY * uiScale), statusSize,
            Mathf.RoundToInt((portrait ? 22f : 17f) * uiScale), TextAnchor.MiddleCenter, Color.white);

        CreateButton(panelAnomalias.transform, "Btn_Enviar_Reporte", "[ ENVIAR REPORTE ]",
            new Vector2(0f, reportY * uiScale), reportSize,
            new Color(0.15f, 0.42f, 0.2f, 1f), Color.white,
            Mathf.RoundToInt((portrait ? 32f : 22f) * uiScale),
            EnviarReporteAlGestor);
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

        if (textoEstadoReporte != null)
        {
            textoEstadoReporte.text = $"Seleccionado: {ObtenerNombreCuartoEspanol(cuartoElegido)} - {ObtenerNombreTipoEspanol(tipoElegido)}";
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

        if (textoEstadoReporte != null)
        {
            textoEstadoReporte.text = $"Seleccionado: {ObtenerNombreCuartoEspanol(cuartoElegido)} - {ObtenerNombreTipoEspanol(tipoElegido)}";
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
}

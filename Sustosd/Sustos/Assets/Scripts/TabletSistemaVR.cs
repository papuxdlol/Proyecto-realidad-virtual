using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class TabletSistemaVR : MonoBehaviour
{
    public static TabletSistemaVR Instancia;

    private const string MainMenuSceneName = "MenuPrincipal";

    [Header("1. Configuración del Turno (00:00 a 06:00 AM)")]
    public float duracionHoraEnSegundos = 50f;
    private float relojInterno = 0f;
    private int horaActual = 0;

    [Header("2. Generador Automático de Anomalías")]
    public float segundosEntreAnomalias = 20f;
    private float temporizadorAnomalias = 0f;
    public List<ObjetoAnomalia> todasLasAnomalias = new List<ObjetoAnomalia>();

    [Header("3. Sonidos Opcionales de la Tablet")]
    public AudioSource audioFeedback;
    public AudioClip sonidoAcierto;
    public AudioClip sonidoFallo;

    private Canvas tabletCanvas;
    private GameObject panelAnomalias;
    private GameObject panelPausaPrincipal;
    private GameObject panelOpcionesVolumen;

    private Text textoRelojHeader;
    private Text textoEstadoReporte;
    private Text textoBotonPausaHeader;

    private Dictionary<string, Image> botonesCuartos = new Dictionary<string, Image>();
    private Dictionary<int, Image> botonesTipos = new Dictionary<int, Image>();

    private string cuartoElegido = "Kitchen";
    private int tipoElegido = 0;

    private bool isPaused = false;
    private float previousTimeScale = 1f;
    private bool aButtonWasPressed = false;
    private InputDevice rightController;

    private readonly Color colorRosaNormal = new Color(0.85f, 0.55f, 0.58f, 1f);
    private readonly Color colorVerdeSeleccionado = new Color(0.45f, 0.95f, 0.55f, 1f);
    private readonly Color colorBotonOscuro = new Color(0.12f, 0.14f, 0.18f, 0.95f);

    private void Awake()
    {
        Instancia = this;
        EnsureUiEventSystem();
    }

    private void Start()
    {
        // Recolectar automáticamente todos los objetos con anomalías de la casa
        todasLasAnomalias.Clear();
        todasLasAnomalias.AddRange(FindObjectsByType<ObjetoAnomalia>(FindObjectsSortMode.None));

        ConstruirInterfazEnTablet();
        SeleccionarCuartoUI("Kitchen");
        SeleccionarTipoUI(0);
    }

    private void Update()
    {
        if (!isPaused && horaActual < 6)
        {
            // 1. Avanzar el reloj de la noche
            relojInterno += Time.deltaTime;
            if (relojInterno >= duracionHoraEnSegundos)
            {
                relojInterno = 0f;
                horaActual++;
                ActualizarRelojUI();
            }

            // 2. Activar una anomalía aleatoria cada cierto tiempo
            temporizadorAnomalias += Time.deltaTime;
            if (temporizadorAnomalias >= segundosEntreAnomalias)
            {
                temporizadorAnomalias = 0f;
                GenerarAnomaliaAleatoria();
            }
        }

        // 3. Detectar Botón A del control derecho para Pausar/Reanudar
        if (!rightController.isValid)
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (rightController.isValid &&
            rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool aButtonPressed))
        {
            if (aButtonPressed && !aButtonWasPressed)
                AlternarPausaEnTablet();

            aButtonWasPressed = aButtonPressed;
        }
        else
        {
            aButtonWasPressed = false;
        }
    }

    private void OnDestroy()
    {
        if (isPaused) Time.timeScale = 1f;
    }

    // =========================================================================
    // GESTOR DE ANOMALÍAS INTEGRADO
    // =========================================================================
    public void GenerarAnomaliaAleatoria()
    {
        List<ObjetoAnomalia> inactivas = todasLasAnomalias.FindAll(a => a != null && !a.estaActiva);
        if (inactivas.Count > 0)
        {
            int indice = Random.Range(0, inactivas.Count);
            inactivas[indice].ActivarAnomalia();
            Debug.Log($"[ANOMALÍA ACTIVADA] Cuarto: {inactivas[indice].nombreCuarto} | Tipo: {inactivas[indice].tipoActivoActual} | Objeto: {inactivas[indice].name}");
        }
    }

    private void EnviarReporteAlGestor()
    {
        TipoAnomalia tipoBuscado = (TipoAnomalia)tipoElegido;

        ObjetoAnomalia anomaliaEncontrada = todasLasAnomalias.Find(a =>
            a != null &&
            a.estaActiva &&
            a.nombreCuarto == cuartoElegido &&
            a.tipoActivoActual == tipoBuscado);

        if (anomaliaEncontrada != null)
        {
            anomaliaEncontrada.RestaurarNormalidad();
            textoEstadoReporte.text = "¡ANOMALÍA CORREGIDA!";
            textoEstadoReporte.color = colorVerdeSeleccionado;
            if (audioFeedback && sonidoAcierto) audioFeedback.PlayOneShot(sonidoAcierto);
        }
        else
        {
            textoEstadoReporte.text = "ERROR: No se detectó esa anomalía.";
            textoEstadoReporte.color = new Color(1f, 0.35f, 0.35f, 1f);
            if (audioFeedback && sonidoFallo) audioFeedback.PlayOneShot(sonidoFallo);
        }
    }

    private void ActualizarRelojUI()
    {
        if (textoRelojHeader == null) return;
        string horaTexto = (horaActual == 0) ? "12:00 AM" : $"0{horaActual}:00 AM";
        if (horaActual >= 6)
        {
            textoRelojHeader.text = "06:00 AM - ¡GANASTE!";
            textoRelojHeader.color = colorVerdeSeleccionado;
        }
        else
        {
            textoRelojHeader.text = horaTexto;
        }
    }

    // =========================================================================
    // CONSTRUCCIÓN AUTOMÁTICA DE LA PANTALLA EN 'Pantalla de reportes'
    // =========================================================================
    private void ConstruirInterfazEnTablet()
    {
        // Usa el Canvas que ya tienes en 'Pantalla de reportes' dentro de la Tablet
        tabletCanvas = GetComponentInChildren<Canvas>(true);

        if (tabletCanvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas_Pantalla_Tablet",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);
            tabletCanvas = canvasObj.GetComponent<Canvas>();

            canvasObj.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            canvasObj.transform.localRotation = Quaternion.Euler(90f, -90f, 0f);
            canvasObj.transform.localScale = Vector3.one * 0.00008f;
        }
        else
        {
            if (tabletCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                tabletCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        tabletCanvas.transform.localRotation *= Quaternion.Euler(0f, 180f, 0f);
        tabletCanvas.renderMode = RenderMode.WorldSpace;
        tabletCanvas.worldCamera = Camera.main;
        tabletCanvas.sortingOrder = 100;

        // La pantalla instalada ya tiene su escala y orientación ajustadas al modelo.
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

        CreateImage(tabletCanvas.transform, "Fondo_Pantalla", canvasSize, Vector2.zero, new Color(0.04f, 0.05f, 0.07f, 0.98f)).raycastTarget = false;

        // 1. BARRA SUPERIOR (Reloj + Botón de Pausa)
        float headerHeight = (portrait ? 80f : 58f) * uiScale;
        float headerY = (designHeight * 0.5f - (portrait ? 40f : 29f)) * uiScale;
        GameObject barraSuperior = CreateRect("Barra_Superior", tabletCanvas.transform,
            new Vector2(designWidth * uiScale, headerHeight), new Vector2(0f, headerY));
        CreateImage(barraSuperior.transform, "Bg_Header", new Vector2(designWidth * uiScale, headerHeight),
            Vector2.zero, new Color(0.09f, 0.1f, 0.14f, 1f)).raycastTarget = false;

        float relojX = portrait ? -160f : -180f;
        float pausaX = portrait ? 190f : 300f;
        Vector2 relojSize = portrait ? new Vector2(300f, 60f) : new Vector2(360f, 48f);
        Vector2 pausaSize = portrait ? new Vector2(240f, 56f) : new Vector2(220f, 44f);
        int headerFontSize = Mathf.RoundToInt((portrait ? 34f : 24f) * uiScale);
        textoRelojHeader = CreateText(barraSuperior.transform, "Reloj_Turno", "12:00 AM",
            new Vector2(relojX * uiScale, 0f), relojSize * uiScale, headerFontSize,
            TextAnchor.MiddleLeft, colorRosaNormal);

        Button btnPausaTop = CreateButton(barraSuperior.transform, "Btn_Pausa_Header", "PAUSA [A]",
            new Vector2(pausaX * uiScale, 0f), pausaSize * uiScale,
            new Color(0.55f, 0.14f, 0.16f, 1f), Color.white,
            Mathf.RoundToInt((portrait ? 26f : 18f) * uiScale), AlternarPausaEnTablet);
        textoBotonPausaHeader = btnPausaTop.GetComponentInChildren<Text>();

        // 2. PANEL DE REPORTE DE ANOMALÍAS
        panelAnomalias = CreateRect("App_Reporte_Anomalias", tabletCanvas.transform, canvasSize, Vector2.zero);

        float roomLabelY = portrait ? 435f : 210f;
        float roomColumnSpacing = portrait ? 215f : 300f;
        float roomButtonWidth = portrait ? 200f : 270f;
        float roomButtonHeight = portrait ? 70f : 46f;
        float roomRowSpacing = portrait ? 82f : 54f;
        float roomFirstRowY = portrait ? 365f : 160f;
        int roomFontSize = Mathf.RoundToInt((portrait ? 24f : 20f) * uiScale);
        CreateText(panelAnomalias.transform, "Lbl_Cuartos", "1. SELECCIONA HABITACIÓN:",
            new Vector2(0f, roomLabelY * uiScale), new Vector2((portrait ? 640f : 860f) * uiScale, 36f * uiScale),
            Mathf.RoundToInt((portrait ? 24f : 21f) * uiScale), TextAnchor.MiddleCenter, Color.white);

        string[,] cuartos = new string[,] {
            { "Kitchen", "COCINA" },
            { "LivingRoom", "SALA" },
            { "Bedroom", "RECÁMARA" },
            { "Bathroom", "BAÑO" },
            { "Laundry", "LAVANDERÍA" },
            { "Basement", "GARAJE / SÓT." }
        };

        for (int i = 0; i < 6; i++)
        {
            int fila = i / 3;
            int col = i % 3;
            float posX = (col - 1) * roomColumnSpacing;
            float posY = roomFirstRowY - (fila * roomRowSpacing);

            string idCuarto = cuartos[i, 0];
            string nombreMostrar = cuartos[i, 1];

            Button bCuarto = CreateButton(panelAnomalias.transform, "Room_" + idCuarto, nombreMostrar,
                new Vector2(posX * uiScale, posY * uiScale),
                new Vector2(roomButtonWidth, roomButtonHeight) * uiScale,
                colorBotonOscuro, colorRosaNormal, roomFontSize,
                () => SeleccionarCuartoUI(idCuarto));

            botonesCuartos[idCuarto] = bCuarto.GetComponent<Image>();
        }

        float typeLabelY = portrait ? 210f : 42f;
        float typeColumnX = portrait ? 160f : 215f;
        float typeFirstRowY = portrait ? 145f : -1f;
        float typeRowSpacing = portrait ? 74f : 42f;
        Vector2 typeButtonSize = (portrait ? new Vector2(305f, 64f) : new Vector2(415f, 38f)) * uiScale;
        int typeFontSize = Mathf.RoundToInt((portrait ? 26f : 20f) * uiScale);
        CreateText(panelAnomalias.transform, "Lbl_Tipos", "2. TIPO DE ANOMALÍA:",
            new Vector2(0f, typeLabelY * uiScale), new Vector2((portrait ? 640f : 860f) * uiScale, 36f * uiScale),
            Mathf.RoundToInt((portrait ? 24f : 21f) * uiScale), TextAnchor.MiddleCenter, Color.white);

        string[] nombresTipos = new string[] {
            "Imagery", "Displacement", "Mimic", "Flawed", "Pure Form",
            "Preacher", "Electrical", "Tulpa", "Corpse", "Unknown"
        };

        for (int i = 0; i < 10; i++)
        {
            int indiceCopia = i;
            int col = i / 5;
            int fila = i % 5;
            float posX = (col == 0) ? -typeColumnX : typeColumnX;
            float posY = typeFirstRowY - (fila * typeRowSpacing);

            Button bTipo = CreateButton(panelAnomalias.transform, "Type_" + nombresTipos[i], nombresTipos[i].ToUpper(),
                new Vector2(posX * uiScale, posY * uiScale), typeButtonSize,
                colorBotonOscuro, colorRosaNormal, typeFontSize,
                () => SeleccionarTipoUI(indiceCopia));

            botonesTipos[indiceCopia] = bTipo.GetComponent<Image>();
        }

        float statusY = portrait ? -245f : -218f;
        float reportY = portrait ? -335f : -269f;
        Vector2 statusSize = (portrait ? new Vector2(640f, 45f) : new Vector2(850f, 32f)) * uiScale;
        Vector2 reportSize = (portrait ? new Vector2(620f, 95f) : new Vector2(700f, 46f)) * uiScale;
        textoEstadoReporte = CreateText(panelAnomalias.transform, "Txt_Estado_Reporte", "Sistema listo para reportar.",
            new Vector2(0f, statusY * uiScale), statusSize,
            Mathf.RoundToInt((portrait ? 25f : 17f) * uiScale), TextAnchor.MiddleCenter, Color.white);

        CreateButton(panelAnomalias.transform, "Btn_Enviar_Reporte", "[ REPORTAR ANOMALÍA ]",
            new Vector2(0f, reportY * uiScale), reportSize,
            new Color(0.15f, 0.42f, 0.2f, 1f), Color.white,
            Mathf.RoundToInt((portrait ? 34f : 22f) * uiScale),
            EnviarReporteAlGestor);

        // 3. PANEL DE PAUSA INTEGRADO
        panelPausaPrincipal = CreateRect("App_Menu_Pausa", tabletCanvas.transform, canvasSize, Vector2.zero);
        CreateImage(panelPausaPrincipal.transform, "Bg_Pausa",
            new Vector2((designWidth - 40f) * uiScale, (designHeight - 40f) * uiScale),
            Vector2.zero, new Color(0.02f, 0.02f, 0.03f, 0.98f)).raycastTarget = false;

        float pauseTitleY = portrait ? 320f : 205f;
        CreateText(panelPausaPrincipal.transform, "Titulo_Pausa", "JUEGO EN PAUSA",
            new Vector2(0f, pauseTitleY * uiScale), new Vector2((designWidth - 80f) * uiScale, 60f * uiScale),
            Mathf.RoundToInt((portrait ? 42f : 28f) * uiScale), TextAnchor.MiddleCenter, Color.white);

        float[] pauseButtonY = portrait
            ? new[] { 160f, 30f, -100f, -230f }
            : new[] { 110f, 30f, -50f, -130f };
        Vector2 pauseButtonSize = (portrait ? new Vector2(520f, 90f) : new Vector2(600f, 62f)) * uiScale;
        int pauseFontSize = Mathf.RoundToInt((portrait ? 30f : 21f) * uiScale);
        CreateButton(panelPausaPrincipal.transform, "Btn_Continuar", "CONTINUAR TURNO",
            new Vector2(0f, pauseButtonY[0] * uiScale), pauseButtonSize,
            new Color(0.48f, 0.12f, 0.15f, 1f), Color.white, pauseFontSize, ReanudarJuego);

        CreateButton(panelPausaPrincipal.transform, "Btn_Opciones", "OPCIONES DE AUDIO",
            new Vector2(0f, pauseButtonY[1] * uiScale), pauseButtonSize,
            colorBotonOscuro, Color.white, pauseFontSize, AbrirPanelOpciones);

        CreateButton(panelPausaPrincipal.transform, "Btn_MenuPrincipal", "VOLVER AL INICIO",
            new Vector2(0f, pauseButtonY[2] * uiScale), pauseButtonSize,
            colorBotonOscuro, Color.white, pauseFontSize, VolverAlMenuPrincipal);

        CreateButton(panelPausaPrincipal.transform, "Btn_Salir", "SALIR DEL JUEGO",
            new Vector2(0f, pauseButtonY[3] * uiScale), pauseButtonSize,
            colorBotonOscuro, Color.white, pauseFontSize, SalirDelJuego);

        // 4. PANEL DE OPCIONES DE VOLUMEN
        panelOpcionesVolumen = CreateRect("App_Opciones_Volumen", tabletCanvas.transform, canvasSize, Vector2.zero);
        CreateImage(panelOpcionesVolumen.transform, "Bg_Opciones",
            new Vector2((designWidth - 40f) * uiScale, (designHeight - 40f) * uiScale),
            Vector2.zero, new Color(0.02f, 0.02f, 0.03f, 0.98f)).raycastTarget = false;
        CreateText(panelOpcionesVolumen.transform, "Titulo_Opciones", "AJUSTE DE VOLUMEN",
            new Vector2(0f, (portrait ? 250f : 200f) * uiScale),
            new Vector2((designWidth - 80f) * uiScale, 60f * uiScale),
            Mathf.RoundToInt((portrait ? 38f : 28f) * uiScale), TextAnchor.MiddleCenter, Color.white);

        CrearSliderVolumen(panelOpcionesVolumen.transform, uiScale);

        CreateButton(panelOpcionesVolumen.transform, "Btn_VolverPausa", "VOLVER A PAUSA",
            new Vector2(0f, (portrait ? -160f : -170f) * uiScale),
            (portrait ? new Vector2(460f, 90f) : new Vector2(500f, 62f)) * uiScale,
            new Color(0.48f, 0.12f, 0.15f, 1f), Color.white,
            Mathf.RoundToInt((portrait ? 30f : 21f) * uiScale), MostrarMenuPausaPrincipal);

        panelPausaPrincipal.SetActive(false);
        panelOpcionesVolumen.SetActive(false);
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

        textoEstadoReporte.text = $"Seleccionado: {cuartoElegido} - {(TipoAnomalia)tipoElegido}";
        textoEstadoReporte.color = Color.white;
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

        textoEstadoReporte.text = $"Seleccionado: {cuartoElegido} - {(TipoAnomalia)tipoElegido}";
        textoEstadoReporte.color = Color.white;
    }

    public void AlternarPausaEnTablet()
    {
        if (isPaused) ReanudarJuego();
        else PausarJuego();
    }

    private void PausarJuego()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isPaused = true;

        panelAnomalias.SetActive(false);
        panelOpcionesVolumen.SetActive(false);
        panelPausaPrincipal.SetActive(true);

        if (textoBotonPausaHeader != null)
            textoBotonPausaHeader.text = "VOLVER [A]";
    }

    private void ReanudarJuego()
    {
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        isPaused = false;

        panelPausaPrincipal.SetActive(false);
        panelOpcionesVolumen.SetActive(false);
        panelAnomalias.SetActive(true);

        if (textoBotonPausaHeader != null)
            textoBotonPausaHeader.text = "PAUSA [A]";
    }

    private void AbrirPanelOpciones()
    {
        panelPausaPrincipal.SetActive(false);
        panelOpcionesVolumen.SetActive(true);
    }

    private void MostrarMenuPausaPrincipal()
    {
        panelOpcionesVolumen.SetActive(false);
        panelPausaPrincipal.SetActive(true);
    }

    private void VolverAlMenuPrincipal()
    {
        Time.timeScale = 1f;
        isPaused = false;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void SalirDelJuego()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }

    private void CrearSliderVolumen(Transform parent, float uiScale)
    {
        Image track = CreateImage(parent, "Barra_Volumen", new Vector2(500f, 45f) * uiScale,
            new Vector2(0f, 60f * uiScale), new Color(0.18f, 0.19f, 0.22f, 1f));
        RectTransform fillRect = CreateRect("Relleno_Volumen", track.transform, Vector2.zero, Vector2.zero).GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fill = fillRect.gameObject.AddComponent<Image>();
        fill.color = new Color(0.64f, 0.15f, 0.18f, 1f);

        Slider slider = track.gameObject.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.targetGraphic = track;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = AudioListener.volume;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.onValueChanged.AddListener((v) => AudioListener.volume = v);
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
        inputModule.enableBuiltinActionsAsFallback = true;
    }

    private GameObject CreateRect(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
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
}
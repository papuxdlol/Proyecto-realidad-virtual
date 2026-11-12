using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

public sealed class HorrorMainMenu : MonoBehaviour
{
    private const string GameplaySceneName = "cuidad";
    private static readonly string[] MenuLabels = { "Jugar", "Opciones", "Anomalías", "Créditos", "Salir" };
    private static readonly MenuSection[] MenuSections =
    {
        MenuSection.None,
        MenuSection.Settings,
        MenuSection.Anomalies,
        MenuSection.Credits,
        MenuSection.None
    };
    private static readonly string[] ClockNumbers = { "XII", "III", "VI", "IX" };

    private enum MenuSection
    {
        None,
        Settings,
        Anomalies,
        Credits
    }

    private Texture2D backgroundTexture;
    private Texture2D lineTexture;
    private Texture2D hoverTexture;
    private Texture2D buttonTexture;
    private Texture2D modalTexture;
    private Texture2D overlayTexture;
    [SerializeField] private Font menuFont;
    private bool ownsMenuFont;
    private GUIStyle titleStyle;
    private GUIStyle centeredTitleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private GUIStyle exitButtonStyle;
    private GUIStyle markerStyle;
    private GUIStyle footerStyle;
    private GUIStyle clockLabelStyle;
    private GUIStyle clockDigitalStyle;
    private GUIStyle panelTitleStyle;
    private GUIStyle panelTextStyle;
    private MenuSection openSection;
    private int stylesScreenHeight = -1;
    private int stylesScreenWidth = -1;
    private float menuOpenedAt;
    private float sectionOpenedAt;
    private Canvas vrMenuCanvas;
    private Text vrStatusText;
    private Text vrSectionTitle;
    private Text vrSectionMessage;
    private Text vrClockDigitalTime;
    private RectTransform vrHourHand;
    private RectTransform vrMinuteHand;
    private RectTransform vrSecondHand;
    private Vector2 vrClockCenter;
    private GameObject vrMainContent;
    private GameObject vrSectionContent;
    private bool reportedMissingXrCamera;
    private string lastVrClockDigitalTime;

    private void Awake()
    {
        menuOpenedAt = Time.unscaledTime;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

    }

    private void Start()
    {
        TryCreateVrMenu();
    }

    private void Update()
    {
        if (vrMenuCanvas == null && XRSettings.isDeviceActive)
        {
            TryCreateVrMenu();
        }

        if (vrClockDigitalTime != null)
        {
            UpdateVrClock();
        }
    }

    private void OnGUI()
    {
        if (XRSettings.isDeviceActive)
        {
            return;
        }

        EnsureDesktopResources();
        EnsureStyles();
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1440f, Screen.height / 900f), 0.65f, 1.1f);
        bool horizontalLayout = Screen.width >= 760 && Screen.height >= 500;

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), backgroundTexture);

        if (horizontalLayout)
        {
            DrawDesktopLayout(scale);
        }
        else
        {
            DrawCompactLayout(scale);
        }

        if (openSection != MenuSection.None)
        {
            DrawSectionPanel(scale);
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape && openSection != MenuSection.None)
        {
            openSection = MenuSection.None;
            menuOpenedAt = Time.unscaledTime;
            Event.current.Use();
        }
    }

    private void TryCreateVrMenu()
    {
        if (!XRSettings.isDeviceActive)
        {
            return;
        }

        Camera xrCamera = Camera.main;
        if (xrCamera == null)
        {
            if (!reportedMissingXrCamera)
            {
                Debug.LogError("No se encontró la cámara del XR Origin para mostrar el menú VR.", this);
                reportedMissingXrCamera = true;
            }

            return;
        }

        EnsureUiEventSystem();

        xrCamera.clearFlags = CameraClearFlags.SolidColor;
        xrCamera.backgroundColor = Color.black;

        Vector3 menuForward = xrCamera.transform.forward;
        menuForward.y = 0f;
        menuForward = menuForward.sqrMagnitude > 0.001f ? menuForward.normalized : Vector3.forward;

        GameObject canvasObject = new GameObject("Menu VR", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
        canvasObject.transform.SetPositionAndRotation(
            xrCamera.transform.position + menuForward * 2f,
            Quaternion.LookRotation(menuForward, Vector3.up));
        canvasObject.transform.localScale = Vector3.one * 0.0015f;

        RectTransform canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.sizeDelta = new Vector2(1300f, 820f);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = xrCamera;
        canvas.sortingOrder = 100;
        vrMenuCanvas = canvas;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;

        Image panel = CreateVrImage("Panel", canvasRect, canvasRect.sizeDelta, Vector2.zero, new Color(0.008f, 0.008f, 0.012f));
        panel.raycastTarget = false;

        vrMainContent = new GameObject("Contenido principal", typeof(RectTransform));
        vrMainContent.transform.SetParent(canvasRect, false);
        RectTransform mainRect = (RectTransform)vrMainContent.transform;
        mainRect.anchorMin = Vector2.zero;
        mainRect.anchorMax = Vector2.one;
        mainRect.offsetMin = Vector2.zero;
        mainRect.offsetMax = Vector2.zero;

        CreateVrText("Title", mainRect, "ANOMALY HOUSE", new Vector2(-330f, 305f), new Vector2(600f, 74f), 50, TextAnchor.MiddleCenter, FontStyle.Bold);
        CreateVrImage("Acento superior", mainRect, new Vector2(300f, 2f), new Vector2(-330f, 258f), new Color(0.64f, 0.15f, 0.18f, 0.9f)).raycastTarget = false;
        CreateVrText("Subtitle", mainRect, "Una casa. Una señal que no debería existir.", new Vector2(-330f, 220f), new Vector2(600f, 38f), 20, TextAnchor.MiddleCenter, FontStyle.Normal);

        float[] buttonPositions = { 125f, 30f, -65f, -160f, -255f };
        for (int i = 0; i < MenuLabels.Length; i++)
        {
            int menuIndex = i;
            CreateVrButton(MenuLabels[i], mainRect, new Vector2(-330f, buttonPositions[i]), () =>
            {
                ActivateMenuItem(menuIndex, MenuSections[menuIndex]);
            }, false, 560f);
        }

        vrStatusText = CreateVrText(
            "Status",
            mainRect,
            "Apunta con un mando y presiona el gatillo para elegir.",
            new Vector2(-330f, -355f),
            new Vector2(600f, 30f),
            16,
            TextAnchor.MiddleCenter,
            FontStyle.Normal);
        vrStatusText.color = new Color(0.73f, 0.73f, 0.75f);

        CreateVrClock(mainRect, new Vector2(340f, 0f), 165f);
        CreateVrSectionPanel(canvasRect, canvasRect.sizeDelta);
    }

    private void EnsureUiEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem VR", typeof(EventSystem), typeof(XRUIInputModule));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        XRUIInputModule inputModule = eventSystem.GetComponent<XRUIInputModule>();
        if (inputModule == null)
        {
            foreach (BaseInputModule existingModule in eventSystem.GetComponents<BaseInputModule>())
            {
                existingModule.enabled = false;
            }

            inputModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();
        }

        inputModule.enableXRInput = true;
        inputModule.enableBuiltinActionsAsFallback = true;
    }

    private void CreateVrClock(Transform parent, Vector2 center, float radius)
    {
        vrClockCenter = center;
        CreateVrText("Etiqueta del reloj", parent, "HORA DEL DISPOSITIVO", center + new Vector2(0f, radius + 58f), new Vector2(380f, 34f), 20, TextAnchor.MiddleCenter, FontStyle.Bold)
            .color = new Color(0.73f, 0.73f, 0.75f);

        for (int i = 0; i < 12; i++)
        {
            float angle = Mathf.PI * 2f * i / 12f;
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            Image marker = CreateVrImage(
                "Marca del reloj " + i,
                parent,
                new Vector2(i % 3 == 0 ? 5f : 3f, i % 3 == 0 ? 18f : 10f),
                center + direction * radius * 0.9f,
                new Color(0.78f, 0.79f, 0.82f, 0.8f));
            marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
            marker.raycastTarget = false;
        }

        for (int i = 0; i < ClockNumbers.Length; i++)
        {
            float angle = Mathf.PI * 2f * i / ClockNumbers.Length;
            Vector2 numberPosition = center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius * 0.69f;
            CreateVrText(
                "Número del reloj " + ClockNumbers[i],
                parent,
                ClockNumbers[i],
                numberPosition,
                new Vector2(72f, 42f),
                26,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
        }

        vrHourHand = CreateVrClockHand(parent, "Manecilla de horas", center, radius * 0.42f, 7f, new Color(0.89f, 0.89f, 0.91f));
        vrMinuteHand = CreateVrClockHand(parent, "Manecilla de minutos", center, radius * 0.63f, 5f, new Color(0.89f, 0.89f, 0.91f));
        vrSecondHand = CreateVrClockHand(parent, "Manecilla de segundos", center, radius * 0.76f, 2f, new Color(0.78f, 0.27f, 0.3f));
        Image centerPoint = CreateVrImage("Centro del reloj", parent, new Vector2(12f, 12f), center, new Color(0.78f, 0.27f, 0.3f));
        centerPoint.raycastTarget = false;

        vrClockDigitalTime = CreateVrText(
            "Hora digital",
            parent,
            string.Empty,
            center + new Vector2(0f, -radius - 48f),
            new Vector2(300f, 42f),
            28,
            TextAnchor.MiddleCenter,
            FontStyle.Normal);
        vrClockDigitalTime.color = new Color(0.78f, 0.79f, 0.82f);
        UpdateVrClock();
    }

    private RectTransform CreateVrClockHand(Transform parent, string objectName, Vector2 center, float length, float thickness, Color color)
    {
        Image hand = CreateVrImage(objectName, parent, new Vector2(thickness, length), center, color);
        hand.raycastTarget = false;
        return hand.rectTransform;
    }

    private void UpdateVrClock()
    {
        DateTime localTime = DateTime.Now;
        float seconds = localTime.Second + localTime.Millisecond / 1000f;
        float minutes = localTime.Minute + seconds / 60f;
        float hours = localTime.Hour % 12 + minutes / 60f;

        SetVrClockHand(vrHourHand, vrClockCenter, hours, 12f);
        SetVrClockHand(vrMinuteHand, vrClockCenter, minutes, 60f);
        SetVrClockHand(vrSecondHand, vrClockCenter, seconds, 60f);

        string digitalTime = localTime.ToString("HH:mm:ss");
        if (digitalTime != lastVrClockDigitalTime)
        {
            vrClockDigitalTime.text = digitalTime;
            lastVrClockDigitalTime = digitalTime;
        }
    }

    private static void SetVrClockHand(RectTransform hand, Vector2 center, float timeUnits, float cycleLength)
    {
        float angle = Mathf.PI * 2f * timeUnits / cycleLength;
        float handLength = hand.sizeDelta.y;
        hand.anchoredPosition = new Vector2(
            center.x + Mathf.Sin(angle) * handLength * 0.5f,
            center.y + Mathf.Cos(angle) * handLength * 0.5f);
        hand.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
    }

    private void CreateVrSectionPanel(Transform parent, Vector2 canvasSize)
    {
        vrSectionContent = new GameObject("Panel de sección", typeof(RectTransform));
        vrSectionContent.transform.SetParent(parent, false);
        RectTransform sectionRect = (RectTransform)vrSectionContent.transform;
        sectionRect.anchorMin = Vector2.zero;
        sectionRect.anchorMax = Vector2.one;
        sectionRect.offsetMin = Vector2.zero;
        sectionRect.offsetMax = Vector2.zero;

        Image overlay = CreateVrImage("Oscurecimiento", sectionRect, canvasSize, Vector2.zero, new Color(0f, 0f, 0f, 0.72f));
        overlay.raycastTarget = false;
        Image card = CreateVrImage("Tarjeta de sección", sectionRect, new Vector2(760f, 340f), Vector2.zero, new Color(0.018f, 0.018f, 0.022f, 0.98f));
        card.raycastTarget = false;
        CreateVrImage("Línea de sección", sectionRect, new Vector2(700f, 2f), new Vector2(0f, 125f), new Color(0.64f, 0.15f, 0.18f, 0.9f)).raycastTarget = false;
        vrSectionTitle = CreateVrText("Título de sección", sectionRect, string.Empty, new Vector2(0f, 78f), new Vector2(650f, 56f), 36, TextAnchor.MiddleCenter, FontStyle.Bold);
        vrSectionMessage = CreateVrText("Texto de sección", sectionRect, string.Empty, new Vector2(0f, -5f), new Vector2(650f, 86f), 24, TextAnchor.MiddleCenter, FontStyle.Normal);
        CreateVrButton("VOLVER", sectionRect, new Vector2(0f, -112f), CloseVrSection, true);
        vrSectionContent.SetActive(false);
    }

    private void CloseVrSection()
    {
        openSection = MenuSection.None;
        vrSectionContent.SetActive(false);
        vrMainContent.SetActive(true);
        vrStatusText.text = "Apunta con un mando y presiona el gatillo para elegir.";
    }

    private Image CreateVrImage(string objectName, Transform parent, Vector2 size, Vector2 position, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateVrText(string objectName, Transform parent, string content, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, FontStyle style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.text = content;
        text.font = GetMenuFont(fontSize);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = new Color(0.89f, 0.89f, 0.91f);
        text.raycastTarget = false;
        return text;
    }

    private void CreateVrButton(string label, Transform parent, Vector2 position, UnityEngine.Events.UnityAction onClick, bool centered = false, float buttonWidth = 700f)
    {
        Vector2 buttonSize = new Vector2(buttonWidth, 78f);
        Image image = CreateVrImage("Button " + label, parent, buttonSize, position, Color.clear);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = new Color(0.64f, 0.15f, 0.18f, 0.3f);
        colors.pressedColor = new Color(0.82f, 0.28f, 0.3f, 0.42f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        CreateVrText(
            "Label " + label,
            image.transform,
            label.ToUpperInvariant(),
            centered ? Vector2.zero : new Vector2(28f, 0f),
            new Vector2(centered ? buttonWidth - 20f : buttonWidth - 60f, 72f),
            32,
            centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft,
            FontStyle.Bold);
    }

    private void DrawDesktopLayout(float scale)
    {
        float left = Mathf.Max(70f * scale, Screen.width * 0.075f);
        float titleTop = Mathf.Max(38f * scale, Screen.height * 0.065f);

        DrawClock(new Vector2(Screen.width * 0.78f, Screen.height * 0.39f), Mathf.Min(202f * scale, Screen.height * 0.33f));
        DrawBrand(new Rect(left, titleTop, Screen.width * 0.72f, 76f * scale), scale);
        GUI.Label(new Rect(left + 32f * scale, titleTop + 72f * scale, 475f * scale, 52f * scale),
            "Una casa. Una señal que no debería existir.", subtitleStyle);
        DrawMenuItems(new Vector2(left + 42f * scale, Screen.height * 0.47f), 48f * scale, 7f * scale, scale);
        DrawDisclaimer(scale);
    }

    private void DrawCompactLayout(float scale)
    {
        float left = 28f * scale;
        float titleTop = 18f * scale;

        DrawClock(new Vector2(Screen.width * 0.77f, Screen.height * 0.45f), Mathf.Min(100f * scale, Screen.width * 0.24f));
        DrawBrand(new Rect(left, titleTop, Screen.width - left * 2f, 42f * scale), scale, true);
        GUI.Label(new Rect(left + 8f * scale, titleTop + 43f * scale, Screen.width - left * 2f, 27f * scale),
            "Una casa. Una señal desconocida.", headerStyle);
        DrawMenuItems(new Vector2(left + 8f * scale, Screen.height * 0.405f), 38f * scale, 3f * scale, scale, true);
        DrawDisclaimer(scale);
    }

    private void DrawBrand(Rect bounds, float scale, bool centered = false)
    {
        float progress = GetIntroProgress();
        Color previousColor = GUI.color;
        GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * progress);
        float offset = (1f - progress) * 10f * scale;
        GUI.Label(new Rect(bounds.x, bounds.y + offset, bounds.width, bounds.height), "ANOMALY HOUSE", centered ? centeredTitleStyle : titleStyle);
        GUI.color = previousColor;

        if (!centered)
        {
            GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * progress * 0.62f);
            GUI.DrawTexture(new Rect(bounds.x, bounds.y + bounds.height + 5f * scale, 174f * scale, 1f * scale), lineTexture);
        }

        GUI.color = previousColor;
    }

    private void DrawMenuItems(Vector2 origin, float height, float spacing, float scale, bool compact = false)
    {
        Color previousColor = GUI.color;

        for (int i = 0; i < MenuLabels.Length; i++)
        {
            float delay = i * 0.09f;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - menuOpenedAt - delay) / 0.3f));
            Rect rect = new Rect(origin.x, origin.y + i * (height + spacing) + (1f - progress) * 8f * scale, compact ? Screen.width * 0.55f : 400f * scale, height);
            GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * progress);
            bool hovered = rect.Contains(Event.current.mousePosition);
            GUIStyle style = i == MenuLabels.Length - 1 ? exitButtonStyle : buttonStyle;

            if (hovered)
            {
                GUI.DrawTexture(rect, hoverTexture);
                GUI.Label(new Rect(rect.x, rect.y, 24f * scale, rect.height), ">", markerStyle);
            }

            bool clicked = GUI.Button(rect, MenuLabels[i], style);
            if (clicked)
            {
                ActivateMenuItem(i, MenuSections[i]);
            }
        }

        GUI.color = previousColor;
    }

    private void ActivateMenuItem(int index, MenuSection section)
    {
        if (index == 0)
        {
            SceneManager.LoadScene(GameplaySceneName);
        }
        else if (index == 4)
        {
            Application.Quit();
        }
        else
        {
            openSection = section;
            sectionOpenedAt = Time.unscaledTime;

            if (vrStatusText != null)
            {
                switch (section)
                {
                    case MenuSection.Settings:
                        vrStatusText.text = "Los ajustes de sonido estarán disponibles pronto.";
                        break;
                    case MenuSection.Anomalies:
                        vrStatusText.text = "El archivo sigue incompleto. Vuelve cuando haya más registros.";
                        break;
                    case MenuSection.Credits:
                        vrStatusText.text = "ANOMALY HOUSE - Una experiencia de terror.";
                        break;
                }
            }

            if (vrSectionContent != null)
            {
                vrMainContent.SetActive(false);
                vrSectionContent.SetActive(true);

                switch (section)
                {
                    case MenuSection.Settings:
                        vrSectionTitle.text = "OPCIONES";
                        vrSectionMessage.text = "Los ajustes de sonido estarán disponibles pronto.";
                        break;
                    case MenuSection.Anomalies:
                        vrSectionTitle.text = "ANOMALÍAS";
                        vrSectionMessage.text = "El archivo sigue incompleto. Vuelve cuando haya más registros.";
                        break;
                    case MenuSection.Credits:
                        vrSectionTitle.text = "CRÉDITOS";
                        vrSectionMessage.text = "ANOMALY HOUSE\nUna experiencia de terror.";
                        break;
                }
            }
        }
    }

    private void DrawClock(Vector2 center, float radius)
    {
        Color previousColor = GUI.color;
        float fade = GetIntroProgress() * 0.42f;
        DateTime localTime = DateTime.Now;
        float seconds = localTime.Second + localTime.Millisecond / 1000f;
        float minutes = localTime.Minute + seconds / 60f;
        float hours = localTime.Hour % 12 + minutes / 60f;
        GUI.color = new Color(0.78f, 0.79f, 0.82f, fade);

        const int segments = 48;
        for (int i = 0; i < segments; i++)
        {
            float angleA = Mathf.PI * 2f * i / segments;
            float angleB = Mathf.PI * 2f * (i + 1) / segments;
            Vector2 a = center + new Vector2(Mathf.Cos(angleA), Mathf.Sin(angleA)) * radius;
            Vector2 b = center + new Vector2(Mathf.Cos(angleB), Mathf.Sin(angleB)) * radius;
            DrawLine(a, b, Mathf.Max(1f, radius * 0.008f));
        }

        for (int i = 0; i < 12; i++)
        {
            float angle = Mathf.PI * 2f * i / 12f - Mathf.PI * 0.5f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            DrawLine(center + direction * radius * 0.86f, center + direction * radius * 0.96f, Mathf.Max(1f, radius * 0.018f));
        }

        GUI.color = new Color(0.86f, 0.86f, 0.88f, fade);
        for (int i = 0; i < ClockNumbers.Length; i++)
        {
            float angle = Mathf.PI * 2f * i / ClockNumbers.Length - Mathf.PI * 0.5f;
            Vector2 numberPosition = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.72f;
            GUI.Label(
                new Rect(numberPosition.x - radius * 0.16f, numberPosition.y - radius * 0.07f, radius * 0.32f, radius * 0.14f),
                ClockNumbers[i],
                clockLabelStyle);
        }

        GUI.color = new Color(0.89f, 0.89f, 0.91f, fade);
        DrawLine(center, GetClockHandEndpoint(center, radius * 0.46f, hours, 12f), Mathf.Max(2f, radius * 0.027f));
        DrawLine(center, GetClockHandEndpoint(center, radius * 0.69f, minutes, 60f), Mathf.Max(1f, radius * 0.017f));
        GUI.color = new Color(0.78f, 0.27f, 0.3f, fade * 0.9f);
        DrawLine(center, GetClockHandEndpoint(center, radius * 0.81f, seconds, 60f), Mathf.Max(1f, radius * 0.009f));
        GUI.DrawTexture(new Rect(center.x - 2f, center.y - 2f, 4f, 4f), lineTexture);

        GUI.color = new Color(0.86f, 0.86f, 0.88f, fade);
        float digitalWidth = radius * 1.5f;
        GUI.Label(
            new Rect(center.x - digitalWidth * 0.5f, center.y + radius * 1.08f, digitalWidth, radius * 0.2f),
            localTime.ToString("HH:mm:ss"),
            clockDigitalStyle);
        GUI.color = previousColor;
    }

    private static Vector2 GetClockHandEndpoint(Vector2 center, float handLength, float timeUnits, float cycleLength)
    {
        float angle = Mathf.PI * 2f * timeUnits / cycleLength;
        return center + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * handLength;
    }

    private void DrawLine(Vector2 start, Vector2 end, float thickness)
    {
        Vector2 direction = end - start;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - thickness * 0.5f, direction.magnitude, thickness), lineTexture);
        GUI.matrix = previousMatrix;
    }

    private void DrawDisclaimer(float scale)
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * 0.62f);
        float width = Mathf.Min(440f * scale, Screen.width - 32f * scale);
        float x = Mathf.Max(20f * scale, Screen.width - width - 36f * scale);
        float y = Screen.height - 49f * scale;
        GUI.Label(new Rect(x, y, width, 18f * scale), "AVISO: EL REGISTRO PUEDE NO SER FIABLE.", footerStyle);
        GUI.Label(new Rect(x, y + 18f * scale, width, 18f * scale), "ALGUNAS PRESENCIAS NO APARECEN EN CÁMARA.", footerStyle);
        GUI.color = previousColor;
    }

    private void DrawSectionPanel(float scale)
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - sectionOpenedAt) / 0.2f)));
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), overlayTexture);

        float width = Mathf.Min(530f * scale, Screen.width - 36f * scale);
        float height = 208f * scale;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.DrawTexture(panel, modalTexture);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 1f * scale), lineTexture);

        string heading;
        string message;
        switch (openSection)
        {
            case MenuSection.Settings:
                heading = "OPCIONES";
                message = "Los ajustes de sonido estarán disponibles pronto.";
                break;
            case MenuSection.Anomalies:
                heading = "ANOMALÍAS";
                message = "El archivo sigue incompleto. Vuelve cuando haya más registros.";
                break;
            default:
                heading = "CRÉDITOS";
                message = "ANOMALY HOUSE\nUna experiencia de terror.";
                break;
        }

        GUI.Label(new Rect(panel.x + 22f * scale, panel.y + 25f * scale, panel.width - 44f * scale, 38f * scale), heading, panelTitleStyle);
        GUI.Label(new Rect(panel.x + 22f * scale, panel.y + 76f * scale, panel.width - 44f * scale, 64f * scale), message, panelTextStyle);

        float backWidth = 160f * scale;
        if (GUI.Button(new Rect(panel.x + 22f * scale, panel.y + panel.height - 54f * scale, backWidth, 34f * scale), "VOLVER", buttonStyle))
        {
            openSection = MenuSection.None;
            menuOpenedAt = Time.unscaledTime;
        }

        GUI.color = previousColor;
    }

    private float GetIntroProgress()
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - menuOpenedAt) / 0.48f));
    }

    private void EnsureStyles()
    {
        if (titleStyle != null && stylesScreenHeight == Screen.height && stylesScreenWidth == Screen.width)
        {
            return;
        }

        stylesScreenHeight = Screen.height;
        stylesScreenWidth = Screen.width;
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1440f, Screen.height / 900f), 0.65f, 1.1f);
        GetMenuFont(Mathf.RoundToInt(28f * scale));

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            font = menuFont,
            fontSize = Mathf.RoundToInt(49f * scale),
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.92f, 0.92f, 0.92f) }
        };
        centeredTitleStyle = new GUIStyle(titleStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(32f * scale)
        };
        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            font = menuFont,
            fontSize = Mathf.RoundToInt(16f * scale),
            normal = { textColor = new Color(0.81f, 0.81f, 0.81f) }
        };
        headerStyle = new GUIStyle(subtitleStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(11f * scale),
            normal = { textColor = new Color(0.65f, 0.65f, 0.67f) }
        };
        buttonStyle = CreateButtonStyle(buttonTexture, new Color(0.89f, 0.89f, 0.89f), Mathf.RoundToInt(29f * scale));
        exitButtonStyle = CreateButtonStyle(buttonTexture, new Color(0.79f, 0.29f, 0.31f), Mathf.RoundToInt(29f * scale));
        markerStyle = new GUIStyle(buttonStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.73f, 0.14f, 0.18f) },
            hover = { textColor = new Color(0.73f, 0.14f, 0.18f) }
        };
        footerStyle = new GUIStyle(subtitleStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.RoundToInt(10f * scale),
            normal = { textColor = new Color(0.65f, 0.65f, 0.67f) }
        };
        clockLabelStyle = new GUIStyle(subtitleStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(13f * scale),
            normal = { textColor = Color.white }
        };
        clockDigitalStyle = new GUIStyle(clockLabelStyle)
        {
            fontSize = Mathf.RoundToInt(16f * scale),
            normal = { textColor = new Color(0.78f, 0.79f, 0.82f) }
        };
        panelTitleStyle = new GUIStyle(titleStyle)
        {
            fontSize = Mathf.RoundToInt(25f * scale),
            normal = { textColor = new Color(0.92f, 0.92f, 0.92f) }
        };
        panelTextStyle = new GUIStyle(subtitleStyle)
        {
            fontSize = Mathf.RoundToInt(14f * scale),
            wordWrap = true,
            normal = { textColor = new Color(0.73f, 0.73f, 0.75f) }
        };
    }

    private Font GetMenuFont(int fontSize)
    {
        if (menuFont == null)
        {
            menuFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Arial" }, fontSize);
            ownsMenuFont = true;
        }

        return menuFont;
    }

    private void EnsureDesktopResources()
    {
        if (backgroundTexture != null)
        {
            return;
        }

        backgroundTexture = CreateBackgroundTexture();
        lineTexture = CreateTexture(Color.white);
        hoverTexture = CreateTexture(new Color(0.75f, 0.76f, 0.79f, 0.08f));
        buttonTexture = CreateTexture(new Color(0f, 0f, 0f, 0f));
        modalTexture = CreateTexture(new Color(0.008f, 0.008f, 0.01f, 1f));
        overlayTexture = CreateTexture(new Color(0f, 0f, 0f, 0.9f));
    }

    private GUIStyle CreateButtonStyle(Texture2D background, Color textColor, int fontSize)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            font = menuFont,
            fontSize = fontSize,
            fontStyle = FontStyle.Normal,
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(Mathf.RoundToInt(26f * Mathf.Clamp(Mathf.Min(Screen.width / 1440f, Screen.height / 900f), 0.65f, 1.1f)), 4, 0, 0),
            normal = { background = background, textColor = textColor },
            hover = { background = background, textColor = textColor },
            active = { background = background, textColor = textColor },
            focused = { background = background, textColor = textColor }
        };
        return style;
    }

    private static Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point
        };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateBackgroundTexture()
    {
        const int width = 256;
        const int height = 144;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);
                float v = (float)y / (height - 1);
                float distanceFromCenter = Mathf.Sqrt(Mathf.Pow((u - 0.5f) * 1.05f, 2f) + Mathf.Pow((v - 0.52f) * 0.92f, 2f));
                float vignette = Mathf.Clamp01(1f - distanceFromCenter * 1.45f);
                float scanline = (y % 3 == 0) ? 0.0015f : 0f;
                float grain = Mathf.Sin((x + 1) * 12.9898f + (y + 1) * 78.233f) * 0.0015f;
                float shade = 0.003f + vignette * 0.006f + scanline + grain;
                Color color = new Color(shade, shade, shade * 1.04f, 1f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        DestroyTexture(backgroundTexture);
        DestroyTexture(lineTexture);
        DestroyTexture(hoverTexture);
        DestroyTexture(buttonTexture);
        DestroyTexture(modalTexture);
        DestroyTexture(overlayTexture);
        if (ownsMenuFont && menuFont != null)
        {
            Destroy(menuFont);
        }
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture != null)
        {
            Destroy(texture);
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

public sealed class VRPauseMenu : MonoBehaviour
{
    private const string MainMenuSceneName = "MenuPrincipal";

    private Canvas menuCanvas;
    private GameObject pauseMenuRoot;
    private GameObject mainMenuPanel;
    private GameObject optionsPanel;
    private Camera xrCamera;
    private float previousTimeScale = 1f;
    private bool isMenuOpen;
    private bool aButtonWasPressed;
    private bool reportedMissingCamera;
    private InputDevice rightController;

    private void Awake()
    {
        EnsureUiEventSystem();
    }

    private void Start()
    {
        TryCreateMenu();
    }

    private void Update()
    {
        if (menuCanvas == null)
        {
            TryCreateMenu();
        }

        if (!rightController.isValid)
        {
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        if (rightController.isValid &&
            rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool aButtonPressed))
        {
            if (aButtonPressed && !aButtonWasPressed)
            {
                ToggleMenu();
            }

            aButtonWasPressed = aButtonPressed;
        }
        else
        {
            aButtonWasPressed = false;
        }
    }

    private void OnDestroy()
    {
        if (isMenuOpen)
        {
            Time.timeScale = previousTimeScale;
        }
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

    private void TryCreateMenu()
    {
        Camera xrCamera = Camera.main;
        if (xrCamera == null)
        {
            if (!reportedMissingCamera)
            {
                Debug.LogError("No se encontró la cámara del XR Origin para mostrar el menú de pausa.", this);
                reportedMissingCamera = true;
            }

            return;
        }

        this.xrCamera = xrCamera;
        GameObject canvasObject = new GameObject(
            "Menu de pausa VR",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster));
        canvasObject.transform.position = xrCamera.transform.position;
        canvasObject.transform.localScale = Vector3.one * 0.0015f;

        RectTransform canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.sizeDelta = new Vector2(1000f, 760f);

        menuCanvas = canvasObject.GetComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.WorldSpace;
        menuCanvas.worldCamera = xrCamera;
        menuCanvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;

        pauseMenuRoot = CreateRectObject("Panel de pausa", canvasRect);
        pauseMenuRoot.GetComponent<RectTransform>().sizeDelta = canvasRect.sizeDelta;
        CreateVrImage(pauseMenuRoot.transform, "Oscurecimiento", canvasRect.sizeDelta, Vector2.zero, new Color(0f, 0f, 0f, 0.62f)).raycastTarget = false;

        mainMenuPanel = CreatePanel(pauseMenuRoot.transform, "Opciones principales", "PAUSA");
        CreateVrButton(mainMenuPanel.transform, "Continuar", "CONTINUAR", new Vector2(0f, 122f), new Vector2(580f, 76f), ResumeGame, true);
        CreateVrButton(mainMenuPanel.transform, "Inicio", "VOLVER AL INICIO", new Vector2(0f, 28f), new Vector2(580f, 76f), ReturnToMainMenu);
        CreateVrButton(mainMenuPanel.transform, "Abrir opciones", "OPCIONES", new Vector2(0f, -66f), new Vector2(580f, 76f), ShowOptions);
        CreateVrButton(mainMenuPanel.transform, "Salir", "SALIR DEL JUEGO", new Vector2(0f, -160f), new Vector2(580f, 76f), QuitGame);
        optionsPanel = CreatePanel(pauseMenuRoot.transform, "Panel de opciones", "OPCIONES");
        CreateVrText(optionsPanel.transform, "Etiqueta de volumen", "VOLUMEN", new Vector2(0f, 74f), new Vector2(500f, 44f), 24, TextAnchor.MiddleCenter);
        CreateVolumeSlider(optionsPanel.transform);
        CreateVrButton(optionsPanel.transform, "Volver", "VOLVER", new Vector2(0f, -150f), new Vector2(420f, 76f), ShowMainMenu);
        optionsPanel.SetActive(false);
        pauseMenuRoot.SetActive(false);
    }

    private GameObject CreatePanel(Transform parent, string objectName, string title)
    {
        GameObject panel = CreateRectObject(objectName, parent);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(740f, 620f);
        CreateVrImage(panel.transform, "Fondo", new Vector2(740f, 620f), Vector2.zero, new Color(0.012f, 0.014f, 0.018f, 0.98f)).raycastTarget = false;
        CreateVrImage(panel.transform, "Linea de acento", new Vector2(620f, 2f), new Vector2(0f, 245f), new Color(0.64f, 0.15f, 0.18f, 0.95f)).raycastTarget = false;
        CreateVrText(panel.transform, "Titulo", title, new Vector2(0f, 270f), new Vector2(620f, 64f), 38, TextAnchor.MiddleCenter, FontStyle.Bold);
        return panel;
    }

    private void CreateVolumeSlider(Transform parent)
    {
        Image track = CreateVrImage(parent, "Barra de volumen", new Vector2(480f, 28f), new Vector2(0f, 20f), new Color(0.18f, 0.19f, 0.21f, 1f));
        RectTransform fillRect = CreateRectObject("Relleno de volumen", track.transform).GetComponent<RectTransform>();
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
        slider.onValueChanged.AddListener(SetVolume);
    }

    private void SetVolume(float volume)
    {
        AudioListener.volume = volume;
    }

    private void ToggleMenu()
    {
        if (pauseMenuRoot == null)
        {
            return;
        }

        if (isMenuOpen)
        {
            ResumeGame();
            return;
        }

        previousTimeScale = Time.timeScale;
        PositionMenuInFrontOfCamera();
        Time.timeScale = 0f;
        isMenuOpen = true;
        ShowMainMenu();
        pauseMenuRoot.SetActive(true);
    }

    private void ResumeGame()
    {
        if (isMenuOpen)
        {
            Time.timeScale = previousTimeScale;
        }

        isMenuOpen = false;
        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(false);
        }
    }

    private void ShowOptions()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    private void ShowMainMenu()
    {
        optionsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    private void ReturnToMainMenu()
    {
        ResumeGame();
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void QuitGame()
    {
        ResumeGame();
        Application.Quit();
    }

    private void PositionMenuInFrontOfCamera()
    {
        if (xrCamera == null)
        {
            return;
        }

        Vector3 menuForward = xrCamera.transform.forward;
        menuForward.y = 0f;
        menuForward = menuForward.sqrMagnitude > 0.001f ? menuForward.normalized : Vector3.forward;
        menuCanvas.transform.SetPositionAndRotation(
            xrCamera.transform.position + menuForward * 1.6f,
            Quaternion.LookRotation(menuForward, Vector3.up));
    }

    private GameObject CreateRectObject(string objectName, Transform parent)
    {
        GameObject newObject = new GameObject(objectName, typeof(RectTransform));
        newObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)newObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return newObject;
    }

    private Image CreateVrImage(Transform parent, string objectName, Vector2 size, Vector2 position, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateVrText(Transform parent, string objectName, string content, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, FontStyle style = FontStyle.Normal)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = new Color(0.9f, 0.9f, 0.91f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private void CreateVrButton(Transform parent, string objectName, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick, bool primary = false)
    {
        Image image = CreateVrImage(
            parent,
            objectName,
            size,
            position,
            primary ? new Color(0.48f, 0.12f, 0.15f, 1f) : new Color(0.12f, 0.15f, 0.18f, 1f));
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.64f, 0.19f, 0.21f, 1f);
        colors.pressedColor = new Color(0.82f, 0.28f, 0.3f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        CreateVrText(image.transform, "Texto", label, Vector2.zero, size, 27, TextAnchor.MiddleCenter, FontStyle.Bold);
    }
}

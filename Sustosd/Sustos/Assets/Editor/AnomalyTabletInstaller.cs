using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class AnomalyTabletInstaller
{
    private const string TabletModelPath = "Assets/Model/Props/ipad/source/Ipad.fbx.fbx";
    private const string TabletName = "Tablet de anomalías";
    private const string InstallRequestFileName = "InstallAnomalyTablet.request";

    static AnomalyTabletInstaller()
    {
        EditorApplication.delayCall += ProcessInstallRequest;
    }

    private static void ProcessInstallRequest()
    {
        string requestPath = Path.Combine(Application.dataPath, "Editor", InstallRequestFileName);
        if (!File.Exists(requestPath))
            return;

        File.Delete(requestPath);
        string metaPath = requestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        if (SceneManager.GetActiveScene().name == "cuidad")
            Install();
        else
            Debug.LogWarning("La solicitud de instalación de la tablet se ignoró porque la escena cuidad no está abierta.");
    }

    [MenuItem("GameObject/Anomaly Tablet/Install in Current Scene", false, 50)]
    private static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "cuidad")
        {
            EditorUtility.DisplayDialog("Tablet de anomalías", "Abre la escena cuidad antes de instalar la tablet.", "Aceptar");
            return;
        }

        GameObject setup = GameObject.Find("Complete XR Origin Set Up Variant");
        Transform xrOrigin = setup == null ? null : FindChild(setup.transform, "XR Origin (XR Rig)");
        if (xrOrigin == null)
        {
            EditorUtility.DisplayDialog("Tablet de anomalías", "No encontré el XR Origin en la escena cuidad.", "Aceptar");
            return;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(TabletModelPath);
        if (model == null)
        {
            EditorUtility.DisplayDialog("Tablet de anomalías", "No encontré el modelo de iPad en " + TabletModelPath, "Aceptar");
            return;
        }

        Type socketType = FindToolkitType("UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor");
        Type grabType = FindToolkitType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable");
        Type rayType = FindToolkitType("UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor");
        Type trackedRaycasterType = FindToolkitType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster");
        if (socketType == null || grabType == null || trackedRaycasterType == null)
        {
            Debug.LogError("No se pudieron encontrar los componentes necesarios de XR Interaction Toolkit.");
            return;
        }

        GameObject holster = FindChild(xrOrigin, "Funda de tablet")?.gameObject;
        if (holster == null)
        {
            holster = new GameObject("Funda de tablet");
            Undo.RegisterCreatedObjectUndo(holster, "Instalar funda de tablet");
            holster.transform.SetParent(xrOrigin, false);
            holster.transform.localPosition = new Vector3(-0.22f, 0.88f, 0.12f);
            holster.transform.localRotation = Quaternion.identity;
        }

        SphereCollider holsterCollider = EnsureComponent<SphereCollider>(holster);
        holsterCollider.isTrigger = true;
        holsterCollider.radius = 0.18f;
        EnsureComponent(holster, socketType);

        GameObject tablet = GameObject.Find(TabletName);
        bool createdTablet = tablet == null;
        if (createdTablet)
        {
            tablet = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
            if (tablet == null)
            {
                Debug.LogError("No se pudo crear una instancia del modelo de iPad.");
                return;
            }

            Undo.RegisterCreatedObjectUndo(tablet, "Instalar tablet de anomalías");
            tablet.name = TabletName;
            tablet.transform.localScale = Vector3.one;
            Bounds initialBounds = GetLocalBounds(tablet);
            float initialLargestSide = Mathf.Max(initialBounds.size.x, initialBounds.size.y, initialBounds.size.z);
            if (initialLargestSide <= 0f)
            {
                Undo.DestroyObjectImmediate(tablet);
                Debug.LogError("El modelo de iPad no contiene mallas visibles.");
                return;
            }

            tablet.transform.localScale = Vector3.one * (0.28f / initialLargestSide);
        }

        tablet.transform.position = holster.transform.position;
        tablet.transform.rotation = holster.transform.rotation;

        Bounds modelBounds = GetLocalBounds(tablet);
        float largestSide = Mathf.Max(modelBounds.size.x, modelBounds.size.y, modelBounds.size.z);
        if (largestSide <= 0f)
        {
            Debug.LogError("El modelo de iPad no contiene mallas visibles.");
            return;
        }

        BoxCollider tabletCollider = EnsureComponent<BoxCollider>(tablet);
        tabletCollider.center = modelBounds.center;
        tabletCollider.size = modelBounds.size;

        Rigidbody body = EnsureComponent<Rigidbody>(tablet);
        body.useGravity = false;
        body.isKinematic = true;

        EnsureComponent(tablet, grabType);
        SetupToggleGrab(xrOrigin);
        SetupRightHandUiRay(xrOrigin, rayType);

        Transform existingScreen = FindChild(tablet.transform, "Pantalla de reportes");
        if (existingScreen != null)
            Undo.DestroyObjectImmediate(existingScreen.gameObject);

        CreateTabletScreen(tablet.transform, modelBounds, trackedRaycasterType);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = tablet;
        Debug.Log("Tablet instalada: acércate a la funda y usa el control izquierdo para tomarla. Usa el rayo derecho para reportar.");
    }

    private static void SetupToggleGrab(Transform xrOrigin)
    {
        Transform leftController = FindChild(xrOrigin, "Left Controller");
        if (leftController == null)
        {
            Debug.LogWarning("No encontré Left Controller; la tablet quedó instalada, pero el agarre no se configuró como toggle.");
            return;
        }

        Component directInteractor = FindComponentByTypeName(leftController, "XRDirectInteractor");
        if (directInteractor == null || !SetSelectActionToggle(directInteractor))
        {
            Debug.LogWarning("No pude configurar el agarre tipo toggle en el control izquierdo.");
            return;
        }

        EditorUtility.SetDirty(directInteractor);
        PrefabUtility.RecordPrefabInstancePropertyModifications(directInteractor);
    }

    private static void SetupRightHandUiRay(Transform xrOrigin, Type rayType)
    {
        if (rayType == null)
            return;

        Transform rightController = FindChild(xrOrigin, "Right Controller");
        Component rayInteractor = rightController == null
            ? null
            : rightController.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component != null && rayType.IsInstanceOfType(component));

        if (rayInteractor == null)
        {
            Debug.LogWarning("No encontré el rayo del control derecho para pulsar la interfaz.");
            return;
        }

        SetBooleanPropertyOrSerializedField(rayInteractor, "enableUIInteraction", "m_EnableUIInteraction", true);
        EditorUtility.SetDirty(rayInteractor);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rayInteractor);
    }

    private static bool SetSelectActionToggle(Component directInteractor)
    {
        PropertyInfo property = directInteractor.GetType().GetProperty("selectActionTrigger",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && property.PropertyType.IsEnum)
        {
            object toggleValue = Enum.Parse(property.PropertyType, "Toggle", true);
            property.SetValue(directInteractor, toggleValue, null);
            return true;
        }

        SerializedObject serializedInteractor = new SerializedObject(directInteractor);
        SerializedProperty trigger = serializedInteractor.FindProperty("m_SelectActionTrigger");
        if (trigger == null || trigger.propertyType != SerializedPropertyType.Enum)
            return false;

        string[] names = trigger.enumDisplayNames;
        int toggleIndex = Array.FindIndex(names, name => string.Equals(name, "Toggle", StringComparison.OrdinalIgnoreCase));
        if (toggleIndex < 0)
            return false;

        trigger.enumValueIndex = toggleIndex;
        serializedInteractor.ApplyModifiedProperties();
        return true;
    }

    private static void SetBooleanPropertyOrSerializedField(Component component, string propertyName, string fieldName, bool value)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
        {
            property.SetValue(component, value, null);
            return;
        }

        SerializedObject serializedComponent = new SerializedObject(component);
        SerializedProperty field = serializedComponent.FindProperty(fieldName);
        if (field != null && field.propertyType == SerializedPropertyType.Boolean)
        {
            field.boolValue = value;
            serializedComponent.ApplyModifiedProperties();
        }
    }

    private static void CreateTabletScreen(Transform tablet, Bounds bounds, Type trackedRaycasterType)
    {
        Transform canvasTransform = new GameObject("Pantalla de reportes", typeof(RectTransform)).transform;
        Undo.RegisterCreatedObjectUndo(canvasTransform.gameObject, "Crear pantalla de la tablet");
        canvasTransform.SetParent(tablet, false);

        Vector3 normal = Vector3.forward;
        Vector3 up = Vector3.up;
        if (bounds.size.x < bounds.size.y && bounds.size.x < bounds.size.z)
        {
            normal = Vector3.right;
            up = Vector3.up;
        }
        else if (bounds.size.y < bounds.size.x && bounds.size.y < bounds.size.z)
        {
            normal = Vector3.up;
            up = Vector3.forward;
        }

        Quaternion screenRotation = Quaternion.LookRotation(normal, up);
        canvasTransform.localRotation = screenRotation;
        canvasTransform.localPosition = bounds.center + normal * (GetExtentAlong(bounds.extents, normal) + 0.001f);

        Vector3 right = Vector3.Cross(up, normal).normalized;
        float screenWidth = Mathf.Abs(right.x) * bounds.size.x + Mathf.Abs(right.y) * bounds.size.y + Mathf.Abs(right.z) * bounds.size.z;
        float screenHeight = Mathf.Abs(up.x) * bounds.size.x + Mathf.Abs(up.y) * bounds.size.y + Mathf.Abs(up.z) * bounds.size.z;
        bool portrait = screenHeight > screenWidth;
        float canvasWidth = portrait ? 600f : 900f;
        float canvasHeight = canvasWidth * screenHeight / Mathf.Max(screenWidth, 0.001f);

        RectTransform rect = (RectTransform)canvasTransform;
        rect.sizeDelta = new Vector2(canvasWidth, canvasHeight);
        canvasTransform.localScale = Vector3.one * (screenWidth * 0.86f / rect.sizeDelta.x);

        Canvas canvas = canvasTransform.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasTransform.gameObject.AddComponent<GraphicRaycaster>();
        canvasTransform.gameObject.AddComponent(trackedRaycasterType);
        canvasTransform.gameObject.AddComponent<AnomalyTabletMenu>();

        CreatePanel(canvasTransform);
        if (portrait)
        {
            float halfHeight = canvasHeight * 0.5f;
            CreateLabel(canvasTransform, "TabletTitle", "REPORTE DE ANOMALÍA", new Vector2(0f, halfHeight - 80f), new Vector2(canvasWidth - 40f, 90f), 34, TextAnchor.MiddleCenter);
            CreateButton(canvasTransform, "Category_Object", "OBJETO ALTERADO", new Vector2(0f, halfHeight - 210f), new Vector2(canvasWidth - 70f, 105f));
            CreateButton(canvasTransform, "Category_Presence", "PRESENCIA", new Vector2(0f, halfHeight - 335f), new Vector2(canvasWidth - 70f, 105f));
            CreateButton(canvasTransform, "Category_Sound", "SONIDO EXTRAÑO", new Vector2(0f, halfHeight - 460f), new Vector2(canvasWidth - 70f, 105f));
            CreateButton(canvasTransform, "Category_Other", "OTRA ANOMALÍA", new Vector2(0f, halfHeight - 585f), new Vector2(canvasWidth - 70f, 105f));
            CreateButton(canvasTransform, "SendReport", "ENVIAR REPORTE", new Vector2(0f, -halfHeight + 145f), new Vector2(canvasWidth - 70f, 90f), true);
            CreateLabel(canvasTransform, "TabletStatus", "Selecciona lo que observaste.", new Vector2(0f, -halfHeight + 75f), new Vector2(canvasWidth - 40f, 50f), 22, TextAnchor.MiddleCenter);
        }
        else
        {
            CreateLabel(canvasTransform, "TabletTitle", "REPORTE DE ANOMALÍA", new Vector2(0f, 235f), new Vector2(canvasWidth - 60f, 70f), 38, TextAnchor.MiddleCenter);
            CreateButton(canvasTransform, "Category_Object", "OBJETO ALTERADO", new Vector2(-210f, 100f));
            CreateButton(canvasTransform, "Category_Presence", "PRESENCIA", new Vector2(210f, 100f));
            CreateButton(canvasTransform, "Category_Sound", "SONIDO EXTRAÑO", new Vector2(-210f, -35f));
            CreateButton(canvasTransform, "Category_Other", "OTRA ANOMALÍA", new Vector2(210f, -35f));
            CreateButton(canvasTransform, "SendReport", "ENVIAR REPORTE", new Vector2(0f, -175f), new Vector2(760f, 82f), true);
            CreateLabel(canvasTransform, "TabletStatus", "Selecciona lo que observaste.", new Vector2(0f, -255f), new Vector2(canvasWidth - 80f, 50f), 26, TextAnchor.MiddleCenter);
        }
    }

    private static void CreatePanel(Transform parent)
    {
        GameObject panel = new GameObject("Fondo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.layer = 5;
        panel.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.055f, 0.96f);
    }

    private static void CreateButton(Transform parent, string name, string label, Vector2 position, Vector2? size = null, bool primary = false)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.layer = 5;
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size ?? new Vector2(390f, 105f);
        rect.anchoredPosition = position;

        Image image = buttonObject.GetComponent<Image>();
        image.color = primary ? new Color(0.48f, 0.12f, 0.15f, 1f) : new Color(0.12f, 0.15f, 0.18f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.64f, 0.19f, 0.21f, 1f);
        colors.pressedColor = new Color(0.82f, 0.28f, 0.3f, 1f);
        button.colors = colors;

        CreateLabel(buttonObject.transform, "Label", label, Vector2.zero, rect.sizeDelta, primary ? 30 : 27, TextAnchor.MiddleCenter);
    }

    private static void CreateLabel(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.layer = 5;
        textObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.92f, 0.94f, 0.95f, 1f);
        text.raycastTarget = false;
    }

    private static Bounds GetLocalBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        bool hasBounds = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);
        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = world.center + Vector3.Scale(world.extents, new Vector3(x, y, z));
                        Vector3 localCorner = root.transform.InverseTransformPoint(corner);
                        if (!hasBounds)
                        {
                            result = new Bounds(localCorner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            result.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        return result;
    }

    private static float GetExtentAlong(Vector3 extents, Vector3 normal)
    {
        return Mathf.Abs(normal.x) * extents.x + Mathf.Abs(normal.y) * extents.y + Mathf.Abs(normal.z) * extents.z;
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        return parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(transform => transform.name == childName);
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(gameObject);
    }

    private static Component EnsureComponent(GameObject gameObject, Type type)
    {
        Component component = gameObject.GetComponent(type);
        return component != null ? component : Undo.AddComponent(gameObject, type);
    }

    private static Component FindComponentByTypeName(Transform root, string typeName)
    {
        return root.GetComponentsInChildren<MonoBehaviour>(true)
            .FirstOrDefault(component => component != null && component.GetType().Name == typeName);
    }

    private static Type FindToolkitType(string fullName)
    {
        return TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
            .FirstOrDefault(type => type.FullName == fullName);
    }
}

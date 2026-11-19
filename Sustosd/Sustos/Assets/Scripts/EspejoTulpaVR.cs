using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class EspejoTulpaVR : MonoBehaviour
{
    public const int CapaSoloEspejo = 29;

    [Header("Referencias del Espejo y Tulpa")]
    public Transform superficieEspejo;
    public MeshRenderer rendererEspejo;
    public GameObject modeloTulpaReflejado;

    [Header("Optimización VR (Meta Quest 2)")]
    public float distanciaActivacionCamara = 5.8f;
    public int resolucionTextura = 512;

    private Camera camaraEspejo;
    private RenderTexture texturaReflejo;
    private Material materialEspejo;
    private Light luzEspectralTulpa;
    private Transform camJugador;
    private int contadorFrames;

    private void Awake()
    {
        ConfigurarCapasYConstruirEspejo();
    }

    public void ConfigurarCapasYConstruirEspejo()
    {
        // 1. Ocultar la capa 29 (CapaSoloEspejo) de la cámara principal del jugador para que Tulpa SOLO se vea dentro del espejo
        if (Camera.main != null)
        {
            camJugador = Camera.main.transform;
            Camera.main.cullingMask &= ~(1 << CapaSoloEspejo);
        }

        // 2. Posicionar el espejo sobre el lavabo del baño (Washbasin_03) mirando hacia el interior del baño
        AlinearEspejoConLavabo();

        // 3. Crear el marco y cristal reflectivo del espejo si aún no existen
        if (superficieEspejo == null)
        {
            Transform existente = transform.Find("Cristal_Espejo_VR");
            if (existente != null)
                superficieEspejo = existente;
        }

        if (superficieEspejo == null)
        {
            GameObject marco = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marco.name = "Marco_Espejo_VR";
            Destroy(marco.GetComponent<BoxCollider>());
            marco.transform.SetParent(transform, false);
            marco.transform.localPosition = new Vector3(0f, 1.48f, -0.02f);
            marco.transform.localRotation = Quaternion.identity;
            marco.transform.localScale = new Vector3(0.84f, 1.04f, 0.03f);

            Shader shLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material matMarco = new Material(shLit);
            matMarco.color = new Color(0.14f, 0.15f, 0.17f, 1f);
            matMarco.SetFloat("_Metallic", 0.8f);
            matMarco.SetFloat("_Smoothness", 0.7f);
            marco.GetComponent<MeshRenderer>().sharedMaterial = matMarco;

            GameObject cristal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cristal.name = "Cristal_Espejo_VR";
            Destroy(cristal.GetComponent<MeshCollider>());
            cristal.transform.SetParent(transform, false);
            cristal.transform.localPosition = new Vector3(0f, 1.48f, 0.005f);
            // El Quad mira hacia +Z local (hacia el interior del baño) por lo que su cara frontal debe mirar a -Z local -> rotar 180 en Y
            cristal.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            cristal.transform.localScale = new Vector3(0.76f, 0.96f, 1f);

            superficieEspejo = cristal.transform;
            rendererEspejo = cristal.GetComponent<MeshRenderer>();
        }
        else if (rendererEspejo == null)
        {
            rendererEspejo = superficieEspejo.GetComponent<MeshRenderer>();
        }

        // 4. Crear RenderTexture y cámara de reflexión en tiempo real
        if (Application.isPlaying)
        {
            InicializarCamaraReflejo();
        }

        // 5. Asegurar que el modelo del Tulpa esté en la Capa 29 (solo visible en el espejo) frente al cristal
        VincularModeloTulpaEnCapaEspejo();
    }

    private void AlinearEspejoConLavabo()
    {
        GameObject house = GameObject.Find("House");
        if (house == null) return;

        Transform lavabo = null;
        Transform gabinete = null;
        foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n == "bathroom_cabinet") gabinete = t;
            else if (n == "washbasin_03") lavabo = t;
        }

        Transform refMueble = gabinete != null ? gabinete : lavabo;
        if (refMueble == null) return;

        Renderer r = refMueble.GetComponent<Renderer>();
        Vector3 centroMueble = r != null ? r.bounds.center : refMueble.position;

        // Colocar la raíz en el suelo justo debajo del lavabo/gabinete
        float pisoY = centroMueble.y > 2.8f ? 3.74f : 0.41f;

        // Buscar la pared detrás del lavabo mediante Raycast horizontal para orientar el espejo hacia el cuarto
        Vector3 origenRayo = new Vector3(centroMueble.x, pisoY + 1.45f, centroMueble.z);
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        Vector3 mejorNormal = Vector3.forward;
        Vector3 puntoPared = new Vector3(centroMueble.x, pisoY, centroMueble.z);
        float menorDist = 1.6f;
        bool halloPared = false;

        foreach (Vector3 d in dirs)
        {
            if (Physics.Raycast(origenRayo, d, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Mathf.Abs(hit.normal.y) < 0.25f && hit.distance < menorDist)
                {
                    menorDist = hit.distance;
                    mejorNormal = hit.normal;
                    puntoPared = hit.point;
                    halloPared = true;
                }
            }
        }

        if (halloPared)
        {
            transform.position = new Vector3(puntoPared.x, pisoY, puntoPared.z) + mejorNormal * 0.03f;
            transform.rotation = Quaternion.LookRotation(mejorNormal, Vector3.up);
        }
        else
        {
            transform.position = new Vector3(centroMueble.x, pisoY, centroMueble.z);
        }
    }

    private void InicializarCamaraReflejo()
    {
        if (texturaReflejo == null)
        {
            texturaReflejo = new RenderTexture(resolucionTextura, resolucionTextura, 16, RenderTextureFormat.Default)
            {
                name = "RT_Espejo_Tulpa_VR",
                antiAliasing = 2
            };
            texturaReflejo.Create();
        }

        Shader shUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
        materialEspejo = new Material(shUnlit)
        {
            name = "Mat_Espejo_Reflectivo_VR"
        };
        materialEspejo.SetTexture("_BaseMap", texturaReflejo);
        materialEspejo.mainTexture = texturaReflejo;
        // Invertir horizontalmente las coordenadas UV (-1 en X) para que la imagen sea un reflejo especular real
        materialEspejo.SetVector("_BaseMap_ST", new Vector4(-1f, 1f, 1f, 0f));
        materialEspejo.mainTextureScale = new Vector2(-1f, 1f);
        materialEspejo.mainTextureOffset = new Vector2(1f, 0f);
        materialEspejo.color = new Color(0.88f, 0.93f, 0.96f, 1f);

        if (rendererEspejo != null)
            rendererEspejo.sharedMaterial = materialEspejo;

        Transform camHija = superficieEspejo.Find("Camara_Reflejo_Espejo");
        if (camHija == null)
        {
            GameObject camObj = new GameObject("Camara_Reflejo_Espejo");
            camObj.transform.SetParent(superficieEspejo, false);
            camHija = camObj.transform;
        }

        camaraEspejo = camHija.GetComponent<Camera>();
        if (camaraEspejo == null)
            camaraEspejo = camHija.gameObject.AddComponent<Camera>();

        camaraEspejo.targetTexture = texturaReflejo;
        camaraEspejo.fieldOfView = 62f;
        camaraEspejo.nearClipPlane = 0.08f;
        camaraEspejo.farClipPlane = 9f;
        camaraEspejo.clearFlags = CameraClearFlags.SolidColor;
        camaraEspejo.backgroundColor = new Color(0.03f, 0.04f, 0.05f, 1f);
        // La cámara del espejo SÍ ve la Capa 29 (CapaSoloEspejo), donde habita el Tulpa
        camaraEspejo.cullingMask = ~0;
        camaraEspejo.enabled = false; // La renderizamos manualmente en intervalos óptimos

        UniversalAdditionalCameraData camData = camaraEspejo.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null)
            camData = camaraEspejo.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camData.renderShadows = false;
        camData.renderPostProcessing = false;
    }

    public void VincularModeloTulpaEnCapaEspejo()
    {
        if (modeloTulpaReflejado == null)
        {
            Transform vis = transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");
            if (vis != null)
                modeloTulpaReflejado = vis.gameObject;
        }

        if (modeloTulpaReflejado == null) return;

        // Colocar al Tulpa a 1.15m frente al espejo mirando directamente hacia el espejo (como si estuviera parado detrás del jugador)
        modeloTulpaReflejado.transform.localPosition = new Vector3(0f, 0f, 1.15f);
        modeloTulpaReflejado.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        AsignarCapaRecursiva(modeloTulpaReflejado.transform, CapaSoloEspejo);

        // Añadir una tenue luz rojiza/espectral en la capa del espejo para que el Tulpa destaque nítidamente en el reflejo
        Transform luzT = modeloTulpaReflejado.transform.Find("Luz_Reflejo_Tulpa");
        if (luzT == null)
        {
            GameObject luzObj = new GameObject("Luz_Reflejo_Tulpa");
            luzObj.layer = CapaSoloEspejo;
            luzObj.transform.SetParent(modeloTulpaReflejado.transform, false);
            luzObj.transform.localPosition = new Vector3(0f, 1.45f, 0.55f);
            luzEspectralTulpa = luzObj.AddComponent<Light>();
            luzEspectralTulpa.type = LightType.Point;
            luzEspectralTulpa.color = new Color(0.95f, 0.78f, 0.78f, 1f);
            luzEspectralTulpa.intensity = 1.6f;
            luzEspectralTulpa.range = 2.6f;
            luzEspectralTulpa.shadows = LightShadows.None;
            luzEspectralTulpa.cullingMask = (1 << CapaSoloEspejo) | 1;
        }
    }

    private static void AsignarCapaRecursiva(Transform raiz, int capa)
    {
        if (raiz == null) return;
        raiz.gameObject.layer = capa;
        for (int i = 0; i < raiz.childCount; i++)
            AsignarCapaRecursiva(raiz.GetChild(i), capa);
    }

    private void LateUpdate()
    {
        if (camaraEspejo == null || superficieEspejo == null) return;

        if (camJugador == null && Camera.main != null)
        {
            camJugador = Camera.main.transform;
            Camera.main.cullingMask &= ~(1 << CapaSoloEspejo);
        }

        if (camJugador == null) return;

        float dist = Vector3.Distance(camJugador.position, superficieEspejo.position);
        if (dist > distanciaActivacionCamara) return;

        // Renderizar cada 2 frames para mantener 72/90 FPS estables en Meta Quest 2
        contadorFrames++;
        if ((contadorFrames & 1) != 0) return;

        // Calcular dirección reflejada respecto a la normal del espejo (transform.forward apunta hacia el interior del baño)
        Vector3 normalEspejo = transform.forward;
        Vector3 posEspejo = superficieEspejo.position;
        Vector3 desdeJugadorAEspejo = (posEspejo - camJugador.position).normalized;
        Vector3 dirReflejada = Vector3.Reflect(desdeJugadorAEspejo, normalEspejo);

        if (dirReflejada.sqrMagnitude > 0.001f)
        {
            camaraEspejo.transform.position = posEspejo + normalEspejo * 0.02f;
            camaraEspejo.transform.rotation = Quaternion.LookRotation(dirReflejada, Vector3.up);
        }

        camaraEspejo.Render();
    }

    private void OnDestroy()
    {
        if (texturaReflejo != null)
        {
            texturaReflejo.Release();
            Destroy(texturaReflejo);
        }
    }
}

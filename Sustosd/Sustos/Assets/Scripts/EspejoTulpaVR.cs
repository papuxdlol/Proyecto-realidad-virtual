using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class EspejoTulpaVR : MonoBehaviour
{
    public const int CapaSoloEspejo = 29;

    [Header("Referencias del Espejo y Tulpa")]
    public Transform superficieEspejo;
    public MeshRenderer rendererEspejo;
    public GameObject modeloTulpaReflejado;
    public bool esEspejoSecundario = false;

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
        if (Camera.main != null)
        {
            camJugador = Camera.main.transform;
            Camera.main.cullingMask &= ~(1 << CapaSoloEspejo);
        }

        if (!esEspejoSecundario)
        {
            AlinearEspejoPrincipalArriba();
            CrearEspejosAdicionalesEnOtrosBanos();
        }

        ConstruirGeometriaEspejo(alturaCentroLocal: 1.54f, ancho: 0.72f, alto: 0.48f);

        if (Application.isPlaying)
        {
            InicializarCamaraReflejo();
        }

        if (!esEspejoSecundario)
        {
            VincularModeloTulpaEnCapaEspejo();
        }
    }

    public void ConstruirGeometriaEspejo(float alturaCentroLocal, float ancho, float alto)
    {
        Transform marcoExistente = transform.Find("Marco_Espejo_VR");
        GameObject marco;
        if (marcoExistente == null)
        {
            marco = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marco.name = "Marco_Espejo_VR";
            Collider c = marco.GetComponent<Collider>();
            if (c != null)
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
            marco.transform.SetParent(transform, false);

            Shader shLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material matMarco = new Material(shLit);
            matMarco.color = new Color(0.14f, 0.15f, 0.17f, 1f);
            matMarco.SetFloat("_Metallic", 0.8f);
            matMarco.SetFloat("_Smoothness", 0.7f);
            marco.GetComponent<MeshRenderer>().sharedMaterial = matMarco;
        }
        else
        {
            marco = marcoExistente.gameObject;
        }

        // Centro local a +1.54m con alto 0.48m -> abarca de +1.30m a +1.78m sobre el suelo:
        // Piso 1 (0.41m): Y = [1.71m, 2.19m] -> por encima del jabón verde (1.678m) y debajo del gabinete (2.235m)
        // Piso 2 (3.74m): Y = [5.04m, 5.52m] -> por encima del jabón verde (5.003m) y debajo del gabinete (5.559m)
        marco.transform.localPosition = new Vector3(0f, alturaCentroLocal, 0.005f);
        marco.transform.localRotation = Quaternion.identity;
        marco.transform.localScale = new Vector3(ancho, alto, 0.010f);

        if (superficieEspejo == null)
        {
            Transform existente = transform.Find("Cristal_Espejo_VR");
            if (existente != null)
                superficieEspejo = existente;
        }

        if (superficieEspejo == null)
        {
            GameObject cristal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cristal.name = "Cristal_Espejo_VR";
            Collider c = cristal.GetComponent<Collider>();
            if (c != null)
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
            cristal.transform.SetParent(transform, false);
            superficieEspejo = cristal.transform;
            rendererEspejo = cristal.GetComponent<MeshRenderer>();
        }
        else if (rendererEspejo == null)
        {
            rendererEspejo = superficieEspejo.GetComponent<MeshRenderer>();
        }

        superficieEspejo.localPosition = new Vector3(0f, alturaCentroLocal, 0.012f);
        superficieEspejo.localRotation = Quaternion.Euler(0f, 180f, 0f);
        superficieEspejo.localScale = new Vector3(ancho - 0.05f, alto - 0.05f, 1f);
    }

    public void AlinearEspejoPrincipalArriba()
    {
        // Baño 1 Arriba (Washbasin_03.001): Muro trasero en X = 17.661, centro Z = -5.818, piso Y = 3.74, mirando hacia +X
        transform.position = new Vector3(17.663f, 3.74f, -5.818f);
        transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

        ObjetoAnomalia anom = GetComponent<ObjetoAnomalia>();
        if (anom != null)
            anom.nombreCuarto = "Bathroom";
    }

    private void CrearEspejosAdicionalesEnOtrosBanos()
    {
        Transform padre = transform.parent;

        // 1. Espejo del Baño de Abajo / Lavandería (sobre Washbasin_03: Muro trasero X = 19.148, Z = -6.220, piso Y = 0.41, mirando hacia -X)
        CrearOActualizarEspejoSecundario(
            "Espejo_Bano_Abajo_VR",
            padre,
            new Vector3(19.145f, 0.41f, -6.220f),
            Quaternion.LookRotation(Vector3.left, Vector3.up));

        // 2. Espejo del Baño 2 de Arriba (sobre Washbasin_07: Muro trasero Z = -0.628, X = 13.710, piso Y = 3.74, mirando hacia -Z)
        CrearOActualizarEspejoSecundario(
            "Espejo_Bano2_Arriba_VR",
            padre,
            new Vector3(13.710f, 3.74f, -0.635f),
            Quaternion.LookRotation(Vector3.back, Vector3.up));
    }

    private void CrearOActualizarEspejoSecundario(string nombre, Transform padre, Vector3 pos, Quaternion rot)
    {
        GameObject obj = GameObject.Find(nombre);
        if (obj == null)
        {
            obj = new GameObject(nombre);
            if (padre != null)
                obj.transform.SetParent(padre, true);
        }

        obj.transform.position = pos;
        obj.transform.rotation = rot;
        obj.transform.localScale = Vector3.one;

        EspejoTulpaVR esp = obj.GetComponent<EspejoTulpaVR>();
        if (esp == null)
            esp = obj.AddComponent<EspejoTulpaVR>();

        esp.esEspejoSecundario = true;
        esp.ConstruirGeometriaEspejo(alturaCentroLocal: 1.54f, ancho: 0.72f, alto: 0.48f);
        if (Application.isPlaying)
        {
            esp.InicializarCamaraReflejo();
        }

        // Clonar también el modelo visual de Tulpa en la capa 29 para que al activarse Tulpa se refleje en cualquiera de los espejos
        if (modeloTulpaReflejado == null)
        {
            Transform vis = transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");
            if (vis != null) modeloTulpaReflejado = vis.gameObject;
        }

        if (modeloTulpaReflejado != null && obj.transform.Find("Tulpa_Reflejo_Secundario") == null && Application.isPlaying)
        {
            GameObject clonTulpa = Instantiate(modeloTulpaReflejado, obj.transform);
            clonTulpa.name = "Tulpa_Reflejo_Secundario";
            clonTulpa.transform.localPosition = new Vector3(0f, 0.14f, 1.48f);
            clonTulpa.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (Animator an in clonTulpa.GetComponentsInChildren<Animator>(true))
                an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            AsignarCapaRecursiva(clonTulpa.transform, CapaSoloEspejo);
            esp.modeloTulpaReflejado = clonTulpa;
        }
    }

    public void InicializarCamaraReflejo()
    {
        if (texturaReflejo == null)
        {
            texturaReflejo = new RenderTexture(resolucionTextura, resolucionTextura, 16, RenderTextureFormat.Default)
            {
                name = "RT_" + gameObject.name,
                antiAliasing = 2
            };
            texturaReflejo.Create();
        }

        Shader shUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
        materialEspejo = new Material(shUnlit)
        {
            name = "Mat_Espejo_" + gameObject.name
        };
        materialEspejo.SetTexture("_BaseMap", texturaReflejo);
        materialEspejo.mainTexture = texturaReflejo;
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
        camaraEspejo.nearClipPlane = 0.25f;
        camaraEspejo.farClipPlane = 8.5f;
        camaraEspejo.clearFlags = CameraClearFlags.SolidColor;
        camaraEspejo.backgroundColor = new Color(0.03f, 0.04f, 0.05f, 1f);
        camaraEspejo.cullingMask = ~0;
        camaraEspejo.enabled = false;

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

        // Colocar a Tulpa a ~1.48m detrás del reflejo y ligeramente elevada para encuadrar cabeza y torso al centro del espejo
        modeloTulpaReflejado.transform.localPosition = new Vector3(0f, 0.14f, 1.48f);
        modeloTulpaReflejado.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        AsignarCapaRecursiva(modeloTulpaReflejado.transform, CapaSoloEspejo);

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

        // Sincronizar visibilidad de Tulpa en espejos secundarios con el espejo principal
        if (esEspejoSecundario && modeloTulpaReflejado != null)
        {
            GameObject principal = GameObject.Find("Anomalia_Tulpa_EspejoBano");
            if (principal != null)
            {
                Transform visPpal = principal.transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");
                if (visPpal != null && modeloTulpaReflejado.activeSelf != visPpal.gameObject.activeSelf)
                {
                    modeloTulpaReflejado.SetActive(visPpal.gameObject.activeSelf);
                }
            }
        }

        if (camJugador == null && Camera.main != null)
        {
            camJugador = Camera.main.transform;
            Camera.main.cullingMask &= ~(1 << CapaSoloEspejo);
        }

        if (camJugador == null) return;

        // Solo renderizar el espejo si el jugador está en el mismo piso y cerca del espejo
        if (Mathf.Abs(camJugador.position.y - superficieEspejo.position.y) > 1.8f) return;
        float dist = Vector3.Distance(camJugador.position, superficieEspejo.position);
        if (dist > distanciaActivacionCamara) return;

        contadorFrames++;
        if ((contadorFrames & 1) != 0) return;

        Vector3 normalEspejo = transform.forward;
        Vector3 posEspejo = superficieEspejo.position;
        Vector3 desdeJugadorAEspejo = (posEspejo - camJugador.position).normalized;
        Vector3 dirReflejada = Vector3.Reflect(desdeJugadorAEspejo, normalEspejo);

        if (dirReflejada.sqrMagnitude > 0.001f)
        {
            // Limitar inclinación vertical para que el reflejo mantenga bien encuadrado el rostro de Tulpa al mirar desde cerca
            dirReflejada.y = Mathf.Clamp(dirReflejada.y, -0.14f, 0.10f);
            dirReflejada.Normalize();
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

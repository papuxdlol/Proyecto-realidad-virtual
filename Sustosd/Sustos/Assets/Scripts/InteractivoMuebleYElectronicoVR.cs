using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public enum TipoInteractivoCasa
{
    CajonDeslizable = 0,
    PuertaElectrodomesticoOGabinete = 1,
    TelevisorConGifOVideo = 2,
    RadioSonido = 3
}

[DefaultExecutionOrder(-85)]
public class InteractivoMuebleYElectronicoVR : MonoBehaviour
{
    [Header("Configuración del Objeto Interactivo")]
    public TipoInteractivoCasa tipoInteractivo = TipoInteractivoCasa.CajonDeslizable;
    public string etiquetaHUD = "Cajón";
    public bool estaActivoOAbierto = false;

    [Header("Si es Cajón Deslizable")]
    public Vector3 direccionDeslizamientoMundial = Vector3.forward;
    public float distanciaAperturaCajon = 0.26f;
    public float velocidadCajon = 1.4f;
    public Transform piezaMovilCajon;
    public AudioClip clipCajonReal;

    [Header("Si es Puerta de Refrigerador / Microondas / Gabinete")]
    public float anguloAperturaPuerta = -85f;
    public float velocidadGiroPuerta = 180f;
    public Vector3 ejeGiroLocalOMundial = Vector3.up;
    public bool usarPivoteBorde = false;
    public AudioClip clipRefrigerador;
    public AudioClip clipMicroondas;
    public AudioClip clipLavadora;
    public AudioClip clipGabinete;
    public AudioClip clipEstaticaTV;

    [Header("Si es Televisor (Soporta GIF por Frames o VideoClip MP4)")]
    [Tooltip("Arrastra aquí los cuadros (sprites/texturas) de tu GIF si quieres mostrar un GIF personalizado")]
    public Texture2D[] framesGifPersonalizado;
    public float fpsGif = 8f;
    [Tooltip("O arrastra aquí un VideoClip (.mp4 / .webm) para reproducirlo en la pantalla de la TV")]
    public VideoClip videoClipPersonalizado;

    private Vector3 posCerradaMundial;
    private Quaternion rotCerradaMundial;
    private Vector3 puntoBisagraMundial;
    private float progresoActual = 0f;
    private float ultimoTiempoInteraccion = -10f;

    private BoxCollider hitboxPositivo;
    private XRSimpleInteractable interactable;
    private AudioSource audioSource;
    private AudioSource audioEstaticaLoop;
    private Coroutine rutinaRecorteAudio;
    private Light luzInteriorOPantalla;

    // Pantalla TV
    private GameObject quadPantallaTV;
    private MeshRenderer rendererPantallaTV;
    private Material materialPantallaTV;
    private VideoPlayer videoPlayerTV;
    private float temporizadorGif = 0f;
    private int frameGifActual = 0;

    private static Texture2D[] framesGifProcedural;
    private static AudioClip clipCajon;
    private static AudioClip clipElectrodomestico;
    private static AudioClip clipBotonTV;
    private static AudioClip cacheFridge;
    private static AudioClip cacheMicro;
    private static AudioClip cacheWash;
    private static AudioClip cacheCabinet;
    private static AudioClip cacheTvStatic;
    private static readonly List<InteractivoMuebleYElectronicoVR> instanciasActivas = new List<InteractivoMuebleYElectronicoVR>();

    public static void NotificarCambioEnergiaGeneral(bool hayEnergia)
    {
        for (int i = instanciasActivas.Count - 1; i >= 0; i--)
        {
            InteractivoMuebleYElectronicoVR inst = instanciasActivas[i];
            if (inst == null)
            {
                instanciasActivas.RemoveAt(i);
                continue;
            }
            inst.AplicarEstadoEnergiaGeneral(hayEnergia);
        }
    }

    public void AplicarEstadoEnergiaGeneral(bool hayEnergia)
    {
        if (tipoInteractivo == TipoInteractivoCasa.TelevisorConGifOVideo)
        {
            if (!hayEnergia)
            {
                estaActivoOAbierto = false;
                if (quadPantallaTV != null) quadPantallaTV.SetActive(false);
                if (luzInteriorOPantalla != null) luzInteriorOPantalla.enabled = false;
                if (videoPlayerTV != null && videoClipPersonalizado != null && videoPlayerTV.isPlaying) videoPlayerTV.Pause();
                if (audioEstaticaLoop != null && audioEstaticaLoop.isPlaying) audioEstaticaLoop.Stop();
            }
        }
        else if (tipoInteractivo == TipoInteractivoCasa.RadioSonido)
        {
            if (!hayEnergia)
            {
                estaActivoOAbierto = false;
                if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
                if (audioEstaticaLoop != null && audioEstaticaLoop.isPlaying) audioEstaticaLoop.Stop();
            }
        }
        else if (tipoInteractivo == TipoInteractivoCasa.PuertaElectrodomesticoOGabinete)
        {
            if (luzInteriorOPantalla != null)
                luzInteriorOPantalla.enabled = estaActivoOAbierto && hayEnergia;
        }
    }

    public void ForzarEstadoPorAnomalia(bool abrirOEncender)
    {
        bool hayEnergia = InterruptorLuz.EnergiaGeneralCasaActiva;
        if (!hayEnergia && abrirOEncender && (tipoInteractivo == TipoInteractivoCasa.TelevisorConGifOVideo || tipoInteractivo == TipoInteractivoCasa.RadioSonido))
            return;

        estaActivoOAbierto = abrirOEncender;

        if (tipoInteractivo == TipoInteractivoCasa.CajonDeslizable)
        {
            AudioClip c = clipCajonReal != null ? clipCajonReal : clipCajon;
            if (abrirOEncender && audioSource != null && c != null)
                audioSource.PlayOneShot(c, 0.70f);
        }
        else if (tipoInteractivo == TipoInteractivoCasa.PuertaElectrodomesticoOGabinete)
        {
            if (abrirOEncender)
                ReproducirSonidoPuertaElectrodomesticoOGabinete(true);
            if (luzInteriorOPantalla != null)
                luzInteriorOPantalla.enabled = estaActivoOAbierto && hayEnergia;
        }
        else if (tipoInteractivo == TipoInteractivoCasa.TelevisorConGifOVideo)
        {
            if (quadPantallaTV != null)
                quadPantallaTV.SetActive(estaActivoOAbierto && hayEnergia);
            if (luzInteriorOPantalla != null)
                luzInteriorOPantalla.enabled = estaActivoOAbierto && hayEnergia;
            if (videoPlayerTV != null && videoClipPersonalizado != null)
            {
                if (estaActivoOAbierto && hayEnergia) videoPlayerTV.Play();
                else videoPlayerTV.Pause();
            }
            if (!estaActivoOAbierto && audioEstaticaLoop != null && audioEstaticaLoop.isPlaying)
                audioEstaticaLoop.Stop();
        }
        else if (tipoInteractivo == TipoInteractivoCasa.RadioSonido)
        {
            if (!estaActivoOAbierto)
            {
                if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
                if (audioEstaticaLoop != null && audioEstaticaLoop.isPlaying) audioEstaticaLoop.Stop();
            }
        }
    }

    public string ObtenerEtiquetaHUD()
    {
        bool hayLuz = InterruptorLuz.EnergiaGeneralCasaActiva;
        if (tipoInteractivo == TipoInteractivoCasa.TelevisorConGifOVideo)
        {
            if (!hayLuz) return $"{etiquetaHUD} (Sin energía)";
            return estaActivoOAbierto ? $"Apagar {etiquetaHUD}" : $"Encender {etiquetaHUD}";
        }
        if (tipoInteractivo == TipoInteractivoCasa.RadioSonido)
        {
            if (!hayLuz) return "Radio (Sin energía)";
            return estaActivoOAbierto ? "Apagar Radio" : "Encender Radio";
        }
        return estaActivoOAbierto ? $"Cerrar {etiquetaHUD}" : $"Abrir {etiquetaHUD}";
    }

    public Vector3 ObtenerPosicionMundial()
    {
        if (hitboxPositivo != null) return hitboxPositivo.bounds.center;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        return mr != null ? mr.bounds.center : transform.position;
    }

    public Vector3 ObtenerPuntoMasCercano(Vector3 origen)
    {
        if (hitboxPositivo != null && hitboxPositivo.enabled)
            return hitboxPositivo.bounds.ClosestPoint(origen);
        MeshRenderer mr = GetComponent<MeshRenderer>();
        return mr != null ? mr.bounds.ClosestPoint(origen) : transform.position;
    }

    void Awake()
    {
        if (PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(transform))
        {
            Destroy(this);
            return;
        }

        AutoConfigurarSegunNombre();
        ConstruirHitboxPositivo();
        AsegurarAudio();
    }

    public void AutoConfigurarSegunNombre()
    {
        string n = gameObject.name.ToLowerInvariant();
        MeshRenderer mr = GetComponent<MeshRenderer>();
        Bounds b = mr != null ? mr.bounds : new Bounds(transform.position, Vector3.one * 0.4f);

        if (n.StartsWith("tv_0"))
        {
            tipoInteractivo = TipoInteractivoCasa.TelevisorConGifOVideo;
            etiquetaHUD = "Televisor";
            PrepararPantallaTelevisor(b);
            return;
        }

        if (n.StartsWith("radio_0"))
        {
            tipoInteractivo = TipoInteractivoCasa.RadioSonido;
            etiquetaHUD = "Radio";
            return;
        }

        if (n.StartsWith("washbasin_drawer"))
        {
            tipoInteractivo = TipoInteractivoCasa.CajonDeslizable;
            etiquetaHUD = "Cajón de Baño";
            piezaMovilCajon = transform;
            distanciaAperturaCajon = 0.25f;

            // Determinar hacia dónde abre según el lavabo padre
            string pName = transform.parent != null ? transform.parent.name.ToLowerInvariant() : "";
            if (pName == "washbasin_03")
                direccionDeslizamientoMundial = Vector3.left;  // En Lavandería mira hacia -X
            else if (pName == "washbasin_03.001")
                direccionDeslizamientoMundial = Vector3.right; // En Baño 1 Arriba mira hacia +X
            else
                direccionDeslizamientoMundial = Vector3.back;  // En Baño 2 Arriba (Washbasin_07) mira hacia -Z

            posCerradaMundial = piezaMovilCajon.position;
            rotCerradaMundial = piezaMovilCajon.rotation;
            return;
        }

        if (n.StartsWith("chest_drawers") || n.StartsWith("tool_cabinet"))
        {
            tipoInteractivo = TipoInteractivoCasa.CajonDeslizable;
            etiquetaHUD = n.StartsWith("tool_cabinet") ? "Cajón de Herramientas" : "Cajón de Cómoda";
            distanciaAperturaCajon = 0.28f;

            // Crear o reutilizar un cajón superior móvil con el mismo material de la cómoda
            direccionDeslizamientoMundial = CalcularDireccionFrontalMueble(b);
            piezaMovilCajon = CrearOActualizarCajonVisualEnComoda(b, mr != null ? mr.sharedMaterial : null, direccionDeslizamientoMundial);
            posCerradaMundial = piezaMovilCajon.position;
            rotCerradaMundial = piezaMovilCajon.rotation;
            return;
        }

        // Puertas de electrodomésticos y gabinetes
        tipoInteractivo = TipoInteractivoCasa.PuertaElectrodomesticoOGabinete;
        posCerradaMundial = transform.position;
        rotCerradaMundial = transform.rotation;
        puntoBisagraMundial = new Vector3(transform.position.x, b.center.y, transform.position.z);

        if (n.StartsWith("refrigerator_door"))
        {
            etiquetaHUD = n.Contains("_01") ? "Congelador" : "Refrigerador";
            anguloAperturaPuerta = 88f; // Abre hacia afuera (hacia la cocina), nunca hacia adentro del refrigerador
            CrearLuzInteriorElectrodomestico(new Color(0.82f, 0.94f, 1f), 1.3f, 1.8f, new Vector3(-0.25f, 0f, 0.35f));
        }
        else if (n.StartsWith("microwave_door"))
        {
            etiquetaHUD = "Microondas";
            anguloAperturaPuerta = 82f; // Invertido a +82° para que abra hacia afuera (hacia el jugador) y nunca hacia adentro del microondas
            CrearLuzInteriorElectrodomestico(new Color(1f, 0.85f, 0.45f), 1.1f, 1.1f, new Vector3(-0.15f, 0f, 0.15f));
        }
        else if (n.StartsWith("washing_machine_d"))
        {
            etiquetaHUD = "Lavadora";
            if (n.Contains("_02"))
            {
                // Lavadora 2: tapa superior que se levanta hacia arriba sobre su bisagra trasera
                puntoBisagraMundial = new Vector3(b.max.x, b.center.y, b.center.z);
                ejeGiroLocalOMundial = Vector3.forward;
                anguloAperturaPuerta = -72f;
            }
            else
            {
                anguloAperturaPuerta = 85f;
            }
        }
        else if (n.StartsWith("shower_door"))
        {
            etiquetaHUD = "Puerta de Regadera";
            puntoBisagraMundial = new Vector3(b.center.x, b.center.y, b.max.z);
            anguloAperturaPuerta = 82f;
        }
        else if (n.StartsWith("bathroom_cabinet_door"))
        {
            etiquetaHUD = "Gabinete de Baño";
            // Bathroom_cabinet (abajo) y Bathroom_cabinet.001 (arriba) tienen escalas opuestas; abrir siempre hacia afuera de la pared
            bool esArriba = b.center.y > 3.15f;
            bool esPuerta01 = n.Contains("_01");
            anguloAperturaPuerta = (esArriba == esPuerta01) ? 85f : -85f;
        }
        else if (n.StartsWith("washbasin_door"))
        {
            etiquetaHUD = "Puerta de Lavabo";
            bool esArriba = b.center.y > 3.15f;
            bool esPuerta03 = n.Contains("03");
            anguloAperturaPuerta = (esArriba == esPuerta03) ? 85f : -85f;
        }
    }

    private Vector3 CalcularDireccionFrontalMueble(Bounds b)
    {
        // Las cómodas están pegadas a una pared; el cajón debe deslizarse alejándose de la pared más cercana
        Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        Vector3 mejorDirHaciaCuarto = Vector3.forward;
        float menorDistPared = float.MaxValue;

        foreach (Vector3 d in dirs)
        {
            if (Physics.Raycast(b.center, d, out RaycastHit hit, 1.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != transform && !hit.transform.IsChildOf(transform) && hit.distance < menorDistPared)
                {
                    menorDistPared = hit.distance;
                    mejorDirHaciaCuarto = -d;
                }
            }
        }

        return mejorDirHaciaCuarto;
    }

    private Transform CrearOActualizarCajonVisualEnComoda(Bounds b, Material matMueble, Vector3 dirApertura)
    {
        Transform existente = transform.Find("Cajon_Superior_Interactivo_VR");
        if (existente != null) return existente;

        GameObject contenedor = new GameObject("Cajon_Superior_Interactivo_VR");
        contenedor.transform.SetParent(transform, true);

        // Posicionar el cajón en el tercio superior frontal de la cómoda
        Vector3 posCajon = new Vector3(
            b.center.x,
            b.min.y + b.size.y * 0.74f,
            b.center.z
        ) + dirApertura * 0.015f;

        contenedor.transform.position = posCajon;
        contenedor.transform.rotation = Quaternion.LookRotation(dirApertura, Vector3.up);

        // Escalado positivo mundial compensando lossyScale del padre
        Vector3 ls = transform.lossyScale;
        contenedor.transform.localScale = new Vector3(
            Mathf.Abs(ls.x) > 0.0001f ? 1f / ls.x : 1f,
            Mathf.Abs(ls.y) > 0.0001f ? 1f / ls.y : 1f,
            Mathf.Abs(ls.z) > 0.0001f ? 1f / ls.z : 1f
        );

        bool orientadoEnX = Mathf.Abs(dirApertura.x) > 0.5f;
        float anchoFrente = orientadoEnX ? b.size.z * 0.86f : b.size.x * 0.86f;
        float profCajon = orientadoEnX ? b.size.x * 0.78f : b.size.z * 0.78f;
        float altoCajon = Mathf.Clamp(b.size.y * 0.22f, 0.14f, 0.24f);

        GameObject frente = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frente.name = "Frente_Cajon";
        Destroy(frente.GetComponent<BoxCollider>());
        frente.transform.SetParent(contenedor.transform, false);
        frente.transform.localPosition = Vector3.zero;
        frente.transform.localRotation = Quaternion.identity;
        frente.transform.localScale = new Vector3(anchoFrente, altoCajon, profCajon);

        if (matMueble != null)
            frente.GetComponent<MeshRenderer>().sharedMaterial = matMueble;

        // Pequeña jaladera/manija en el frente del cajón
        GameObject manija = GameObject.CreatePrimitive(PrimitiveType.Cube);
        manija.name = "Manija_Cajon";
        Destroy(manija.GetComponent<BoxCollider>());
        manija.transform.SetParent(contenedor.transform, false);
        manija.transform.localPosition = new Vector3(0f, 0f, profCajon * 0.5f + 0.015f);
        manija.transform.localRotation = Quaternion.identity;
        manija.transform.localScale = new Vector3(Mathf.Min(0.14f, anchoFrente * 0.25f), 0.025f, 0.03f);

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material matMetal = new Material(sh) { color = new Color(0.22f, 0.22f, 0.24f, 1f) };
        manija.GetComponent<MeshRenderer>().sharedMaterial = matMetal;

        return contenedor.transform;
    }

    private void CrearLuzInteriorElectrodomestico(Color colorLuz, float intensidad, float rango, Vector3 offsetMundial)
    {
        Transform luzT = transform.Find("Luz_Interior_VR");
        if (luzT == null)
        {
            GameObject luzObj = new GameObject("Luz_Interior_VR");
            luzObj.transform.SetParent(transform, true);
            MeshRenderer mr = GetComponent<MeshRenderer>();
            Vector3 c = mr != null ? mr.bounds.center : transform.position;
            luzObj.transform.position = c + offsetMundial;
            luzInteriorOPantalla = luzObj.AddComponent<Light>();
            luzInteriorOPantalla.type = LightType.Point;
            luzInteriorOPantalla.color = colorLuz;
            luzInteriorOPantalla.intensity = intensidad;
            luzInteriorOPantalla.range = rango;
            luzInteriorOPantalla.shadows = LightShadows.None;
            luzInteriorOPantalla.enabled = false;
        }
        else
        {
            luzInteriorOPantalla = luzT.GetComponent<Light>();
        }
    }

    private void PrepararPantallaTelevisor(Bounds b)
    {
        Transform existente = transform.Find("Pantalla_Interactiva_TV");
        if (existente != null)
        {
            quadPantallaTV = existente.gameObject;
            rendererPantallaTV = quadPantallaTV.GetComponent<MeshRenderer>();
        }
        else
        {
            quadPantallaTV = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadPantallaTV.name = "Pantalla_Interactiva_TV";
            Destroy(quadPantallaTV.GetComponent<Collider>());
            quadPantallaTV.transform.SetParent(transform, true);

            // Todas las TVs (TV_03, TV_03.001, TV_04) están montadas mirando hacia +X en coordenadas de mundo
            Vector3 normalPantalla = (b.size.x < b.size.z) ? Vector3.right : Vector3.forward;
            float anchoPantalla = (b.size.x < b.size.z) ? b.size.z * 0.90f : b.size.x * 0.90f;
            float altoPantalla = b.size.y * 0.78f;

            quadPantallaTV.transform.position = new Vector3(
                b.max.x + 0.008f,
                b.center.y + 0.04f,
                b.center.z
            );
            // El frente de un Quad en Unity mira hacia -Z local, por lo que LookRotation(-normalPantalla) hace que sea visible mirando hacia la TV
            quadPantallaTV.transform.rotation = Quaternion.LookRotation(-normalPantalla, Vector3.up);

            Vector3 ls = transform.lossyScale;
            quadPantallaTV.transform.localScale = new Vector3(
                anchoPantalla / Mathf.Max(0.001f, Mathf.Abs(ls.x)),
                altoPantalla / Mathf.Max(0.001f, Mathf.Abs(ls.y)),
                1f
            );

            rendererPantallaTV = quadPantallaTV.GetComponent<MeshRenderer>();
        }

        Shader shUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
        materialPantallaTV = new Material(shUnlit)
        {
            name = "Mat_Pantalla_TV_GifVideo",
            color = Color.white
        };

        AsegurarFramesGifProcedurales();
        Texture2D texInicial = (framesGifPersonalizado != null && framesGifPersonalizado.Length > 0 && framesGifPersonalizado[0] != null)
            ? framesGifPersonalizado[0]
            : framesGifProcedural[0];

        materialPantallaTV.SetTexture("_BaseMap", texInicial);
        materialPantallaTV.mainTexture = texInicial;
        if (rendererPantallaTV != null)
            rendererPantallaTV.sharedMaterial = materialPantallaTV;

        CrearLuzInteriorElectrodomestico(new Color(0.55f, 0.78f, 1f), 1.4f, 3.2f, new Vector3(0.28f, 0f, 0f));

        if (videoClipPersonalizado != null)
        {
            videoPlayerTV = quadPantallaTV.GetComponent<VideoPlayer>();
            if (videoPlayerTV == null)
                videoPlayerTV = quadPantallaTV.AddComponent<VideoPlayer>();
            videoPlayerTV.playOnAwake = false;
            videoPlayerTV.isLooping = true;
            videoPlayerTV.clip = videoClipPersonalizado;
            videoPlayerTV.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayerTV.targetMaterialRenderer = rendererPantallaTV;
            videoPlayerTV.targetMaterialProperty = "_BaseMap";
        }

        quadPantallaTV.SetActive(estaActivoOAbierto);
    }

    private void ConstruirHitboxPositivo()
    {
        BoxCollider bcDirecto = GetComponent<BoxCollider>();
        if (bcDirecto != null)
            bcDirecto.enabled = false;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        Bounds b = mr != null ? mr.bounds : new Bounds(transform.position, Vector3.one * 0.35f);

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

        hitboxPositivo.isTrigger = true;
        hitboxPositivo.center = Vector3.zero;
        hitboxPositivo.size = new Vector3(
            Mathf.Max(b.size.x, 0.24f),
            Mathf.Max(b.size.y, 0.24f),
            Mathf.Max(b.size.z, 0.24f)
        );
        hitboxPositivo.enabled = true;

        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null)
            interactable = gameObject.AddComponent<XRSimpleInteractable>();

        interactable.colliders.Clear();
        interactable.colliders.Add(hitboxPositivo);
    }

    private void AsegurarAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 0.6f;
        audioSource.maxDistance = 8f;

#if UNITY_EDITOR
        if (cacheFridge == null)
            cacheFridge = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-fridge-hum-and-door-68902.mp3");
        if (cacheMicro == null)
            cacheMicro = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-microwave-door-open-90798.mp3");
        if (cacheWash == null)
            cacheWash = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-door-washing-machine-94161.mp3");
        if (cacheCabinet == null)
            cacheCabinet = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-opening-closing-cabinet-door-101970.mp3");
        if (cacheTvStatic == null)
            cacheTvStatic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/yourugor-tv-static-noise-291374.mp3");
#endif
        if (clipRefrigerador == null) clipRefrigerador = cacheFridge;
        if (clipMicroondas == null) clipMicroondas = cacheMicro;
        if (clipLavadora == null) clipLavadora = cacheWash;
        if (clipGabinete == null) clipGabinete = cacheCabinet;
        if (clipEstaticaTV == null) clipEstaticaTV = cacheTvStatic;

        if (tipoInteractivo == TipoInteractivoCasa.TelevisorConGifOVideo || tipoInteractivo == TipoInteractivoCasa.RadioSonido)
        {
            if (audioEstaticaLoop == null)
            {
                GameObject objEst = new GameObject("Emisor_Estatica_TV_VR");
                objEst.transform.SetParent(transform, false);
                audioEstaticaLoop = objEst.AddComponent<AudioSource>();
                audioEstaticaLoop.playOnAwake = false;
                audioEstaticaLoop.spatialBlend = 1f;
                audioEstaticaLoop.rolloffMode = AudioRolloffMode.Linear;
                audioEstaticaLoop.minDistance = 0.6f;
                audioEstaticaLoop.maxDistance = 6.5f;
                audioEstaticaLoop.loop = true;
                audioEstaticaLoop.volume = 0.22f;
            }
            if (clipEstaticaTV != null)
                audioEstaticaLoop.clip = clipEstaticaTV;
        }

        if (clipCajon == null)
        {
#if UNITY_EDITOR
            clipCajon = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/freesound_community-drawer-41055.mp3");
#endif
            if (clipCajon == null && clipCajonReal != null)
                clipCajon = clipCajonReal;
            if (clipCajon == null)
                clipCajon = CrearSonidoProcedural("DrawerSlide", 130f, 95f, 0.18f, 0.35f);
        }
        if (clipElectrodomestico == null)
            clipElectrodomestico = CrearSonidoProcedural("ApplianceLatch", 320f, 180f, 0.12f, 0.4f);
        if (clipBotonTV == null)
            clipBotonTV = CrearSonidoProcedural("TVSwitch", 640f, 880f, 0.08f, 0.35f);
    }

    void OnEnable()
    {
        if (!instanciasActivas.Contains(this))
            instanciasActivas.Add(this);

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
        instanciasActivas.Remove(this);

        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelect);
            interactable.activated.RemoveListener(OnActivate);
        }
    }

    private void OnSelect(SelectEnterEventArgs args)
    {
        if (!InteractorUniversalVR.EstaDentroDeRangoYLineaDeVision(ObtenerPosicionMundial(), 2.0f, transform))
            return;
        if (args.interactorObject is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.45f, 0.10f);
        AlternarEstado();
    }

    private void OnActivate(ActivateEventArgs args)
    {
        if (!InteractorUniversalVR.EstaDentroDeRangoYLineaDeVision(ObtenerPosicionMundial(), 2.0f, transform))
            return;
        if (args.interactorObject is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.45f, 0.10f);
        AlternarEstado();
    }

    void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        if (!InteractorUniversalVR.EstaDentroDeRangoYLineaDeVision(ObtenerPosicionMundial(), 2.0f, transform))
            return;
        AlternarEstado();
    }

    public void AlternarEstado()
    {
        if (Time.unscaledTime - ultimoTiempoInteraccion < 0.35f) return;
        ultimoTiempoInteraccion = Time.unscaledTime;

        bool hayEnergia = InterruptorLuz.EnergiaGeneralCasaActiva;

        // Si es TV o Radio y NO hay energía en la casa, no puede encender (solo suena clic apagado)
        if (!hayEnergia && (tipoInteractivo == TipoInteractivoCasa.TelevisorConGifOVideo || tipoInteractivo == TipoInteractivoCasa.RadioSonido))
        {
            estaActivoOAbierto = false;
            if (audioSource != null && clipBotonTV != null)
            {
                audioSource.pitch = 0.65f;
                audioSource.PlayOneShot(clipBotonTV, 0.55f);
                audioSource.pitch = 1f;
            }
            AplicarEstadoEnergiaGeneral(false);
            return;
        }

        estaActivoOAbierto = !estaActivoOAbierto;

        if (tipoInteractivo == TipoInteractivoCasa.CajonDeslizable)
        {
            AudioClip c = clipCajonReal != null ? clipCajonReal : clipCajon;
            if (audioSource != null && c != null)
                audioSource.PlayOneShot(c, 0.75f);
        }
        else if (tipoInteractivo == TipoInteractivoCasa.PuertaElectrodomesticoOGabinete)
        {
            ReproducirSonidoPuertaElectrodomesticoOGabinete(estaActivoOAbierto);
            if (luzInteriorOPantalla != null)
                luzInteriorOPantalla.enabled = estaActivoOAbierto && hayEnergia;
        }
        else if (tipoInteractivo == TipoInteractivoCasa.TelevisorConGifOVideo)
        {
            if (audioSource != null && clipBotonTV != null)
                audioSource.PlayOneShot(clipBotonTV);
            if (quadPantallaTV != null)
                quadPantallaTV.SetActive(estaActivoOAbierto);
            if (luzInteriorOPantalla != null)
                luzInteriorOPantalla.enabled = estaActivoOAbierto;

            if (audioEstaticaLoop != null && clipEstaticaTV != null)
            {
                audioEstaticaLoop.clip = clipEstaticaTV;
                if (estaActivoOAbierto)
                {
                    audioEstaticaLoop.volume = 0.28f;
                    audioEstaticaLoop.Play();
                }
                else if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(RutinaDesvanecerAudio(audioEstaticaLoop, 0.14f));
                }
                else
                {
                    audioEstaticaLoop.Stop();
                }
            }

            if (videoPlayerTV != null && videoClipPersonalizado != null)
            {
                if (estaActivoOAbierto) videoPlayerTV.Play();
                else videoPlayerTV.Pause();
            }
        }
        else if (tipoInteractivo == TipoInteractivoCasa.RadioSonido)
        {
            if (audioSource != null && clipBotonTV != null)
                audioSource.PlayOneShot(clipBotonTV);
            if (audioEstaticaLoop != null && clipEstaticaTV != null)
            {
                audioEstaticaLoop.clip = clipEstaticaTV;
                if (estaActivoOAbierto)
                {
                    audioEstaticaLoop.volume = 0.26f;
                    audioEstaticaLoop.Play();
                }
                else if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(RutinaDesvanecerAudio(audioEstaticaLoop, 0.14f));
                }
                else
                {
                    audioEstaticaLoop.Stop();
                }
            }
        }
    }

    private void ReproducirSonidoPuertaElectrodomesticoOGabinete(bool abriendo)
    {
        if (audioSource == null) return;
        string n = gameObject.name.ToLowerInvariant();

        if (n.StartsWith("refrigerator_door") && clipRefrigerador != null)
        {
            // En fridge-hum-and-door: 0.08s = abrir sello del refri; 44.92s = golpe real al cerrar la puerta con cola acústica completa
            ReproducirSegmentoExacto(clipRefrigerador, abriendo ? 0.08f : 44.92f, abriendo ? 1.25f : 1.65f, 0.82f);
            return;
        }
        if (n.StartsWith("microwave_door") && clipMicroondas != null)
        {
            // En microwave-door-open: 0.24s = abrir puerta; 3.40s = clic real al cerrar con decaimiento natural
            ReproducirSegmentoExacto(clipMicroondas, abriendo ? 0.24f : 3.40f, abriendo ? 1.08f : 1.02f, 0.80f);
            return;
        }
        if (n.StartsWith("washing_machine_d") && clipLavadora != null)
        {
            // En door-washing-machine: el pestillo empieza en 0.70s + resonancia natural sin cortar en seco
            ReproducirSegmentoExacto(clipLavadora, 0.70f, 1.32f, 0.82f);
            return;
        }
        if (clipGabinete != null)
        {
            // En opening-closing-cabinet-door: 2.22s = abrir gabinete; 6.90s = cerrar gabinete con cola suave
            ReproducirSegmentoExacto(clipGabinete, abriendo ? 2.22f : 6.90f, abriendo ? 1.04f : 0.96f, 0.80f);
            return;
        }

        if (clipElectrodomestico != null)
            audioSource.PlayOneShot(clipElectrodomestico, 0.75f);
    }

    private void ReproducirSegmentoExacto(AudioClip clip, float inicioSeg, float duracionSeg, float volumen)
    {
        if (audioSource == null || clip == null) return;
        if (rutinaRecorteAudio != null)
        {
            StopCoroutine(rutinaRecorteAudio);
            rutinaRecorteAudio = null;
        }

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.pitch = Random.Range(0.96f, 1.04f);
        audioSource.volume = volumen;
        audioSource.time = Mathf.Clamp(inicioSeg, 0f, Mathf.Max(0f, clip.length - 0.05f));
        audioSource.Play();

        if (gameObject.activeInHierarchy)
            rutinaRecorteAudio = StartCoroutine(RutinaDetenerSegmentoAudio(duracionSeg, volumen));
    }

    private IEnumerator RutinaDetenerSegmentoAudio(float duracion, float volInicial)
    {
        float fadeSeg = Mathf.Min(0.30f, duracion * 0.35f);
        float esperaPlena = Mathf.Max(0.05f, duracion - fadeSeg);
        yield return new WaitForSeconds(esperaPlena);

        float t = 0f;
        while (t < fadeSeg && audioSource != null && audioSource.isPlaying)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeSeg);
            audioSource.volume = volInicial * (0.5f * (1f + Mathf.Cos(p * Mathf.PI)));
            yield return null;
        }

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
        if (audioSource != null)
            audioSource.volume = volInicial;
        rutinaRecorteAudio = null;
    }

    private static IEnumerator RutinaDesvanecerAudio(AudioSource src, float duracion)
    {
        if (src == null || !src.isPlaying) yield break;
        float v0 = src.volume;
        float t = 0f;
        while (t < duracion && src != null && src.isPlaying)
        {
            t += Time.deltaTime;
            src.volume = Mathf.Lerp(v0, 0f, t / duracion);
            yield return null;
        }
        if (src != null)
        {
            src.Stop();
            src.volume = v0;
        }
    }

    void Update()
    {
        if (tipoInteractivo == TipoInteractivoCasa.CajonDeslizable)
        {
            if (piezaMovilCajon == null) return;
            float objetivo = estaActivoOAbierto ? 1f : 0f;
            if (Mathf.Abs(progresoActual - objetivo) > 0.005f)
            {
                progresoActual = Mathf.MoveTowards(progresoActual, objetivo, velocidadCajon * Time.deltaTime / Mathf.Max(0.05f, distanciaAperturaCajon));
                piezaMovilCajon.position = posCerradaMundial + direccionDeslizamientoMundial * (progresoActual * distanciaAperturaCajon);
            }
        }
        else if (tipoInteractivo == TipoInteractivoCasa.PuertaElectrodomesticoOGabinete)
        {
            float objetivo = estaActivoOAbierto ? anguloAperturaPuerta : 0f;
            if (Mathf.Abs(progresoActual - objetivo) > 0.05f)
            {
                progresoActual = Mathf.MoveTowards(progresoActual, objetivo, velocidadGiroPuerta * Time.deltaTime);
                Vector3 eje = ejeGiroLocalOMundial.sqrMagnitude > 0.01f ? ejeGiroLocalOMundial : Vector3.up;
                Quaternion rotGiro = Quaternion.AngleAxis(progresoActual, eje);
                Vector3 offset = posCerradaMundial - puntoBisagraMundial;
                transform.position = puntoBisagraMundial + (rotGiro * offset);
                transform.rotation = rotGiro * rotCerradaMundial;
            }
        }
        else if (tipoInteractivo == TipoInteractivoCasa.TelevisorConGifOVideo && estaActivoOAbierto)
        {
            if (videoPlayerTV != null && videoClipPersonalizado != null) return;

            Texture2D[] frames = (framesGifPersonalizado != null && framesGifPersonalizado.Length > 0 && framesGifPersonalizado[0] != null)
                ? framesGifPersonalizado
                : framesGifProcedural;

            if (frames != null && frames.Length > 0 && materialPantallaTV != null)
            {
                temporizadorGif += Time.deltaTime;
                float duracionFrame = 1f / Mathf.Max(1f, fpsGif);
                if (temporizadorGif >= duracionFrame)
                {
                    temporizadorGif = 0f;
                    frameGifActual = (frameGifActual + 1) % frames.Length;
                    Texture2D tex = frames[frameGifActual];
                    if (tex != null)
                    {
                        materialPantallaTV.SetTexture("_BaseMap", tex);
                        materialPantallaTV.mainTexture = tex;
                    }
                }
            }
        }
    }

    private static void AsegurarFramesGifProcedurales()
    {
        if (framesGifProcedural != null && framesGifProcedural.Length > 0 && framesGifProcedural[0] != null)
            return;

        // Genera un GIF animado retro de 12 cuadros (64x48 px) con un personaje bailando sobre barras de colores de TV
        int numFrames = 12;
        int w = 64;
        int h = 48;
        framesGifProcedural = new Texture2D[numFrames];

        Color[] barras = new Color[]
        {
            new Color(0.85f, 0.85f, 0.85f),
            new Color(0.90f, 0.85f, 0.15f),
            new Color(0.15f, 0.85f, 0.90f),
            new Color(0.15f, 0.85f, 0.25f),
            new Color(0.85f, 0.20f, 0.85f),
            new Color(0.90f, 0.18f, 0.18f),
            new Color(0.18f, 0.25f, 0.90f)
        };

        for (int f = 0; f < numFrames; f++)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[w * h];
            int bounceY = Mathf.RoundToInt(Mathf.Abs(Mathf.Sin(f * Mathf.PI / 3f)) * 5f);
            int offsetX = Mathf.RoundToInt(Mathf.Sin(f * Mathf.PI / 6f) * 8f);
            int cx = (w / 2) + offsetX;
            int cy = (h / 2) + bounceY;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int barraIdx = Mathf.Clamp((x * barras.Length) / w, 0, barras.Length - 1);
                    Color col = barras[barraIdx] * 0.45f;

                    // Scanlines de TV
                    if ((y + f) % 3 == 0) col *= 0.78f;

                    // Silueta pixel-art animada en el centro (personaje meme bailando)
                    int dx = x - cx;
                    int dy = y - cy;
                    bool esCabeza = (dx * dx + (dy - 6) * (dy - 6)) <= 36;
                    bool esCuerpo = Mathf.Abs(dx) <= 4 && dy >= -7 && dy <= 1;
                    int brazoY = (f % 2 == 0) ? (dy - 2) : (-dy - 2);
                    bool esBrazos = Mathf.Abs(dx) <= 10 && Mathf.Abs(dx) > 4 && Mathf.Abs(brazoY) <= 2;

                    if (esCabeza || esCuerpo || esBrazos)
                    {
                        col = new Color(0.98f, 0.96f, 0.92f, 1f);
                        // Ojos y sonrisa del personaje
                        if (esCabeza && ((Mathf.Abs(dx) == 2 && dy == 7) || (Mathf.Abs(dx) <= 2 && dy == 4)))
                            col = new Color(0.08f, 0.08f, 0.12f, 1f);
                    }

                    pixels[y * w + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            framesGifProcedural[f] = tex;
        }
    }

    private static AudioClip CrearSonidoProcedural(string nombre, float freqInicio, float freqFin, float duracion, float volumen)
    {
        int sampleRate = 22050;
        int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duracion));
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float p = (float)i / samples;
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(freqInicio, freqFin, p);
            float env = Mathf.Exp(-p * 3.5f);
            float noise = (Random.value * 2f - 1f) * 0.25f;
            data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.75f + noise) * env * volumen;
        }
        AudioClip clip = AudioClip.Create(nombre, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

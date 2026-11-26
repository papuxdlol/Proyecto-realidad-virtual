using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(XRSimpleInteractable))]
public class InterruptorLuz : MonoBehaviour
{
    public const float IntensidadPiso1 = 2.4f;
    public const float RangoPiso1 = 5.8f;
    public const float IntensidadPiso2 = 1.75f;
    public const float RangoPiso2 = 4.8f;

    public static bool EnergiaGeneralCasaActiva = true;
    public static float FactorVoltajeGlobal = 1.0f;
    public static bool EnEventoHoraMuerta3AM = false;

    // Tabla determinista 1 a 1: cada uno de los 14 switches tiene su propio foco único (0 repeticiones)
    private static readonly Dictionary<string, string> MapaExactoSwitchALuz = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Light_switch",     "focus02"   },
        { "Light_switch.001", "focus0202" },
        { "Light_switch.003", "focus0204" },
        { "Light_switch.004", "focus0203" },
        { "Light_switch.005", "focus0105" },
        { "Light_switch.006", "focus0205" },
        { "Light_switch.007", "focus0102" },
        { "Light_switch.008", "focus0104" },
        { "Light_switch.009", "focus0209" },
        { "Light_switch.010", "focus0208" },
        { "Light_switch.011", "focus0207" },
        { "Light_switch.012", "focus0212" },
        { "Light_switch.013", "focus0206" },
        { "Light_switch.014", "focus0211" }
    };

    [Header("Conexiones (100% únicas por Switch, sin repetir)")]
    public Light luzDelCuarto;
    public List<Light> lucesExtraDelCuarto = new List<Light>();
    public AudioSource sonidoClick;

    [Header("Respuesta y Delay")]
    public float delayConmutacion = 0.12f;
    public float cooldownInteraccion = 0.40f;

    [Header("Estado")]
    public bool estaEncendida = true;
    public bool bloqueadaPorAnomalia = false;
    public bool parpadeoAnomaloActivo = false;

    private XRSimpleInteractable interactable;
    private BoxCollider hitboxPositivo;
    private Quaternion rotInicialSwitch;
    private Coroutine rutinaDelayLuz;
    private int ultimaInteraccionFrame = -1;
    private float ultimoTiempoInteraccion = -10f;
    private float proximoCambioParpadeo = 0f;
    private float intensidadBaseLuz = IntensidadPiso1;

    private static AudioClip clipClickProcedural;
    private static int ultimoFrameAsignacionGlobal = -1;
    private static readonly Dictionary<Light, Renderer> mapaLuzABulbo3D = new Dictionary<Light, Renderer>();
    private static MaterialPropertyBlock bloquePropiedadBulbo;

    private struct ParSwitchLuz
    {
        public InterruptorLuz sw;
        public Light luz;
        public float distXZ;
    }

    void Awake()
    {
        if (PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(transform))
        {
            Destroy(this);
            return;
        }

        rotInicialSwitch = transform.localRotation;

        Transform luzVieja = transform.Find("Luz_Real_Cuarto");
        if (luzVieja != null)
            Destroy(luzVieja.gameObject);

        HabilitarLucesAdicionalesEnURP();
        ConfigurarColliderPositivoEInteractable();
        AsegurarAudioClick();
        AsignarLucesSinRepeticionGlobalmente();

        if (luzDelCuarto != null)
            intensidadBaseLuz = luzDelCuarto.transform.position.y > 3.6f ? IntensidadPiso2 : IntensidadPiso1;

        estaEncendida = true;
        AplicarEstadoALuces(estaEncendida && EnergiaGeneralCasaActiva);
    }

    void Update()
    {
        // Coherencia eléctrica total: si hay apagón general, NINGÚN foco puede parpadear ni quedar encendido
        if (!EnergiaGeneralCasaActiva)
        {
            if (luzDelCuarto != null && luzDelCuarto.enabled)
                AplicarEstadoALuces(false);
            return;
        }

        // Si hay una anomalía Electrical en este cuarto (y sí hay corriente), hacer que la luz parpadee
        if (parpadeoAnomaloActivo && luzDelCuarto != null)
        {
            if (Time.time >= proximoCambioParpadeo)
            {
                proximoCambioParpadeo = Time.time + Random.Range(0.04f, 0.24f);
                bool encendidaMomento = Random.value > 0.28f;
                float factor = encendidaMomento ? Random.Range(0.25f, 1.45f) : 0.03f;

                luzDelCuarto.enabled = true;
                luzDelCuarto.intensity = intensidadBaseLuz * factor * FactorVoltajeGlobal;
                ActualizarVisualBulbo3D(luzDelCuarto, encendidaMomento);
            }
        }
        else if (EnEventoHoraMuerta3AM && estaEncendida && luzDelCuarto != null && luzDelCuarto.enabled)
        {
            // Durante La Hora Muerta (03:00 AM), aplicar caída de voltaje ámbar fluctuante
            Color colNormal = luzDelCuarto.transform.position.y > 3.6f ? new Color(1.0f, 0.89f, 0.74f) : new Color(1.0f, 0.90f, 0.76f);
            Color colAmbar = new Color(1.0f, 0.44f, 0.16f);
            luzDelCuarto.intensity = intensidadBaseLuz * FactorVoltajeGlobal;
            luzDelCuarto.color = Color.Lerp(colAmbar, colNormal, Mathf.Clamp01((FactorVoltajeGlobal - 0.22f) / 0.78f));
        }
    }

    public void ActivarParpadeoAnomalo(bool activo)
    {
        parpadeoAnomaloActivo = activo;
        bloqueadaPorAnomalia = activo;

        if (!activo && luzDelCuarto != null)
        {
            intensidadBaseLuz = luzDelCuarto.transform.position.y > 3.6f ? IntensidadPiso2 : IntensidadPiso1;
            luzDelCuarto.intensity = intensidadBaseLuz;
            AplicarEstadoALuces(estaEncendida && EnergiaGeneralCasaActiva);
        }
    }

    public static void HabilitarLucesAdicionalesEnURP()
    {
        ForzarPipelineAssetLuces(QualitySettings.renderPipeline);
        ForzarPipelineAssetLuces(GraphicsSettings.currentRenderPipeline);
        ForzarPipelineAssetLuces(GraphicsSettings.defaultRenderPipeline);
    }

    private static void ForzarPipelineAssetLuces(RenderPipelineAsset asset)
    {
        if (asset == null) return;
        System.Type t = asset.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        FieldInfo fMode = t.GetField("m_AdditionalLightsRenderingMode", flags);
        if (fMode != null && fMode.FieldType.IsEnum)
            fMode.SetValue(asset, System.Enum.ToObject(fMode.FieldType, 1));

        FieldInfo fLimit = t.GetField("m_AdditionalLightsPerObjectLimit", flags);
        if (fLimit != null)
            fLimit.SetValue(asset, 8);

        FieldInfo fMaxPixel = t.GetField("m_MaxPixelLights", flags);
        if (fMaxPixel != null)
            fMaxPixel.SetValue(asset, 8);
    }

    public static void AsignarLucesSinRepeticionGlobalmente()
    {
        if (Application.isPlaying && ultimoFrameAsignacionGlobal == Time.frameCount)
            return;
        ultimoFrameAsignacionGlobal = Time.frameCount;

        GameObject contenedorLuces = GameObject.Find("Contenedor_Luces_Techo");
        if (contenedorLuces == null)
            contenedorLuces = new GameObject("Contenedor_Luces_Techo");

        Light[] todasLasLuces = FindObjectsByType<Light>(FindObjectsInactive.Include);
        Dictionary<string, Light> lucesPorNombre = new Dictionary<string, Light>(System.StringComparer.OrdinalIgnoreCase);
        List<Light> lucesPiso1 = new List<Light>();
        List<Light> lucesPiso2 = new List<Light>();

        foreach (Light l in todasLasLuces)
        {
            if (l == null || (l.type != LightType.Point && l.type != LightType.Spot)) continue;
            if (l.name.Contains("Fusibles") || l.name.Contains("Unknown") || l.name.Contains("Electrico") || l.name.Contains("Tulpa") || l.name.Contains("Tablet") || l.name.Contains("Interior") || l.name.Contains("Halo_Techo"))
                continue;
            if (l.name.Contains("Luz_Real_Cuarto"))
            {
                if (Application.isPlaying) Destroy(l.gameObject);
                else DestroyImmediate(l.gameObject);
                continue;
            }

            if (l.transform.parent != null && l.transform.parent.name.ToLowerInvariant().StartsWith("focus"))
            {
                Renderer rFoco = l.transform.parent.GetComponent<Renderer>();
                Vector3 posFoco = rFoco != null ? rFoco.bounds.center : l.transform.parent.position;
                l.transform.SetParent(contenedorLuces.transform, true);
                l.transform.position = posFoco;
                l.transform.localScale = Vector3.one;
            }

            Vector3 pos = l.transform.position;
            bool esSegundoPiso = pos.y > 3.6f;

            if (!esSegundoPiso && pos.y > 2.70f && pos.y < 4.30f)
                pos.y = 2.52f;
            else if (esSegundoPiso && pos.y > 5.85f)
                pos.y = 5.75f;
            l.transform.position = pos;

            // Convertir en lámpara arquitectónica real: cono ancho hacia abajo (evita mancha blanca en techo y evita atravesar muros laterales)
            l.gameObject.SetActive(true);
            l.type = LightType.Spot;
            l.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            l.spotAngle = 144f;
            l.innerSpotAngle = 76f;
            l.renderMode = LightRenderMode.ForcePixel;
            l.shadows = LightShadows.None;
            l.intensity = esSegundoPiso ? IntensidadPiso2 : IntensidadPiso1;
            l.range = esSegundoPiso ? RangoPiso2 : RangoPiso1;
            l.color = esSegundoPiso ? new Color(1.0f, 0.89f, 0.74f) : new Color(1.0f, 0.90f, 0.76f);
            l.cullingMask = ~0;

            // Añadir un halo de relleno corto (2.4m) justo bajo el foco para iluminar suavemente el techo sin cruzar paredes
            AsegurarHaloSuaveDeTecho(l);

            lucesPorNombre[l.name.Trim()] = l;
            if (esSegundoPiso) lucesPiso2.Add(l);
            else lucesPiso1.Add(l);
        }

        InterruptorLuz[] todosSwitches = FindObjectsByType<InterruptorLuz>(FindObjectsInactive.Include);
        HashSet<Light> lucesYaAsignadas = new HashSet<Light>();
        List<InterruptorLuz> sinMapaExplicitoP1 = new List<InterruptorLuz>();
        List<InterruptorLuz> sinMapaExplicitoP2 = new List<InterruptorLuz>();

        foreach (InterruptorLuz sw in todosSwitches)
        {
            if (sw == null || PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(sw.transform)) continue;
            sw.lucesExtraDelCuarto.Clear();

            string nombreSw = sw.gameObject.name.Trim();
            if (MapaExactoSwitchALuz.TryGetValue(nombreSw, out string nombreFoco) &&
                lucesPorNombre.TryGetValue(nombreFoco, out Light focoExacto) &&
                focoExacto != null)
            {
                sw.luzDelCuarto = focoExacto;
                lucesYaAsignadas.Add(focoExacto);
            }
            else
            {
                sw.luzDelCuarto = null;
                if (sw.ObtenerPosicionMundial().y > 2.8f) sinMapaExplicitoP2.Add(sw);
                else sinMapaExplicitoP1.Add(sw);
            }
        }

        if (sinMapaExplicitoP1.Count > 0)
            EmparejarRestantesSinRepeticion(sinMapaExplicitoP1, lucesPiso1, lucesYaAsignadas);
        if (sinMapaExplicitoP2.Count > 0)
            EmparejarRestantesSinRepeticion(sinMapaExplicitoP2, lucesPiso2, lucesYaAsignadas);

        // Vincular los 4 focos restantes (ej. focus01, focus0101, focus0103 del porche exterior, focus0210)
        // como lucesExtraDelCuarto del switch más cercano de su mismo piso (sin repetir en ningún otro switch)
        AsignarFocosHuerfanosASwitchMasCercano(todosSwitches, lucesPiso1, false, lucesYaAsignadas);
        AsignarFocosHuerfanosASwitchMasCercano(todosSwitches, lucesPiso2, true, lucesYaAsignadas);

        // Mapear cada Light con su malla 3D de foco (Focus_*) para oscurecer el bulbo cuando la luz se apaga
        ConstruirMapaLuzABulbo3D(lucesPiso1, lucesPiso2);
    }

    private static void AsignarFocosHuerfanosASwitchMasCercano(
        InterruptorLuz[] todosSwitches,
        List<Light> lucesPiso,
        bool esPiso2,
        HashSet<Light> lucesYaAsignadas)
    {
        for (int i = 0; i < lucesPiso.Count; i++)
        {
            Light luzHuerfana = lucesPiso[i];
            if (luzHuerfana == null || lucesYaAsignadas.Contains(luzHuerfana)) continue;

            InterruptorLuz mejorSw = null;
            float menorDist = float.MaxValue;
            Vector3 pLuz = luzHuerfana.transform.position;

            for (int s = 0; s < todosSwitches.Length; s++)
            {
                InterruptorLuz sw = todosSwitches[s];
                if (sw == null || PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(sw.transform)) continue;
                bool swEnPiso2 = sw.ObtenerPosicionMundial().y > 2.8f;
                if (swEnPiso2 != esPiso2) continue;

                float d = Vector3.Distance(sw.ObtenerPosicionMundial(), pLuz);
                if (d < menorDist)
                {
                    menorDist = d;
                    mejorSw = sw;
                }
            }

            if (mejorSw != null)
            {
                if (!mejorSw.lucesExtraDelCuarto.Contains(luzHuerfana))
                    mejorSw.lucesExtraDelCuarto.Add(luzHuerfana);
                lucesYaAsignadas.Add(luzHuerfana);
            }
        }
    }

    private static void ConstruirMapaLuzABulbo3D(List<Light> l1, List<Light> l2)
    {
        mapaLuzABulbo3D.Clear();
        GameObject house = GameObject.Find("House");
        if (house == null) return;

        List<Renderer> bulbos = new List<Renderer>();
        foreach (Renderer r in house.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null && r.name.ToLowerInvariant().StartsWith("focus"))
                bulbos.Add(r);
        }

        VincularListaLucesABulbos(l1, bulbos);
        VincularListaLucesABulbos(l2, bulbos);
    }

    private static void VincularListaLucesABulbos(List<Light> luces, List<Renderer> bulbos)
    {
        for (int i = 0; i < luces.Count; i++)
        {
            Light l = luces[i];
            if (l == null) continue;
            Renderer mejor = null;
            float menorDist = 1.6f;
            for (int b = 0; b < bulbos.Count; b++)
            {
                float d = Vector3.Distance(l.transform.position, bulbos[b].bounds.center);
                if (d < menorDist)
                {
                    menorDist = d;
                    mejor = bulbos[b];
                }
            }
            if (mejor != null)
                mapaLuzABulbo3D[l] = mejor;
        }
    }

    private static void ActualizarVisualBulbo3D(Light l, bool encendida)
    {
        if (l == null) return;
        if (!mapaLuzABulbo3D.TryGetValue(l, out Renderer r) || r == null) return;

        if (bloquePropiedadBulbo == null)
            bloquePropiedadBulbo = new MaterialPropertyBlock();

        r.GetPropertyBlock(bloquePropiedadBulbo);
        Color colBase = encendida ? new Color(1.0f, 0.94f, 0.80f, 1f) : new Color(0.14f, 0.14f, 0.16f, 1f);
        Color colEmis = encendida ? new Color(0.95f, 0.85f, 0.65f, 1f) : Color.black;
        bloquePropiedadBulbo.SetColor("_BaseColor", colBase);
        bloquePropiedadBulbo.SetColor("_Color", colBase);
        bloquePropiedadBulbo.SetColor("_EmissionColor", colEmis);
        r.SetPropertyBlock(bloquePropiedadBulbo);
    }

    private static void EmparejarRestantesSinRepeticion(List<InterruptorLuz> switches, List<Light> luces, HashSet<Light> lucesYaAsignadas)
    {
        List<ParSwitchLuz> pares = new List<ParSwitchLuz>();
        foreach (InterruptorLuz sw in switches)
        {
            Vector3 pSw = sw.ObtenerPosicionMundial();
            Vector2 xzSw = new Vector2(pSw.x, pSw.z);
            foreach (Light l in luces)
            {
                if (lucesYaAsignadas.Contains(l)) continue;
                Vector3 pLuz = l.transform.position;
                float d = Vector2.Distance(xzSw, new Vector2(pLuz.x, pLuz.z));
                pares.Add(new ParSwitchLuz { sw = sw, luz = l, distXZ = d });
            }
        }

        pares.Sort((a, b) => a.distXZ.CompareTo(b.distXZ));
        HashSet<InterruptorLuz> swAsignados = new HashSet<InterruptorLuz>();

        foreach (ParSwitchLuz par in pares)
        {
            if (swAsignados.Contains(par.sw) || lucesYaAsignadas.Contains(par.luz))
                continue;

            par.sw.luzDelCuarto = par.luz;
            swAsignados.Add(par.sw);
            lucesYaAsignadas.Add(par.luz);
        }
    }

    public void ConfigurarColliderPositivoEInteractable()
    {
        BoxCollider colliderDirecto = GetComponent<BoxCollider>();
        if (colliderDirecto != null)
            colliderDirecto.enabled = false;

        Transform hijoHitbox = transform.Find("Hitbox_VR_Positivo");
        if (hijoHitbox == null)
        {
            GameObject objHitbox = new GameObject("Hitbox_VR_Positivo");
            objHitbox.layer = 0;
            hijoHitbox = objHitbox.transform;
            hijoHitbox.SetParent(transform, false);
        }

        MeshRenderer mr = GetComponent<MeshRenderer>();
        Vector3 centroMundial = mr != null ? mr.bounds.center : transform.position;
        hijoHitbox.position = centroMundial;
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
        hitboxPositivo.size = new Vector3(0.24f, 0.24f, 0.24f);
        hitboxPositivo.enabled = true;

        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null)
            interactable = gameObject.AddComponent<XRSimpleInteractable>();

        interactable.colliders.Clear();
        interactable.colliders.Add(hitboxPositivo);

        if (Application.isPlaying && interactable.interactionManager != null && interactable.isActiveAndEnabled)
        {
            XRInteractionManager mgr = interactable.interactionManager;
            mgr.UnregisterInteractable(interactable as IXRInteractable);
            mgr.RegisterInteractable(interactable as IXRInteractable);
        }
    }

    private void AsegurarAudioClick()
    {
        if (sonidoClick == null)
        {
            sonidoClick = GetComponent<AudioSource>();
            if (sonidoClick == null)
                sonidoClick = gameObject.AddComponent<AudioSource>();
        }

        sonidoClick.playOnAwake = false;
        sonidoClick.spatialBlend = 1f;
        sonidoClick.minDistance = 0.5f;
        sonidoClick.maxDistance = 8f;

        if (sonidoClick.clip == null)
            sonidoClick.clip = ObtenerClipClick();
    }

    public Vector3 ObtenerPosicionMundial()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null) return mr.bounds.center;
        if (hitboxPositivo != null) return hitboxPositivo.transform.position;
        return transform.position;
    }

    void OnEnable()
    {
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
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelect);
            interactable.activated.RemoveListener(OnActivate);
        }
    }

    void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        ProcesarInteraccion(null);
    }

    private void OnSelect(SelectEnterEventArgs args) => ProcesarInteraccion(args.interactorObject);
    private void OnActivate(ActivateEventArgs args) => ProcesarInteraccion(args.interactorObject);

    private void ProcesarInteraccion(IXRInteractor interactor)
    {
        if (!InteractorUniversalVR.EstaDentroDeRangoYLineaDeVision(ObtenerPosicionMundial(), 1.90f, transform))
            return;

        if (ultimaInteraccionFrame == Time.frameCount) return;
        if (Time.unscaledTime - ultimoTiempoInteraccion < cooldownInteraccion) return;

        ultimaInteraccionFrame = Time.frameCount;
        ultimoTiempoInteraccion = Time.unscaledTime;

        if (sonidoClick != null && sonidoClick.clip != null)
            sonidoClick.PlayOneShot(sonidoClick.clip);

        if (interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.45f, 0.1f);

        if (bloqueadaPorAnomalia || !EnergiaGeneralCasaActiva)
        {
            estaEncendida = !estaEncendida;
            transform.localRotation = rotInicialSwitch * Quaternion.Euler(estaEncendida ? 0f : 12f, 0f, 0f);
            if (!EnergiaGeneralCasaActiva)
                AplicarEstadoALuces(false);
            return;
        }

        if (luzDelCuarto == null)
            AsignarLucesSinRepeticionGlobalmente();

        bool nuevoEstado = !estaEncendida;
        estaEncendida = nuevoEstado;
        transform.localRotation = rotInicialSwitch * Quaternion.Euler(estaEncendida ? 0f : 12f, 0f, 0f);

        if (Application.isPlaying && gameObject.activeInHierarchy && delayConmutacion > 0.01f)
        {
            if (rutinaDelayLuz != null) StopCoroutine(rutinaDelayLuz);
            rutinaDelayLuz = StartCoroutine(RutinaAplicarLuzConDelay(estaEncendida));
        }
        else
        {
            AplicarEstadoALuces(estaEncendida);
        }
    }

    private IEnumerator RutinaAplicarLuzConDelay(bool encender)
    {
        yield return new WaitForSeconds(delayConmutacion);
        AplicarEstadoALuces(encender && EnergiaGeneralCasaActiva && !bloqueadaPorAnomalia);
        rutinaDelayLuz = null;
    }

    public void CambiarEstado(bool nuevoEstado)
    {
        if (rutinaDelayLuz != null)
        {
            StopCoroutine(rutinaDelayLuz);
            rutinaDelayLuz = null;
        }

        estaEncendida = nuevoEstado;
        transform.localRotation = rotInicialSwitch * Quaternion.Euler(estaEncendida ? 0f : 12f, 0f, 0f);
        AplicarEstadoALuces(estaEncendida && EnergiaGeneralCasaActiva);
    }

    public void RefrescarSegunEnergiaGeneral()
    {
        AplicarEstadoALuces(estaEncendida && EnergiaGeneralCasaActiva && !bloqueadaPorAnomalia);
    }

    private void AplicarEstadoALuces(bool encender)
    {
        if (luzDelCuarto != null)
        {
            luzDelCuarto.gameObject.SetActive(true);
            luzDelCuarto.renderMode = LightRenderMode.ForcePixel;
            if (!parpadeoAnomaloActivo)
            {
                luzDelCuarto.intensity = (luzDelCuarto.transform.position.y > 3.6f ? IntensidadPiso2 : IntensidadPiso1) * FactorVoltajeGlobal;
                luzDelCuarto.color = luzDelCuarto.transform.position.y > 3.6f ? new Color(1.0f, 0.89f, 0.74f) : new Color(1.0f, 0.90f, 0.76f);
            }
            luzDelCuarto.enabled = encender;
            ActualizarHaloHijo(luzDelCuarto, encender);
            ActualizarVisualBulbo3D(luzDelCuarto, encender);
        }

        if (lucesExtraDelCuarto != null)
        {
            for (int i = 0; i < lucesExtraDelCuarto.Count; i++)
            {
                Light l = lucesExtraDelCuarto[i];
                if (l != null)
                {
                    l.gameObject.SetActive(true);
                    l.renderMode = LightRenderMode.ForcePixel;
                    l.enabled = encender;
                    ActualizarHaloHijo(l, encender);
                    ActualizarVisualBulbo3D(l, encender);
                }
            }
        }
    }

    private static void AsegurarHaloSuaveDeTecho(Light luzPrincipal)
    {
        if (luzPrincipal == null) return;
        Transform haloT = luzPrincipal.transform.Find("Halo_Techo_Suave");
        Light halo;
        if (haloT == null)
        {
            GameObject hObj = new GameObject("Halo_Techo_Suave");
            hObj.transform.SetParent(luzPrincipal.transform, false);
            hObj.transform.localPosition = new Vector3(0f, 0f, 0.08f);
            halo = hObj.AddComponent<Light>();
        }
        else
        {
            halo = haloT.GetComponent<Light>();
        }

        if (halo != null)
        {
            halo.type = LightType.Point;
            halo.range = 2.35f;
            halo.intensity = luzPrincipal.transform.position.y > 3.6f ? 0.32f : 0.42f;
            halo.color = luzPrincipal.color;
            halo.shadows = LightShadows.None;
            halo.renderMode = LightRenderMode.ForcePixel;
            halo.enabled = luzPrincipal.enabled;
        }
    }

    private static void ActualizarHaloHijo(Light luzPrincipal, bool encender)
    {
        if (luzPrincipal == null) return;
        Transform haloT = luzPrincipal.transform.Find("Halo_Techo_Suave");
        if (haloT != null)
        {
            Light h = haloT.GetComponent<Light>();
            if (h != null) h.enabled = encender;
        }
    }

    private static GameObject domoCorteOscuroVR;
    private static Material matCorteOscuroVR;

    private static GameObject ObtenerOCrearDomoCorteOscuro()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        if (domoCorteOscuroVR != null && domoCorteOscuroVR.transform.parent == cam.transform)
            return domoCorteOscuroVR;

        if (domoCorteOscuroVR != null)
            Destroy(domoCorteOscuroVR);

        domoCorteOscuroVR = new GameObject("Corte_Oscuro_Reporte_VR");
        domoCorteOscuroVR.transform.SetParent(cam.transform, false);
        domoCorteOscuroVR.transform.localPosition = Vector3.zero;
        domoCorteOscuroVR.transform.localRotation = Quaternion.identity;

        if (matCorteOscuroVR == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            matCorteOscuroVR = new Material(sh)
            {
                name = "Mat_CorteOscuro_VR",
                color = Color.black,
                renderQueue = 4000
            };
            matCorteOscuroVR.SetColor("_BaseColor", Color.black);
        }

        // 6 caras internas formando caja negra envolvente a 0.32m del visor (cubre 360° en Quest 2 y PC sin clipping de near plane)
        (Vector3 pos, Vector3 rot)[] caras = new (Vector3, Vector3)[]
        {
            (new Vector3( 0.00f,  0.00f,  0.32f), new Vector3(  0f,   0f, 0f)),
            (new Vector3( 0.00f,  0.00f, -0.32f), new Vector3(  0f, 180f, 0f)),
            (new Vector3( 0.32f,  0.00f,  0.00f), new Vector3(  0f,  90f, 0f)),
            (new Vector3(-0.32f,  0.00f,  0.00f), new Vector3(  0f, -90f, 0f)),
            (new Vector3( 0.00f,  0.32f,  0.00f), new Vector3(-90f,   0f, 0f)),
            (new Vector3( 0.00f, -0.32f,  0.00f), new Vector3( 90f,   0f, 0f)),
        };

        for (int i = 0; i < caras.Length; i++)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Cara_Negra_" + i;
            Collider c = q.GetComponent<Collider>();
            if (c != null) Destroy(c);
            q.transform.SetParent(domoCorteOscuroVR.transform, false);
            q.transform.localPosition = caras[i].pos;
            q.transform.localEulerAngles = caras[i].rot;
            q.transform.localScale = new Vector3(0.68f, 0.68f, 1f);
            MeshRenderer mr = q.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = matCorteOscuroVR;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        domoCorteOscuroVR.SetActive(false);
        return domoCorteOscuroVR;
    }

    /// <summary>
    /// Corte total de luz / parpadeo oscuro de cámara (0.28 s) al validar un reporte:
    /// evita al 100% que cualquier entidad u objeto desaparezca "en el aire" frente al jugador.
    /// </summary>
    public static void EjecutarMicroCorteYRestaurar(MonoBehaviour runner, ObjetoAnomalia anomalia, System.Action alRestaurarCallback = null)
    {
        if (anomalia == null)
        {
            alRestaurarCallback?.Invoke();
            return;
        }
        if (runner == null || !runner.gameObject.activeInHierarchy)
        {
            anomalia.RestaurarNormalidad();
            alRestaurarCallback?.Invoke();
            return;
        }
        runner.StartCoroutine(RutinaMicroCorteYRestaurar(anomalia, alRestaurarCallback));
    }

    private static IEnumerator RutinaMicroCorteYRestaurar(ObjetoAnomalia anomalia, System.Action alRestaurarCallback)
    {
        Vector3 posAnom = anomalia != null ? anomalia.ObtenerPosicionVisualActiva() : Vector3.zero;
        InterruptorLuz swCercano = BuscarSwitchMasCercano(posAnom);
        bool teniaLuz = swCercano != null && swCercano.estaEncendida && EnergiaGeneralCasaActiva;

        GameObject domoNegro = ObtenerOCrearDomoCorteOscuro();
        if (domoNegro != null)
            domoNegro.SetActive(true);

        if (teniaLuz)
            swCercano.AplicarEstadoALuces(false);

        yield return new WaitForSeconds(0.16f);

        if (anomalia != null)
        {
            try { anomalia.RestaurarNormalidad(); }
            catch (System.Exception ex) { Debug.LogWarning($"[MicroCorte] {ex.Message}"); }
        }

        alRestaurarCallback?.Invoke();

        yield return new WaitForSeconds(0.12f);

        if (teniaLuz && swCercano != null)
            swCercano.RefrescarSegunEnergiaGeneral();

        if (domoNegro != null)
            domoNegro.SetActive(false);
    }

    public static InterruptorLuz BuscarSwitchMasCercano(Vector3 pos)
    {
        InterruptorLuz[] todos = FindObjectsByType<InterruptorLuz>(FindObjectsInactive.Include);
        InterruptorLuz mejor = null;
        float menorDist = float.MaxValue;
        bool piso2 = pos.y >= 3.15f;

        for (int i = 0; i < todos.Length; i++)
        {
            InterruptorLuz sw = todos[i];
            if (sw == null) continue;
            Vector3 pSw = sw.ObtenerPosicionMundial();
            if ((pSw.y >= 3.15f) != piso2) continue;
            float d = Vector3.Distance(pos, pSw);
            if (d < menorDist)
            {
                menorDist = d;
                mejor = sw;
            }
        }
        return mejor;
    }

    public void AlternarDesdeRayo()
    {
        ProcesarInteraccion(null);
    }

    private static AudioClip ObtenerClipClick()
    {
        if (clipClickProcedural == null)
        {
            int sampleRate = 22050;
            int samples = sampleRate / 25;
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float env = Mathf.Exp(-i / (samples * 0.18f));
                data[i] = (Random.value * 2f - 1f) * env * 0.7f;
            }
            clipClickProcedural = AudioClip.Create("SwitchClick", samples, 1, sampleRate, false);
            clipClickProcedural.SetData(data, 0);
        }
        return clipClickProcedural;
    }
}

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
    public const float IntensidadPiso1 = 2.0f;
    public const float RangoPiso1 = 7.0f;
    public const float IntensidadPiso2 = 1.35f;
    public const float RangoPiso2 = 5.6f;

    public static bool EnergiaGeneralCasaActiva = true;

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
        // Si hay una anomalía Electrical en este cuarto, hacer que la luz parpadee sin explicación
        if (parpadeoAnomaloActivo && EnergiaGeneralCasaActiva && luzDelCuarto != null)
        {
            if (Time.time >= proximoCambioParpadeo)
            {
                proximoCambioParpadeo = Time.time + Random.Range(0.04f, 0.24f);
                bool encendidaMomento = Random.value > 0.28f;
                float factor = encendidaMomento ? Random.Range(0.25f, 1.45f) : 0.03f;

                luzDelCuarto.enabled = true;
                luzDelCuarto.intensity = intensidadBaseLuz * factor;
            }
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
            if (l == null || l.type != LightType.Point) continue;
            if (l.name.Contains("Fusibles") || l.name.Contains("Unknown") || l.name.Contains("Electrico") || l.name.Contains("Tulpa"))
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
                l.transform.rotation = Quaternion.identity;
                l.transform.localScale = Vector3.one;
            }

            Vector3 pos = l.transform.position;
            bool esSegundoPiso = pos.y > 3.6f;

            if (!esSegundoPiso && pos.y > 2.70f && pos.y < 4.30f)
                pos.y = 2.52f;
            else if (esSegundoPiso && pos.y > 5.85f)
                pos.y = 5.75f;
            l.transform.position = pos;

            l.gameObject.SetActive(true);
            l.renderMode = LightRenderMode.ForcePixel;
            l.shadows = LightShadows.None;
            l.intensity = esSegundoPiso ? IntensidadPiso2 : IntensidadPiso1;
            l.range = esSegundoPiso ? RangoPiso2 : RangoPiso1;
            l.color = esSegundoPiso ? new Color(1.0f, 0.90f, 0.76f) : new Color(1.0f, 0.91f, 0.78f);
            l.cullingMask = ~0;

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
        hitboxPositivo.size = new Vector3(0.38f, 0.38f, 0.38f);
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
                luzDelCuarto.intensity = luzDelCuarto.transform.position.y > 3.6f ? IntensidadPiso2 : IntensidadPiso1;
            luzDelCuarto.enabled = encender;
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
                }
            }
        }
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

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
    public AudioClip clipSwitchReal;

    [Header("Respuesta y Delay")]
    public float delayConmutacion = 0.12f;
    public float cooldownInteraccion = 0.40f;

    [Header("Estado")]
    public bool estaEncendida = true;
    public bool bloqueadaPorAnomalia = false;
    public bool parpadeoAnomaloActivo = false;

    private XRSimpleInteractable interactable;
    private BoxCollider hitboxPositivo;
    private Vector3 normalFrenteMundial = Vector3.forward;
    private float temporizadorChequeoPared = 0f;
    private Coroutine rutinaDelayLuz;
    private int ultimaInteraccionFrame = -1;
    private float ultimoTiempoInteraccion = -10f;
    private float proximoCambioParpadeo = 0f;
    private float intensidadBaseLuz = IntensidadPiso1;

    private static AudioClip clipClickProcedural;
    private static AudioClip clipClickRealCache;
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

        Transform luzVieja = transform.Find("Luz_Real_Cuarto");
        if (luzVieja != null)
            Destroy(luzVieja.gameObject);

        Transform rockerViejo = transform.Find("Palanca_Rocker_VR");
        if (rockerViejo != null)
            Destroy(rockerViejo.gameObject);

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
        // Bloquear el collider del switch cuando el jugador está detrás de la pared o en otro piso
        // para que el rayo de XRRayInteractor JAMÁS atraviese muros para mostrar "UI Press / Grab"
        temporizadorChequeoPared -= Time.unscaledDeltaTime;
        if (temporizadorChequeoPared <= 0f)
        {
            temporizadorChequeoPared = 0.08f;
            ActualizarDisponibilidadHitboxSegunPared();
        }

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

            // El techo de Piso 1 está en Y=3.408 y el de Piso 2 en Y=6.281: colocar justo bajo el bulbo para iluminación uniforme sin anillos duros
            if (!esSegundoPiso && pos.y > 3.24f && pos.y < 4.30f)
                pos.y = 3.20f;
            else if (!esSegundoPiso && pos.y < 2.95f && pos.y > 2.35f)
                pos.y = 3.16f;
            else if (esSegundoPiso && pos.y > 6.10f)
                pos.y = 6.04f;
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

    public bool EstaDelLadoFrontal(Vector3 puntoObservador)
    {
        Vector3 posSw = ObtenerPosicionMundial();
        return Vector3.Dot(puntoObservador - posSw, normalFrenteMundial) > 0.035f;
    }

    private void ActualizarDisponibilidadHitboxSegunPared()
    {
        if (hitboxPositivo == null) return;
        Camera cam = Camera.main;
        if (cam == null)
        {
            if (!hitboxPositivo.enabled) hitboxPositivo.enabled = true;
            return;
        }

        Vector3 posCam = cam.transform.position;
        Vector3 posSw = ObtenerPosicionMundial();
        bool mismoPiso = (posCam.y >= 3.15f) == (posSw.y >= 3.15f);
        bool ladoCorrecto = mismoPiso &&
                            Vector3.Distance(posCam, posSw) < 2.65f &&
                            EstaDelLadoFrontal(posCam) &&
                            InteractorUniversalVR.TieneLineaDeVisionDirecta(posCam, posSw + normalFrenteMundial * 0.025f, transform);

        if (hitboxPositivo.enabled != ladoCorrecto)
            hitboxPositivo.enabled = ladoCorrecto;
    }

    private Vector3 DetectarNormalFrontalFueraDePared(Bounds wb)
    {
        // La placa del switch mide ~0.11m x 0.19m y solo ~0.02m de grosor: el eje más delgado es la normal del muro
        Vector3 sz = wb.size;
        Vector3 ejeCandidato;
        if (sz.x <= sz.y && sz.x <= sz.z)
            ejeCandidato = Vector3.right;
        else if (sz.z <= sz.x && sz.z <= sz.y)
            ejeCandidato = Vector3.forward;
        else
            ejeCandidato = Vector3.up;

        // Lanzar raycast corto a ambos lados (+eje y -eje) ignorando el propio switch para ver de qué lado está el muro sólido
        float distMas = EspacioLibreEnDireccion(wb.center, ejeCandidato);
        float distMenos = EspacioLibreEnDireccion(wb.center, -ejeCandidato);

        if (Mathf.Abs(distMas - distMenos) > 0.015f)
            return (distMas > distMenos) ? ejeCandidato : -ejeCandidato;

        // Fallback hacia el centro de su habitación asignada
        string cuarto = ObjetoAnomalia.NormalizarCuartoPorPosicion(wb.center, "", gameObject.name);
        Vector3 haciaCentro = ObjetoAnomalia.ObtenerCentroDeHabitacion(cuarto) - wb.center;
        return Vector3.Dot(haciaCentro, ejeCandidato) >= 0f ? ejeCandidato : -ejeCandidato;
    }

    private float EspacioLibreEnDireccion(Vector3 origen, Vector3 dir)
    {
        RaycastHit[] hits = Physics.RaycastAll(origen - dir * 0.005f, dir, 0.45f, ~0, QueryTriggerInteraction.Ignore);
        float menor = 0.45f;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c == null || c.isTrigger) continue;
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (hits[i].distance < menor)
                menor = hits[i].distance;
        }
        return menor;
    }

    private void ActualizarVisualPalancaSwitch()
    {
        // NUNCA modificar transform.localRotation ni agregar cubos extra: la malla original de Light_switch ya tiene su propio botón modelado y queda al ras del muro
    }

    public void ConfigurarColliderPositivoEInteractable()
    {
        BoxCollider colliderDirecto = GetComponent<BoxCollider>();
        if (colliderDirecto != null)
            colliderDirecto.enabled = false;

        // Eliminar cualquier Palanca_Rocker_VR residual de versiones previas
        Transform rockerViejo = transform.Find("Palanca_Rocker_VR");
        if (rockerViejo != null)
        {
            if (Application.isPlaying) Destroy(rockerViejo.gameObject);
            else DestroyImmediate(rockerViejo.gameObject);
        }

        MeshRenderer mr = GetComponent<MeshRenderer>();
        Bounds wb = mr != null ? mr.bounds : new Bounds(transform.position, new Vector3(0.11f, 0.19f, 0.02f));
        Vector3 centroMundial = wb.center;

        normalFrenteMundial = DetectarNormalFrontalFueraDePared(wb);

        Transform hijoHitbox = transform.Find("Hitbox_VR_Positivo");
        if (hijoHitbox == null)
        {
            GameObject objHitbox = new GameObject("Hitbox_VR_Positivo");
            objHitbox.layer = 0;
            hijoHitbox = objHitbox.transform;
            hijoHitbox.SetParent(transform, false);
        }

        // Colocar el hitbox exclusivamente DELANTE de la cara frontal del switch (+2.4cm hacia el cuarto) y delgado (4.5cm)
        // para que jamás sobresalga por detrás de un muro divisorio
        hijoHitbox.SetParent(null, true);
        hijoHitbox.position = centroMundial + normalFrenteMundial * 0.024f;
        hijoHitbox.rotation = Quaternion.LookRotation(normalFrenteMundial, Vector3.up);
        hijoHitbox.localScale = Vector3.one;
        hijoHitbox.SetParent(transform, true);

        hitboxPositivo = hijoHitbox.GetComponent<BoxCollider>();
        if (hitboxPositivo == null)
            hitboxPositivo = hijoHitbox.gameObject.AddComponent<BoxCollider>();

        hitboxPositivo.isTrigger = true;
        hitboxPositivo.center = Vector3.zero;
        hitboxPositivo.size = new Vector3(0.14f, 0.21f, 0.045f);
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
        sonidoClick.maxDistance = 7f;

        AudioClip mejorClip = ObtenerClipClick();
        if (mejorClip != null)
            sonidoClick.clip = mejorClip;
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
        Vector3 posFrontalSwitch = ObtenerPosicionMundial() + normalFrenteMundial * 0.025f;
        if (Camera.main != null && !EstaDelLadoFrontal(Camera.main.transform.position))
            return;

        if (interactor is Component compInteractor && !EstaDelLadoFrontal(compInteractor.transform.position))
            return;

        if (!InteractorUniversalVR.EstaDentroDeRangoYLineaDeVision(posFrontalSwitch, 1.90f, transform))
            return;

        if (ultimaInteraccionFrame == Time.frameCount) return;
        if (Time.unscaledTime - ultimoTiempoInteraccion < cooldownInteraccion) return;

        ultimaInteraccionFrame = Time.frameCount;
        ultimoTiempoInteraccion = Time.unscaledTime;

        if (sonidoClick != null)
        {
            AudioClip c = clipSwitchReal != null ? clipSwitchReal : sonidoClick.clip;
            if (c != null) sonidoClick.PlayOneShot(c, 0.85f);
        }

        if (interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.45f, 0.1f);

        if (bloqueadaPorAnomalia || !EnergiaGeneralCasaActiva)
        {
            estaEncendida = !estaEncendida;
            ActualizarVisualPalancaSwitch();
            if (!EnergiaGeneralCasaActiva)
                AplicarEstadoALuces(false);
            return;
        }

        if (luzDelCuarto == null)
            AsignarLucesSinRepeticionGlobalmente();

        bool nuevoEstado = !estaEncendida;
        estaEncendida = nuevoEstado;
        ActualizarVisualPalancaSwitch();

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
        ActualizarVisualPalancaSwitch();
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

    private static bool enObturadorCamaraActivo = false;
    private static int mascaraPrevCamara = ~0;
    private static CameraClearFlags clearFlagsPrevCamara = CameraClearFlags.SolidColor;
    private static Color colorFondoPrevCamara = Color.black;

    private static void LimpiarDomoAntiguoSiExiste(Camera cam)
    {
        if (cam == null) return;
        Transform domoViejo = cam.transform.Find("Corte_Oscuro_Reporte_VR");
        if (domoViejo != null)
            Destroy(domoViejo.gameObject);
    }

    private static void ActivarObturadorNegroCamara(Camera cam, bool activar)
    {
        if (cam == null || FinalTurnoVR.EstaEnSecuenciaFinal) return;
        LimpiarDomoAntiguoSiExiste(cam);

        if (activar)
        {
            if (!enObturadorCamaraActivo)
            {
                mascaraPrevCamara = cam.cullingMask;
                clearFlagsPrevCamara = cam.clearFlags;
                colorFondoPrevCamara = cam.backgroundColor;
                enObturadorCamaraActivo = true;
            }
            // CullingMask = 0 oculta al 100% mandos blancos VR, rayos azules, etiquetas UI y geometría (cero clipping de caja negra!)
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
        }
        else if (enObturadorCamaraActivo)
        {
            enObturadorCamaraActivo = false;
            cam.cullingMask = mascaraPrevCamara;
            cam.clearFlags = clearFlagsPrevCamara;
            cam.backgroundColor = colorFondoPrevCamara;
        }
    }

    /// <summary>
    /// Parpadeo eléctrico realista + obturador limpio de cámara al validar un reporte:
    /// Si el jugador está lejos y sin línea de visión, solo parpadea la luz del cuarto lejano sin cegarlo;
    /// si está en el mismo cuarto o viendo la anomalía, hace un parpadeo eléctrico + corte de cámara 100% limpio (sin mandos ni rayos visibles).
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
        Camera cam = Camera.main;
        Vector3 posAnom = anomalia != null ? anomalia.ObtenerPosicionVisualActiva() : Vector3.zero;
        InterruptorLuz swAnom = BuscarSwitchMasCercano(posAnom);
        InterruptorLuz swJugador = cam != null ? BuscarSwitchMasCercano(cam.transform.position) : null;

        bool teniaLuzAnom = swAnom != null && swAnom.estaEncendida && EnergiaGeneralCasaActiva;
        bool teniaLuzJug = swJugador != null && swJugador != swAnom && swJugador.estaEncendida && EnergiaGeneralCasaActiva;

        bool jugadorVeAnomaliaOMismoCuarto = false;
        if (cam != null && anomalia != null)
        {
            jugadorVeAnomaliaOMismoCuarto = ObjetoAnomalia.PuntoVisibleOMismoCuartoJugador(cam.transform, posAnom, anomalia.nombreCuarto);
        }

        // 1. Breve caída de tensión eléctrica previa (0.06s) como reacción de la casa al contener la anomalía
        if (teniaLuzAnom) swAnom.AplicarEstadoALuces(false);
        if (jugadorVeAnomaliaOMismoCuarto && teniaLuzJug) swJugador.AplicarEstadoALuces(false);
        yield return new WaitForSeconds(0.055f);

        if (teniaLuzAnom && swAnom != null) swAnom.AplicarEstadoALuces(true);
        yield return new WaitForSeconds(0.035f);

        // 2. Corte de contención: apagar luz del cuarto afectado y cerrar obturador de cámara SOLO si el jugador podría ver el cambio
        if (teniaLuzAnom && swAnom != null) swAnom.AplicarEstadoALuces(false);
        if (jugadorVeAnomaliaOMismoCuarto && teniaLuzJug && swJugador != null) swJugador.AplicarEstadoALuces(false);

        if (jugadorVeAnomaliaOMismoCuarto && cam != null)
            ActivarObturadorNegroCamara(cam, true);

        yield return new WaitForSeconds(jugadorVeAnomaliaOMismoCuarto ? 0.14f : 0.06f);

        if (anomalia != null)
        {
            try { anomalia.RestaurarNormalidad(); }
            catch (System.Exception ex) { Debug.LogWarning($"[MicroCorte] {ex.Message}"); }
        }

        alRestaurarCallback?.Invoke();

        yield return new WaitForSeconds(jugadorVeAnomaliaOMismoCuarto ? 0.08f : 0.04f);

        if (cam != null)
            ActivarObturadorNegroCamara(cam, false);

        if (teniaLuzAnom && swAnom != null)
            swAnom.RefrescarSegunEnergiaGeneral();
        if (teniaLuzJug && swJugador != null)
            swJugador.RefrescarSegunEnergiaGeneral();
    }

    /// <summary>
    /// Cuando se manifiesta una anomalía nueva: parpadea brevemente el foco de esa habitación lejana,
    /// y si por algún caso extremo el jugador tuviera línea de visión hacia el punto, aplica un micro-parpadeo de 0.11s para que jamás aparezca en el aire.
    /// </summary>
    public static void EjecutarTransicionAparicionAnomalia(MonoBehaviour runner, ObjetoAnomalia anomalia, TipoAnomalia? tipoForzado)
    {
        if (anomalia == null) return;
        if (runner == null || !runner.gameObject.activeInHierarchy)
        {
            anomalia.ActivarAnomalia(tipoForzado);
            return;
        }
        runner.StartCoroutine(RutinaAparicionAnomaliaConParpadeo(anomalia, tipoForzado));
    }

    private static IEnumerator RutinaAparicionAnomaliaConParpadeo(ObjetoAnomalia anomalia, TipoAnomalia? tipoForzado)
    {
        Camera cam = Camera.main;
        MeshRenderer mr = anomalia.GetComponent<MeshRenderer>();
        Vector3 posPrev = mr != null ? mr.bounds.center : anomalia.transform.position;
        string nLow = anomalia.gameObject.name.ToLowerInvariant();
        bool esMovil = nLow.StartsWith("anomalia_flawed") || nLow.StartsWith("anomalia_pureform") ||
                       nLow.StartsWith("anomalia_preacher") || nLow.StartsWith("anomalia_corpse") ||
                       nLow.Contains("puertaabismal");

        bool visibleAntes = !esMovil && cam != null &&
                            ObjetoAnomalia.PuntoVisibleOMismoCuartoJugador(cam.transform, posPrev, anomalia.nombreCuarto);

        if (visibleAntes && cam != null)
            ActivarObturadorNegroCamara(cam, true);

        anomalia.ActivarAnomalia(tipoForzado);

        Vector3 posReal = anomalia.ObtenerPosicionVisualActiva();
        InterruptorLuz swCuarto = BuscarSwitchMasCercano(posReal);
        bool teniaLuz = swCuarto != null && swCuarto.estaEncendida && EnergiaGeneralCasaActiva && !swCuarto.parpadeoAnomaloActivo;

        bool visibleDespues = cam != null &&
                              ObjetoAnomalia.PuntoVisibleOMismoCuartoJugador(cam.transform, posReal, anomalia.nombreCuarto);

        if (visibleDespues && !visibleAntes && cam != null)
            ActivarObturadorNegroCamara(cam, true);

        if (teniaLuz)
            swCuarto.AplicarEstadoALuces(false);

        yield return new WaitForSeconds((visibleAntes || visibleDespues) ? 0.11f : 0.07f);

        if (cam != null)
            ActivarObturadorNegroCamara(cam, false);

        if (teniaLuz && swCuarto != null)
            swCuarto.RefrescarSegunEnergiaGeneral();
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

    private AudioClip ObtenerClipClick()
    {
        if (clipSwitchReal != null)
            return clipSwitchReal;

#if UNITY_EDITOR
        if (clipClickRealCache == null)
            clipClickRealCache = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/168860__orginaljun__light-switch-01.mp3");
#endif
        if (clipClickRealCache != null)
            return clipClickRealCache;

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

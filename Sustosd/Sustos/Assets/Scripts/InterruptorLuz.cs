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
    [Header("Conexiones")]
    public Light luzDelCuarto;
    public AudioSource sonidoClick;

    [Header("Estado")]
    public bool estaEncendida = true;
    public bool bloqueadaPorAnomalia = false;

    private readonly List<Light> lucesAsociadas = new List<Light>();
    private Light luzLocalCuarto;
    private XRSimpleInteractable interactable;
    private BoxCollider hitboxPositivo;
    private Quaternion rotInicialSwitch;
    private int ultimaInteraccionFrame = -1;
    private float ultimoTiempoInteraccion = -10f;

    private static AudioClip clipClickProcedural;
    private static int ultimoFrameInicializacionGlobal = -1;

    void Awake()
    {
        if (PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(transform))
        {
            Destroy(this);
            return;
        }

        rotInicialSwitch = transform.localRotation;

        HabilitarLucesAdicionalesEnURP();
        InicializarLucesDeTechoGlobalmente();
        ConfigurarColliderPositivoEInteractable();
        AsegurarAudioClick();
        VincularLucesRealesDelCuarto();

        // Iniciar con las luces encendidas para que el jugador vea la casa y note los apagones/fallos eléctricos
        estaEncendida = true;
        AplicarEstadoALuces(estaEncendida);
    }

    public static void HabilitarLucesAdicionalesEnURP()
    {
        // Performance URP Config tenía m_AdditionalLightsRenderingMode = 0 (Disabled).
        // Forzamos PerPixel (1) y límite de 8 luces por objeto tanto en el asset de Quality como en GraphicsSettings en memoria.
        ForzarPipelineAssetLuces(QualitySettings.renderPipeline);
        ForzarPipelineAssetLuces(GraphicsSettings.currentRenderPipeline);
        ForzarPipelineAssetLuces(GraphicsSettings.defaultRenderPipeline);
    }

    private static void ForzarPipelineAssetLuces(RenderPipelineAsset asset)
    {
        if (asset == null) return;
        System.Type t = asset.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // 1 = LightRenderingMode.PerPixel
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

    private static void InicializarLucesDeTechoGlobalmente()
    {
        if (ultimoFrameInicializacionGlobal == Time.frameCount) return;
        ultimoFrameInicializacionGlobal = Time.frameCount;

        GameObject contenedorLuces = GameObject.Find("Contenedor_Luces_Techo");
        if (contenedorLuces == null)
            contenedorLuces = new GameObject("Contenedor_Luces_Techo");

        Light[] todasLasLuces = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Light l in todasLasLuces)
        {
            if (l == null || l.type != LightType.Point) continue;
            if (l.name.Contains("Fusibles") || l.name.Contains("Unknown") || l.name.Contains("Electrico") || l.name.Contains("Luz_Real_Cuarto"))
                continue;

            // Si sigue dentro de un Focus con escala negativa (-95.79), sacarla a Contenedor_Luces_Techo
            if (l.transform.parent != null && l.transform.parent.name.ToLower().StartsWith("focus"))
            {
                Renderer rFoco = l.transform.parent.GetComponent<Renderer>();
                Vector3 posFoco = rFoco != null ? rFoco.bounds.center : l.transform.parent.position;
                l.transform.SetParent(contenedorLuces.transform, true);
                l.transform.position = posFoco;
                l.transform.rotation = Quaternion.identity;
                l.transform.localScale = Vector3.one;
            }

            // Corregir altura Y: los focos estaban en Y=3.20m (atrapados dentro de la losa entre Piso 1 y Piso 2) y Y=6.51m (sobre el techo del Piso 2)
            Vector3 pos = l.transform.position;
            if (pos.y > 2.70f && pos.y < 4.30f)
                pos.y = 2.52f; // Debajo del techo del Piso 1 (suelo = 0.41m, techo = ~2.90m)
            else if (pos.y > 5.90f)
                pos.y = 5.78f; // Debajo del techo del Piso 2 (suelo = 3.74m, techo = ~6.15m)
            l.transform.position = pos;

            l.gameObject.SetActive(true);
            l.renderMode = LightRenderMode.ForcePixel;
            l.shadows = LightShadows.None;
            l.intensity = 11f;
            l.range = 9.5f;
            l.color = new Color(1f, 0.95f, 0.84f);
            l.cullingMask = ~0;
        }
    }

    public void ConfigurarColliderPositivoEInteractable()
    {
        // 1. Desactivar cualquier BoxCollider directo en el objeto con escala negativa (-95.79)
        BoxCollider colliderDirecto = GetComponent<BoxCollider>();
        if (colliderDirecto != null)
        {
            colliderDirecto.enabled = false;
        }

        // 2. Crear o actualizar hijo con escala mundial positiva (1, 1, 1)
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
        hitboxPositivo.size = new Vector3(0.30f, 0.30f, 0.30f);
        hitboxPositivo.enabled = true;

        // 3. Vincular el BoxCollider de escala positiva al XRSimpleInteractable
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

    public void VincularLucesRealesDelCuarto()
    {
        lucesAsociadas.Clear();

        Vector3 miPos = ObtenerPosicionMundial();
        bool switchEnSegundoPiso = miPos.y > 2.8f;

        Light[] luces = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Light masCercanaMismoPiso = null;
        float menorDistXZ = Mathf.Infinity;

        foreach (Light luz in luces)
        {
            if (luz == null || luz.type != LightType.Point) continue;
            if (luz.name.Contains("Fusibles") || luz.name.Contains("Unknown") || luz.name.Contains("Electrico") || luz.name.Contains("Luz_Real_Cuarto"))
                continue;

            bool luzEnSegundoPiso = luz.transform.position.y > 3.6f;
            if (luzEnSegundoPiso != switchEnSegundoPiso) continue;

            float distXZ = Vector2.Distance(new Vector2(miPos.x, miPos.z), new Vector2(luz.transform.position.x, luz.transform.position.z));
            if (distXZ < menorDistXZ)
            {
                menorDistXZ = distXZ;
                masCercanaMismoPiso = luz;
            }

            // Incluir las luces de techo cercanas del mismo cuarto (radio horizontal <= 3.6m)
            if (distXZ <= 3.6f && !lucesAsociadas.Contains(luz))
            {
                lucesAsociadas.Add(luz);
            }
        }

        if (masCercanaMismoPiso != null)
        {
            luzDelCuarto = masCercanaMismoPiso;
            if (!lucesAsociadas.Contains(masCercanaMismoPiso))
                lucesAsociadas.Add(masCercanaMismoPiso);
        }

        // Crear o actualizar una luz dedicada para este interruptor dentro de la habitación
        // para garantizar iluminación inmediata frente al jugador sin depender del límite de mallas combinadas
        CrearOActualizarLuzLocalDeCuarto(miPos, switchEnSegundoPiso, masCercanaMismoPiso);
    }

    private void CrearOActualizarLuzLocalDeCuarto(Vector3 posSwitch, bool esPiso2, Light referenciaTecho)
    {
        if (!Application.isPlaying) return;

        if (luzLocalCuarto == null)
        {
            Transform existente = transform.Find("Luz_Real_Cuarto");
            if (existente != null)
                luzLocalCuarto = existente.GetComponent<Light>();
        }

        if (luzLocalCuarto == null)
        {
            GameObject objLuz = new GameObject("Luz_Real_Cuarto");
            objLuz.transform.SetParent(transform, false);
            luzLocalCuarto = objLuz.AddComponent<Light>();
        }

        // Colocar la luz dentro del cuarto (hacia la luz de techo más cercana o separada de la pared)
        Vector3 posLuz = posSwitch;
        if (referenciaTecho != null)
        {
            Vector3 haciaCentroCuarto = referenciaTecho.transform.position - posSwitch;
            haciaCentroCuarto.y = 0f;
            if (haciaCentroCuarto.sqrMagnitude > 0.05f)
                posLuz += haciaCentroCuarto.normalized * Mathf.Min(haciaCentroCuarto.magnitude * 0.5f, 1.35f);
        }
        posLuz.y = esPiso2 ? 5.65f : 2.45f;

        luzLocalCuarto.transform.position = posLuz;
        luzLocalCuarto.transform.rotation = Quaternion.identity;
        luzLocalCuarto.type = LightType.Point;
        luzLocalCuarto.renderMode = LightRenderMode.ForcePixel;
        luzLocalCuarto.shadows = LightShadows.None;
        luzLocalCuarto.color = new Color(1f, 0.95f, 0.84f);
        luzLocalCuarto.intensity = 10f;
        luzLocalCuarto.range = 8.5f;
        luzLocalCuarto.cullingMask = ~0;
    }

    public Vector3 ObtenerPosicionMundial()
    {
        if (hitboxPositivo != null) return hitboxPositivo.transform.position;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        return mr != null ? mr.bounds.center : transform.position;
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
        if (Time.unscaledTime - ultimoTiempoInteraccion < 0.25f) return;

        ultimaInteraccionFrame = Time.frameCount;
        ultimoTiempoInteraccion = Time.unscaledTime;

        if (sonidoClick != null && sonidoClick.clip != null)
            sonidoClick.PlayOneShot(sonidoClick.clip);

        if (interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.45f, 0.1f);

        if (bloqueadaPorAnomalia)
        {
            Debug.Log($"[Switch] {name} está bloqueado por una anomalía eléctrica.");
            return;
        }

        if (lucesAsociadas.Count == 0 && luzDelCuarto == null)
            VincularLucesRealesDelCuarto();

        CambiarEstado(!estaEncendida);
    }

    public void CambiarEstado(bool nuevoEstado)
    {
        estaEncendida = nuevoEstado;
        AplicarEstadoALuces(estaEncendida);

        // Pequeña inclinación visual del botón del interruptor al encender/apagar
        transform.localRotation = rotInicialSwitch * Quaternion.Euler(estaEncendida ? 0f : 12f, 0f, 0f);
    }

    private void AplicarEstadoALuces(bool encender)
    {
        if (luzDelCuarto != null)
        {
            luzDelCuarto.gameObject.SetActive(true);
            luzDelCuarto.renderMode = LightRenderMode.ForcePixel;
            luzDelCuarto.enabled = encender;
        }

        foreach (Light l in lucesAsociadas)
        {
            if (l != null)
            {
                l.gameObject.SetActive(true);
                l.renderMode = LightRenderMode.ForcePixel;
                l.enabled = encender;
            }
        }

        if (luzLocalCuarto != null)
        {
            luzLocalCuarto.gameObject.SetActive(true);
            luzLocalCuarto.renderMode = LightRenderMode.ForcePixel;
            luzLocalCuarto.enabled = encender;
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
            int samples = sampleRate / 25; // 40ms
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

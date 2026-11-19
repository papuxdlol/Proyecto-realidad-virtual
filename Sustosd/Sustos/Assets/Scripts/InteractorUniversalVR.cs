using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevice = UnityEngine.XR.InputDevice;

[DefaultExecutionOrder(-90)]
public class InteractorUniversalVR : MonoBehaviour
{
    public static InteractorUniversalVR Instancia;

    [Header("Distancia para activar Switches, Puertas y Fusibles")]
    public float distanciaMaxima = 3.2f;

    private MonoBehaviour objetivoActual;
    private string nombreObjetivo = "";
    private bool botonVRAnterior = false;
    private float ultimoTiempoAccion = -10f;

    private InterruptorLuz[] cacheSwitches;
    private CajaFusiblesVR[] cacheFusibles;
    private PuertaAutoCierre[] cachePuertas;
    private XRRayInteractor[] cacheRayosXR;

    void Awake()
    {
        if (GetComponent<TabletSistemaVR>() != null || name.ToLowerInvariant().Contains("tablet"))
        {
            Destroy(this);
            return;
        }

        if (Instancia != null && Instancia != this)
        {
            Destroy(this);
            return;
        }

        Instancia = this;

        // 1. Si hay un Meta Quest 2 real activo, desactivar el simulador de PC para que no bloquee la cámara ni los mandos
        CiudadCameraLook.DesactivarSimuladorSiHayVisorReal();
        AsegurarControlDeCamaraActivo();

        // 2. Habilitar luces URP, noche oscura real, ventanas transparentes, vallas de límites y puerta faltante de Lavandería
        InterruptorLuz.HabilitarLucesAdicionalesEnURP();
        VentanasYLimitesVR.ConfigurarNocheOscuraReal();
        VentanasYLimitesVR.HacerVentanasTransparentes();
        VentanasYLimitesVR.ConstruirBarrerasLimitesMapa();
        VentanasYLimitesVR.RestaurarPuertaLavanderiaFaltante();
        AsegurarPuertasYSwitchesEnEscena();
    }

    void Start()
    {
        RefrescarCacheInteractivos();
    }

    private void AsegurarControlDeCamaraActivo()
    {
        CiudadCameraLook look = FindAnyObjectByType<CiudadCameraLook>(FindObjectsInactive.Include);
        if (look != null)
        {
            if (!look.gameObject.activeSelf)
                look.gameObject.SetActive(true);
            look.enabled = true;
        }
        else
        {
            gameObject.AddComponent<CiudadCameraLook>();
        }
    }

    public static void AsegurarPuertasYSwitchesEnEscena()
    {
        GameObject house = GameObject.Find("House");
        if (house != null)
        {
            foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                string n = t.name.ToLowerInvariant();

                bool esPuerta = (n.StartsWith("door") || n.StartsWith("closet_door")) && !n.Contains("frame") && !n.Contains("clonmimic");
                if (!esPuerta) continue;

                if (PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(t))
                {
                    PuertaAutoCierre puertaInvalida = t.GetComponent<PuertaAutoCierre>();
                    if (puertaInvalida != null)
                        Destroy(puertaInvalida);
                    Collider colInvalido = t.GetComponent<Collider>();
                    if (colInvalido != null)
                        colInvalido.enabled = false;
                    continue;
                }

                PuertaAutoCierre puerta = t.GetComponent<PuertaAutoCierre>();
                if (puerta == null)
                    puerta = t.gameObject.AddComponent<PuertaAutoCierre>();

                ObjetoAnomalia anom = t.GetComponent<ObjetoAnomalia>();
                if (anom != null && anom.posiblesVariantes != null)
                {
                    foreach (var v in anom.posiblesVariantes)
                    {
                        if (v != null && v.tipo == TipoAnomalia.Unknown && v.puertaQueSeAbreSola == null && v.objetoExtraActivable == null)
                            v.puertaQueSeAbreSola = puerta;
                    }
                }
            }
        }
    }

    public void RefrescarCacheInteractivos()
    {
        cacheSwitches = FindObjectsByType<InterruptorLuz>(FindObjectsInactive.Include);
        cacheFusibles = FindObjectsByType<CajaFusiblesVR>(FindObjectsInactive.Include);

        PuertaAutoCierre[] todasPuertas = FindObjectsByType<PuertaAutoCierre>(FindObjectsInactive.Include);
        List<PuertaAutoCierre> puertasVisibles = new List<PuertaAutoCierre>();
        foreach (var pt in todasPuertas)
        {
            if (pt != null && !PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(pt.transform))
                puertasVisibles.Add(pt);
        }
        cachePuertas = puertasVisibles.ToArray();

        cacheRayosXR = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include);
    }

    void Update()
    {
        if (Time.timeScale == 0f)
        {
            objetivoActual = null;
            return;
        }

        if (EstaApuntandoAInterfazUI())
        {
            objetivoActual = null;
            nombreObjetivo = "";
            botonVRAnterior = LeerPulsacionControlesVR();
            return;
        }

        BuscarObjetoFrenteAlJugador();

        if (objetivoActual != null && DetectarPulsacion())
        {
            if (Time.unscaledTime - ultimoTiempoAccion >= 0.32f)
            {
                ultimoTiempoAccion = Time.unscaledTime;
                EjecutarAccion(objetivoActual);
            }
        }
    }

    private bool EstaApuntandoAInterfazUI()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return true;

        if (cacheRayosXR != null)
        {
            foreach (var ray in cacheRayosXR)
            {
                if (ray != null && ray.isActiveAndEnabled && ray.TryGetCurrentUIRaycastResult(out RaycastResult res) && res.isValid)
                    return true;
            }
        }
        return false;
    }

    private void BuscarObjetoFrenteAlJugador()
    {
        objetivoActual = null;
        nombreObjetivo = "";

        if (cacheSwitches == null || cachePuertas == null || cachePuertas.Length == 0)
            RefrescarCacheInteractivos();

        Transform cam = Camera.main != null ? Camera.main.transform : transform;
        float mejorPuntaje = float.MaxValue;

        if (cacheSwitches != null)
        {
            foreach (var sw in cacheSwitches)
            {
                if (sw == null || !sw.isActiveAndEnabled) continue;
                EvaluarCandidato(sw, sw.ObtenerPosicionMundial(), cam, ref mejorPuntaje, "Switch de Luz");
            }
        }

        if (cacheFusibles != null)
        {
            foreach (var cf in cacheFusibles)
            {
                if (cf == null || !cf.isActiveAndEnabled) continue;
                EvaluarCandidato(cf, ObtenerCentroReal(cf.transform), cam, ref mejorPuntaje, "Caja de Fusibles");
            }
        }

        if (cachePuertas != null)
        {
            foreach (var pt in cachePuertas)
            {
                if (pt == null || !pt.isActiveAndEnabled) continue;
                if (PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(pt.transform)) continue;

                Vector3 puntoFrenteCam = cam.position + cam.forward * 1.1f;
                Vector3 posCercana = pt.ObtenerPuntoMasCercano(puntoFrenteCam);
                EvaluarCandidato(pt, posCercana, cam, ref mejorPuntaje, "Puerta");
                EvaluarCandidato(pt, pt.ObtenerPosicionMundial(), cam, ref mejorPuntaje, "Puerta");
            }
        }
    }

    private void EvaluarCandidato(MonoBehaviour script, Vector3 posObjeto, Transform cam, ref float mejorPuntaje, string etiqueta)
    {
        float distCam = Vector3.Distance(cam.position, posObjeto);
        if (distCam > distanciaMaxima) return;

        if (Mathf.Abs(cam.position.y - posObjeto.y) > 1.85f) return;

        // A) Verificar si alguno de los rayos de los mandos VR apunta hacia el objeto (Prioridad en Meta Quest 2)
        if (cacheRayosXR != null)
        {
            foreach (var ray in cacheRayosXR)
            {
                if (ray == null || !ray.isActiveAndEnabled) continue;
                Transform rayOrigin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
                float distMano = Vector3.Distance(rayOrigin.position, posObjeto);
                if (distMano > distanciaMaxima) continue;

                Vector3 dirDesdeMano = (posObjeto - rayOrigin.position).normalized;
                float anguloRayo = Vector3.Angle(rayOrigin.forward, dirDesdeMano);
                if (anguloRayo < 28f)
                {
                    float puntajeRayo = distMano * 0.55f + (anguloRayo * 0.035f);
                    if (puntajeRayo < mejorPuntaje)
                    {
                        mejorPuntaje = puntajeRayo;
                        objetivoActual = script;
                        nombreObjetivo = $"{etiqueta} ({script.name})";
                    }
                }
            }
        }

        // B) Verificar cono de visión de la cámara principal
        Vector3 dirHaciaObjeto = (posObjeto - cam.position).normalized;
        float anguloCam = Vector3.Angle(cam.forward, dirHaciaObjeto);
        if (anguloCam < 42f)
        {
            float puntajeCam = distCam + (anguloCam * 0.045f);
            if (puntajeCam < mejorPuntaje)
            {
                mejorPuntaje = puntajeCam;
                objetivoActual = script;
                nombreObjetivo = $"{etiqueta} ({script.name})";
            }
        }
    }

    private Vector3 ObtenerCentroReal(Transform t)
    {
        MeshRenderer mr = t.GetComponentInChildren<MeshRenderer>();
        return mr != null ? mr.bounds.center : t.position;
    }

    private bool DetectarPulsacion()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.fKey.wasPressedThisFrame ||
                Keyboard.current.gKey.wasPressedThisFrame)
                return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        bool vrPresionado = LeerPulsacionControlesVR();
        if (vrPresionado && !botonVRAnterior)
        {
            botonVRAnterior = true;
            return true;
        }
        botonVRAnterior = vrPresionado;

        return false;
    }

    private bool LeerPulsacionControlesVR()
    {
        if (LeerControlesXR(XRNode.RightHand) || LeerControlesXR(XRNode.LeftHand))
            return true;

        if (LeerNewInputXR(XRController.rightHand) || LeerNewInputXR(XRController.leftHand))
            return true;

        return false;
    }

    private bool LeerControlesXR(XRNode mano)
    {
        InputDevice dev = InputDevices.GetDeviceAtXRNode(mano);
        if (!dev.isValid) return false;

        dev.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger);
        dev.TryGetFeatureValue(CommonUsages.gripButton, out bool grip);
        return trigger || grip;
    }

    private bool LeerNewInputXR(XRController ctrl)
    {
        if (ctrl == null) return false;
        foreach (var control in ctrl.allControls)
        {
            if (control is UnityEngine.InputSystem.Controls.ButtonControl btn)
            {
                string n = btn.name.ToLowerInvariant();
                if ((n.Contains("trigger") || n.Contains("grip") || n.Contains("select")) && btn.isPressed)
                    return true;
            }
        }
        return false;
    }

    private void EnviarVibracionHapticaVR(float amplitud, float duracion)
    {
        InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (right.isValid)
            right.SendHapticImpulse(0u, amplitud, duracion);

        InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (left.isValid)
            left.SendHapticImpulse(0u, amplitud * 0.7f, duracion);
    }

    private void EjecutarAccion(MonoBehaviour objetivo)
    {
        if (objetivo is InterruptorLuz sw)
        {
            sw.AlternarDesdeRayo();
            EnviarVibracionHapticaVR(0.45f, 0.10f);
            Debug.Log($"[ÉXITO] Switch accionado: {sw.name}");
        }
        else if (objetivo is PuertaAutoCierre pt)
        {
            pt.AlternarPuerta();
            EnviarVibracionHapticaVR(0.55f, 0.14f);
            Debug.Log($"[ÉXITO] Puerta accionada: {pt.name}");
        }
        else if (objetivo is CajaFusiblesVR cf)
        {
            cf.RepararFusibles(null);
            EnviarVibracionHapticaVR(0.80f, 0.22f);
            Debug.Log($"[ÉXITO] Caja de fusibles accionada: {cf.name}");
        }
    }

    void OnGUI()
    {
        if (CiudadCameraLook.EsVisorVRRealActivo()) return;

        if (objetivoActual != null)
        {
            GUIStyle estilo = new GUIStyle(GUI.skin.box);
            estilo.fontSize = 20;
            estilo.fontStyle = FontStyle.Bold;
            estilo.normal.textColor = Color.green;
            estilo.alignment = TextAnchor.MiddleCenter;

            Rect caja = new Rect(Screen.width / 2f - 260f, Screen.height - 85f, 520f, 45f);
            GUI.Box(caja, $"[ Clic / 'F' / Gatillo ] -> {nombreObjetivo}", estilo);
        }
    }
}

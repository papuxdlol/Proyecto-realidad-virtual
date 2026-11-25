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

    [Header("Distancias Realistas de Interacción (Rango de Usuario)")]
    public float distanciaMaximaPuertas = 2.05f;
    public float distanciaMaximaSwitchesYMuebles = 1.80f;

    private MonoBehaviour objetivoActual;
    private string nombreObjetivo = "";
    private bool botonVRAnterior = false;
    private float ultimoTiempoAccion = -10f;

    private InterruptorLuz[] cacheSwitches;
    private CajaFusiblesVR[] cacheFusibles;
    private PuertaAutoCierre[] cachePuertas;
    private InteractivoMuebleYElectronicoVR[] cacheMueblesYElectronicos;
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

        // 2. Habilitar luces URP, noche oscura real, ventanas transparentes, vallas de límites, puerta faltante de Lavandería, cajones y electrónicos
        InterruptorLuz.HabilitarLucesAdicionalesEnURP();
        VentanasYLimitesVR.ConfigurarNocheOscuraReal();
        VentanasYLimitesVR.HacerVentanasTransparentes();
        VentanasYLimitesVR.ConstruirBarrerasLimitesMapa();
        VentanasYLimitesVR.RestaurarPuertaLavanderiaFaltante();
        VentanasYLimitesVR.AsegurarCollidersMueblesTangibles();
        AsegurarPuertasYSwitchesEnEscena();

        // 3. Asegurar sistema de tensión psicológica, latido, glitch de Tablet, paranoia ambiental (Fase 2) y Fin de Turno / Screamer / Death Screen (Fase 3)
        if (FindAnyObjectByType<TensionYAmbienteVR>(FindObjectsInactive.Include) == null)
            gameObject.AddComponent<TensionYAmbienteVR>();
        if (FindAnyObjectByType<FinalTurnoVR>(FindObjectsInactive.Include) == null)
            gameObject.AddComponent<FinalTurnoVR>();
        if (FindAnyObjectByType<Bloque1AnomaliasInteractivasVR>(FindObjectsInactive.Include) == null)
            gameObject.AddComponent<Bloque1AnomaliasInteractivasVR>();
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

    public static bool EstaDentroDeRangoYLineaDeVision(Vector3 posObjeto, float rangoMaximo = 2.05f, Transform objetivoTransform = null)
    {
        if (Camera.main == null) return true;
        Transform cam = Camera.main.transform;

        // 1. Separación estricta entre Piso 1 (Y < 3.15m) y Piso 2 (Y >= 3.15m)
        bool jugadorEnPiso2 = cam.position.y >= 3.15f;
        bool objetoEnPiso2 = posObjeto.y >= 3.15f;
        if (jugadorEnPiso2 != objetoEnPiso2)
            return false;

        // 2. Diferencia vertical máxima realista (evita activar objetos del techo o de otro nivel al mirar hacia arriba)
        if (Mathf.Abs(cam.position.y - posObjeto.y) > 1.35f)
            return false;

        // 3. Distancia máxima 3D realista respecto al jugador
        float dist3D = Vector3.Distance(cam.position, posObjeto);
        if (dist3D > rangoMaximo)
            return false;

        // 4. Línea de visión directa (cualquier muro, mueble o collider sólido entre la cámara y el objetivo bloquea la interacción)
        return TieneLineaDeVisionDirecta(cam.position, posObjeto, objetivoTransform);
    }

    public static bool TieneLineaDeVisionDirecta(Vector3 origen, Vector3 destino, Transform objetivoTransform = null)
    {
        Vector3 dir = destino - origen;
        float dist = dir.magnitude;
        if (dist < 0.18f) return true;

        Transform raizJugador = Camera.main != null ? Camera.main.transform.root : null;

        RaycastHit[] hits = Physics.RaycastAll(origen, dir.normalized, dist - 0.08f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c == null || c.isTrigger) continue;
            if ( c is CharacterController ) continue;

            Transform tc = c.transform;
            if (raizJugador != null && (tc == raizJugador || tc.IsChildOf(raizJugador)))
                continue;

            if (objetivoTransform != null && (tc == objetivoTransform || tc.IsChildOf(objetivoTransform) || objetivoTransform.IsChildOf(tc)))
                continue;

            // Si el impacto es en el collider del mueble justo pegado a su propia puerta/cajón/TV (ej. cuerpo del refrigerador o lavabo), no auto-bloquear
            if (tc.name == "Collider_Mueble_Solido" && Vector3.Distance(hits[i].point, destino) < 0.35f)
                continue;

            // Hay un muro, puerta cerrada o mueble sólido atravesado entre el jugador y el objeto
            return false;
        }

        return true;
    }

    public static void AsegurarPuertasYSwitchesEnEscena()
    {
        GameObject house = GameObject.Find("House");
        if (house == null) return;

        foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            string n = t.name.ToLowerInvariant();
            if (n.Contains("clonmimic") || n.Contains("hitbox_vr_positivo")) continue;

            bool esPuertaPrincipal = (n.StartsWith("door") || n.StartsWith("closet_door") || n.StartsWith("garden_gate")) && !n.Contains("frame");
            if (esPuertaPrincipal)
            {
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
                continue;
            }

            // Configurar cajones, puertas de refrigerador, microondas, lavadoras, gabinetes, TVs y radio
            bool esInteractivoMuebleOElectronico =
                n.StartsWith("washbasin_drawer") ||
                n.StartsWith("chest_drawers") ||
                n.StartsWith("tool_cabinet") ||
                n.StartsWith("refrigerator_door") ||
                n.StartsWith("microwave_door") ||
                n.StartsWith("washing_machine_d") ||
                n.StartsWith("bathroom_cabinet_door") ||
                n.StartsWith("washbasin_door") ||
                n.StartsWith("shower_door") ||
                n.StartsWith("tv_03") ||
                n.StartsWith("tv_04") ||
                n.StartsWith("radio_01");

            if (esInteractivoMuebleOElectronico && !PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(t))
            {
                if (t.GetComponent<InteractivoMuebleYElectronicoVR>() == null)
                {
                    t.gameObject.AddComponent<InteractivoMuebleYElectronicoVR>();
                }
            }
        }
    }

    public void RefrescarCacheInteractivos()
    {
        cacheSwitches = FindObjectsByType<InterruptorLuz>(FindObjectsInactive.Include);
        cacheFusibles = FindObjectsByType<CajaFusiblesVR>(FindObjectsInactive.Include);
        cacheMueblesYElectronicos = FindObjectsByType<InteractivoMuebleYElectronicoVR>(FindObjectsInactive.Include);

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
                EvaluarCandidato(sw, sw.ObtenerPosicionMundial(), cam, distanciaMaximaSwitchesYMuebles, ref mejorPuntaje, "Switch de Luz");
            }
        }

        if (cacheFusibles != null)
        {
            foreach (var cf in cacheFusibles)
            {
                if (cf == null || !cf.isActiveAndEnabled) continue;
                EvaluarCandidato(cf, ObtenerCentroReal(cf.transform), cam, distanciaMaximaPuertas, ref mejorPuntaje, "Caja de Fusibles");
            }
        }

        if (cachePuertas != null)
        {
            foreach (var pt in cachePuertas)
            {
                if (pt == null || !pt.isActiveAndEnabled) continue;
                if (PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(pt.transform)) continue;

                Vector3 posCercana = pt.ObtenerPuntoMasCercano(cam.position);
                EvaluarCandidato(pt, posCercana, cam, distanciaMaximaPuertas, ref mejorPuntaje, "Puerta");
                EvaluarCandidato(pt, pt.ObtenerPosicionMundial(), cam, distanciaMaximaPuertas, ref mejorPuntaje, "Puerta");
            }
        }

        if (cacheMueblesYElectronicos != null)
        {
            foreach (var me in cacheMueblesYElectronicos)
            {
                if (me == null || !me.isActiveAndEnabled) continue;
                Vector3 posCercana = me.ObtenerPuntoMasCercano(cam.position);
                EvaluarCandidato(me, posCercana, cam, distanciaMaximaSwitchesYMuebles, ref mejorPuntaje, me.ObtenerEtiquetaHUD());
            }
        }
    }

    private void EvaluarCandidato(MonoBehaviour script, Vector3 posObjeto, Transform cam, float rangoMax, ref float mejorPuntaje, string etiqueta)
    {
        if (!EstaDentroDeRangoYLineaDeVision(posObjeto, rangoMax, script != null ? script.transform : null))
            return;

        float distCam = Vector3.Distance(cam.position, posObjeto);

        // A) Verificar si alguno de los rayos de los mandos VR apunta hacia el objeto (dentro del rango realista)
        if (cacheRayosXR != null)
        {
            foreach (var ray in cacheRayosXR)
            {
                if (ray == null || !ray.isActiveAndEnabled) continue;
                Transform rayOrigin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
                float distMano = Vector3.Distance(rayOrigin.position, posObjeto);
                if (distMano > rangoMax) continue;

                Vector3 dirDesdeMano = (posObjeto - rayOrigin.position).normalized;
                float anguloRayo = Vector3.Angle(rayOrigin.forward, dirDesdeMano);
                if (anguloRayo < 22f)
                {
                    float puntajeRayo = distMano * 0.6f + (anguloRayo * 0.04f);
                    if (puntajeRayo < mejorPuntaje)
                    {
                        mejorPuntaje = puntajeRayo;
                        objetivoActual = script;
                        nombreObjetivo = $"{etiqueta} ({script.name})";
                    }
                }
            }
        }

        // B) Verificar cono frontal de visión de la cámara principal (más estrecho para no seleccionar objetos periféricos lejanos)
        Vector3 dirHaciaObjeto = (posObjeto - cam.position).normalized;
        float anguloCam = Vector3.Angle(cam.forward, dirHaciaObjeto);
        if (anguloCam < 28f)
        {
            float puntajeCam = distCam + (anguloCam * 0.05f);
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
        else if (objetivo is InteractivoMuebleYElectronicoVR me)
        {
            me.AlternarEstado();
            EnviarVibracionHapticaVR(0.45f, 0.10f);
            Debug.Log($"[ÉXITO] Objeto accionado: {me.name}");
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

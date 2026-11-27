using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum TipoAnomalia
{
    Imagery = 0,      // Cuadros, fotos, pinturas o pósters que cambian o se invierten
    Displacement = 1, // Objetos que cambian de lugar, se mueven, giran o desaparecen
    Mimic = 2,        // Aparición de un objeto duplicado que imita a uno existente
    Flawed = 3,       // Figura humana con proporciones, rostro o extremidades distorsionadas
    PureForm = 4,     // Entidad oscura / sombra humanoide con ojos blancos y brillantes
    Preacher = 5,     // Figura enigmática oculta en rincones/armarios con susurros audibles
    Electrical = 6,   // Pantallas con error/estática, luces que parpadean o caja de fusibles
    Tulpa = 7,        // Manifestación psicológica que aparece exclusivamente en espejos
    Corpse = 8,       // Aparición repentina de un cuerpo sin vida dentro de la casa
    Unknown = 9       // Luz de día en ventana de madrugada, mensajes en español en pared, puerta que se abre sola
}

[System.Serializable]
public class VarianteAnomalia
{
    public string nombreDescriptivo = "Ej. Se mueve / Se clona";
    public TipoAnomalia tipo = TipoAnomalia.Displacement;

    [Header("Si es Displacement:")]
    public bool desapareceElObjeto = false;
    public Vector3 moverPosicion;
    public Vector3 girarRotacion;

    [Header("Si es Mimic, Entidad, Corpse, Tulpa o Unknown:")]
    [Tooltip("Arrastra aquí el clon duplicado o el monstruo que debe aparecer")]
    public GameObject objetoExtraActivable;

    [Header("Si es Imagery o TV Electrical:")]
    public MeshRenderer rendererCuadroOTV;
    public Material materialAlterado;
    [HideInInspector] public Material materialOriginalGuardado;

    [Header("Si es Electrical (Parpadeo o apagón de luces):")]
    public InterruptorLuz[] interruptoresAfectados;

    [Header("Sonido opcional de esta variante:")]
    public AudioSource audioEvento;
    public bool sonidoEnBucle = false;

    [Header("Si es Unknown (Puerta que se abre sola):")]
    public PuertaAutoCierre puertaQueSeAbreSola;
}

public class ObjetoAnomalia : MonoBehaviour
{
    [Header("Ubicación para la Tablet")]
    public string nombreCuarto = "Kitchen";

    [Header("Posibles Anomalías de este Objeto (Elige 1 al azar)")]
    public List<VarianteAnomalia> posiblesVariantes = new List<VarianteAnomalia>();

    [HideInInspector] public bool estaActiva = false;
    [HideInInspector] public TipoAnomalia tipoActivoActual;

    public Vector3 ObtenerPosicionVisualActiva()
    {
        if (varianteActual != null && varianteActual.objetoExtraActivable != null && varianteActual.objetoExtraActivable.activeInHierarchy)
        {
            Renderer r = varianteActual.objetoExtraActivable.GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : varianteActual.objetoExtraActivable.transform.position;
        }
        if (miRenderer != null)
            return miRenderer.bounds.center;
        return transform.position;
    }

    private VarianteAnomalia varianteActual;
    private Vector3 posOriginal;
    private Quaternion rotOriginal;
    private string cuartoOriginalGuardado;
    private MeshRenderer miRenderer;
    private Light luzElectricaTemporal;
    private GameObject rastroDisplacementActual;
    private bool enLevitacionDisplacement = false;
    private Vector3 posBaseLevitacion;
    private readonly List<InteractivoMuebleYElectronicoVR> interactivosAfectadosActuales = new List<InteractivoMuebleYElectronicoVR>();
    private readonly Dictionary<InterruptorLuz, bool> estadoPrevioSwitches = new Dictionary<InterruptorLuz, bool>();

    private static Material materialElectricoFallback;
    private static Material materialImageryFallback;
    private static Material materialCieloDiaVentana;
    private static Material materialDomoCieloPanoramicoDia;
    private static Material materialHazVolumetricoSol;
    private static Material materialProyeccionSolPiso;
    private static Mesh mallaDomoCieloCurvo;
    private static Material materialRastroPolvo;
    private static Material materialSombraPureForm;
    private static Material materialOjosBlancosPureForm;
    private static Material materialHaloLuzOjosPureForm;
    private static Material materialTunicaPreacher;
    private static Material materialBocaPreacher;
    private static Material materialDientesPreacher;
    private static Material materialRostroPreacher;
    private static Material materialOjosPreacher;
    private static AudioClip clipZumbidoElectrico;
    private static AudioClip clipSusurrosPreacher;
    private static List<Bounds> cacheVentanasCasa;

    private struct InfoCamaCasa
    {
        public MeshRenderer mr;
        public float offsetColchonY;
    }

    private struct InfoAsientoCasa
    {
        public string cuarto;
        public Vector3 posPisoFrenteAsiento;
        public Quaternion rotSentado;
    }

    private struct InfoVentanaExteriorFlawed
    {
        public string cuarto;
        public Vector3 posCristalInterior;
        public Vector3 posExteriorEntidad;
        public Quaternion rotMirandoAdentro;
    }

    private static bool cacheMueblesContextualesListo = false;
    private static readonly Dictionary<string, InfoCamaCasa> cacheCamasPorCuarto = new Dictionary<string, InfoCamaCasa>(System.StringComparer.OrdinalIgnoreCase);
    private static readonly List<InfoAsientoCasa> cacheAsientosFlawed = new List<InfoAsientoCasa>();
    private static readonly List<InfoVentanaExteriorFlawed> cacheVentanasExterioresFlawed = new List<InfoVentanaExteriorFlawed>();
    private static MeshRenderer cacheBathtubCasaArriba;
    private static readonly Dictionary<string, MeshRenderer> cacheCoberturaPureForm = new Dictionary<string, MeshRenderer>(System.StringComparer.OrdinalIgnoreCase);

    private MeshRenderer bathtubCasaOcultadaPorCorpse;
    private bool flawedSentadoActivoActual = false;
    private bool flawedVentanaActivoActual = false;
    private Light luzContraluzVentanaFlawed;

    public bool EnReaccionReportePreEliminacion { get; private set; } = false;

    public bool EsFlawedSentadoActivo()
    {
        return estaActiva && tipoActivoActual == TipoAnomalia.Flawed && flawedSentadoActivoActual;
    }

    public bool EsFlawedVentanaActivo()
    {
        return estaActiva && tipoActivoActual == TipoAnomalia.Flawed && flawedVentanaActivoActual;
    }

    public bool TieneReaccionPreEliminacionPorReporte()
    {
        return estaActiva &&
               (tipoActivoActual == TipoAnomalia.Flawed ||
                tipoActivoActual == TipoAnomalia.PureForm ||
                tipoActivoActual == TipoAnomalia.Tulpa);
    }

    public void DispararSitToStandPorReporte()
    {
        DispararReaccionPreEliminacionPorReporte();
    }

    public void DispararReaccionPreEliminacionPorReporte()
    {
        if (!TieneReaccionPreEliminacionPorReporte()) return;
        EnReaccionReportePreEliminacion = true;

        Transform vis = transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");
        if (vis != null)
        {
            AnimadorEntidadTerror anim = vis.GetComponent<AnimadorEntidadTerror>();
            if (anim != null)
                anim.DispararReaccionPorReporte();
        }

        if (tipoActivoActual == TipoAnomalia.PureForm && TensionYAmbienteVR.Instancia != null)
        {
            TensionYAmbienteVR.Instancia.DispararPulsoGlitchPantallaCompleta(2.45f, 0.92f);
        }
        else if (tipoActivoActual == TipoAnomalia.Tulpa)
        {
            EspejoTulpaVR[] espejos = FindObjectsByType<EspejoTulpaVR>(FindObjectsInactive.Exclude);
            for (int i = 0; i < espejos.Length; i++)
            {
                EspejoTulpaVR esp = espejos[i];
                if (esp == null) continue;
                esp.DispararReaccionReporteEnEspejo(2.40f);
                if (esp.modeloTulpaReflejado != null)
                {
                    AnimadorEntidadTerror animRef = esp.modeloTulpaReflejado.GetComponent<AnimadorEntidadTerror>();
                    if (animRef != null)
                        animRef.DispararReaccionPorReporte();
                }
            }
        }
    }

    private struct PuntoClosetPreacher
    {
        public string cuarto;
        public Vector3 posEnCloset;
        public float rotY;
        public string nombrePuertaCloset;
    }

    // El Monje (Preacher) SOLO aparece dentro o junto a los closets/roperos reales de la casa
    private static readonly PuntoClosetPreacher[] PuntosClosetsPreacher = new PuntoClosetPreacher[]
    {
        new PuntoClosetPreacher
        {
            cuarto = "LivingRoom",
            posEnCloset = new Vector3(18.25f, 0.41f, -0.05f),
            rotY = 180f,
            nombrePuertaCloset = "closet_door_01"
        },
        new PuntoClosetPreacher
        {
            cuarto = "LivingRoom",
            posEnCloset = new Vector3(15.92f, 0.41f, -0.68f),
            rotY = -90f,
            nombrePuertaCloset = "closet_door_02"
        },
        new PuntoClosetPreacher
        {
            cuarto = "Bedroom3",
            posEnCloset = new Vector3(13.78f, 3.74f, -5.98f),
            rotY = 0f,
            nombrePuertaCloset = "closet_door_03"
        },
        new PuntoClosetPreacher
        {
            cuarto = "Bedroom1",
            posEnCloset = new Vector3(22.85f, 3.74f, -6.92f),
            rotY = 0f,
            nombrePuertaCloset = "Door_002" // Puerta del clóset de la Recámara Principal (Master Bedroom)
        }
    };

    private PuertaAutoCierre puertaClosetAbiertaPorPreacher;

    private struct PuntoAparicionCuarto
    {
        public string cuarto;
        public Vector3 posCentro;
        public Vector3 posRincon;
        public Vector3 posPared;
        public float rotParedY;
    }

    private static readonly PuntoAparicionCuarto[] PuntosHabitaciones = new PuntoAparicionCuarto[]
    {
        new PuntoAparicionCuarto
        {
            cuarto = "LivingRoom",
            posCentro = new Vector3(13.20f, 0.41f, 0.50f),
            posRincon = new Vector3(11.60f, 0.41f, 1.25f),
            posPared  = new Vector3(11.15f, 1.78f, 0.20f),
            rotParedY = -90f
        },
        new PuntoAparicionCuarto
        {
            cuarto = "Kitchen",
            posCentro = new Vector3(14.55f, 0.41f, -6.30f),
            posRincon = new Vector3(11.80f, 0.41f, -9.20f),
            posPared  = new Vector3(14.55f, 1.75f, -6.30f),
            rotParedY = 180f
        },
        new PuntoAparicionCuarto
        {
            cuarto = "Bathroom",
            posCentro = new Vector3(18.90f, 3.74f, -6.40f),
            posRincon = new Vector3(13.60f, 3.74f, -2.50f),
            posPared  = new Vector3(18.90f, 5.10f, -7.75f),
            rotParedY = 180f
        },
        new PuntoAparicionCuarto
        {
            cuarto = "Laundry",
            posCentro = new Vector3(17.50f, 0.41f, -7.80f),
            posRincon = new Vector3(16.55f, 0.41f, -9.30f),
            posPared  = new Vector3(16.25f, 1.78f, -7.80f),
            rotParedY = -90f
        },
        new PuntoAparicionCuarto
        {
            cuarto = "Garage",
            posCentro = new Vector3(21.50f, 0.15f, -4.50f),
            posRincon = new Vector3(22.80f, 0.15f, -8.50f),
            posPared  = new Vector3(24.15f, 1.65f, -4.50f),
            rotParedY = 90f
        },
        new PuntoAparicionCuarto
        {
            cuarto = "Bedroom1",
            posCentro = new Vector3(22.20f, 3.74f, -2.50f),
            posRincon = new Vector3(23.30f, 3.74f, -6.80f),
            posPared  = new Vector3(23.10f, 5.10f, -7.50f),
            rotParedY = 180f
        },
        new PuntoAparicionCuarto
        {
            cuarto = "Bedroom2",
            posCentro = new Vector3(15.20f, 3.74f, 0.80f),
            posRincon = new Vector3(13.60f, 3.74f, 1.80f),
            posPared  = new Vector3(13.10f, 5.10f, 0.80f),
            rotParedY = -90f
        },
        new PuntoAparicionCuarto
        {
            cuarto = "Bedroom3",
            posCentro = new Vector3(15.20f, 3.74f, -8.20f),
            posRincon = new Vector3(13.60f, 3.74f, -9.20f),
            posPared  = new Vector3(15.20f, 5.10f, -9.90f),
            rotParedY = 180f
        }
    };

    public static string NormalizarCuartoPorPosicion(Vector3 posMundial, string cuartoOriginal, string nombreObjeto = "")
    {
        if (!string.IsNullOrEmpty(nombreObjeto))
        {
            string nl = nombreObjeto.Trim().ToLowerInvariant();

            // 0. Si es una entidad móvil, cuadro en pared, mensaje en pared o puerta abismal que ya asignó explícitamente su cuarto, respetarlo
            if ((nl.StartsWith("cuadro_imagery_") || nl.StartsWith("anomalia_")) && nl != "anomalia_tulpa_espejobano")
            {
                if (!string.IsNullOrEmpty(cuartoOriginal) &&
                    !string.Equals(cuartoOriginal, "Bedroom", System.StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(cuartoOriginal, "Basement", System.StringComparison.OrdinalIgnoreCase)
                        ? "Garage"
                        : cuartoOriginal.Trim();
                }
            }

            // 1. Mapeo exacto por nombre de Puertas y Switches para evitar que la cercanía a una pared los asigne al cuarto vecino
            switch (nl)
            {
                case "door":
                case "closet_door":
                case "closet_door_01":
                case "closet_door_02":
                case "light_switch":
                case "light_switch.001":
                case "light_switch.003":
                case "tv_03":
                    return "LivingRoom";

                case "garden_gate":
                case "garden_gate_01":
                case "light_switch.004":
                case "light_switch.005":
                    return "Kitchen";

                case "door_007":
                case "light_switch.006":
                    return "Laundry";

                case "door_008":
                case "door_009":
                case "light_switch.007":
                case "light_switch.008":
                case "caja_de_fusibles_vr":
                    return "Garage";

                case "door_003":
                case "door_005":
                case "shower_door":
                case "light_switch.010":
                case "light_switch.012":
                case "anomalia_tulpa_espejobano":
                    return "Bathroom";

                case "door_002":
                case "door_004":
                case "light_switch.009":
                case "tv_03.001":
                    return "Bedroom1";

                case "door_001":
                case "light_switch.013":
                case "light_switch.014":
                case "tv_04":
                    return "Bedroom2";

                case "door_006":
                case "closet_door_03":
                case "light_switch.011":
                    return "Bedroom3";
            }
        }

        // 2. Clasificación arquitectónica exacta por polígonos de muros (Piso 1 vs Piso 2)
        if (posMundial.y < 3.15f)
        {
            // PISO 1
            if (posMundial.x > 18.62f)
                return "Garage";

            if (posMundial.x >= 15.85f && posMundial.z < -5.50f)
                return "Laundry";

            if (posMundial.z >= -3.80f)
                return "LivingRoom";

            return "Kitchen";
        }
        else
        {
            // PISO 2
            // Baño 1 Arriba (Regadera + Lavabo Washbasin_03.001 + Inodoro Toilet_01.001)
            bool enBano1Arriba = posMundial.x >= 17.55f && posMundial.x <= 20.75f && posMundial.z < -5.12f;
            // Baño 2 Arriba (Tina bathtub + Doble Lavabo Washbasin_07 + Inodoro Toilet_01.003)
            bool enBano2Arriba = posMundial.x <= 14.48f && posMundial.z >= -4.42f && posMundial.z <= -0.55f;

            if (enBano1Arriba || enBano2Arriba)
                return "Bathroom";

            if (posMundial.x > 17.45f)
                return "Bedroom1"; // Recámara 1 (Principal Oeste + Clóset Vestidor)

            if (posMundial.z > -2.50f)
                return "Bedroom2"; // Recámara 2 (Frente Norte)

            return "Bedroom3";     // Recámara 3 (Sur Trasera)
        }
    }

    public static Vector3 ObtenerCentroDeHabitacion(string cuarto)
    {
        for (int i = 0; i < PuntosHabitaciones.Length; i++)
        {
            if (string.Equals(PuntosHabitaciones[i].cuarto, cuarto, System.StringComparison.OrdinalIgnoreCase))
                return PuntosHabitaciones[i].posCentro;
        }
        return new Vector3(15.0f, 0.41f, -3.0f);
    }

    /// <summary>
    /// Devuelve una altura de suelo 100% segura por piso arquitectónico:
    /// En Piso 2 (Floor Y=3.732, techo Cocina Y=3.408) NUNCA permite bajar de 3.74f aunque un Raycast atraviese colliders_floor_02 (3.528) o caiga a la Cocina.
    /// </summary>
    public static float ObtenerAlturaPisoSegura(Vector3 punto, string cuarto = "")
    {
        bool esPiso2 = punto.y > 3.15f ||
                       (!string.IsNullOrEmpty(cuarto) && (cuarto.StartsWith("Bedroom", System.StringComparison.OrdinalIgnoreCase) || string.Equals(cuarto, "Bathroom", System.StringComparison.OrdinalIgnoreCase) && punto.y > 2.5f));
        if (esPiso2)
        {
            float baseP2 = 3.74f;
            if (Physics.Raycast(new Vector3(punto.x, baseP2 + 0.9f, punto.z), Vector3.down, out RaycastHit hit2, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit2.point.y >= 3.73f && hit2.point.y <= 3.82f)
                    return hit2.point.y;
            }
            return baseP2;
        }

        bool esGaraje = string.Equals(cuarto, "Garage", System.StringComparison.OrdinalIgnoreCase) || punto.x > 18.62f;
        float minP1 = esGaraje ? 0.15f : 0.405f;
        float maxP1 = esGaraje ? 0.25f : 0.48f;
        if (Physics.Raycast(new Vector3(punto.x, minP1 + 0.9f, punto.z), Vector3.down, out RaycastHit hit1, 1.3f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit1.point.y >= minP1 - 0.02f && hit1.point.y <= maxP1)
                return Mathf.Max(minP1, hit1.point.y);
        }
        return esGaraje ? 0.16f : 0.41f;
    }

    private static bool CuartoTieneOtraAnomaliaActiva(string cuarto, ObjetoAnomalia ignorar)
    {
        if (string.IsNullOrEmpty(cuarto) || TabletSistemaVR.Instancia == null) return false;
        List<ObjetoAnomalia> lista = TabletSistemaVR.Instancia.todasLasAnomalias;
        for (int i = 0; i < lista.Count; i++)
        {
            ObjetoAnomalia a = lista[i];
            if (a == null || a == ignorar || !a.estaActiva) continue;
            if (string.Equals(a.nombreCuarto, cuarto, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static readonly string[] MensajesParedEspanol = new string[]
    {
        "NADA VALE EL RIESGO",
        "NO MIRES ATRÁS",
        "ÉL ESTÁ DENTRO DE LA CASA",
        "NO CONFÍES EN TU REFLEJO",
        "CIERRA TODAS LAS PUERTAS"
    };

    void Awake()
    {
        // Escrituras en las paredes desactivadas definitivamente para evitar bugs visuales
        if (gameObject.name.ToLowerInvariant().Contains("mensajepared"))
        {
            estaActiva = false;
            if (posiblesVariantes != null) posiblesVariantes.Clear();
            gameObject.SetActive(false);
            return;
        }

        AutoRepararGeometriaYReferencias();

        miRenderer = GetComponent<MeshRenderer>();
        Vector3 posReal = miRenderer != null ? miRenderer.bounds.center : transform.position;
        nombreCuarto = NormalizarCuartoPorPosicion(posReal, nombreCuarto, gameObject.name);
        posOriginal = transform.localPosition;
        rotOriginal = transform.localRotation;
        cuartoOriginalGuardado = nombreCuarto;

        foreach (var v in posiblesVariantes)
        {
            if (v == null) continue;

            if (v.objetoExtraActivable != null)
            {
                AutoRepararObjetoExtra(v);
                v.objetoExtraActivable.SetActive(false);
            }

            if (v.rendererCuadroOTV != null)
                v.materialOriginalGuardado = v.rendererCuadroOTV.sharedMaterial;
        }
    }

    private void AutoRepararGeometriaYReferencias()
    {
        string n = gameObject.name.ToLowerInvariant();

        // 1. Si es una puerta con anomalía Unknown pero le falta PuertaAutoCierre, agregárselo y vincularlo
        bool esPuerta = (n.StartsWith("door") || n.StartsWith("closet_door")) && !n.Contains("frame") && !PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(transform);
        if (esPuerta)
        {
            PuertaAutoCierre puerta = GetComponent<PuertaAutoCierre>();
            if (puerta == null)
                puerta = gameObject.AddComponent<PuertaAutoCierre>();

            foreach (var v in posiblesVariantes)
            {
                if (v != null && v.tipo == TipoAnomalia.Unknown && v.puertaQueSeAbreSola == null && v.objetoExtraActivable == null)
                {
                    v.puertaQueSeAbreSola = puerta;
                }
            }
        }

        // 2. Mensaje escrito en la pared en español (Unknown)
        if (n.Contains("mensajepared"))
        {
            RepararMensajePared();
        }

        // 3. Configurar las 5 entidades humanoides
        if (n.StartsWith("anomalia_pureform") ||
            n.StartsWith("anomalia_preacher") ||
            n.StartsWith("anomalia_flawed") ||
            n.StartsWith("anomalia_tulpa") ||
            n.StartsWith("anomalia_corpse"))
        {
            RepararAlturaYEstiloEntidad(n);
        }
    }

    private void AutoRepararObjetoExtra(VarianteAnomalia v)
    {
        GameObject extra = v.objetoExtraActivable;
        if (extra == null) return;

        string extraName = extra.name.ToLowerInvariant();

        if (extraName.Contains("luzdedia_unknown"))
        {
            MeshRenderer parentRenderer = GetComponent<MeshRenderer>();
            if (parentRenderer != null)
            {
                Bounds wb = parentRenderer.bounds;
                bool orientadaEnX = wb.size.x < wb.size.z;
                float tamX = orientadaEnX ? 0.03f : Mathf.Clamp(wb.size.x * 0.96f, 0.55f, 2.5f);
                float tamY = Mathf.Clamp(wb.size.y * 0.95f, 0.75f, 2.3f);
                float tamZ = !orientadaEnX ? 0.03f : Mathf.Clamp(wb.size.z * 0.96f, 0.55f, 2.5f);

                // Empujar el cielo azul ligeramente hacia el EXTERIOR de la casa para que los marcos negros de la ventana queden al frente
                Vector3 centroCasa = new Vector3(15.2f, wb.center.y, -4.5f);
                Vector3 haciaAfuera = (wb.center - centroCasa);
                haciaAfuera = orientadaEnX
                    ? new Vector3(Mathf.Sign(haciaAfuera.x), 0f, 0f)
                    : new Vector3(0f, 0f, Mathf.Sign(haciaAfuera.z));
                if (haciaAfuera.sqrMagnitude < 0.01f) haciaAfuera = Vector3.forward;

                Transform padreOrig = extra.transform.parent;
                extra.transform.SetParent(null, true);
                extra.transform.position = wb.center + haciaAfuera * 0.05f;
                extra.transform.rotation = Quaternion.identity;
                extra.transform.localScale = new Vector3(tamX, tamY, tamZ);
                if (padreOrig != null)
                    extra.transform.SetParent(padreOrig, true);

                // Limpiar cualquier diorama/caja antigua para que NUNCA haya cubos ni pisos verdes falsos afuera de la ventana
                Transform dioramaViejo = extra.transform.Find("Diorama_Exterior_Dia_VR");
                if (dioramaViejo != null)
                {
                    if (Application.isPlaying) Destroy(dioramaViejo.gameObject);
                    else DestroyImmediate(dioramaViejo.gameObject);
                }

                // El cristal usa un resplandor solar óptico limpio (dejando ver el jardín real iluminado y el cielo diurno panorámico al fondo)
                MeshRenderer mrExtra = extra.GetComponent<MeshRenderer>();
                if (mrExtra != null)
                {
                    mrExtra.enabled = true;
                    mrExtra.sharedMaterial = ObtenerMaterialCieloDiaRealista();
                    mrExtra.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mrExtra.receiveShadows = false;
                }

                // Construir horizonte diurno panorámico inmune a la niebla negra nocturna + haces volumétricos de sol entrando al cuarto + luces
                ConfigurarIluminacionDiaExteriorReal(extra.transform, wb, haciaAfuera.normalized);
            }
        }
        else if (extraName.Contains("clonmimic"))
        {
            MeshRenderer mrOrig = GetComponent<MeshRenderer>();
            MeshRenderer mrClon = extra.GetComponent<MeshRenderer>();
            if (mrOrig != null && mrClon != null)
            {
                Bounds bOrig = mrOrig.bounds;
                Vector3 centroCuarto = ObtenerCentroDeHabitacion(nombreCuarto);
                Vector3 haciaCentro = new Vector3(centroCuarto.x - bOrig.center.x, 0f, centroCuarto.z - bOrig.center.z);
                if (haciaCentro.sqrMagnitude < 0.01f) haciaCentro = Vector3.forward;
                haciaCentro.Normalize();

                Vector3 lateral = Vector3.Cross(Vector3.up, haciaCentro).normalized;
                float separacion = Mathf.Clamp(Mathf.Max(bOrig.size.x, bOrig.size.z) + 0.14f, 0.28f, 0.52f);

                // Probar dirección lateral o hacia el centro del cuarto que no choque con paredes (Anti-Clipping)
                Vector3 mejorOffset = (lateral * separacion) + (haciaCentro * 0.08f);
                if (Physics.Raycast(bOrig.center, mejorOffset.normalized, separacion + 0.12f, ~0, QueryTriggerInteraction.Ignore))
                {
                    mejorOffset = (-lateral * separacion) + (haciaCentro * 0.08f);
                    if (Physics.Raycast(bOrig.center, mejorOffset.normalized, separacion + 0.12f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        mejorOffset = haciaCentro * separacion;
                    }
                }

                extra.transform.position = transform.position + mejorOffset;
                extra.transform.rotation = transform.rotation * Quaternion.Euler(0f, 32f, 0f);
            }
        }
    }

    private void RepararMensajePared()
    {
        TextMeshPro tmp = GetComponentInChildren<TextMeshPro>(true);
        if (tmp == null) return;

        tmp.transform.localPosition = Vector3.zero;
        tmp.transform.localRotation = Quaternion.identity;
        tmp.transform.localScale = Vector3.one;

        RectTransform rt = tmp.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(1.55f, 0.45f);

        tmp.text = MensajesParedEspanol[0]; // "NADA VALE EL RIESGO"
        tmp.fontSize = 1.15f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void RepararAlturaYEstiloEntidad(string nombreLower)
    {
        Vector3 pos = transform.position;
        float alturaPiso = ObtenerAlturaPisoSegura(pos, nombreCuarto);

        Transform visual = transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");

        if (nombreLower.StartsWith("anomalia_corpse"))
        {
            transform.position = new Vector3(pos.x, alturaPiso + 0.01f, pos.z);
            if (visual != null)
            {
                ConfigurarAnimadorEnVisual(visual.gameObject, TipoAnomalia.Corpse, false);
            }
        }
        else if (nombreLower.StartsWith("anomalia_tulpa"))
        {
            // Tulpa: exclusivo de espejos (Baño)
            transform.position = new Vector3(pos.x, alturaPiso, pos.z);
            if (visual != null)
                ConfigurarAnimadorEnVisual(visual.gameObject, TipoAnomalia.Tulpa, false);

            EspejoTulpaVR espejo = GetComponent<EspejoTulpaVR>();
            if (espejo == null)
                espejo = gameObject.AddComponent<EspejoTulpaVR>();
            if (visual != null)
                espejo.modeloTulpaReflejado = visual.gameObject;
        }
        else if (nombreLower.StartsWith("anomalia_pureform"))
        {
            // Pure Form: Sombra humanoide completamente oscura con ojos blancos y brillantes
            transform.position = new Vector3(pos.x, alturaPiso, pos.z);
            if (visual != null)
            {
                AsegurarAspectoSombraConOjosBlancos(visual);
                ConfigurarAnimadorEnVisual(visual.gameObject, TipoAnomalia.PureForm, true);
            }
        }
        else if (nombreLower.StartsWith("anomalia_preacher"))
        {
            // Preacher: Figura enigmática en rincón/armario identificada por susurros audibles en bucle
            transform.position = new Vector3(pos.x, alturaPiso, pos.z);
            if (visual != null)
            {
                AplicarMaterialesTerrorPreacher(visual, null);
                ConfigurarAnimadorEnVisual(visual.gameObject, TipoAnomalia.Preacher, false);
            }

            AudioSource aud = GetComponent<AudioSource>();
            if (aud == null) aud = gameObject.AddComponent<AudioSource>();
            ConfigurarAudioEspacialPreacher(aud);

            foreach (var v in posiblesVariantes)
            {
                if (v != null && v.tipo == TipoAnomalia.Preacher)
                {
                    v.audioEvento = aud;
                    v.sonidoEnBucle = true;
                }
            }
        }
        else if (nombreLower.StartsWith("anomalia_flawed"))
        {
            // Flawed: Humanoides vestidos con anatomía real y rasgos no humanos (Sin Rostro / Cuello Quebrado)
            transform.position = new Vector3(pos.x, alturaPiso, pos.z);
            if (visual != null)
            {
                visual.localScale = Vector3.one;
                AplicarMaterialesMateFlawed(visual);
                ConfigurarAnimadorEnVisual(visual.gameObject, TipoAnomalia.Flawed, true);
            }
        }
    }

    public static void AplicarMaterialesMateFlawed(Transform visual)
    {
        if (visual == null) return;

        Shader shUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            string rn = r.gameObject.name;

            if (rn.Contains("FL_CuencaNegra"))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (materialSombraPureForm == null || materialSombraPureForm.shader != shUnlit)
                {
                    materialSombraPureForm = new Material(shUnlit)
                    {
                        name = "Mat_VacioNegro_Unlit_PureForm",
                        color = Color.black
                    };
                    materialSombraPureForm.SetColor("_BaseColor", Color.black);
                }
                r.sharedMaterial = materialSombraPureForm;
                continue;
            }

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Min(m.GetFloat("_Smoothness"), 0.14f));
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", Mathf.Min(m.GetFloat("_Glossiness"), 0.14f));
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.0f);
                if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0.0f);
                if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0.0f);
                if (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null)
                    m.EnableKeyword("_NORMALMAP");
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
        }
    }

    public static void AplicarMaterialesTerrorPreacher(Transform visual, Texture2D texRostroOverride = null)
    {
        if (visual == null) return;

        // CRÍTICO: Usar Unlit tanto en la túnica/cabello negro (#000000) como en el retrato 1:1 del rostro:
        // Así conserva intactos al 100% los trazos reales de carboncillo/claroscuro de la imagen original y NUNCA se ve como plastilina brillante
        Shader shUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");

        if (materialTunicaPreacher == null || materialTunicaPreacher.shader != shUnlit)
        {
            materialTunicaPreacher = new Material(shUnlit)
            {
                name = "Mat_TunicaNegra_Unlit_Preacher",
                color = Color.black
            };
            materialTunicaPreacher.SetColor("_BaseColor", Color.black);
        }

        Renderer rRostro = null;
        Renderer rTunica = null;

        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
        {
            string rn = r.gameObject.name;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            if (rn.Contains("PR_Rostro"))
            {
                rRostro = r;
                Texture texExistente = texRostroOverride != null
                    ? texRostroOverride
                    : (r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null);

                if (materialRostroPreacher == null || materialRostroPreacher.shader != shUnlit || texRostroOverride != null)
                {
                    Color tinteNocturno = new Color(0.68f, 0.68f, 0.74f, 1f);
                    materialRostroPreacher = new Material(shUnlit)
                    {
                        name = "Mat_RostroReal1a1_Unlit_Preacher",
                        color = tinteNocturno
                    };
                    materialRostroPreacher.SetColor("_BaseColor", tinteNocturno);
                    if (texExistente != null)
                    {
                        materialRostroPreacher.SetTexture("_BaseMap", texExistente);
                        materialRostroPreacher.SetTexture("_MainTex", texExistente);
                        materialRostroPreacher.mainTexture = texExistente;
                    }
                }
                if (materialRostroPreacher != null && materialRostroPreacher.mainTexture != null)
                    r.sharedMaterial = materialRostroPreacher;
            }
            else
            {
                rTunica = r;
                r.sharedMaterial = materialTunicaPreacher;
            }
        }

        if (rRostro != null && rTunica != null && rRostro.transform.parent != null && rRostro.transform.parent != visual)
        {
            Transform raizModelo = rRostro.transform.parent;
            Vector3 dirRostroLocal = visual.InverseTransformPoint(rRostro.bounds.center) - visual.InverseTransformPoint(rTunica.bounds.center);
            if (dirRostroLocal.z < -0.005f)
                raizModelo.localRotation *= Quaternion.Euler(0f, 180f, 0f);
        }
    }

    public static void AplicarMaterialesAbismalesPureForm(Transform visual)
    {
        AsegurarAspectoSombraConOjosBlancos(visual);
    }

    private static Material ObtenerMaterialHaloLuzOjosPureForm()
    {
        if (materialHaloLuzOjosPureForm != null) return materialHaloLuzOjosPureForm;

        int sz = 128;
        Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false)
        {
            name = "Tex_HaloLuz_Ojos_PureForm",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < sz; y++)
        {
            float ny = ((y + 0.5f) / sz) * 2f - 1f;
            for (int x = 0; x < sz; x++)
            {
                float nx = ((x + 0.5f) / sz) * 2f - 1f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float nucleo = Mathf.Exp(-d * d * 42f);
                float haloSuave = Mathf.Exp(-d * 4.2f) * Mathf.Clamp01((1f - d) * 1.6f);
                float intensidad = Mathf.Clamp01(nucleo + haloSuave * 0.62f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, intensidad));
            }
        }
        tex.Apply();

        Shader shHalo = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        materialHaloLuzOjosPureForm = new Material(shHalo)
        {
            name = "Mat_HaloLuz_Ojos_PureForm",
            color = new Color(0.96f, 0.98f, 1.0f, 0.95f),
            renderQueue = 3050
        };
        materialHaloLuzOjosPureForm.SetTexture("_MainTex", tex);
        materialHaloLuzOjosPureForm.SetTexture("_BaseMap", tex);
        materialHaloLuzOjosPureForm.mainTexture = tex;
        return materialHaloLuzOjosPureForm;
    }

    private static void AsegurarAspectoSombraConOjosBlancos(Transform visual)
    {
        if (visual == null) return;

        // CRÍTICO: Usar SIEMPRE Unlit negro puro (#000000) para que NUNCA refleje luces de la casa ni parezca plastilina o piel en el Screamer
        Shader shUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        if (materialSombraPureForm == null || materialSombraPureForm.shader != shUnlit)
        {
            materialSombraPureForm = new Material(shUnlit)
            {
                name = "Mat_VacioNegro_Unlit_PureForm",
                color = Color.black
            };
            materialSombraPureForm.SetColor("_BaseColor", Color.black);
        }

        if (materialOjosBlancosPureForm == null)
        {
            materialOjosBlancosPureForm = new Material(shUnlit)
            {
                name = "Mat_FocoLuzBlanca_PureForm"
            };
            Color blancoIncandescente = new Color(3.2f, 3.3f, 3.6f, 1f);
            materialOjosBlancosPureForm.color = Color.white;
            materialOjosBlancosPureForm.SetColor("_BaseColor", blancoIncandescente);
        }

        Material matHalo = ObtenerMaterialHaloLuzOjosPureForm();

        Renderer rendererOjosModelados = null;
        Renderer rendererCuerpoModelado = null;
        Renderer rendererHalosModelados = null;

        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
        {
            string rn = r.gameObject.name;
            if (rn.Contains("Ojo_Blanco")) continue;

            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            Material matObjetivo;
            if (rn.Contains("PF_HaloOjos"))
            {
                rendererHalosModelados = r;
                matObjetivo = matHalo;
            }
            else if (rn.Contains("PF_Ojos"))
            {
                rendererOjosModelados = r;
                matObjetivo = materialOjosBlancosPureForm;
            }
            else
            {
                rendererCuerpoModelado = r;
                matObjetivo = materialSombraPureForm;
            }

            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = matObjetivo;
            r.sharedMaterials = mats;
        }

        // Si el modelo PureForm_Silueta incluye PF_Ojos y PF_HaloOjos, limpiar esferas antiguas, alinear +Z y añadir micro-luz blanca real en ojos
        if (rendererOjosModelados != null)
        {
            Transform ojoViejoIzq = visual.Find("Ojo_Blanco_Izq");
            Transform ojoViejoDer = visual.Find("Ojo_Blanco_Der");
            if (ojoViejoIzq != null) { if (Application.isPlaying) Destroy(ojoViejoIzq.gameObject); else DestroyImmediate(ojoViejoIzq.gameObject); }
            if (ojoViejoDer != null) { if (Application.isPlaying) Destroy(ojoViejoDer.gameObject); else DestroyImmediate(ojoViejoDer.gameObject); }

            if (rendererCuerpoModelado != null && rendererOjosModelados.transform.parent != null && rendererOjosModelados.transform.parent != visual)
            {
                Transform raizModelo = rendererOjosModelados.transform.parent;
                Vector3 dirOjosLocal = visual.InverseTransformPoint(rendererOjosModelados.bounds.center) - visual.InverseTransformPoint(rendererCuerpoModelado.bounds.center);
                if (dirOjosLocal.z < -0.005f)
                    raizModelo.localRotation *= Quaternion.Euler(0f, 180f, 0f);
            }

            // Luz puntual tenue en los ojos para que brillen sobre muros/marcos cercanos sin iluminar jamás el cuerpo negro Unlit
            Transform tLuzOjos = visual.Find("Luz_Ojos_PureForm");
            if (tLuzOjos == null)
            {
                GameObject lObj = new GameObject("Luz_Ojos_PureForm");
                tLuzOjos = lObj.transform;
                tLuzOjos.SetParent(visual, false);
            }
            Vector3 posOjosLocal = visual.InverseTransformPoint((rendererHalosModelados ?? rendererOjosModelados).bounds.center);
            tLuzOjos.localPosition = posOjosLocal + new Vector3(0f, 0f, 0.04f);
            Light luzOjos = tLuzOjos.GetComponent<Light>();
            if (luzOjos == null) luzOjos = tLuzOjos.gameObject.AddComponent<Light>();
            luzOjos.type = LightType.Point;
            luzOjos.color = new Color(0.92f, 0.96f, 1.0f, 1f);
            luzOjos.intensity = 0.28f;
            luzOjos.range = 0.75f;
            luzOjos.shadows = LightShadows.None;
            return;
        }

        // Fallback únicamente si no estuviera presente PF_Ojos
        for (int lado = -1; lado <= 1; lado += 2)
        {
            string nombreOjo = lado < 0 ? "Ojo_Blanco_Izq" : "Ojo_Blanco_Der";
            Transform ojoT = visual.Find(nombreOjo);
            if (ojoT == null)
            {
                GameObject ojoObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ojoObj.name = nombreOjo;
                Collider col = ojoObj.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
                ojoT = ojoObj.transform;
                ojoT.SetParent(visual, false);
            }

            ojoT.localPosition = new Vector3(lado * 0.045f, 1.62f, 0.11f);
            ojoT.localScale = new Vector3(0.014f, 0.012f, 0.010f);
            MeshRenderer mrOjo = ojoT.GetComponent<MeshRenderer>();
            if (mrOjo != null)
                mrOjo.sharedMaterial = materialOjosBlancosPureForm;
        }
    }

    private static void ConfigurarAnimadorEnVisual(GameObject visualObj, TipoAnomalia tipo, bool mirarJugador)
    {
        if (visualObj == null) return;
        AnimadorEntidadTerror anim = visualObj.GetComponent<AnimadorEntidadTerror>();
        if (anim == null)
            anim = visualObj.AddComponent<AnimadorEntidadTerror>();
        anim.tipoEntidad = tipo;
        anim.mirarAlJugador = mirarJugador;
    }

    public void ActivarAnomalia(TipoAnomalia? tipoForzado = null)
    {
        if (estaActiva || posiblesVariantes.Count == 0) return;

        if (tipoForzado.HasValue)
        {
            List<VarianteAnomalia> coincidentes = posiblesVariantes.FindAll(v => v != null && v.tipo == tipoForzado.Value);
            varianteActual = coincidentes.Count > 0
                ? coincidentes[Random.Range(0, coincidentes.Count)]
                : posiblesVariantes[Random.Range(0, posiblesVariantes.Count)];
        }
        else
        {
            int indiceAzar = Random.Range(0, posiblesVariantes.Count);
            varianteActual = posiblesVariantes[indiceAzar];
        }

        tipoActivoActual = varianteActual.tipo;
        estaActiva = true;
        EnReaccionReportePreEliminacion = false;

        // 0. Si es una entidad humanoide móvil (Flawed, PureForm, Preacher, Corpse) o el Mensaje en la Pared,
        // aparecer dinámicamente en CUALQUIER habitación de la casa (excepto Tulpa, que es exclusiva de espejos)
        if (EsEntidadMovilEntreHabitaciones(tipoActivoActual))
        {
            ReubicarEntidadEnHabitacionAleatoria(tipoActivoActual);
        }
        else if (gameObject.name.ToLowerInvariant().Contains("mensajepared"))
        {
            ReubicarMensajeParedEnHabitacionAleatoria();
        }
        else if (gameObject.name.ToLowerInvariant().Contains("puertaabismal"))
        {
            ReubicarPuertaAbismalEnHabitacionAleatoria();
        }

        // 0b. Si este objeto (o sus hijos/hermanos en el mismo mueble) es un Cajón (Displacement) o Aparato (Electrical),
        // abrir el cajón o encender el aparato automáticamente
        ActivarInteractivosMuebleOElectronicoSiCorresponde();

        // 1. ¿Desaparece el objeto original? (Displacement)
        if (varianteActual.desapareceElObjeto && miRenderer != null)
        {
            CrearOMostrarRastroDisplacement();
            miRenderer.enabled = false;
        }

        // 2. ¿Se mueve, levita o rota? (Displacement / Imagery)
        if (varianteActual.moverPosicion != Vector3.zero || varianteActual.girarRotacion != Vector3.zero)
        {
            if (tipoActivoActual == TipoAnomalia.Displacement)
                CrearOMostrarRastroDisplacement();
            AplicarTransformacionAnomala(varianteActual);
        }

        // 3. ¿Aparece un clon o entidad? (Mimic, Entidades, Tulpa, Corpse, Unknown)
        if (varianteActual.objetoExtraActivable != null)
        {
            if (tipoActivoActual == TipoAnomalia.Mimic)
            {
                PosicionarClonMimicSinClipping(varianteActual.objetoExtraActivable);
            }
            varianteActual.objetoExtraActivable.SetActive(true);
        }

        // 4. ¿Cambia la textura de un cuadro o TV? (Imagery / Electrical)
        if (varianteActual.rendererCuadroOTV != null)
        {
            if (varianteActual.tipo == TipoAnomalia.Electrical)
            {
                varianteActual.rendererCuadroOTV.material = ObtenerMaterialElectricoFallback();
                CrearLuzFalloElectrico();
            }
            else if (varianteActual.materialAlterado != null)
            {
                varianteActual.rendererCuadroOTV.material = varianteActual.materialAlterado;
            }
            else if (varianteActual.tipo == TipoAnomalia.Imagery)
            {
                varianteActual.rendererCuadroOTV.material = ObtenerMaterialImageryFallback();
            }
        }

        // 5. ¿Fallo eléctrico en luces? (Electrical: parpadeo inexplicable en cuarto o apagón en caja de fusibles)
        if (varianteActual.interruptoresAfectados != null && varianteActual.interruptoresAfectados.Length > 0)
        {
            estadoPrevioSwitches.Clear();
            bool esApagonGeneral = varianteActual.interruptoresAfectados.Length > 2;

            foreach (var sw in varianteActual.interruptoresAfectados)
            {
                if (sw != null)
                {
                    estadoPrevioSwitches[sw] = sw.estaEncendida;
                    if (esApagonGeneral)
                    {
                        sw.CambiarEstado(false);
                        sw.bloqueadaPorAnomalia = true;
                    }
                    else
                    {
                        // Si es anomalía Electrical de un cuarto individual, la luz parpadea sin explicación
                        sw.ActivarParpadeoAnomalo(true);
                    }
                }
            }
        }

        // 6. ¿Suena algo? (Preacher = audio original extraído a volumen bajo y atenuado por cuarto; Electrical = estática/zumbido)
        if (varianteActual.audioEvento != null)
        {
            if (varianteActual.tipo == TipoAnomalia.Preacher)
            {
                ConfigurarAudioEspacialPreacher(varianteActual.audioEvento);
                if (varianteActual.audioEvento.clip == null || varianteActual.audioEvento.clip.name == "SusurrosPreacher")
                    varianteActual.audioEvento.clip = ObtenerClipSusurrosPreacher();
                varianteActual.audioEvento.pitch = 1.0f;
                // Inicia en silencio y ActualizarAtenuacionEspacialPreacher() regula el volumen real según piso/muros/cercanía
                varianteActual.audioEvento.volume = 0f;
                varianteActual.sonidoEnBucle = true;
            }
            else if (varianteActual.audioEvento.clip == null && varianteActual.tipo == TipoAnomalia.Electrical)
            {
                varianteActual.audioEvento.clip = ObtenerClipZumbidoElectrico();
            }

            if (varianteActual.audioEvento.clip != null)
            {
                varianteActual.audioEvento.loop = varianteActual.sonidoEnBucle;
                varianteActual.audioEvento.Play();
            }
        }

        // 7. ¿Puerta que se abre sola? (Unknown)
        if (varianteActual.puertaQueSeAbreSola != null)
            varianteActual.puertaQueSeAbreSola.AbrirPuertaPorAnomalia();
    }

    private static bool EsEntidadMovilEntreHabitaciones(TipoAnomalia tipo)
    {
        return tipo == TipoAnomalia.Flawed ||
               tipo == TipoAnomalia.PureForm ||
               tipo == TipoAnomalia.Preacher ||
               tipo == TipoAnomalia.Corpse;
    }

    public static bool PuntoVisibleOMismoCuartoJugador(Transform cam, Vector3 punto, string cuartoCandidato)
    {
        if (cam == null) return false;

        string cuartoJugador = NormalizarCuartoPorPosicion(cam.position, "LivingRoom");
        if (!string.IsNullOrEmpty(cuartoCandidato) &&
            string.Equals(cuartoCandidato.Trim(), cuartoJugador, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Mismo piso: verificar si el jugador tiene el punto dentro de su cono de visión y con línea de visión libre
        if (Mathf.Abs(cam.position.y - punto.y) < 2.35f)
        {
            Vector3 puntoOjos = punto + Vector3.up * 1.15f;
            Vector3 haciaPunto = puntoOjos - cam.position;
            float dist = haciaPunto.magnitude;
            if (dist < 2.8f) return true;
            if (dist < 14f && Vector3.Angle(cam.forward, haciaPunto / dist) < 82f)
            {
                if (InteractorUniversalVR.TieneLineaDeVisionDirecta(cam.position, puntoOjos, null))
                    return true;
            }
        }
        return false;
    }

    private void ReubicarEntidadEnHabitacionAleatoria(TipoAnomalia tipo)
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        flawedSentadoActivoActual = false;
        flawedVentanaActivoActual = false;
        if (luzContraluzVentanaFlawed != null)
            luzContraluzVentanaFlawed.enabled = false;

        // Restricción estricta para el Monje (Preacher): SOLO aparece en habitaciones con Closets y fuera de la vista del jugador
        if (tipo == TipoAnomalia.Preacher)
        {
            List<PuntoClosetPreacher> closetsDisponibles = new List<PuntoClosetPreacher>();
            for (int i = 0; i < PuntosClosetsPreacher.Length; i++)
            {
                if (!CuartoTieneOtraAnomaliaActiva(PuntosClosetsPreacher[i].cuarto, this) &&
                    !PuntoVisibleOMismoCuartoJugador(cam, PuntosClosetsPreacher[i].posEnCloset, PuntosClosetsPreacher[i].cuarto))
                    closetsDisponibles.Add(PuntosClosetsPreacher[i]);
            }
            if (closetsDisponibles.Count == 0)
            {
                for (int i = 0; i < PuntosClosetsPreacher.Length; i++)
                {
                    if (!PuntoVisibleOMismoCuartoJugador(cam, PuntosClosetsPreacher[i].posEnCloset, PuntosClosetsPreacher[i].cuarto))
                        closetsDisponibles.Add(PuntosClosetsPreacher[i]);
                }
            }
            if (closetsDisponibles.Count == 0)
                closetsDisponibles.AddRange(PuntosClosetsPreacher);

            PuntoClosetPreacher closetElegido = closetsDisponibles[Random.Range(0, closetsDisponibles.Count)];
            nombreCuarto = closetElegido.cuarto;

            Vector3 posFinalPreacher = closetElegido.posEnCloset;
            float rotFinalPreacherY = closetElegido.rotY;

            // Entreabrir la puerta del clóset (~42%) y situar al Preacher exactamente detrás de la rendija mirando hacia la habitación (como 1443881f.png)
            puertaClosetAbiertaPorPreacher = null;
            if (!string.IsNullOrEmpty(closetElegido.nombrePuertaCloset))
            {
                GameObject objPuertaCloset = GameObject.Find(closetElegido.nombrePuertaCloset);
                if (objPuertaCloset != null)
                {
                    PuertaAutoCierre pac = objPuertaCloset.GetComponent<PuertaAutoCierre>();
                    if (pac != null)
                    {
                        if (pac.CalcularPuntoAsomoPreacherDetrasDeRendija(ObtenerCentroDeHabitacion(nombreCuarto), out Vector3 posRendija, out float rotRendijaY))
                        {
                            posFinalPreacher = new Vector3(posRendija.x, closetElegido.posEnCloset.y, posRendija.z);
                            rotFinalPreacherY = rotRendijaY;
                        }

                        if (!pac.estaAbierta)
                        {
                            pac.AbrirEntreabiertaPorPreacher();
                            puertaClosetAbiertaPorPreacher = pac;
                        }
                    }
                }
            }

            float pisoCloset = ObtenerAlturaPisoSegura(posFinalPreacher, nombreCuarto);
            transform.position = new Vector3(posFinalPreacher.x, pisoCloset, posFinalPreacher.z);
            transform.rotation = Quaternion.Euler(0f, rotFinalPreacherY, 0f);
            return;
        }

        if (tipo == TipoAnomalia.Corpse)
        {
            ReubicarCorpseContextual(cam);
            return;
        }

        if (tipo == TipoAnomalia.PureForm)
        {
            ReubicarPureFormConVariantes(cam);
            return;
        }

        if (tipo == TipoAnomalia.Flawed)
        {
            InicializarCacheMueblesContextuales();
            Transform visualFlawed = transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");
            int idxElegido = 0;
            bool elegirSentado = false;
            bool elegirEspaldas = false;
            bool elegirVentana = false;

            if (visualFlawed != null && visualFlawed.childCount > 0)
            {
                visualFlawed.localPosition = Vector3.zero;
                visualFlawed.localRotation = Quaternion.identity;
                visualFlawed.localScale = Vector3.one;

                idxElegido = Random.Range(0, visualFlawed.childCount);
                for (int i = 0; i < visualFlawed.childCount; i++)
                {
                    GameObject ch = visualFlawed.GetChild(i).gameObject;
                    bool activo = (i == idxElegido);
                    ch.SetActive(activo);
                    if (activo)
                    {
                        string cn = ch.name.ToLowerInvariant();
                        // Variante 3 = Flawed_04_HombreTraje (SIEMPRE sentado en sofá/sillón/silla real)
                        // Variante 4 = Flawed_05_CabezaGirada (cuerpo de espaldas con cabeza girada 180°)
                        // Variante 5 = Flawed_06_Ventana (EXCLUSIVO afuera de las ventanas exteriores mirando hacia adentro!)
                        elegirSentado = (i == 3) || cn.Contains("traje");
                        elegirEspaldas = (i == 4) || cn.Contains("cabezagirada");
                        elegirVentana = (i == 5) || cn.Contains("ventana");
                    }
                }
                AplicarMaterialesMateFlawed(visualFlawed);
            }

            // Si salió el Flawed de Ventana (Flawed_06_Ventana), colocarlo EXCLUSIVAMENTE en el exterior de una ventana de la casa mirando hacia adentro
            if (elegirVentana && cacheVentanasExterioresFlawed.Count > 0)
            {
                List<InfoVentanaExteriorFlawed> ventsLibres = new List<InfoVentanaExteriorFlawed>();
                for (int i = 0; i < cacheVentanasExterioresFlawed.Count; i++)
                {
                    if (!CuartoTieneOtraAnomaliaActiva(cacheVentanasExterioresFlawed[i].cuarto, this) &&
                        !PuntoVisibleOMismoCuartoJugador(cam, cacheVentanasExterioresFlawed[i].posCristalInterior, cacheVentanasExterioresFlawed[i].cuarto))
                        ventsLibres.Add(cacheVentanasExterioresFlawed[i]);
                }
                if (ventsLibres.Count == 0)
                {
                    for (int i = 0; i < cacheVentanasExterioresFlawed.Count; i++)
                    {
                        if (!PuntoVisibleOMismoCuartoJugador(cam, cacheVentanasExterioresFlawed[i].posCristalInterior, cacheVentanasExterioresFlawed[i].cuarto))
                            ventsLibres.Add(cacheVentanasExterioresFlawed[i]);
                    }
                }
                if (ventsLibres.Count == 0)
                    ventsLibres.AddRange(cacheVentanasExterioresFlawed);

                InfoVentanaExteriorFlawed vent = ventsLibres[Random.Range(0, ventsLibres.Count)];
                nombreCuarto = vent.cuarto;
                flawedVentanaActivoActual = true;

                transform.position = vent.posExteriorEntidad;
                transform.rotation = vent.rotMirandoAdentro;

                // Luz lunar fría muy tenue afuera del cristal para recortar su rostro hueco tras la ventana nocturna
                if (luzContraluzVentanaFlawed == null)
                {
                    Transform lt = transform.Find("Luz_Contraluz_Ventana_Flawed");
                    if (lt == null)
                    {
                        GameObject goL = new GameObject("Luz_Contraluz_Ventana_Flawed");
                        goL.transform.SetParent(transform, false);
                        luzContraluzVentanaFlawed = goL.AddComponent<Light>();
                    }
                    else
                    {
                        luzContraluzVentanaFlawed = lt.GetComponent<Light>();
                    }
                    luzContraluzVentanaFlawed.type = LightType.Point;
                    luzContraluzVentanaFlawed.color = new Color(0.66f, 0.75f, 0.92f, 1f);
                    luzContraluzVentanaFlawed.intensity = 1.25f;
                    luzContraluzVentanaFlawed.range = 2.1f;
                    luzContraluzVentanaFlawed.shadows = LightShadows.None;
                }
                luzContraluzVentanaFlawed.transform.localPosition = new Vector3(0f, 1.54f, 0.16f);
                luzContraluzVentanaFlawed.enabled = true;

                if (visualFlawed != null)
                {
                    AnimadorEntidadTerror animFlawed = visualFlawed.GetComponent<AnimadorEntidadTerror>();
                    if (animFlawed != null)
                    {
                        animFlawed.GuardarTransformBase();
                        animFlawed.ConfigurarModoVarianteFlawed(false, false, esVentana: true);
                    }
                }
                return;
            }

            // Si salió el Hombre de Traje (Flawed_04), sentarlo sobre una silla, sillón o sofá real fuera de la vista del jugador
            if (elegirSentado && cacheAsientosFlawed.Count > 0)
            {
                List<InfoAsientoCasa> asientosLibres = new List<InfoAsientoCasa>();
                for (int i = 0; i < cacheAsientosFlawed.Count; i++)
                {
                    if (!CuartoTieneOtraAnomaliaActiva(cacheAsientosFlawed[i].cuarto, this) &&
                        !PuntoVisibleOMismoCuartoJugador(cam, cacheAsientosFlawed[i].posPisoFrenteAsiento, cacheAsientosFlawed[i].cuarto))
                        asientosLibres.Add(cacheAsientosFlawed[i]);
                }
                if (asientosLibres.Count == 0)
                {
                    for (int i = 0; i < cacheAsientosFlawed.Count; i++)
                    {
                        if (!PuntoVisibleOMismoCuartoJugador(cam, cacheAsientosFlawed[i].posPisoFrenteAsiento, cacheAsientosFlawed[i].cuarto))
                            asientosLibres.Add(cacheAsientosFlawed[i]);
                    }
                }
                if (asientosLibres.Count == 0)
                    asientosLibres.AddRange(cacheAsientosFlawed);

                InfoAsientoCasa asiento = asientosLibres[Random.Range(0, asientosLibres.Count)];
                nombreCuarto = asiento.cuarto;
                flawedSentadoActivoActual = true;

                float pisoSegAsiento = ObtenerAlturaPisoSegura(asiento.posPisoFrenteAsiento, nombreCuarto);
                transform.position = new Vector3(asiento.posPisoFrenteAsiento.x, pisoSegAsiento, asiento.posPisoFrenteAsiento.z);
                transform.rotation = asiento.rotSentado;

                if (visualFlawed != null)
                {
                    AnimadorEntidadTerror animFlawed = visualFlawed.GetComponent<AnimadorEntidadTerror>();
                    if (animFlawed != null)
                    {
                        animFlawed.GuardarTransformBase();
                        animFlawed.ConfigurarModoVarianteFlawed(true, false, false);
                    }
                }
                return;
            }

            if (visualFlawed != null)
            {
                AnimadorEntidadTerror animFlawed = visualFlawed.GetComponent<AnimadorEntidadTerror>();
                if (animFlawed != null)
                {
                    animFlawed.GuardarTransformBase();
                    animFlawed.ConfigurarModoVarianteFlawed(false, elegirEspaldas, false);
                }
            }
        }

        List<PuntoAparicionCuarto> candidatos = new List<PuntoAparicionCuarto>();

        for (int i = 0; i < PuntosHabitaciones.Length; i++)
        {
            PuntoAparicionCuarto p = PuntosHabitaciones[i];
            if (!CuartoTieneOtraAnomaliaActiva(p.cuarto, this) &&
                !PuntoVisibleOMismoCuartoJugador(cam, p.posCentro, p.cuarto))
                candidatos.Add(p);
        }

        if (candidatos.Count == 0)
        {
            for (int i = 0; i < PuntosHabitaciones.Length; i++)
            {
                PuntoAparicionCuarto p = PuntosHabitaciones[i];
                if (!PuntoVisibleOMismoCuartoJugador(cam, p.posCentro, p.cuarto))
                    candidatos.Add(p);
            }
        }

        if (candidatos.Count == 0)
            candidatos.AddRange(PuntosHabitaciones);

        PuntoAparicionCuarto elegido = candidatos[Random.Range(0, candidatos.Count)];
        nombreCuarto = elegido.cuarto;

        Vector3 posElegida = elegido.posCentro;
        float alturaPiso = ObtenerAlturaPisoSegura(posElegida, nombreCuarto);

        transform.position = new Vector3(posElegida.x, alturaPiso, posElegida.z);
        Vector3 mirarCentro = cam != null ? (cam.position - transform.position) : Vector3.forward;
        mirarCentro.y = 0f;
        transform.rotation = mirarCentro.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(mirarCentro.normalized, Vector3.up)
            : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    private static void InicializarCacheMueblesContextuales()
    {
        if (cacheMueblesContextualesListo && cacheCamasPorCuarto.Count > 0) return;

        cacheCamasPorCuarto.Clear();
        cacheAsientosFlawed.Clear();
        cacheVentanasExterioresFlawed.Clear();
        cacheCoberturaPureForm.Clear();
        cacheBathtubCasaArriba = null;

        GameObject house = GameObject.Find("House");
        if (house == null) return;

        foreach (MeshRenderer mr in house.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr == null) continue;
            string nl = mr.gameObject.name.Trim().ToLowerInvariant();
            if (nl.Contains("clonmimic") || nl.Contains("collider")) continue;

            Bounds b = mr.bounds;
            string cuartoMueble = NormalizarCuartoPorPosicion(b.center, "");

            // Detectar cristales de ventanas EXTERIORES reales para la 6ta variante Flawed_06_Ventana (excluyendo mamparas de regadera y cristales pequeños)
            if (nl.StartsWith("glass") && !nl.Contains("shower") && !nl.Contains("table") && !nl.Contains("cup") && !nl.Contains("door") && b.size.y >= 0.80f && Mathf.Max(b.size.x, b.size.z) >= 0.65f)
            {
                bool orientadaEnX = b.size.x < b.size.z;
                Vector3 centroCasa = new Vector3(15.6f, b.center.y, -4.2f);
                Vector3 haciaAfuera = orientadaEnX
                    ? new Vector3(Mathf.Sign(b.center.x - centroCasa.x), 0f, 0f)
                    : new Vector3(0f, 0f, Mathf.Sign(b.center.z - centroCasa.z));
                if (haciaAfuera.sqrMagnitude < 0.01f) haciaAfuera = Vector3.forward;
                haciaAfuera.Normalize();

                Vector3 puntoInterior = b.center - haciaAfuera * 0.85f;
                string cuartoVent = NormalizarCuartoPorPosicion(puntoInterior, "LivingRoom");
                if (cuartoVent != "Bathroom")
                {
                    // Alinear el torso y el rostro hueco (+1.48m desde la base del modelo) justo al centro vertical del cristal de la ventana
                    float posYEntidad = Mathf.Min(b.center.y - 1.38f, b.max.y - 1.72f);
                    Vector3 posExt = new Vector3(b.center.x, posYEntidad, b.center.z) + haciaAfuera * 0.23f;
                    cacheVentanasExterioresFlawed.Add(new InfoVentanaExteriorFlawed
                    {
                        cuarto = cuartoVent,
                        posCristalInterior = puntoInterior,
                        posExteriorEntidad = posExt,
                        rotMirandoAdentro = Quaternion.LookRotation(-haciaAfuera, Vector3.up)
                    });
                }
            }

            // Detectar sillones y sillas interiores reales (Sala, Cocina/Comedor y Recámaras) para el Hombre de Traje sentado
            bool esSillonSala = (nl == "armchair_15.001" || nl == "armchair_17.001") && cuartoMueble == "LivingRoom";
            bool esSillaInterior = (nl == "chair_01" || nl == "chair_02" || nl == "chair_03" || nl == "chair_04" || nl == "chair_11") &&
                                   (cuartoMueble == "Kitchen" || cuartoMueble == "LivingRoom" || cuartoMueble.StartsWith("Bedroom"));
            if (esSillonSala || esSillaInterior)
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Vector3[] vs = mf.sharedMesh.vertices;
                    Transform tr = mr.transform;
                    float umbralRespaldoY = b.min.y + 0.68f * b.size.y;
                    Vector3 sumaRespaldo = Vector3.zero;
                    int cntRespaldo = 0;
                    int paso = Mathf.Max(1, vs.Length / 250);
                    for (int iv = 0; iv < vs.Length; iv += paso)
                    {
                        Vector3 wp = tr.TransformPoint(vs[iv]);
                        if (wp.y > umbralRespaldoY)
                        {
                            sumaRespaldo += wp;
                            cntRespaldo++;
                        }
                    }

                    Vector3 dirFrente = Vector3.forward;
                    if (cntRespaldo > 0)
                    {
                        Vector3 promRespaldo = sumaRespaldo / cntRespaldo;
                        dirFrente = new Vector3(b.center.x - promRespaldo.x, 0f, b.center.z - promRespaldo.z);
                    }
                    if (dirFrente.sqrMagnitude < 0.001f)
                        dirFrente = ObtenerCentroDeHabitacion(cuartoMueble) - b.center;
                    dirFrente.y = 0f;
                    dirFrente.Normalize();

                    float pisoAsiento = b.min.y > 3.15f ? 3.74f : 0.41f;
                    float avanceFrente = esSillonSala ? 0.18f : 0.14f;
                    Vector3 posSentado = new Vector3(b.center.x, pisoAsiento, b.center.z) + dirFrente * avanceFrente;

                    cacheAsientosFlawed.Add(new InfoAsientoCasa
                    {
                        cuarto = cuartoMueble,
                        posPisoFrenteAsiento = posSentado,
                        rotSentado = Quaternion.LookRotation(dirFrente, Vector3.up)
                    });
                }
            }

            if (nl == "bed_01" || nl == "bed_02" || nl == "bed_03")
            {
                // Alturas exactas medidas por raycast sobre la superficie del colchón en House.fbx
                float offsetColchon = nl == "bed_01" ? 0.742f : (nl == "bed_02" ? 0.659f : 0.653f);
                cacheCamasPorCuarto[cuartoMueble] = new InfoCamaCasa { mr = mr, offsetColchonY = offsetColchon };
            }
            else if (nl == "bathtub" && b.center.y > 3.15f)
            {
                // Bañera exclusiva del baño superior (el baño de abajo no tiene bañera)
                cacheBathtubCasaArriba = mr;
            }
            else if (nl == "shelving.002" || (nl == "shelving" && !cacheCoberturaPureForm.ContainsKey("Garage")))
            {
                cacheCoberturaPureForm["Garage"] = mr;
            }
            else if (nl == "shelving.003")
            {
                cacheCoberturaPureForm["Laundry"] = mr;
            }
            else if (nl == "shower")
            {
                cacheCoberturaPureForm["Bathroom"] = mr;
            }
            else if (nl == "refrigerator.001")
            {
                cacheCoberturaPureForm["Kitchen"] = mr;
            }
            else if (nl == "wardrobe")
            {
                cacheCoberturaPureForm["Bedroom1"] = mr;
            }
            else if (nl == "armchair_17.001")
            {
                cacheCoberturaPureForm["LivingRoom"] = mr;
            }
        }

        cacheMueblesContextualesListo = true;
    }

    private static float DetectarTechoRealSobrePunto(Vector3 puntoBase, string cuarto)
    {
        float techoDefault = puntoBase.y > 3.15f ? 6.26f : (cuarto == "Garage" ? 3.25f : 3.42f);
        Vector3 origen = new Vector3(puntoBase.x, puntoBase.y + 1.35f, puntoBase.z);
        RaycastHit[] hits = Physics.RaycastAll(origen, Vector3.up, 3.6f, ~0, QueryTriggerInteraction.Ignore);
        float menorY = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || hits[i].collider.isTrigger) continue;
            if (hits[i].normal.y < -0.65f && hits[i].point.y > puntoBase.y + 2.15f && hits[i].point.y < menorY)
                menorY = hits[i].point.y;
        }
        return menorY < float.MaxValue ? menorY : techoDefault;
    }

    private static void ElegirUnicoHijoAleatorio(Transform contenedor)
    {
        if (contenedor == null || contenedor.childCount == 0) return;
        int elegido = Random.Range(0, contenedor.childCount);
        for (int i = 0; i < contenedor.childCount; i++)
            contenedor.GetChild(i).gameObject.SetActive(i == elegido);
    }

    private void ReubicarCorpseContextual(Transform cam)
    {
        InicializarCacheMueblesContextuales();

        // Si había una bañera ocultada previamente, restaurarla antes de reubicar
        if (bathtubCasaOcultadaPorCorpse != null)
        {
            bathtubCasaOcultadaPorCorpse.enabled = true;
            bathtubCasaOcultadaPorCorpse = null;
        }

        Transform visual = transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");
        Transform grpAcostado = visual != null ? visual.Find("Corpse_Acostado") : null;
        Transform grpColgado  = visual != null ? visual.Find("Corpse_Colgado")  : null;
        Transform grpBanera   = visual != null ? visual.Find("Corpse_Banera")   : null;
        AnimadorEntidadTerror anim = visual != null ? visual.GetComponent<AnimadorEntidadTerror>() : null;

        List<PuntoAparicionCuarto> candidatos = new List<PuntoAparicionCuarto>();
        for (int i = 0; i < PuntosHabitaciones.Length; i++)
        {
            PuntoAparicionCuarto p = PuntosHabitaciones[i];
            // Si el cuarto es Bathroom, la única ubicación permitida para Corpse es la bañera del baño superior
            if (p.cuarto == "Bathroom" && (cacheBathtubCasaArriba == null || grpBanera == null))
                continue;

            Vector3 refPos = (p.cuarto == "Bathroom" && cacheBathtubCasaArriba != null)
                ? cacheBathtubCasaArriba.bounds.center
                : p.posCentro;

            if (!PuntoVisibleOMismoCuartoJugador(cam, refPos, p.cuarto))
                candidatos.Add(p);
        }

        if (candidatos.Count == 0)
        {
            for (int i = 0; i < PuntosHabitaciones.Length; i++)
            {
                if (PuntosHabitaciones[i].cuarto == "Bathroom" && (cacheBathtubCasaArriba == null || grpBanera == null))
                    continue;
                candidatos.Add(PuntosHabitaciones[i]);
            }
        }

        PuntoAparicionCuarto elegido = candidatos[Random.Range(0, candidatos.Count)];
        nombreCuarto = elegido.cuarto;

        // Reiniciar subgrupos y transform local del contenedor visual
        if (visual != null)
        {
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
        }
        if (grpAcostado != null) grpAcostado.gameObject.SetActive(false);
        if (grpColgado != null)  grpColgado.gameObject.SetActive(false);
        if (grpBanera != null)   grpBanera.gameObject.SetActive(false);

        // CASO 1: BAÑO -> EXCLUSIVAMENTE la variante de bañera en el baño de arriba que sí tiene bathtub
        if (nombreCuarto == "Bathroom" && cacheBathtubCasaArriba != null && grpBanera != null)
        {
            Bounds tb = cacheBathtubCasaArriba.bounds;
            bathtubCasaOcultadaPorCorpse = cacheBathtubCasaArriba;
            bathtubCasaOcultadaPorCorpse.enabled = false;

            grpBanera.gameObject.SetActive(true);
            Transform tina = grpBanera.Find("Tina_Con_Cuerpo");
            if (tina != null)
            {
                Transform blood = tina.Find("Blood");
                if (blood != null) blood.gameObject.SetActive(true);

                string[] nombresCuerposTina = { "deadbody_Tub1", "deadbody_Tub2", "deadbody_Tub3" };
                int idxCuerpo = Random.Range(0, nombresCuerposTina.Length);
                for (int c = 0; c < nombresCuerposTina.Length; c++)
                {
                    Transform tc = tina.Find(nombresCuerposTina[c]);
                    if (tc != null) tc.gameObject.SetActive(c == idxCuerpo);
                }
            }

            float rotBaneraY = tb.size.x >= tb.size.z ? 0f : 90f;
            transform.position = new Vector3(tb.center.x, tb.min.y, tb.center.z);
            transform.rotation = Quaternion.Euler(0f, rotBaneraY, 0f);

            if (anim != null)
            {
                anim.modoCorpse = ModoCorpseContextual.EnBanera;
                anim.GuardarTransformBase();
            }
            return;
        }

        // CASO 2: RECÁMARAS (Bedroom1, Bedroom2, Bedroom3) -> 50% Acostado sobre la cama / 50% Colgado del techo
        bool esRecamara = nombreCuarto.StartsWith("Bedroom", System.StringComparison.OrdinalIgnoreCase);
        bool usarColgado = grpColgado != null && grpColgado.childCount > 0 && Random.value < 0.50f;

        if (esRecamara && !usarColgado && cacheCamasPorCuarto.TryGetValue(nombreCuarto, out InfoCamaCasa infoCama) && infoCama.mr != null)
        {
            Bounds cb = infoCama.mr.bounds;
            if (grpAcostado != null)
            {
                grpAcostado.gameObject.SetActive(true);
                ElegirUnicoHijoAleatorio(grpAcostado);
            }

            // Orientar el cuerpo a lo largo del eje longitudinal de la cama con un leve ángulo orgánico
            float rotCamaY = (cb.size.x > cb.size.z ? 90f : 0f) + (Random.value < 0.5f ? 0f : 180f) + Random.Range(-9f, 9f);
            transform.position = new Vector3(cb.center.x, cb.min.y + infoCama.offsetColchonY, cb.center.z);
            transform.rotation = Quaternion.Euler(0f, rotCamaY, 0f);

            if (anim != null)
            {
                anim.modoCorpse = ModoCorpseContextual.Acostado;
                anim.GuardarTransformBase();
            }
            return;
        }

        // CASO 3: COLGADO DEL TECHO (en Recámara o en Sala/Cocina/Garaje/Lavandería)
        if (usarColgado && grpColgado != null)
        {
            float techoY = DetectarTechoRealSobrePunto(elegido.posCentro, nombreCuarto);
            grpColgado.gameObject.SetActive(true);
            ElegirUnicoHijoAleatorio(grpColgado);

            transform.position = new Vector3(elegido.posCentro.x, techoY + 0.02f, elegido.posCentro.z);
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            if (anim != null)
            {
                anim.modoCorpse = ModoCorpseContextual.Colgado;
                anim.GuardarTransformBase();
            }
            return;
        }

        // CASO 4: ACOSTADO EN EL SUELO (Sala, Cocina, Garaje o Lavandería)
        float pisoY = ObtenerAlturaPisoSegura(elegido.posCentro, nombreCuarto);

        if (grpAcostado != null)
        {
            grpAcostado.gameObject.SetActive(true);
            ElegirUnicoHijoAleatorio(grpAcostado);
        }

        transform.position = new Vector3(elegido.posCentro.x, pisoY + 0.01f, elegido.posCentro.z);
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        if (anim != null)
        {
            anim.modoCorpse = ModoCorpseContextual.Acostado;
            anim.GuardarTransformBase();
        }
    }

    private void ReubicarPureFormConVariantes(Transform cam)
    {
        InicializarCacheMueblesContextuales();

        List<PuntoAparicionCuarto> candidatos = new List<PuntoAparicionCuarto>();
        for (int i = 0; i < PuntosHabitaciones.Length; i++)
        {
            if (!CuartoTieneOtraAnomaliaActiva(PuntosHabitaciones[i].cuarto, this) &&
                !PuntoVisibleOMismoCuartoJugador(cam, PuntosHabitaciones[i].posCentro, PuntosHabitaciones[i].cuarto))
                candidatos.Add(PuntosHabitaciones[i]);
        }
        if (candidatos.Count == 0)
        {
            for (int i = 0; i < PuntosHabitaciones.Length; i++)
            {
                if (!PuntoVisibleOMismoCuartoJugador(cam, PuntosHabitaciones[i].posCentro, PuntosHabitaciones[i].cuarto))
                    candidatos.Add(PuntosHabitaciones[i]);
            }
        }
        if (candidatos.Count == 0)
            candidatos.AddRange(PuntosHabitaciones);

        PuntoAparicionCuarto elegido = candidatos[Random.Range(0, candidatos.Count)];
        nombreCuarto = elegido.cuarto;

        Transform visual = transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");
        AnimadorEntidadTerror anim = visual != null ? visual.GetComponent<AnimadorEntidadTerror>() : null;
        if (visual != null)
        {
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
        }

        // 50% Variante Directa (se planta de frente al jugador) / 50% Variante Acechador Escondido (se asoma detrás de muebles o rincones)
        bool varianteEscondida = Random.value < 0.50f;
        Vector3 posObjetivo = elegido.posCentro;
        float signoAsomo = Random.value < 0.5f ? 1f : -1f;

        if (varianteEscondida)
        {
            posObjetivo = elegido.posRincon;

            // Si el cuarto cuenta con un mueble alto ideal (estantería, regadera, refrigerador, ropero o sofá), asomarse justo detrás de su borde
            if (cacheCoberturaPureForm.TryGetValue(nombreCuarto, out MeshRenderer mrCov) && mrCov != null)
            {
                Bounds bc = mrCov.bounds;
                Vector3 haciaCentro = elegido.posCentro - bc.center;
                haciaCentro.y = 0f;
                if (haciaCentro.sqrMagnitude < 0.01f) haciaCentro = Vector3.forward;
                haciaCentro.Normalize();

                Vector3 lateral = Vector3.Cross(Vector3.up, haciaCentro).normalized;
                float medioAncho = Mathf.Max(bc.extents.x, bc.extents.z);
                Vector3 candidatoAsomo = new Vector3(bc.center.x, elegido.posCentro.y, bc.center.z)
                                         + lateral * (signoAsomo * (medioAncho + 0.14f))
                                         - haciaCentro * 0.16f;

                // Verificar que el punto de asomo no quede dentro de un muro
                if (!Physics.CheckSphere(candidatoAsomo + Vector3.up * 0.9f, 0.16f, ~0, QueryTriggerInteraction.Ignore))
                    posObjetivo = candidatoAsomo;
            }
        }

        float alturaPiso = ObtenerAlturaPisoSegura(posObjetivo, nombreCuarto);

        transform.position = new Vector3(posObjetivo.x, alturaPiso, posObjetivo.z);

        Vector3 mirarHacia = varianteEscondida ? elegido.posCentro : (cam != null ? cam.position : elegido.posCentro);
        Vector3 dirVista = mirarHacia - transform.position;
        dirVista.y = 0f;
        if (dirVista.sqrMagnitude < 0.01f) dirVista = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(dirVista.normalized, Vector3.up);

        if (anim != null)
        {
            anim.modoPureFormEscondido = varianteEscondida;
            anim.inclinacionAsomoZ = varianteEscondida ? (-signoAsomo * 16.5f) : 0f;
            anim.offsetRetraccionLocal = new Vector3(signoAsomo * -0.18f, -0.04f, -0.10f);
            anim.mirarAlJugador = true;
            anim.GuardarTransformBase();
            anim.InicializarAnimatorYAnimationRigging();
        }
    }

    private void ReubicarMensajeParedEnHabitacionAleatoria()
    {
        PuntoAparicionCuarto elegido = PuntosHabitaciones[Random.Range(0, PuntosHabitaciones.Length)];
        nombreCuarto = elegido.cuarto;

        TextMeshPro tmp = GetComponentInChildren<TextMeshPro>(true);
        if (tmp != null)
        {
            tmp.text = MensajesParedEspanol[Random.Range(0, MensajesParedEspanol.Length)];
        }

        Vector3 origenBusqueda = new Vector3(elegido.posCentro.x, elegido.posCentro.y + 1.38f, elegido.posCentro.z);
        Vector3[] direcciones = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        RaycastHit mejorHit = default;
        float mejorPuntaje = float.MaxValue;
        bool encontroPared = false;

        foreach (Vector3 dir in direcciones)
        {
            RaycastHit[] hits = Physics.RaycastAll(origenBusqueda, dir, 4.5f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int h = 0; h < hits.Length; h++)
            {
                RaycastHit hit = hits[h];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (Mathf.Abs(hit.normal.y) > 0.2f) continue;

                // Solo pegar el texto sobre muros arquitectónicos reales (House_Colliders), NUNCA sobre muebles, puertas ni ventanas
                if (!EsMuroArquitectonicoReal(hit.collider.transform)) continue;
                if (HayVentanaCercaDePunto(hit.point)) continue;

                // Verificar que la pared sea suficientemente ancha (±0.65m a ambos lados) para que el texto nunca sobresalga en el aire
                Vector3 tangente = Vector3.Cross(Vector3.up, hit.normal).normalized;
                Vector3 puntoIzq = origenBusqueda + tangente * 0.65f;
                Vector3 puntoDer = origenBusqueda - tangente * 0.65f;
                bool anchoOk =
                    TieneMuroEnMismoPlano(puntoIzq, dir, hit.distance, hit.normal) &&
                    TieneMuroEnMismoPlano(puntoDer, dir, hit.distance, hit.normal);

                float puntaje = hit.distance + (anchoOk ? 0f : 10f);
                if (puntaje < mejorPuntaje)
                {
                    mejorPuntaje = puntaje;
                    mejorHit = hit;
                    encontroPared = true;
                }
                break;
            }
        }

        if (encontroPared)
        {
            transform.position = mejorHit.point + mejorHit.normal * 0.025f;
            transform.rotation = Quaternion.LookRotation(-mejorHit.normal, Vector3.up);
        }
        else
        {
            transform.position = elegido.posPared;
            transform.rotation = Quaternion.Euler(0f, elegido.rotParedY, 0f);
        }
    }

    private static bool EsMuroArquitectonicoReal(Transform t)
    {
        Transform cur = t;
        while (cur != null)
        {
            string n = cur.name.ToLowerInvariant();
            if (n.Contains("collider_mueble") || n.Contains("door") || n.Contains("window") || n.Contains("hitbox"))
                return false;
            if (n == "house_colliders" || n.StartsWith("colliders"))
                return true;
            cur = cur.parent;
        }
        return false;
    }

    private static bool TieneMuroEnMismoPlano(Vector3 origen, Vector3 dir, float distEsperada, Vector3 normalEsperada)
    {
        RaycastHit[] hits = Physics.RaycastAll(origen, dir, distEsperada + 0.35f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || hits[i].collider.isTrigger) continue;
            if (!EsMuroArquitectonicoReal(hits[i].collider.transform)) continue;
            if (Mathf.Abs(hits[i].distance - distEsperada) < 0.20f && Vector3.Dot(hits[i].normal, normalEsperada) > 0.92f)
                return true;
        }
        return false;
    }

    private static bool HayVentanaCercaDePunto(Vector3 punto)
    {
        if (cacheVentanasCasa == null)
        {
            cacheVentanasCasa = new List<Bounds>();
            GameObject house = GameObject.Find("House");
            if (house != null)
            {
                foreach (MeshRenderer mr in house.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr == null) continue;
                    string n = mr.name.ToLowerInvariant();
                    if (n.StartsWith("window"))
                        cacheVentanasCasa.Add(mr.bounds);
                }
            }
        }

        for (int i = 0; i < cacheVentanasCasa.Count; i++)
        {
            Bounds b = cacheVentanasCasa[i];
            b.Expand(new Vector3(0.95f, 0.65f, 0.95f));
            if (b.Contains(punto))
                return true;
        }
        return false;
    }

    private static Material ObtenerMaterialCieloDiaRealista()
    {
        if (materialCieloDiaVentana != null) return materialCieloDiaVentana;

        int w = 128;
        int h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Tex_GlareSolCristal_Unknown_VR",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        // Deslumbramiento óptico solar limpio en el vidrio: casi transparente en los bordes (para ver nítido el jardín real y el cielo)
        // y halo diagonal cálido de sol matutino arriba al centro
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / (w - 1);
                float dx = (u - 0.56f) * 1.55f;
                float dy = (v - 0.68f) * 1.45f;
                float distSol = Mathf.Sqrt(dx * dx + dy * dy);
                float nucleo = Mathf.Exp(-distSol * distSol * 7.5f);
                float rayoDiag = Mathf.Exp(-Mathf.Pow((u - 0.56f) + (v - 0.68f) * 0.65f, 2f) * 18f) * Mathf.Clamp01(1.1f - distSol);
                float borde = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 7f);

                float alfa = Mathf.Clamp(0.04f + nucleo * 0.34f + rayoDiag * 0.14f, 0.03f, 0.42f) * Mathf.Lerp(0.45f, 1f, borde);
                Color col = Color.Lerp(new Color(0.80f, 0.92f, 1.00f, alfa), new Color(1.00f, 0.98f, 0.90f, alfa), Mathf.Clamp01(nucleo * 1.5f + 0.25f));
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();

        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        materialCieloDiaVentana = new Material(sh)
        {
            name = "Mat_VeloSol_Ventana_Unknown",
            color = Color.white
        };
        materialCieloDiaVentana.SetTexture("_BaseMap", tex);
        materialCieloDiaVentana.SetTexture("_MainTex", tex);
        materialCieloDiaVentana.mainTexture = tex;
        return materialCieloDiaVentana;
    }

    private static Material ObtenerMaterialDomoCieloPanoramicoDia()
    {
        if (materialDomoCieloPanoramicoDia != null) return materialDomoCieloPanoramicoDia;

        int w = 256;
        int h = 192;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Tex_DomoCieloDia_Unknown_VR",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        // Cielo diurno hiper-luminoso (inmune a la niebla negra nocturna gracias a Sprites/Default):
        // Elimina al 100% el vacío negro detrás de los arbustos/cerca y muestra cielo azul celeste, nubes soleadas, disco solar y bruma matinal
        Color azulCenit    = new Color(0.28f, 0.61f, 0.96f, 1f);
        Color celesteMedio = new Color(0.62f, 0.85f, 1.00f, 1f);
        Color oroHorizonte = new Color(1.00f, 0.96f, 0.86f, 1f);
        Color pradoLejano  = new Color(0.42f, 0.56f, 0.36f, 1f);
        Color arboledaSol  = new Color(0.30f, 0.44f, 0.29f, 1f);

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / (w - 1);
                Color col;

                // Línea de horizonte suave con arboleda lejana bañada por bruma solar matutina (v < 0.24)
                float siluetaArboles = 0.21f + (Mathf.PerlinNoise(u * 14f + 3.1f, 1.7f) - 0.5f) * 0.065f
                                             + (Mathf.PerlinNoise(u * 34f + 7.4f, 4.2f) - 0.5f) * 0.025f;
                if (v < siluetaArboles)
                {
                    float tTierra = Mathf.Clamp01(v / Mathf.Max(0.05f, siluetaArboles));
                    Color baseVeg = Color.Lerp(pradoLejano, arboledaSol, Mathf.PerlinNoise(u * 22f, v * 18f));
                    // Bruma atmosférica dorada cerca del horizonte para profundidad realista
                    col = Color.Lerp(baseVeg, oroHorizonte, Mathf.Pow(tTierra, 2.2f) * 0.62f);
                }
                else
                {
                    float tCielo = Mathf.Clamp01((v - 0.20f) / 0.80f);
                    col = tCielo < 0.38f
                        ? Color.Lerp(oroHorizonte, celesteMedio, tCielo / 0.38f)
                        : Color.Lerp(celesteMedio, azulCenit, (tCielo - 0.38f) / 0.62f);

                    // Nubes cúmulos suaves iluminadas desde arriba
                    float n1 = Mathf.PerlinNoise(u * 4.8f + 1.4f, v * 6.2f + 2.3f);
                    float n2 = Mathf.PerlinNoise(u * 11.5f + 5.2f, v * 13.0f + 0.8f);
                    float nube = Mathf.SmoothStep(0.50f, 0.78f, n1 * 0.72f + n2 * 0.28f) * Mathf.Clamp01((v - 0.24f) * 3.2f);
                    col = Color.Lerp(col, new Color(1.0f, 0.99f, 0.96f, 1f), nube * 0.72f);

                    // Sol matutino cegador + corona atmosférica Mie
                    float dx = (u - 0.55f) * 1.65f;
                    float dy = (v - 0.58f) * 1.15f;
                    float dSol = Mathf.Sqrt(dx * dx + dy * dy);
                    float corona = Mathf.Exp(-dSol * 4.2f) * 0.68f + Mathf.Exp(-dSol * dSol * 55f) * 0.95f;
                    col = Color.Lerp(col, new Color(1.0f, 0.99f, 0.93f, 1f), Mathf.Clamp01(corona));
                }

                // Desvanecer únicamente el borde lateral extremo del arco para que nunca se note un corte duro
                float fadeBordeU = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 9.0f);
                col.a = fadeBordeU;
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();

        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        materialDomoCieloPanoramicoDia = new Material(sh)
        {
            name = "Mat_DomoCieloPanoramicoDia_Unknown",
            color = Color.white
        };
        materialDomoCieloPanoramicoDia.SetTexture("_BaseMap", tex);
        materialDomoCieloPanoramicoDia.SetTexture("_MainTex", tex);
        materialDomoCieloPanoramicoDia.mainTexture = tex;
        return materialDomoCieloPanoramicoDia;
    }

    private static Mesh ObtenerMallaArcoPanoramicoCielo()
    {
        if (mallaDomoCieloCurvo != null) return mallaDomoCieloCurvo;

        // Semicilindro curvo de 148° que abraza toda la vista exterior desde cualquier ángulo dentro de la habitación
        const int segsX = 18;
        const float radio = 4.55f;
        const float alto = 6.8f;
        const float anguloMaxGrados = 74f;

        Vector3[] verts = new Vector3[(segsX + 1) * 2];
        Vector2[] uvs = new Vector2[(segsX + 1) * 2];
        int[] tris = new int[segsX * 6];

        for (int i = 0; i <= segsX; i++)
        {
            float u = (float)i / segsX;
            float angRad = Mathf.Lerp(-anguloMaxGrados, anguloMaxGrados, u) * Mathf.Deg2Rad;
            float x = Mathf.Sin(angRad) * radio;
            float z = Mathf.Cos(angRad) * radio;

            verts[i * 2 + 0] = new Vector3(x, -2.2f, z);
            verts[i * 2 + 1] = new Vector3(x, alto - 2.2f, z);
            uvs[i * 2 + 0] = new Vector2(u, 0f);
            uvs[i * 2 + 1] = new Vector2(u, 1f);

            if (i < segsX)
            {
                int b = i * 2;
                int t = i * 6;
                // Caras mirando hacia adentro (-Z/origen, es decir, hacia la ventana de la casa)
                tris[t + 0] = b + 0;
                tris[t + 1] = b + 1;
                tris[t + 2] = b + 2;
                tris[t + 3] = b + 2;
                tris[t + 4] = b + 1;
                tris[t + 5] = b + 3;
            }
        }

        mallaDomoCieloCurvo = new Mesh { name = "Mesh_ArcoCieloDia_Unknown" };
        mallaDomoCieloCurvo.vertices = verts;
        mallaDomoCieloCurvo.uv = uvs;
        mallaDomoCieloCurvo.triangles = tris;
        mallaDomoCieloCurvo.RecalculateNormals();
        mallaDomoCieloCurvo.RecalculateBounds();
        return mallaDomoCieloCurvo;
    }

    private static Material ObtenerMaterialHazVolumetricoSol()
    {
        if (materialHazVolumetricoSol != null) return materialHazVolumetricoSol;

        int w = 64, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Tex_HazVolumetricoSol_Unknown",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < h; y++)
        {
            // v=1 junto al cristal de la ventana, v=0 al desvanecerse dentro del cuarto
            float v = (float)y / (h - 1);
            float longFade = Mathf.Pow(v, 1.35f) * Mathf.Clamp01((1f - v) * 9f);
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / (w - 1);
                float latFade = Mathf.Sin(u * Mathf.PI);
                // Sutiles franjas de barrotes de ventana en el haz de luz
                float franjas = 0.82f + 0.18f * Mathf.Sin(u * Mathf.PI * 5f);
                float alfa = longFade * latFade * latFade * franjas * 0.16f;
                tex.SetPixel(x, y, new Color(1.0f, 0.96f, 0.82f, alfa));
            }
        }
        tex.Apply();

        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        materialHazVolumetricoSol = new Material(sh)
        {
            name = "Mat_HazVolumetricoSol_Unknown",
            color = Color.white,
            renderQueue = 3020
        };
        materialHazVolumetricoSol.SetTexture("_BaseMap", tex);
        materialHazVolumetricoSol.SetTexture("_MainTex", tex);
        materialHazVolumetricoSol.mainTexture = tex;
        return materialHazVolumetricoSol;
    }

    private static Material ObtenerMaterialProyeccionSolPiso()
    {
        if (materialProyeccionSolPiso != null) return materialProyeccionSolPiso;

        int w = 96, h = 96;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Tex_ProyeccionVentanaPiso_Unknown",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            float ey = Mathf.Sin(v * Mathf.PI);
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / (w - 1);
                float ex = Mathf.Sin(u * Mathf.PI);
                // Cruz suave simulando la sombra del marco de la ventana proyectada por el sol sobre el suelo
                float barroteV = Mathf.Abs(u - 0.5f) < 0.035f ? 0.35f : 1f;
                float barroteH = Mathf.Abs(v - 0.5f) < 0.035f ? 0.42f : 1f;
                float alfa = Mathf.Pow(ex * ey, 0.55f) * barroteV * barroteH * 0.24f;
                tex.SetPixel(x, y, new Color(1.0f, 0.95f, 0.78f, alfa));
            }
        }
        tex.Apply();

        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        materialProyeccionSolPiso = new Material(sh)
        {
            name = "Mat_ProyeccionSolPiso_Unknown",
            color = Color.white
        };
        materialProyeccionSolPiso.SetTexture("_BaseMap", tex);
        materialProyeccionSolPiso.SetTexture("_MainTex", tex);
        materialProyeccionSolPiso.mainTexture = tex;
        return materialProyeccionSolPiso;
    }

    private static void ConfigurarIluminacionDiaExteriorReal(Transform contenedorExtra, Bounds wb, Vector3 haciaAfuera)
    {
        Vector3 dirAfuera = haciaAfuera.normalized;
        Vector3 haciaAdentro = -dirAfuera;
        float anchoVentana = Mathf.Clamp(Mathf.Max(wb.size.x, wb.size.z), 0.85f, 3.2f);
        float altoVentana = Mathf.Clamp(wb.size.y, 0.90f, 2.3f);

        // 0. Arco Panorámico de Cielo Diurno afuera de la ventana (a ~4.5m detrás de los arbustos/flores reales, inmune a la niebla nocturna!)
        Transform domoT = contenedorExtra.Find("Cielo_Panoramico_Dia_Unknown");
        GameObject domoObj;
        if (domoT == null)
        {
            domoObj = new GameObject("Cielo_Panoramico_Dia_Unknown");
            domoObj.transform.SetParent(contenedorExtra, true);
        }
        else
        {
            domoObj = domoT.gameObject;
        }
        domoObj.transform.SetParent(null, true);
        domoObj.transform.position = wb.center + dirAfuera * 0.35f;
        domoObj.transform.rotation = Quaternion.LookRotation(dirAfuera, Vector3.up);
        domoObj.transform.localScale = Vector3.one;
        domoObj.transform.SetParent(contenedorExtra, true);

        MeshFilter mfDomo = domoObj.GetComponent<MeshFilter>();
        if (mfDomo == null) mfDomo = domoObj.AddComponent<MeshFilter>();
        mfDomo.sharedMesh = ObtenerMallaArcoPanoramicoCielo();

        MeshRenderer mrDomo = domoObj.GetComponent<MeshRenderer>();
        if (mrDomo == null) mrDomo = domoObj.AddComponent<MeshRenderer>();
        mrDomo.sharedMaterial = ObtenerMaterialDomoCieloPanoramicoDia();
        mrDomo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrDomo.receiveShadows = false;

        // 1. Haces Volumétricos de Sol (God-Rays) entrando oblicuamente desde la ventana hacia la habitación oscura
        Transform hacesT = contenedorExtra.Find("Haces_Volumetricos_Sol_Unknown");
        if (hacesT != null)
        {
            if (Application.isPlaying) Destroy(hacesT.gameObject);
            else DestroyImmediate(hacesT.gameObject);
        }
        GameObject hacesRoot = new GameObject("Haces_Volumetricos_Sol_Unknown");
        hacesRoot.transform.position = wb.center + haciaAdentro * 0.04f;
        hacesRoot.transform.rotation = Quaternion.LookRotation(haciaAdentro, Vector3.up);
        hacesRoot.transform.localScale = Vector3.one;

        Material matHaz = ObtenerMaterialHazVolumetricoSol();
        float[] alturasRel = { 0.24f, 0.0f, -0.22f };
        for (int h = 0; h < alturasRel.Length; h++)
        {
            GameObject qHaz = GameObject.CreatePrimitive(PrimitiveType.Quad);
            qHaz.name = "Rayo_Sol_" + h;
            Collider cH = qHaz.GetComponent<Collider>();
            if (cH != null) { if (Application.isPlaying) Destroy(cH); else DestroyImmediate(cH); }
            qHaz.transform.SetParent(hacesRoot.transform, false);
            // Inclinar el plano para que sea visible tanto de frente como de costado al entrar al cuarto
            qHaz.transform.localPosition = new Vector3(0f, alturasRel[h] * altoVentana * 0.55f - 0.42f, 1.15f);
            qHaz.transform.localRotation = Quaternion.Euler(62f + h * 6f, 0f, 0f);
            qHaz.transform.localScale = new Vector3(anchoVentana * 0.94f, 2.45f, 1f);
            MeshRenderer mrH = qHaz.GetComponent<MeshRenderer>();
            if (mrH != null)
            {
                mrH.sharedMaterial = matHaz;
                mrH.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mrH.receiveShadows = false;
            }
        }

        // Proyección dorada del marco de la ventana sobre el piso interior de madera
        float pisoInteriorY = ObtenerAlturaPisoSegura(wb.center + haciaAdentro * 1.1f);
        GameObject qPiso = GameObject.CreatePrimitive(PrimitiveType.Quad);
        qPiso.name = "Proyeccion_Sol_Piso_Unknown";
        Collider cP = qPiso.GetComponent<Collider>();
        if (cP != null) { if (Application.isPlaying) Destroy(cP); else DestroyImmediate(cP); }
        qPiso.transform.position = new Vector3(wb.center.x, pisoInteriorY + 0.008f, wb.center.z) + haciaAdentro * 1.35f;
        qPiso.transform.rotation = Quaternion.LookRotation(Vector3.down, haciaAdentro) * Quaternion.Euler(0f, 0f, 180f);
        qPiso.transform.rotation = Quaternion.Euler(90f, Quaternion.LookRotation(haciaAdentro, Vector3.up).eulerAngles.y, 0f);
        qPiso.transform.localScale = new Vector3(anchoVentana * 0.95f, 1.55f, 1f);
        MeshRenderer mrPiso = qPiso.GetComponent<MeshRenderer>();
        if (mrPiso != null)
        {
            mrPiso.sharedMaterial = ObtenerMaterialProyeccionSolPiso();
            mrPiso.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrPiso.receiveShadows = false;
        }
        qPiso.transform.SetParent(hacesRoot.transform, true);
        hacesRoot.transform.SetParent(contenedorExtra, true);

        // 2. Luz Solar Exterior Real: ilumina el pasto, los arbustos, las flores y la cerca REALES justo afuera de esa ventana
        Transform solExtT = contenedorExtra.Find("Sol_Jardin_Exterior_Unknown");
        Light solExt = solExtT != null ? solExtT.GetComponent<Light>() : null;
        if (solExt == null)
        {
            GameObject obj = new GameObject("Sol_Jardin_Exterior_Unknown");
            obj.transform.SetParent(contenedorExtra, true);
            solExt = obj.AddComponent<Light>();
        }
        solExt.transform.position = wb.center + dirAfuera * 1.1f + Vector3.up * 2.8f;
        solExt.transform.rotation = Quaternion.LookRotation((dirAfuera + Vector3.down * 0.55f).normalized, Vector3.up);
        solExt.type = LightType.Spot;
        solExt.spotAngle = 152f;
        solExt.innerSpotAngle = 92f;
        solExt.color = new Color(1.0f, 0.96f, 0.85f, 1f);
        solExt.intensity = 7.8f;
        solExt.range = 14f;
        solExt.shadows = LightShadows.None;
        solExt.renderMode = LightRenderMode.ForcePixel;
        solExt.enabled = true;

        // 3. Luz Celeste de Cielo Diurno Exterior: baña los arbustos cercanos con relleno celeste diurno
        Transform cieloExtT = contenedorExtra.Find("Cielo_Exterior_Relleno_Unknown");
        Light cieloExt = cieloExtT != null ? cieloExtT.GetComponent<Light>() : null;
        if (cieloExt == null)
        {
            GameObject obj = new GameObject("Cielo_Exterior_Relleno_Unknown");
            obj.transform.SetParent(contenedorExtra, true);
            cieloExt = obj.AddComponent<Light>();
        }
        cieloExt.transform.position = wb.center + dirAfuera * 2.4f + Vector3.up * 1.4f;
        cieloExt.type = LightType.Point;
        cieloExt.color = new Color(0.72f, 0.88f, 1.0f, 1f);
        cieloExt.intensity = 3.6f;
        cieloExt.range = 10f;
        cieloExt.shadows = LightShadows.None;
        cieloExt.renderMode = LightRenderMode.ForcePixel;
        cieloExt.enabled = true;

        // 4. Haz de Sol Entrando al Cuarto por ESA ventana (ilumina el sofá/cama y el piso de madera adentro del cuarto)
        Transform luzSolT = contenedorExtra.Find("Luz_Sol_Ventana_Unknown");
        Light luzSol = luzSolT != null ? luzSolT.GetComponent<Light>() : null;
        if (luzSol == null)
        {
            GameObject lObj = new GameObject("Luz_Sol_Ventana_Unknown");
            lObj.transform.SetParent(contenedorExtra, true);
            luzSol = lObj.AddComponent<Light>();
        }
        Vector3 dirRayoSol = (haciaAdentro + Vector3.down * 0.42f).normalized;
        luzSol.transform.position = wb.center + dirAfuera * 1.15f + Vector3.up * 0.50f;
        luzSol.transform.rotation = Quaternion.LookRotation(dirRayoSol, Vector3.up);
        luzSol.type = LightType.Spot;
        luzSol.spotAngle = 76f;
        luzSol.innerSpotAngle = 44f;
        luzSol.color = new Color(1.0f, 0.94f, 0.78f, 1f);
        luzSol.intensity = 6.2f;
        luzSol.range = 8.8f;
        luzSol.shadows = LightShadows.None;
        luzSol.renderMode = LightRenderMode.ForcePixel;
        luzSol.enabled = true;
    }

    private void AplicarTransformacionAnomala(VarianteAnomalia v)
    {
        transform.localPosition = posOriginal;
        transform.localRotation = rotOriginal;

        Vector3 centroVisual = miRenderer != null ? miRenderer.bounds.center : transform.position;

        if (v.girarRotacion != Vector3.zero)
        {
            if (v.tipo == TipoAnomalia.Imagery && Mathf.Abs(v.girarRotacion.z) > 90f)
            {
                Vector3 ejeNormal = Vector3.forward;
                if (miRenderer != null)
                {
                    Vector3 sz = miRenderer.bounds.size;
                    ejeNormal = (sz.x < sz.z) ? Vector3.right : Vector3.forward;
                }
                transform.RotateAround(centroVisual, ejeNormal, 180f);
            }
            else
            {
                float anguloY = v.girarRotacion.y != 0f ? v.girarRotacion.y : v.girarRotacion.z;
                Vector3 pivoteGiro = centroVisual;

                if (miRenderer != null && (name.ToLowerInvariant().Contains("door") || name.ToLowerInvariant().Contains("manija")))
                {
                    Bounds b = miRenderer.bounds;
                    pivoteGiro = (b.extents.x > b.extents.z)
                        ? new Vector3(b.min.x, b.center.y, b.center.z)
                        : new Vector3(b.center.x, b.center.y, b.min.z);
                }

                transform.RotateAround(pivoteGiro, Vector3.up, anguloY);
            }
        }

        if (v.moverPosicion != Vector3.zero)
        {
            // Calcular desplazamiento claramente visible hacia zona despejada sin atravesar muros, suelos ni otros muebles/objetos (Anti-Clipping + Soporte real)
            Bounds bOrig = miRenderer != null ? miRenderer.bounds : new Bounds(transform.position, Vector3.one * 0.24f);
            float pisoCuarto = ObtenerAlturaPisoSegura(centroVisual, nombreCuarto);
            string nLower = gameObject.name.ToLowerInvariant();
            bool esObjetoDeEstante = nLower.StartsWith("box") || nLower.StartsWith("paint_pot") || nLower.StartsWith("petrol_can") || nLower.StartsWith("toolbox");
            bool sobreSuperficieElevada = (bOrig.min.y - pisoCuarto) > 0.25f && !esObjetoDeEstante;

            bool puedeLevitar = v.tipo == TipoAnomalia.Displacement &&
                                !nLower.Contains("door") &&
                                !nLower.Contains("drawer") &&
                                !nLower.Contains("cuadro") &&
                                ((v.nombreDescriptivo != null && v.nombreDescriptivo.ToLowerInvariant().Contains("levita")) || Random.value < 0.28f);

            Vector3 centroCuarto = ObtenerCentroDeHabitacion(nombreCuarto);
            Vector3 dirHaciaCentro = new Vector3(centroCuarto.x - centroVisual.x, 0f, centroCuarto.z - centroVisual.z);
            if (dirHaciaCentro.sqrMagnitude < 0.01f)
                dirHaciaCentro = new Vector3(v.moverPosicion.x, 0f, v.moverPosicion.z);
            if (dirHaciaCentro.sqrMagnitude < 0.001f)
                dirHaciaCentro = Vector3.forward;
            dirHaciaCentro.Normalize();

            Vector3 lat = Vector3.Cross(Vector3.up, dirHaciaCentro).normalized;
            Vector3[] candidatosDir = new Vector3[]
            {
                dirHaciaCentro,
                lat,
                -lat,
                (dirHaciaCentro + lat).normalized,
                (dirHaciaCentro - lat).normalized,
                -dirHaciaCentro
            };

            Vector3 halfExtents = Vector3.Max(bOrig.extents * 0.84f, new Vector3(0.06f, 0.04f, 0.06f));
            Vector3 offsetPivoteACentro = bOrig.center - transform.position;
            float distObjetivo = sobreSuperficieElevada
                ? Mathf.Clamp(Mathf.Max(bOrig.size.x, bOrig.size.z) + 0.18f, 0.24f, 0.44f)
                : Mathf.Clamp(new Vector2(v.moverPosicion.x, v.moverPosicion.z).magnitude * 3.2f, 0.46f, 0.68f);

            if (puedeLevitar)
            {
                Vector3 dirLev = dirHaciaCentro;
                float distLev = 0.36f;
                for (int i = 0; i < candidatosDir.Length; i++)
                {
                    Vector3 candCentro = new Vector3(bOrig.center.x, pisoCuarto + 1.02f, bOrig.center.z) + candidatosDir[i] * 0.38f;
                    if (!Physics.Raycast(new Vector3(bOrig.center.x, pisoCuarto + 1.02f, bOrig.center.z), candidatosDir[i], 0.55f, ~0, QueryTriggerInteraction.Ignore) &&
                        EsEspacioLibreDeObstaculos(candCentro, halfExtents, transform, null, -999f))
                    {
                        dirLev = candidatosDir[i];
                        break;
                    }
                }
                Vector3 destXZ = transform.position + dirLev * distLev;
                posBaseLevitacion = new Vector3(destXZ.x, pisoCuarto + Random.Range(0.88f, 1.16f), destXZ.z);
                transform.position = posBaseLevitacion;
                transform.rotation = transform.rotation * Quaternion.Euler(18f, 35f, -14f);
                enLevitacionDisplacement = true;
                return;
            }

            // 1. Si está sobre una mesa/encimera/buró, intentar primero desplazarlo sobre la MISMA superficie sin quedar flotando en el aire ni chocar con otros props
            if (sobreSuperficieElevada)
            {
                float[] escalasDistMesa = { 1.0f, 0.65f };
                for (int e = 0; e < escalasDistMesa.Length; e++)
                {
                    float dPrueba = distObjetivo * escalasDistMesa[e];
                    for (int i = 0; i < candidatosDir.Length; i++)
                    {
                        Vector3 d = candidatosDir[i];
                        Vector3 candCentro = bOrig.center + d * dPrueba;
                        if (Physics.Raycast(new Vector3(candCentro.x, bOrig.max.y + 0.10f, candCentro.z), Vector3.down, out RaycastHit hitSup, 0.65f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (hitSup.collider != null && !hitSup.collider.transform.IsChildOf(transform) &&
                                Mathf.Abs(hitSup.point.y - bOrig.min.y) <= 0.065f)
                            {
                                Vector3 centroAjustado = new Vector3(candCentro.x, hitSup.point.y + bOrig.extents.y + 0.004f, candCentro.z);
                                if (EsEspacioLibreDeObstaculos(centroAjustado, halfExtents, transform, hitSup.collider.transform, hitSup.point.y))
                                {
                                    transform.position = centroAjustado - offsetPivoteACentro;
                                    transform.RotateAround(centroAjustado, Vector3.up, 26f);
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            // 2. Colocar en espacio despejado sobre el piso real de la habitación (ya sea mueble de suelo, caja de estante o prop tirado frente a su mesa)
            float distPisoBase = (esObjetoDeEstante || sobreSuperficieElevada) ? 0.56f : distObjetivo;
            float[] distanciasPiso = { distPisoBase, 0.40f, 0.72f };
            for (int dp = 0; dp < distanciasPiso.Length; dp++)
            {
                float distActual = distanciasPiso[dp];
                for (int i = 0; i < candidatosDir.Length; i++)
                {
                    Vector3 d = candidatosDir[i];
                    if (Physics.Raycast(new Vector3(bOrig.center.x, pisoCuarto + 0.55f, bOrig.center.z), d, distActual + halfExtents.x + 0.12f, ~0, QueryTriggerInteraction.Ignore))
                        continue;

                    Vector3 candXZ = bOrig.center + d * distActual;
                    float ySuelo = ObtenerAlturaPisoSegura(new Vector3(candXZ.x, pisoCuarto + 0.2f, candXZ.z), nombreCuarto);
                    Vector3 centroEnSuelo = new Vector3(candXZ.x, ySuelo + bOrig.extents.y + 0.005f, candXZ.z);

                    if (EsEspacioLibreDeObstaculos(centroEnSuelo, halfExtents, transform, null, ySuelo))
                    {
                        transform.position = centroEnSuelo - offsetPivoteACentro;
                        if (esObjetoDeEstante || sobreSuperficieElevada)
                            transform.RotateAround(centroEnSuelo, Vector3.up, 32f);
                        return;
                    }
                }
            }

            // Fallback seguro: rotación visible en su propio sitio sin penetrar geometría vecina
            transform.RotateAround(centroVisual, Vector3.up, 42f);
        }
    }

    private void PosicionarClonMimicSinClipping(GameObject clon)
    {
        if (clon == null || miRenderer == null) return;
        MeshRenderer mrClon = clon.GetComponent<MeshRenderer>();
        if (mrClon == null) return;

        Bounds bOrig = miRenderer.bounds;
        float pisoBase = ObtenerAlturaPisoSegura(bOrig.center, nombreCuarto);
        bool sobreMueble = (bOrig.min.y - pisoBase) > 0.24f;

        // Alinear temporalmente con el original para medir el desfase exacto entre transform.position y bounds.center del clon
        clon.transform.position = transform.position;
        clon.transform.rotation = transform.rotation * Quaternion.Euler(0f, 28f, 0f);
        Bounds bClonBase = mrClon.bounds;
        Vector3 offsetPivoteClon = bClonBase.center - clon.transform.position;
        Vector3 halfExt = Vector3.Max(bOrig.extents * 0.84f, new Vector3(0.05f, 0.035f, 0.05f));

        Vector3 centroCuarto = ObtenerCentroDeHabitacion(nombreCuarto);
        Vector3 haciaCentro = new Vector3(centroCuarto.x - bOrig.center.x, 0f, centroCuarto.z - bOrig.center.z);
        if (haciaCentro.sqrMagnitude < 0.01f) haciaCentro = Vector3.forward;
        haciaCentro.Normalize();
        Vector3 lat = Vector3.Cross(Vector3.up, haciaCentro).normalized;

        Vector3[] dirs = new Vector3[]
        {
            lat,
            -lat,
            (lat + haciaCentro * 0.45f).normalized,
            (-lat + haciaCentro * 0.45f).normalized,
            haciaCentro,
            (lat - haciaCentro * 0.45f).normalized,
            (-lat - haciaCentro * 0.45f).normalized,
            -haciaCentro
        };

        float anchoMax = Mathf.Max(bOrig.size.x, bOrig.size.z);
        float sep1 = Mathf.Clamp(anchoMax + 0.09f, 0.18f, 0.36f);
        float sep2 = Mathf.Clamp(anchoMax + 0.20f, 0.28f, 0.52f);
        float[] seps = { sep1, sep2 };

        // 1. Si el objeto original está sobre una mesa/lavabo/encimera, colocar el clon sobre la MISMA superficie sin salirse al aire ni chocar
        if (sobreMueble)
        {
            for (int s = 0; s < seps.Length; s++)
            {
                for (int d = 0; d < dirs.Length; d++)
                {
                    Vector3 cand = bOrig.center + dirs[d] * seps[s];
                    if (Physics.Raycast(new Vector3(cand.x, bOrig.max.y + 0.10f, cand.z), Vector3.down, out RaycastHit hitSup, 0.55f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hitSup.collider != null &&
                            !hitSup.collider.transform.IsChildOf(transform) &&
                            !hitSup.collider.transform.IsChildOf(clon.transform) &&
                            Mathf.Abs(hitSup.point.y - bOrig.min.y) <= 0.065f)
                        {
                            Vector3 centroFinal = new Vector3(cand.x, hitSup.point.y + bOrig.extents.y + 0.003f, cand.z);
                            if (EsEspacioLibreDeObstaculos(centroFinal, halfExt, clon.transform, hitSup.collider.transform, hitSup.point.y))
                            {
                                clon.transform.position = centroFinal - offsetPivoteClon;
                                return;
                            }
                        }
                    }
                }
            }
        }

        // 2. Si está en el suelo (o la superficie superior era muy estrecha), apoyarlo limpiamente sobre el piso despejado al lado sin flotar
        float[] sepsPiso = { sep2, sep2 + 0.16f, sep1 };
        for (int s = 0; s < sepsPiso.Length; s++)
        {
            for (int d = 0; d < dirs.Length; d++)
            {
                Vector3 cand = bOrig.center + dirs[d] * sepsPiso[s];
                float ySuelo = ObtenerAlturaPisoSegura(new Vector3(cand.x, pisoBase + 0.2f, cand.z), nombreCuarto);
                Vector3 centroEnSuelo = new Vector3(cand.x, ySuelo + bOrig.extents.y + 0.004f, cand.z);
                if (EsEspacioLibreDeObstaculos(centroEnSuelo, halfExt, clon.transform, null, ySuelo))
                {
                    clon.transform.position = centroEnSuelo - offsetPivoteClon;
                    return;
                }
            }
        }
    }

    private static bool EsEspacioLibreDeObstaculos(Vector3 centroCaja, Vector3 halfExtents, Transform ignorarA, Transform ignorarSoporte, float alturaSuperficieApoyo)
    {
        Collider[] cols = Physics.OverlapBox(centroCaja, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c == null || !c.enabled || c.isTrigger) continue;
            Transform ct = c.transform;
            if (ignorarA != null && (ct == ignorarA || ct.IsChildOf(ignorarA))) continue;
            if (ignorarSoporte != null && (ct == ignorarSoporte || ct.IsChildOf(ignorarSoporte))) continue;

            // Ignorar únicamente la losa del suelo o la superficie plana exacta debajo del objeto
            string cn = ct.name.ToLowerInvariant();
            if (cn.Contains("floor") && c.bounds.max.y <= alturaSuperficieApoyo + 0.05f) continue;
            if (alturaSuperficieApoyo > -100f && c.bounds.max.y <= alturaSuperficieApoyo + 0.035f) continue;

            return false;
        }
        return true;
    }

    void Update()
    {
        if (estaActiva && enLevitacionDisplacement)
        {
            float flotar = Mathf.Sin(Time.time * 1.8f) * 0.045f;
            transform.position = posBaseLevitacion + new Vector3(0f, flotar, 0f);
            transform.Rotate(new Vector3(12f, 24f, 8f) * Time.deltaTime, Space.World);
        }

        if (estaActiva && tipoActivoActual == TipoAnomalia.Preacher && varianteActual != null && varianteActual.audioEvento != null)
        {
            ActualizarAtenuacionEspacialPreacher(varianteActual.audioEvento);
        }
    }

    public static void ConfigurarAudioEspacialPreacher(AudioSource aud)
    {
        if (aud == null) return;
        aud.playOnAwake = false;
        aud.spatialBlend = 1.0f;
        aud.dopplerLevel = 0f;
        aud.spread = 20f;
        aud.minDistance = 0.45f;
        aud.maxDistance = 5.4f;
        aud.rolloffMode = AudioRolloffMode.Custom;
        AnimationCurve curva = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),
            new Keyframe(0.18f, 0.58f),
            new Keyframe(0.45f, 0.22f),
            new Keyframe(0.75f, 0.06f),
            new Keyframe(1.00f, 0.00f)
        );
        aud.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curva);
        aud.loop = true;
    }

    private void ActualizarAtenuacionEspacialPreacher(AudioSource aud)
    {
        if (VRPauseMenu.IsMenuOpen || FinalTurnoVR.EstaEnSecuenciaFinal)
        {
            aud.volume = 0f;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            aud.volume = 0.12f;
            return;
        }

        Vector3 posCam = cam.transform.position;
        Vector3 posPreacher = ObtenerPosicionVisualActiva();

        // Si el jugador está en otro piso (ej. Sala en Piso 1 y Preacher en Recámara 3 en Piso 2), silencio total (0.0f)
        bool mismoPiso = (posCam.y >= 3.15f) == (posPreacher.y >= 3.15f);
        if (!mismoPiso || Mathf.Abs(posCam.y - posPreacher.y) > 2.25f)
        {
            aud.volume = Mathf.MoveTowards(aud.volume, 0f, Time.deltaTime * 1.2f);
            return;
        }

        float dist = Vector3.Distance(posCam, posPreacher);
        const float rangoMaxEscucha = 5.2f;
        if (dist >= rangoMaxEscucha)
        {
            aud.volume = Mathf.MoveTowards(aud.volume, 0f, Time.deltaTime * 1.2f);
            return;
        }

        // Sube gradualmente conforme te acercas al clóset, pero siempre a niveles bajos y sutiles (máx. 0.22f pegado a él)
        float cercania = Mathf.Clamp01(1f - (dist / rangoMaxEscucha));
        float volObjetivo = Mathf.Pow(cercania, 1.65f) * 0.22f;

        // Si hay muros o puertas cerradas de por medio entre el jugador y el clóset, amortiguar fuertemente
        string cuartoJugador = NormalizarCuartoPorPosicion(posCam, "LivingRoom");
        bool mismoCuarto = string.Equals(cuartoJugador, nombreCuarto, System.StringComparison.OrdinalIgnoreCase);
        if (!mismoCuarto)
        {
            volObjetivo *= 0.22f;
        }
        else if (!InteractorUniversalVR.TieneLineaDeVisionDirecta(posCam, posPreacher + Vector3.up * 0.4f, transform))
        {
            volObjetivo *= 0.48f;
        }

        aud.volume = Mathf.MoveTowards(aud.volume, volObjetivo, Time.deltaTime * 0.65f);
    }

    private void ActivarInteractivosMuebleOElectronicoSiCorresponde()
    {
        interactivosAfectadosActuales.Clear();
        InteractivoMuebleYElectronicoVR propio = GetComponent<InteractivoMuebleYElectronicoVR>();
        if (propio == null) return;

        // Muebles (Cajones / Gabinetes) -> Displacement | Aparatos (TV / Refri / Microondas / Radio) -> Electrical
        bool esMueble = propio.tipoInteractivo == TipoInteractivoCasa.CajonDeslizable;
        if ((esMueble && tipoActivoActual == TipoAnomalia.Displacement) ||
            (!esMueble && tipoActivoActual == TipoAnomalia.Electrical))
        {
            propio.ForzarEstadoPorAnomalia(true);
            interactivosAfectadosActuales.Add(propio);

            // Si es un cajón de baño o cómoda, abrir también los cajones hermanos cercanos del mismo mueble para que sea claramente visible
            if (esMueble && transform.parent != null)
            {
                foreach (InteractivoMuebleYElectronicoVR hermano in transform.parent.GetComponentsInChildren<InteractivoMuebleYElectronicoVR>(true))
                {
                    if (hermano != null && hermano != propio && hermano.tipoInteractivo == TipoInteractivoCasa.CajonDeslizable &&
                        Vector3.Distance(hermano.ObtenerPosicionMundial(), propio.ObtenerPosicionMundial()) < 1.2f)
                    {
                        hermano.ForzarEstadoPorAnomalia(true);
                        interactivosAfectadosActuales.Add(hermano);
                    }
                }
            }
        }
    }

    private void CrearOMostrarRastroDisplacement()
    {
        Vector3 centroVisual = miRenderer != null ? miRenderer.bounds.center : transform.position;
        float pisoY = ObtenerAlturaPisoSegura(centroVisual, nombreCuarto);
        if (Physics.Raycast(centroVisual + Vector3.up * 0.15f, Vector3.down, out RaycastHit hitPiso, 2.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hitPiso.point.y >= pisoY - 0.03f)
                pisoY = hitPiso.point.y;
        }

        if (rastroDisplacementActual == null)
        {
            rastroDisplacementActual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rastroDisplacementActual.name = "Rastro_Displacement_VR";
            Collider c = rastroDisplacementActual.GetComponent<Collider>();
            if (c != null) Destroy(c);

            MeshRenderer mr = rastroDisplacementActual.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = ObtenerMaterialRastroPolvo();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        float tam = miRenderer != null
            ? Mathf.Clamp(Mathf.Max(miRenderer.bounds.size.x, miRenderer.bounds.size.z) * 1.15f, 0.32f, 0.95f)
            : 0.45f;

        rastroDisplacementActual.transform.position = new Vector3(centroVisual.x, pisoY + 0.006f, centroVisual.z);
        rastroDisplacementActual.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        rastroDisplacementActual.transform.localScale = new Vector3(tam, tam * 0.75f, 1f);
        rastroDisplacementActual.SetActive(true);
    }

    private static Material ObtenerMaterialRastroPolvo()
    {
        if (materialRastroPolvo != null) return materialRastroPolvo;

        int w = 64, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Tex_Rastro_Displacement_VR",
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < h; y++)
        {
            float ny = (y / (float)(h - 1)) * 2f - 1f;
            for (int x = 0; x < w; x++)
            {
                float nx = (x / (float)(w - 1)) * 2f - 1f;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                float anillo = Mathf.Exp(-Mathf.Pow((dist - 0.58f) * 3.8f, 2f));
                float arrastre = (Mathf.Abs(ny) < 0.35f && nx > -0.5f && nx < 0.7f) ? (1f - Mathf.Abs(ny) / 0.35f) * 0.55f : 0f;
                float ruido = Mathf.PerlinNoise(nx * 5f + 1.3f, ny * 5f + 2.7f);
                float alpha = Mathf.Clamp01((anillo * 0.75f + arrastre) * ruido) * 0.72f * Mathf.Clamp01((1f - dist) * 2.5f);
                tex.SetPixel(x, y, new Color(0.08f, 0.07f, 0.06f, alpha));
            }
        }
        tex.Apply();

        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        materialRastroPolvo = new Material(sh)
        {
            name = "Mat_Rastro_Polvo_Displacement",
            color = new Color(0.12f, 0.10f, 0.09f, 0.75f)
        };
        materialRastroPolvo.SetTexture("_BaseMap", tex);
        materialRastroPolvo.mainTexture = tex;
        return materialRastroPolvo;
    }

    private void ReubicarPuertaAbismalEnHabitacionAleatoria()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        List<PuntoAparicionCuarto> candidatos = new List<PuntoAparicionCuarto>();

        for (int i = 0; i < PuntosHabitaciones.Length; i++)
        {
            if (PuntosHabitaciones[i].cuarto == "Bathroom" || PuntosHabitaciones[i].cuarto == "Laundry")
                continue;
            if (PuntoVisibleOMismoCuartoJugador(cam, PuntosHabitaciones[i].posCentro, PuntosHabitaciones[i].cuarto))
                continue;
            candidatos.Add(PuntosHabitaciones[i]);
        }
        if (candidatos.Count == 0)
            candidatos.AddRange(PuntosHabitaciones);

        PuntoAparicionCuarto elegido = candidatos[Random.Range(0, candidatos.Count)];
        nombreCuarto = elegido.cuarto;

        float pisoY = elegido.posCentro.y > 3.15f ? 3.74f : (elegido.cuarto == "Garage" ? 0.16f : 0.41f);
        Vector3 origenBusqueda = new Vector3(elegido.posCentro.x, pisoY + 1.25f, elegido.posCentro.z);
        Vector3[] direcciones = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        RaycastHit mejorHit = default;
        float mejorPuntaje = float.MaxValue;
        bool encontroPared = false;

        foreach (Vector3 dir in direcciones)
        {
            RaycastHit[] hits = Physics.RaycastAll(origenBusqueda, dir, 4.8f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int h = 0; h < hits.Length; h++)
            {
                RaycastHit hit = hits[h];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (Mathf.Abs(hit.normal.y) > 0.2f) continue;
                if (!EsMuroArquitectonicoReal(hit.collider.transform)) continue;
                if (HayVentanaCercaDePunto(hit.point)) continue;

                Vector3 tangente = Vector3.Cross(Vector3.up, hit.normal).normalized;
                bool anchoOk =
                    TieneMuroEnMismoPlano(origenBusqueda + tangente * 0.55f, dir, hit.distance, hit.normal) &&
                    TieneMuroEnMismoPlano(origenBusqueda - tangente * 0.55f, dir, hit.distance, hit.normal);

                float puntaje = hit.distance + (anchoOk ? 0f : 10f);
                if (puntaje < mejorPuntaje)
                {
                    mejorPuntaje = puntaje;
                    mejorHit = hit;
                    encontroPared = true;
                }
                break;
            }
        }

        Vector3 posEnPiso = encontroPared
            ? new Vector3(mejorHit.point.x, pisoY, mejorHit.point.z) + mejorHit.normal * 0.015f
            : new Vector3(elegido.posPared.x, pisoY, elegido.posPared.z);
        Vector3 normalPared = encontroPared
            ? mejorHit.normal
            : (Quaternion.Euler(0f, elegido.rotParedY, 0f) * Vector3.back);

        PuertaAbismalUnknownVR compPuerta = GetComponentInChildren<PuertaAbismalUnknownVR>(true);
        if (compPuerta != null)
        {
            compPuerta.ReiniciarYPosicionarEnPared(posEnPiso, normalPared);
        }
        else
        {
            transform.position = posEnPiso;
            transform.rotation = Quaternion.LookRotation(normalPared, Vector3.up);
        }
    }

    private void CrearLuzFalloElectrico()
    {
        if (luzElectricaTemporal == null)
        {
            GameObject luzObj = new GameObject("Luz_Fallo_Electrico");
            luzObj.transform.SetParent(transform, false);
            if (miRenderer != null)
                luzObj.transform.position = miRenderer.bounds.center + Vector3.up * 0.15f;
            luzElectricaTemporal = luzObj.AddComponent<Light>();
            luzElectricaTemporal.type = LightType.Point;
            luzElectricaTemporal.color = new Color(0.45f, 0.75f, 1f);
            luzElectricaTemporal.intensity = 1.6f;
            luzElectricaTemporal.range = 3.2f;
            luzElectricaTemporal.shadows = LightShadows.None;
        }
        luzElectricaTemporal.enabled = true;
    }

    public void RestaurarNormalidad()
    {
        if (!estaActiva || varianteActual == null) return;

        VarianteAnomalia v = varianteActual;
        estaActiva = false;
        flawedSentadoActivoActual = false;
        flawedVentanaActivoActual = false;
        EnReaccionReportePreEliminacion = false;
        varianteActual = null;
        enLevitacionDisplacement = false;

        if (luzContraluzVentanaFlawed != null)
            luzContraluzVentanaFlawed.enabled = false;

        if (v.tipo == TipoAnomalia.Tulpa)
        {
            EspejoTulpaVR[] espejos = FindObjectsByType<EspejoTulpaVR>(FindObjectsInactive.Include);
            for (int i = 0; i < espejos.Length; i++)
            {
                if (espejos[i] != null)
                    espejos[i].RestaurarEstadoNormalEspejo();
            }
        }

        if (rastroDisplacementActual != null)
            rastroDisplacementActual.SetActive(false);

        if (bathtubCasaOcultadaPorCorpse != null)
        {
            bathtubCasaOcultadaPorCorpse.enabled = true;
            bathtubCasaOcultadaPorCorpse = null;
        }

        if (puertaClosetAbiertaPorPreacher != null)
        {
            puertaClosetAbiertaPorPreacher.CerrarPuerta(false);
            puertaClosetAbiertaPorPreacher = null;
        }

        if (interactivosAfectadosActuales.Count > 0)
        {
            for (int i = 0; i < interactivosAfectadosActuales.Count; i++)
            {
                if (interactivosAfectadosActuales[i] != null)
                    interactivosAfectadosActuales[i].ForzarEstadoPorAnomalia(false);
            }
            interactivosAfectadosActuales.Clear();
        }

        if (v.puertaQueSeAbreSola != null)
        {
            v.puertaQueSeAbreSola.CerrarPuerta(false);
        }
        else if (!EsEntidadMovilEntreHabitaciones(v.tipo) &&
                 !gameObject.name.ToLowerInvariant().Contains("mensajepared") &&
                 !gameObject.name.ToLowerInvariant().Contains("puertaabismal"))
        {
            transform.localPosition = posOriginal;
            transform.localRotation = rotOriginal;
        }

        if (miRenderer != null) miRenderer.enabled = true;

        if (v.objetoExtraActivable != null)
            v.objetoExtraActivable.SetActive(false);

        if (v.rendererCuadroOTV != null && v.materialOriginalGuardado != null)
            v.rendererCuadroOTV.sharedMaterial = v.materialOriginalGuardado;

        if (luzElectricaTemporal != null)
            luzElectricaTemporal.enabled = false;

        if (v.interruptoresAfectados != null)
        {
            foreach (var sw in v.interruptoresAfectados)
            {
                if (sw != null)
                {
                    sw.ActivarParpadeoAnomalo(false);
                    sw.bloqueadaPorAnomalia = false;
                    bool encender = estadoPrevioSwitches.TryGetValue(sw, out bool previo) ? previo : true;
                    sw.CambiarEstado(encender);
                }
            }
            estadoPrevioSwitches.Clear();
        }

        if (v.audioEvento != null && v.audioEvento.isPlaying)
        {
            if (gameObject.activeInHierarchy)
                StartCoroutine(RutinaDesvanecerAudioEvento(v.audioEvento, 0.22f));
            else
                v.audioEvento.Stop();
        }
    }

    private static System.Collections.IEnumerator RutinaDesvanecerAudioEvento(AudioSource src, float duracion)
    {
        if (src == null || !src.isPlaying) yield break;
        float v0 = src.volume;
        float t = 0f;
        while (t < duracion && src != null && src.isPlaying)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duracion);
            src.volume = v0 * (0.5f * (1f + Mathf.Cos(p * Mathf.PI)));
            yield return null;
        }
        if (src != null)
        {
            src.Stop();
            src.volume = v0;
        }
    }

    private static Material ObtenerMaterialElectricoFallback()
    {
        if (materialElectricoFallback == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            materialElectricoFallback = new Material(sh);

            // Generar textura de estática de TV con franjas de error de sistema
            int w = 128, h = 128;
            Texture2D texEstatica = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            Color[] pix = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                bool franjaError = (y > 54 && y < 74) || (y % 16 == 0);
                for (int x = 0; x < w; x++)
                {
                    float ruido = Random.Range(0.25f, 0.95f);
                    if (franjaError && (x % 8 < 5))
                        pix[y * w + x] = new Color(0.95f, 0.12f, 0.15f, 1f);
                    else
                        pix[y * w + x] = new Color(ruido * 0.75f, ruido * 0.88f, ruido, 1f);
                }
            }
            texEstatica.SetPixels(pix);
            texEstatica.Apply();

            materialElectricoFallback.SetTexture("_BaseMap", texEstatica);
            materialElectricoFallback.mainTexture = texEstatica;
            materialElectricoFallback.color = Color.white;
        }
        return materialElectricoFallback;
    }

    private static Material ObtenerMaterialImageryFallback()
    {
        if (materialImageryFallback == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            materialImageryFallback = new Material(sh);
            materialImageryFallback.color = new Color(0.55f, 0.04f, 0.04f, 1f);
        }
        return materialImageryFallback;
    }

    private static AudioClip ObtenerClipZumbidoElectrico()
    {
#if UNITY_EDITOR
        if (clipZumbidoElectrico == null)
            clipZumbidoElectrico = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/yourugor-tv-static-noise-291374.mp3");
#endif
        if (clipZumbidoElectrico == null)
        {
            Bloque1AnomaliasInteractivasVR b1 = FindAnyObjectByType<Bloque1AnomaliasInteractivasVR>(FindObjectsInactive.Include);
            if (b1 != null && b1.clipEstaticaTV != null)
                clipZumbidoElectrico = b1.clipEstaticaTV;
        }
        if (clipZumbidoElectrico == null)
        {
            int sampleRate = 22050;
            int samples = sampleRate;
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.15f;
                float buzz = (Random.value * 2f - 1f) * 0.08f;
                data[i] = hum + buzz;
            }
            clipZumbidoElectrico = AudioClip.Create("ZumbidoElectrico", samples, 1, sampleRate, false);
            clipZumbidoElectrico.SetData(data, 0);
        }
        return clipZumbidoElectrico;
    }

    private static AudioClip ObtenerClipSusurrosPreacher()
    {
#if UNITY_EDITOR
        if (clipSusurrosPreacher == null)
            clipSusurrosPreacher = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_preacher_original.wav");
#endif
        if (clipSusurrosPreacher == null)
        {
            int sampleRate = 22050;
            int durationSec = 3;
            int samples = sampleRate * durationSec;
            float[] data = new float[samples];
            float filtro1 = 0f;
            float filtro2 = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Modulación silábica de susurros ("psst... shhh... murmullo") en rincón/armario
                float silaba = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 2.4f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 3.9f * t) * 0.4f);
                float respiracion = 0.25f + 0.75f * silaba;

                float blanco = Random.value * 2f - 1f;
                filtro1 += 0.28f * (blanco - filtro1);
                filtro2 += 0.06f * (filtro1 - filtro2);
                float fricativo = (filtro1 - filtro2) * 1.8f;

                float murmulloGrave = Mathf.Sin(2f * Mathf.PI * (110f + 18f * Mathf.Sin(t * 7f)) * t) * 0.12f * silaba;
                data[i] = Mathf.Clamp((fricativo * respiracion + murmulloGrave) * 0.65f, -0.9f, 0.9f);
            }

            clipSusurrosPreacher = AudioClip.Create("SusurrosPreacher", samples, 1, sampleRate, false);
            clipSusurrosPreacher.SetData(data, 0);
        }
        return clipSusurrosPreacher;
    }
}

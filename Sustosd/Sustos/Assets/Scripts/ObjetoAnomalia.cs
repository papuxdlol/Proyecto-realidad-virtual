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
    private static Material materialRastroPolvo;
    private static Material materialSombraPureForm;
    private static Material materialOjosBlancosPureForm;
    private static AudioClip clipZumbidoElectrico;
    private static AudioClip clipSusurrosPreacher;
    private static List<Bounds> cacheVentanasCasa;

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
            nombrePuertaCloset = "" // Closet vestidor abierto entre Wardrobe y Wardrobe_01
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

                // El cristal usa un velo translúcido muy suave de contraluz solar (dejando ver los arbustos, pasto y jardín REALES detrás)
                MeshRenderer mrExtra = extra.GetComponent<MeshRenderer>();
                if (mrExtra != null)
                {
                    mrExtra.enabled = true;
                    mrExtra.sharedMaterial = ObtenerMaterialCieloDiaRealista();
                    mrExtra.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mrExtra.receiveShadows = false;
                }

                // Iluminar de día el JARDÍN Y EXTERIOR 3D REAL afuera de esa ventana + proyectar haz de sol hacia adentro del cuarto
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
        float alturaPiso = pos.y > 2.8f ? 3.74f : 0.41f;

        Vector3 rayStart = new Vector3(pos.x, pos.y + 0.8f, pos.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 4.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.point.y < pos.y + 0.2f)
                alturaPiso = hit.point.y;
        }

        Transform visual = transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");

        if (nombreLower.StartsWith("anomalia_corpse"))
        {
            transform.position = new Vector3(pos.x, alturaPiso + 0.04f, pos.z);
            if (visual != null)
            {
                if (visual.childCount > 0 && visual.GetChild(0).name == "Capsule")
                    visual.GetChild(0).localPosition = Vector3.zero;
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
                ConfigurarAnimadorEnVisual(visual.gameObject, TipoAnomalia.Preacher, false);

            AudioSource aud = GetComponent<AudioSource>();
            if (aud == null) aud = gameObject.AddComponent<AudioSource>();
            aud.playOnAwake = false;
            aud.spatialBlend = 1f;
            aud.minDistance = 1.2f;
            aud.maxDistance = 12f;
            aud.volume = 0.78f;
            aud.loop = true;

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
            // Flawed: Figura humana con proporciones, rostro o extremidades distorsionadas
            transform.position = new Vector3(pos.x, alturaPiso, pos.z);
            if (visual != null)
            {
                // Distorsionar sutilmente las proporciones para acentuar la anatomía anómala de Flawed
                visual.localScale = new Vector3(0.84f, 1.16f, 0.88f);
                ConfigurarAnimadorEnVisual(visual.gameObject, TipoAnomalia.Flawed, true);
            }
        }
    }

    private static void AsegurarAspectoSombraConOjosBlancos(Transform visual)
    {
        if (materialSombraPureForm == null)
        {
            Shader shLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            materialSombraPureForm = new Material(shLit)
            {
                name = "Mat_Sombra_PureForm"
            };
            Color negroSombra = new Color(0.02f, 0.02f, 0.025f, 1f);
            materialSombraPureForm.SetColor("_BaseColor", negroSombra);
            materialSombraPureForm.color = negroSombra;
            materialSombraPureForm.SetFloat("_Smoothness", 0.1f);
        }

        if (materialOjosBlancosPureForm == null)
        {
            Shader shUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            materialOjosBlancosPureForm = new Material(shUnlit)
            {
                name = "Mat_Ojos_Blancos_PureForm",
                color = Color.white
            };
            materialOjosBlancosPureForm.SetColor("_BaseColor", Color.white);
        }

        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject.name.Contains("Ojo_Blanco")) continue;
            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = materialSombraPureForm;
            r.sharedMaterials = mats;
        }

        // Crear o actualizar los 2 ojos blancos brillantes de Pure Form a altura de rostro (~1.54m)
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

            ojoT.localPosition = new Vector3(lado * 0.055f, 1.54f, 0.14f);
            ojoT.localScale = new Vector3(0.032f, 0.022f, 0.025f);
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
            varianteActual.objetoExtraActivable.SetActive(true);

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

        // 6. ¿Suena algo? (Preacher = susurros audibles en bucle; Electrical = estática/zumbido)
        if (varianteActual.audioEvento != null)
        {
            if (varianteActual.tipo == TipoAnomalia.Preacher)
            {
                varianteActual.audioEvento.clip = ObtenerClipSusurrosPreacher();
                varianteActual.audioEvento.pitch = 1.0f;
                varianteActual.audioEvento.volume = 0.85f;
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

    private void ReubicarEntidadEnHabitacionAleatoria(TipoAnomalia tipo)
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;

        // Restricción estricta para el Monje (Preacher): SOLO aparece en habitaciones con Closets y DENTRO/JUNTO al closet
        if (tipo == TipoAnomalia.Preacher)
        {
            List<PuntoClosetPreacher> closetsDisponibles = new List<PuntoClosetPreacher>();
            for (int i = 0; i < PuntosClosetsPreacher.Length; i++)
            {
                if (cam == null || Vector3.Distance(cam.position, PuntosClosetsPreacher[i].posEnCloset) > 3.0f)
                    closetsDisponibles.Add(PuntosClosetsPreacher[i]);
            }
            if (closetsDisponibles.Count == 0)
                closetsDisponibles.AddRange(PuntosClosetsPreacher);

            PuntoClosetPreacher closetElegido = closetsDisponibles[Random.Range(0, closetsDisponibles.Count)];
            nombreCuarto = closetElegido.cuarto;

            float pisoCloset = closetElegido.posEnCloset.y;
            if (Physics.Raycast(closetElegido.posEnCloset + Vector3.up * 1.0f, Vector3.down, out RaycastHit hitC, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hitC.point.y < closetElegido.posEnCloset.y + 0.35f)
                    pisoCloset = hitC.point.y;
            }

            transform.position = new Vector3(closetElegido.posEnCloset.x, pisoCloset, closetElegido.posEnCloset.z);
            transform.rotation = Quaternion.Euler(0f, closetElegido.rotY, 0f);

            // Entreabrir automáticamente la puerta de ese closet para que se vea entreabierta y salgan los susurros de adentro
            puertaClosetAbiertaPorPreacher = null;
            if (!string.IsNullOrEmpty(closetElegido.nombrePuertaCloset))
            {
                GameObject objPuertaCloset = GameObject.Find(closetElegido.nombrePuertaCloset);
                if (objPuertaCloset != null)
                {
                    PuertaAutoCierre pac = objPuertaCloset.GetComponent<PuertaAutoCierre>();
                    if (pac != null && !pac.estaAbierta)
                    {
                        pac.AbrirPuertaPorAnomalia();
                        puertaClosetAbiertaPorPreacher = pac;
                    }
                }
            }
            return;
        }

        List<PuntoAparicionCuarto> candidatos = new List<PuntoAparicionCuarto>();

        for (int i = 0; i < PuntosHabitaciones.Length; i++)
        {
            PuntoAparicionCuarto p = PuntosHabitaciones[i];
            Vector3 destino = p.posCentro;
            if (cam == null || Vector3.Distance(cam.position, destino) > 3.2f)
                candidatos.Add(p);
        }

        if (candidatos.Count == 0)
            candidatos.AddRange(PuntosHabitaciones);

        PuntoAparicionCuarto elegido = candidatos[Random.Range(0, candidatos.Count)];
        nombreCuarto = elegido.cuarto;

        Vector3 posElegida = elegido.posCentro;
        float alturaPiso = posElegida.y;

        Vector3 rayStart = new Vector3(posElegida.x, posElegida.y + 1.1f, posElegida.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 3.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.point.y < posElegida.y + 0.35f)
                alturaPiso = hit.point.y;
        }

        if (tipo == TipoAnomalia.Corpse)
            alturaPiso += 0.04f;

        transform.position = new Vector3(posElegida.x, alturaPiso, posElegida.z);
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
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

        int w = 64;
        int h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Tex_VeloSolCristal_Unknown_VR",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        // Velo translúcido de resplandor solar y cielo celeste en el vidrio (permite ver el jardín y arbustos reales iluminados afuera)
        Color cieloSuave = new Color(0.70f, 0.88f, 1.00f, 0.16f);
        Color solCentro = new Color(1.00f, 0.97f, 0.88f, 0.34f);

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / (w - 1);
                float dx = (u - 0.5f) * 1.4f;
                float dy = (v - 0.58f) * 1.2f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float halo = Mathf.Pow(Mathf.Clamp01(1f - dist), 1.5f);
                Color c = Color.Lerp(cieloSuave, solCentro, halo);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();

        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        materialCieloDiaVentana = new Material(sh)
        {
            name = "Mat_VeloSol_Ventana_Unknown",
            color = new Color(1f, 0.98f, 0.92f, 0.30f)
        };
        materialCieloDiaVentana.SetTexture("_BaseMap", tex);
        materialCieloDiaVentana.mainTexture = tex;
        return materialCieloDiaVentana;
    }

    private static void ConfigurarIluminacionDiaExteriorReal(Transform contenedorExtra, Bounds wb, Vector3 haciaAfuera)
    {
        Vector3 haciaAdentro = -haciaAfuera.normalized;

        // 1. Luz Solar Exterior Real: ilumina el pasto, los arbustos, las flores, la cerca de madera y los árboles REALES afuera de la ventana
        Transform solExtT = contenedorExtra.Find("Sol_Jardin_Exterior_Unknown");
        Light solExt = solExtT != null ? solExtT.GetComponent<Light>() : null;
        if (solExt == null)
        {
            GameObject obj = new GameObject("Sol_Jardin_Exterior_Unknown");
            obj.transform.SetParent(contenedorExtra, true);
            solExt = obj.AddComponent<Light>();
        }
        solExt.transform.position = wb.center + haciaAfuera * 1.2f + Vector3.up * 3.2f;
        solExt.transform.rotation = Quaternion.LookRotation((haciaAfuera + Vector3.down * 0.55f).normalized, Vector3.up);
        solExt.type = LightType.Spot;
        solExt.spotAngle = 155f;
        solExt.innerSpotAngle = 95f;
        solExt.color = new Color(1.0f, 0.97f, 0.88f, 1f);
        solExt.intensity = 9.5f;
        solExt.range = 24f;
        solExt.shadows = LightShadows.None;
        solExt.renderMode = LightRenderMode.ForcePixel;
        solExt.enabled = true;

        // 2. Luz Celeste de Cielo Diurno Exterior: baña el jardín exterior con luz azul de día para eliminar las sombras negras nocturnas afuera
        Transform cieloExtT = contenedorExtra.Find("Cielo_Exterior_Relleno_Unknown");
        Light cieloExt = cieloExtT != null ? cieloExtT.GetComponent<Light>() : null;
        if (cieloExt == null)
        {
            GameObject obj = new GameObject("Cielo_Exterior_Relleno_Unknown");
            obj.transform.SetParent(contenedorExtra, true);
            cieloExt = obj.AddComponent<Light>();
        }
        cieloExt.transform.position = wb.center + haciaAfuera * 4.2f + Vector3.up * 1.8f;
        cieloExt.type = LightType.Point;
        cieloExt.color = new Color(0.68f, 0.86f, 1.0f, 1f);
        cieloExt.intensity = 4.2f;
        cieloExt.range = 20f;
        cieloExt.shadows = LightShadows.None;
        cieloExt.renderMode = LightRenderMode.ForcePixel;
        cieloExt.enabled = true;

        // 3. Haz de Sol Entrando al Cuarto por ESA ventana (ilumina el sofá/cama y el piso de madera adentro del cuarto)
        Transform luzSolT = contenedorExtra.Find("Luz_Sol_Ventana_Unknown");
        Light luzSol = luzSolT != null ? luzSolT.GetComponent<Light>() : null;
        if (luzSol == null)
        {
            GameObject lObj = new GameObject("Luz_Sol_Ventana_Unknown");
            lObj.transform.SetParent(contenedorExtra, true);
            luzSol = lObj.AddComponent<Light>();
        }
        Vector3 dirRayoSol = (haciaAdentro + Vector3.down * 0.40f).normalized;
        luzSol.transform.position = wb.center + haciaAfuera * 1.25f + Vector3.up * 0.45f;
        luzSol.transform.rotation = Quaternion.LookRotation(dirRayoSol, Vector3.up);
        luzSol.type = LightType.Spot;
        luzSol.spotAngle = 72f;
        luzSol.innerSpotAngle = 42f;
        luzSol.color = new Color(1.0f, 0.95f, 0.82f, 1f);
        luzSol.intensity = 5.5f;
        luzSol.range = 8.5f;
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
            // Calcular desplazamiento claramente visible (0.48m a 0.72m) hacia el centro despejado de la habitación
            // para que nunca se mueva solo 2-7 cm ni atraviese muros o estantes (Anti-Clipping)
            Vector3 centroCuarto = ObtenerCentroDeHabitacion(nombreCuarto);
            Vector3 dirHaciaCentro = new Vector3(centroCuarto.x - centroVisual.x, 0f, centroCuarto.z - centroVisual.z);
            if (dirHaciaCentro.sqrMagnitude < 0.01f)
                dirHaciaCentro = new Vector3(v.moverPosicion.x, 0f, v.moverPosicion.z);
            if (dirHaciaCentro.sqrMagnitude < 0.001f)
                dirHaciaCentro = Vector3.forward;
            dirHaciaCentro.Normalize();

            float distObjetivo = Mathf.Clamp(new Vector2(v.moverPosicion.x, v.moverPosicion.z).magnitude * 3.2f, 0.48f, 0.72f);

            // Verificar que no haya pared/obstáculo en esa dirección; si lo hay, invertir o usar la dirección hacia el centro del cuarto
            Vector3[] candidatosDir = new Vector3[]
            {
                dirHaciaCentro,
                Vector3.Cross(Vector3.up, dirHaciaCentro).normalized,
                -Vector3.Cross(Vector3.up, dirHaciaCentro).normalized,
                -dirHaciaCentro
            };

            Vector3 dirSegura = dirHaciaCentro;
            float distSegura = distObjetivo;

            foreach (Vector3 d in candidatosDir)
            {
                if (!Physics.Raycast(centroVisual, d, out RaycastHit hit, distObjetivo + 0.20f, ~0, QueryTriggerInteraction.Ignore))
                {
                    dirSegura = d;
                    distSegura = distObjetivo;
                    break;
                }
                else if (hit.distance > 0.35f)
                {
                    dirSegura = d;
                    distSegura = hit.distance - 0.15f;
                    break;
                }
            }

            Vector3 nuevoDestino = transform.position + dirSegura * distSegura;

            // Si es una caja, bote de pintura o bidón en un estante alto (ej. Garaje o Lavandería), bajarlo al suelo frente al estante
            // y volcarlo ligeramente para que el jugador identifique al instante qué objeto se desplazó
            string nLower = gameObject.name.ToLowerInvariant();
            bool esObjetoDeEstante = nLower.StartsWith("box") || nLower.StartsWith("paint_pot") || nLower.StartsWith("petrol_can") || nLower.StartsWith("toolbox");
            bool puedeLevitar = v.tipo == TipoAnomalia.Displacement &&
                                !nLower.Contains("door") &&
                                !nLower.Contains("drawer") &&
                                !nLower.Contains("cuadro") &&
                                ((v.nombreDescriptivo != null && v.nombreDescriptivo.ToLowerInvariant().Contains("levita")) || Random.value < 0.30f);

            if (puedeLevitar)
            {
                float pisoCuarto = centroVisual.y > 3.15f ? 3.74f : (nombreCuarto == "Garage" ? 0.16f : 0.41f);
                posBaseLevitacion = new Vector3(nuevoDestino.x, pisoCuarto + Random.Range(0.85f, 1.18f), nuevoDestino.z);
                transform.position = posBaseLevitacion;
                transform.rotation = transform.rotation * Quaternion.Euler(18f, 35f, -14f);
                enLevitacionDisplacement = true;
            }
            else if (esObjetoDeEstante)
            {
                float pisoCuarto = centroVisual.y > 3.15f ? 3.74f : (nombreCuarto == "Garage" ? 0.16f : 0.41f);
                float mitadAlto = miRenderer != null ? miRenderer.bounds.extents.y : 0.15f;
                nuevoDestino.y = pisoCuarto + mitadAlto;
                transform.position = nuevoDestino;
                transform.RotateAround(miRenderer != null ? miRenderer.bounds.center : transform.position, dirSegura, 28f);
            }
            else
            {
                transform.position = nuevoDestino;
            }
        }
    }

    void Update()
    {
        if (estaActiva && enLevitacionDisplacement)
        {
            float flotar = Mathf.Sin(Time.time * 1.8f) * 0.045f;
            transform.position = posBaseLevitacion + new Vector3(0f, flotar, 0f);
            transform.Rotate(new Vector3(12f, 24f, 8f) * Time.deltaTime, Space.World);
        }
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
        float pisoY = centroVisual.y > 3.15f ? 3.74f : (nombreCuarto == "Garage" ? 0.16f : 0.41f);
        if (Physics.Raycast(centroVisual + Vector3.up * 0.15f, Vector3.down, out RaycastHit hitPiso, 2.2f, ~0, QueryTriggerInteraction.Ignore))
        {
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
            if (cam != null && Vector3.Distance(cam.position, PuntosHabitaciones[i].posCentro) < 3.6f)
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
        varianteActual = null;
        enLevitacionDisplacement = false;

        if (rastroDisplacementActual != null)
            rastroDisplacementActual.SetActive(false);

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
            v.audioEvento.Stop();
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

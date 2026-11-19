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

    private VarianteAnomalia varianteActual;
    private Vector3 posOriginal;
    private Quaternion rotOriginal;
    private string cuartoOriginalGuardado;
    private MeshRenderer miRenderer;
    private Light luzElectricaTemporal;
    private readonly Dictionary<InterruptorLuz, bool> estadoPrevioSwitches = new Dictionary<InterruptorLuz, bool>();

    private static Material materialElectricoFallback;
    private static Material materialImageryFallback;
    private static Material materialSombraPureForm;
    private static Material materialOjosBlancosPureForm;
    private static AudioClip clipZumbidoElectrico;
    private static AudioClip clipSusurrosPreacher;

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
            posCentro = new Vector3(13.10f, 0.41f, -6.20f),
            posRincon = new Vector3(11.80f, 0.41f, -9.20f),
            posPared  = new Vector3(13.20f, 1.78f, -6.95f),
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

    public static string NormalizarCuartoPorPosicion(Vector3 posMundial, string cuartoOriginal)
    {
        string c = string.IsNullOrEmpty(cuartoOriginal) ? "LivingRoom" : cuartoOriginal.Trim();
        if (string.Equals(c, "Basement", System.StringComparison.OrdinalIgnoreCase))
            return "Garage";

        if (string.Equals(c, "Bedroom", System.StringComparison.OrdinalIgnoreCase))
        {
            if (posMundial.x > 17.4f)
                return "Bedroom1"; // Recámara 1 (Principal Oeste + Clóset Vestidor)
            if (posMundial.z >= -2.5f)
                return "Bedroom2"; // Recámara 2 (Frente Norte)
            return "Bedroom3";     // Recámara 3 (Sur Trasera)
        }

        return c;
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

        nombreCuarto = NormalizarCuartoPorPosicion(transform.position, nombreCuarto);
        posOriginal = transform.localPosition;
        rotOriginal = transform.localRotation;
        cuartoOriginalGuardado = nombreCuarto;
        miRenderer = GetComponent<MeshRenderer>();

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
            Vector3 lossy = transform.lossyScale;
            float sx = Mathf.Abs(lossy.x) > 0.0001f ? 1f / Mathf.Abs(lossy.x) : 1f;
            float sy = Mathf.Abs(lossy.y) > 0.0001f ? 1f / Mathf.Abs(lossy.y) : 1f;
            float sz = Mathf.Abs(lossy.z) > 0.0001f ? 1f / Mathf.Abs(lossy.z) : 1f;

            if (parentRenderer != null)
            {
                Bounds wb = parentRenderer.bounds;
                extra.transform.position = wb.center;
                extra.transform.rotation = Quaternion.identity;

                Vector3 worldSize = new Vector3(
                    Mathf.Clamp(wb.size.x, 0.08f, 2.2f),
                    Mathf.Clamp(wb.size.y, 0.8f, 2.2f),
                    Mathf.Clamp(wb.size.z, 0.08f, 2.2f)
                );

                extra.transform.localScale = new Vector3(worldSize.x * sx, worldSize.z * sy, worldSize.y * sz);
            }
            else
            {
                extra.transform.localScale = new Vector3(1.1f * sx, 0.05f * sy, 1.4f * sz);
            }
        }
        else if (extraName.Contains("clonmimic"))
        {
            MeshRenderer mrOrig = GetComponent<MeshRenderer>();
            MeshRenderer mrClon = extra.GetComponent<MeshRenderer>();
            if (mrOrig != null && mrClon != null)
            {
                float dist = Vector3.Distance(mrOrig.bounds.center, mrClon.bounds.center);
                if (dist > 1.2f || dist < 0.02f)
                {
                    extra.transform.position = transform.position + new Vector3(0.18f, 0f, 0.14f);
                }
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
            rt.sizeDelta = new Vector2(2.8f, 0.8f);

        tmp.text = MensajesParedEspanol[0]; // "NADA VALE EL RIESGO"
        tmp.fontSize = 2.0f;
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

    public void ActivarAnomalia()
    {
        if (estaActiva || posiblesVariantes.Count == 0) return;

        int indiceAzar = Random.Range(0, posiblesVariantes.Count);
        varianteActual = posiblesVariantes[indiceAzar];
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

        // 1. ¿Desaparece el objeto original? (Displacement)
        if (varianteActual.desapareceElObjeto && miRenderer != null)
            miRenderer.enabled = false;

        // 2. ¿Se mueve o rota? (Displacement / Imagery)
        if (varianteActual.moverPosicion != Vector3.zero || varianteActual.girarRotacion != Vector3.zero)
        {
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
        List<PuntoAparicionCuarto> candidatos = new List<PuntoAparicionCuarto>();

        for (int i = 0; i < PuntosHabitaciones.Length; i++)
        {
            PuntoAparicionCuarto p = PuntosHabitaciones[i];
            Vector3 destino = tipo == TipoAnomalia.Preacher ? p.posRincon : p.posCentro;
            if (cam == null || Vector3.Distance(cam.position, destino) > 3.2f)
                candidatos.Add(p);
        }

        if (candidatos.Count == 0)
            candidatos.AddRange(PuntosHabitaciones);

        PuntoAparicionCuarto elegido = candidatos[Random.Range(0, candidatos.Count)];
        nombreCuarto = elegido.cuarto;

        Vector3 posElegida = (tipo == TipoAnomalia.Preacher) ? elegido.posRincon : elegido.posCentro;
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

        Vector3 origenBusqueda = elegido.posPared;
        Vector3[] direcciones = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        RaycastHit mejorHit = default;
        float menorDist = float.MaxValue;
        bool encontroPared = false;

        foreach (Vector3 dir in direcciones)
        {
            if (Physics.Raycast(origenBusqueda, dir, out RaycastHit hit, 3.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Mathf.Abs(hit.normal.y) < 0.25f && hit.distance < menorDist)
                {
                    menorDist = hit.distance;
                    mejorHit = hit;
                    encontroPared = true;
                }
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
            Vector3 offsetMundial = new Vector3(
                Mathf.Clamp(v.moverPosicion.x, -0.35f, 0.35f),
                Mathf.Clamp(v.moverPosicion.y, -0.15f, 0.15f),
                Mathf.Clamp(v.moverPosicion.z, -0.35f, 0.35f)
            );
            transform.position += offsetMundial;
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

        if (v.puertaQueSeAbreSola != null)
        {
            v.puertaQueSeAbreSola.CerrarPuerta(false);
        }
        else if (!EsEntidadMovilEntreHabitaciones(v.tipo) && !gameObject.name.ToLowerInvariant().Contains("mensajepared"))
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

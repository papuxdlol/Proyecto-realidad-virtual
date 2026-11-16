using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum TipoAnomalia
{
    Imagery = 0,      // Cuadros, fotos que cambian
    Displacement = 1, // Se mueve, gira o desaparece
    Mimic = 2,        // Aparece un clon exacto
    Flawed = 3,       // Entidad deforme
    PureForm = 4,     // Entidad negra con ojos
    Preacher = 5,     // Figura en rincón con audio
    Electrical = 6,   // Luces, TV, Caja de fusibles
    Tulpa = 7,        // Reflejo en el espejo
    Corpse = 8,       // Cuerpo en el suelo
    Unknown = 9       // Luz de día en ventana, texto en pared, puerta extra
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

    [Header("Si es Electrical (Apagar luces):")]
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
    [HideInInspector] public TipoAnomalia tipoActivoActual; // Lo que revisa la Tablet

    private VarianteAnomalia varianteActual;
    private Vector3 posOriginal;
    private Quaternion rotOriginal;
    private Vector3 posMundialOriginal;
    private Quaternion rotMundialOriginal;
    private MeshRenderer miRenderer;
    private Light luzElectricaTemporal;
    private Dictionary<InterruptorLuz, bool> estadoPrevioSwitches = new Dictionary<InterruptorLuz, bool>();

    private static Material materialElectricoFallback;
    private static Material materialImageryFallback;
    private static AudioClip clipZumbidoElectrico;

    void Awake()
    {
        AutoRepararGeometriaYReferencias();

        posOriginal = transform.localPosition;
        rotOriginal = transform.localRotation;
        posMundialOriginal = transform.position;
        rotMundialOriginal = transform.rotation;
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
        string n = gameObject.name.ToLower();

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

        // 2. Corregir posición y orientación del mensaje de sangre en la pared ("NO MIRES ATRÁS")
        if (n.Contains("mensajepared"))
        {
            RepararMensajePared();
        }

        // 3. Corregir altura de las 5 entidades placeholder para que pisen el suelo real y no floten en el techo
        if (n.StartsWith("anomalia_pureform") ||
            n.StartsWith("anomalia_preacher") ||
            n.StartsWith("anomalia_flawed") ||
            n.StartsWith("anomalia_tulpa") ||
            n.StartsWith("anomalia_corpse"))
        {
            RepararAlturaEntidad(n);
        }
    }

    private void AutoRepararObjetoExtra(VarianteAnomalia v)
    {
        GameObject extra = v.objetoExtraActivable;
        if (extra == null) return;

        string extraName = extra.name.ToLower();

        // Corregir los cubos de "LuzDeDia_Unknown" en las ventanas que heredaban la escala -95.79 del padre
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

                // Tamaño en metros reales de la ventana (sin multiplicarse por 95.79)
                Vector3 worldSize = new Vector3(
                    Mathf.Clamp(wb.size.x, 0.08f, 2.2f),
                    Mathf.Clamp(wb.size.y, 0.8f, 2.2f),
                    Mathf.Clamp(wb.size.z, 0.08f, 2.2f)
                );

                // Ajustar localScale considerando la rotación/escala del padre
                extra.transform.localScale = new Vector3(worldSize.x * sx, worldSize.z * sy, worldSize.y * sz);
            }
            else
            {
                extra.transform.localScale = new Vector3(1.1f * sx, 0.05f * sy, 1.4f * sz);
            }
        }
        else if (extraName.Contains("clonmimic"))
        {
            // Asegurar que el clon Mimic esté cerca del objeto original en espacio mundial (no a 15 metros)
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

        // Buscar la TV de la sala como referencia para poner el texto en la pared detrás o junto a la TV a altura de ojos
        GameObject house = GameObject.Find("House");
        Transform refTv = null;
        if (house != null)
        {
            foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLower().StartsWith("tv_03"))
                {
                    refTv = t;
                    break;
                }
            }
        }

        Vector3 origenBusqueda = refTv != null
            ? (refTv.GetComponent<Renderer>() != null ? refTv.GetComponent<Renderer>().bounds.center : refTv.position)
            : transform.position;

        // Altura de los ojos en el primer piso (suelo ~0.41m -> ojos ~1.75m)
        origenBusqueda.y = 1.78f;

        Vector3[] direcciones = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        RaycastHit mejorHit = default;
        float menorDist = float.MaxValue;
        bool encontroPared = false;

        foreach (Vector3 dir in direcciones)
        {
            if (Physics.Raycast(origenBusqueda, dir, out RaycastHit hit, 3.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Ignorar pisos/techos buscando normales horizontales
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
            // Colocar 2 cm por delante de la pared para evitar Z-fighting y orientar el texto hacia la habitación
            transform.position = mejorHit.point + mejorHit.normal * 0.02f;
            transform.rotation = Quaternion.LookRotation(-mejorHit.normal, Vector3.up);
        }
        else
        {
            transform.position = new Vector3(origenBusqueda.x, 1.78f, origenBusqueda.z - 0.15f);
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }

        tmp.transform.localPosition = Vector3.zero;
        tmp.transform.localRotation = Quaternion.identity;
        tmp.transform.localScale = Vector3.one;

        RectTransform rt = tmp.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(2.4f, 0.7f);

        tmp.fontSize = 2.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void RepararAlturaEntidad(string nombreLower)
    {
        // Determinar la altura real del piso debajo de la entidad
        Vector3 pos = transform.position;
        float alturaPiso = pos.y > 2.8f ? 3.74f : 0.41f; // Piso 2 (~3.74m) vs Piso 1 (~0.41m)

        Vector3 rayStart = new Vector3(pos.x, pos.y + 0.8f, pos.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 4.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            // Si golpea el suelo o una superficie baja del cuarto
            if (hit.point.y < pos.y + 0.2f)
                alturaPiso = hit.point.y;
        }

        if (nombreLower.StartsWith("anomalia_corpse"))
        {
            // El cuerpo está acostado (rotado 90° en X), por lo que debe quedar al ras del suelo (+0.16m del radio de la cápsula)
            transform.position = new Vector3(pos.x, alturaPiso + 0.16f, pos.z);
            Transform visual = transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");
            if (visual != null && visual.childCount > 0)
            {
                Transform cuerpo = visual.GetChild(0);
                cuerpo.localPosition = Vector3.zero;
            }
        }
        else if (nombreLower.StartsWith("anomalia_tulpa"))
        {
            // Tulpa frente al espejo del baño, de pie sobre el piso
            transform.position = new Vector3(pos.x, alturaPiso, pos.z);
        }
        else
        {
            // PureForm, Preacher, Flawed de pie sobre el piso real
            transform.position = new Vector3(pos.x, alturaPiso, pos.z);
        }
    }

    public void ActivarAnomalia()
    {
        if (estaActiva || posiblesVariantes.Count == 0) return;

        int indiceAzar = Random.Range(0, posiblesVariantes.Count);
        varianteActual = posiblesVariantes[indiceAzar];
        tipoActivoActual = varianteActual.tipo;
        estaActiva = true;

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
            if (varianteActual.materialAlterado != null)
            {
                varianteActual.rendererCuadroOTV.material = varianteActual.materialAlterado;
            }
            else if (varianteActual.tipo == TipoAnomalia.Electrical)
            {
                // Si el aparato eléctrico (TV, microondas, radio, estufa) no tenía material asignado, aplicar brillo de fallo eléctrico
                varianteActual.rendererCuadroOTV.material = ObtenerMaterialElectricoFallback();
                CrearLuzFalloElectrico();
            }
            else if (varianteActual.tipo == TipoAnomalia.Imagery)
            {
                varianteActual.rendererCuadroOTV.material = ObtenerMaterialImageryFallback();
            }
        }

        // 5. ¿Apaga o bloquea interruptores? (Electrical)
        if (varianteActual.interruptoresAfectados != null && varianteActual.interruptoresAfectados.Length > 0)
        {
            estadoPrevioSwitches.Clear();
            foreach (var sw in varianteActual.interruptoresAfectados)
            {
                if (sw != null)
                {
                    estadoPrevioSwitches[sw] = sw.estaEncendida;
                    sw.CambiarEstado(false);
                    sw.bloqueadaPorAnomalia = true;
                }
            }
        }

        // 6. ¿Suena algo?
        if (varianteActual.audioEvento != null)
        {
            if (varianteActual.audioEvento.clip == null && varianteActual.tipo == TipoAnomalia.Electrical)
                varianteActual.audioEvento.clip = ObtenerClipZumbidoElectrico();

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

    private void AplicarTransformacionAnomala(VarianteAnomalia v)
    {
        transform.localPosition = posOriginal;
        transform.localRotation = rotOriginal;

        Vector3 centroVisual = miRenderer != null ? miRenderer.bounds.center : transform.position;

        if (v.girarRotacion != Vector3.zero)
        {
            if (v.tipo == TipoAnomalia.Imagery && Mathf.Abs(v.girarRotacion.z) > 90f)
            {
                // Cuadro invertido 180°: girar sobre el eje perpendicular al cuadro pasando por su centro visual
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
                // Displacement o puerta de mueble: girar alrededor del eje vertical del mundo (Vector3.up)
                // para no inclinar chuecos los modelos exportados de Blender con X = -90°
                float anguloY = v.girarRotacion.y != 0f ? v.girarRotacion.y : v.girarRotacion.z;
                Vector3 pivoteGiro = centroVisual;

                if (miRenderer != null && (name.ToLower().Contains("door") || name.ToLower().Contains("manija")))
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
            // Mover en coordenadas horizontales del mundo para evitar desplazamientos gigantes por escala -95.79
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
            luzElectricaTemporal.color = new Color(0.3f, 0.65f, 1f);
            luzElectricaTemporal.intensity = 3.5f;
            luzElectricaTemporal.range = 4.0f;
        }
        luzElectricaTemporal.enabled = true;
    }

    public void RestaurarNormalidad()
    {
        if (!estaActiva || varianteActual == null) return;

        VarianteAnomalia v = varianteActual;
        estaActiva = false;
        varianteActual = null;

        // 1. Si era una puerta que se abrió sola, cerrarla y sincronizar su transform
        if (v.puertaQueSeAbreSola != null)
        {
            v.puertaQueSeAbreSola.CerrarPuerta(false);
        }
        else
        {
            transform.localPosition = posOriginal;
            transform.localRotation = rotOriginal;
        }

        // 2. Restaurar visibilidad
        if (miRenderer != null) miRenderer.enabled = true;

        // 3. Ocultar clon o entidad
        if (v.objetoExtraActivable != null)
            v.objetoExtraActivable.SetActive(false);

        // 4. Restaurar cuadro o TV
        if (v.rendererCuadroOTV != null && v.materialOriginalGuardado != null)
            v.rendererCuadroOTV.sharedMaterial = v.materialOriginalGuardado;

        if (luzElectricaTemporal != null)
            luzElectricaTemporal.enabled = false;

        // 5. Desbloquear y restaurar luces
        if (v.interruptoresAfectados != null)
        {
            foreach (var sw in v.interruptoresAfectados)
            {
                if (sw != null)
                {
                    sw.bloqueadaPorAnomalia = false;
                    bool encender = estadoPrevioSwitches.TryGetValue(sw, out bool previo) ? previo : true;
                    sw.CambiarEstado(encender);
                }
            }
            estadoPrevioSwitches.Clear();
        }

        // 6. Apagar sonido
        if (v.audioEvento != null && v.audioEvento.isPlaying)
            v.audioEvento.Stop();
    }

    private static Material ObtenerMaterialElectricoFallback()
    {
        if (materialElectricoFallback == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            materialElectricoFallback = new Material(sh);
            materialElectricoFallback.color = new Color(0.55f, 0.85f, 1f, 1f);
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
            int samples = sampleRate; // 1 segundo en bucle
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.15f;
                float buzz = (Random.value * 2f - 1f) * 0.04f;
                data[i] = hum + buzz;
            }
            clipZumbidoElectrico = AudioClip.Create("ZumbidoElectrico", samples, 1, sampleRate, false);
            clipZumbidoElectrico.SetData(data, 0);
        }
        return clipZumbidoElectrico;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

public class AutomatizadorCasaVR : EditorWindow
{
    // =========================================================================
    // BOTÓN 1: ACTUALIZAR PROPS Y CUARTOS (Incluye Soap, Softener, Stove, etc.)
    // =========================================================================
    [MenuItem("Herramientas VR/1. Actualizar Props (Sin tocar Puertas ni Luces)")]
    public static void ConfigurarSoloAnomalias()
    {
        GameObject house = GameObject.Find("House");
        if (house == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró el objeto 'House' en la escena.", "OK");
            return;
        }

        Transform[] todosLosHijos = house.GetComponentsInChildren<Transform>(true);
        Dictionary<Transform, string> anclasDeCuartos = ObtenerAnclasDeCuartos(todosLosHijos);

        string[] palabrasClaveProps = new string[] {
            "alarm_clock", "book", "bottle", "box_pizza", "box_strawberries", "box.",
            "broom", "cake", "can.", "cereal_bowl", "chair_", "chemical",
            "chicken", "coffee_maker", "control", "cooking_pot", "cutlery",
            "desk_lamp", "dish", "fried_foods", "gadgets", "basket", "bote",
            "toaster", "toilet_paper", "toolbox", "toothbrush", "toothpaste",
            "trash_can", "tumbler", "vaso", "watering_can", "hand_soap",
            "magazines", "meat", "milk", "mop", "paint_pot", "palette",
            "paper_rolls", "petrol_can", "phone", "pizza", "plastic_boat",
            "plate", "rake", "sarten", "shampoo", "shovel", "dirty _clothes",
            // Última tanda de capturas:
            "soap", "softener"
        };

        string[] puertasDeMuebles = new string[] { "refrigerator_door", "shower_door", "manija_washbasin" };
        string[] aparatosElectricos = new string[] { "tv_03", "tv_04", "video_player", "radio_01", "microwave_02", "stove" };

        int nuevasAnomalias = 0;
        int cuartosActualizados = 0;

        foreach (Transform t in todosLosHijos)
        {
            if (t == house.transform) continue;
            GameObject obj = t.gameObject;
            string nombre = obj.name.ToLower();

            if (nombre.Contains("_clonmimic") || nombre.Contains("_unknown")) continue;

            ObjetoAnomalia existente = obj.GetComponent<ObjetoAnomalia>();
            if (existente != null)
            {
                Undo.RecordObject(existente, "Actualizar Cuarto");
                existente.nombreCuarto = DetectarCuartoMasCercano(obj.transform.position, anclasDeCuartos);
                cuartosActualizados++;
                continue;
            }

            if (obj.GetComponent<MeshRenderer>() == null) continue;

            bool esProp = false;
            foreach (string clave in palabrasClaveProps)
            {
                if (nombre.StartsWith(clave)) { esProp = true; break; }
            }

            if (esProp)
            {
                ObjetoAnomalia anom = Undo.AddComponent<ObjetoAnomalia>(obj);
                anom.nombreCuarto = DetectarCuartoMasCercano(obj.transform.position, anclasDeCuartos);
                anom.posiblesVariantes = new List<VarianteAnomalia>();

                VarianteAnomalia varMover = new VarianteAnomalia
                {
                    nombreDescriptivo = "Se mueve de lugar",
                    tipo = TipoAnomalia.Displacement,
                    moverPosicion = new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f)),
                    girarRotacion = new Vector3(0f, Random.Range(25f, 80f), 0f)
                };
                anom.posiblesVariantes.Add(varMover);

                VarianteAnomalia varDesaparecer = new VarianteAnomalia
                {
                    nombreDescriptivo = "Desaparece",
                    tipo = TipoAnomalia.Displacement,
                    desapareceElObjeto = true
                };
                anom.posiblesVariantes.Add(varDesaparecer);

                GameObject clon = Instantiate(obj, obj.transform.parent);
                clon.name = obj.name + "_ClonMimic";
                DestroyImmediate(clon.GetComponent<ObjetoAnomalia>());
                clon.transform.localPosition = obj.transform.localPosition + new Vector3(0.15f, 0f, 0.12f);
                clon.SetActive(false);
                Undo.RegisterCreatedObjectUndo(clon, "Crear Clon Mimic");

                VarianteAnomalia varMimic = new VarianteAnomalia
                {
                    nombreDescriptivo = "Clon Mimic",
                    tipo = TipoAnomalia.Mimic,
                    objetoExtraActivable = clon
                };
                anom.posiblesVariantes.Add(varMimic);

                nuevasAnomalias++;
                continue;
            }

            foreach (string claveMueble in puertasDeMuebles)
            {
                if (nombre.StartsWith(claveMueble))
                {
                    ObjetoAnomalia anom = Undo.AddComponent<ObjetoAnomalia>(obj);
                    anom.nombreCuarto = DetectarCuartoMasCercano(obj.transform.position, anclasDeCuartos);
                    anom.posiblesVariantes = new List<VarianteAnomalia>
                    {
                        new VarianteAnomalia
                        {
                            nombreDescriptivo = "Se abre solo",
                            tipo = TipoAnomalia.Displacement,
                            girarRotacion = new Vector3(0f, 65f, 0f)
                        }
                    };
                    nuevasAnomalias++;
                    break;
                }
            }

            foreach (string claveElec in aparatosElectricos)
            {
                if (nombre.StartsWith(claveElec))
                {
                    ObjetoAnomalia anom = Undo.AddComponent<ObjetoAnomalia>(obj);
                    anom.nombreCuarto = DetectarCuartoMasCercano(obj.transform.position, anclasDeCuartos);
                    AudioSource aud = obj.GetComponent<AudioSource>() ?? Undo.AddComponent<AudioSource>(obj);
                    aud.playOnAwake = false;
                    aud.spatialBlend = 1f;

                    anom.posiblesVariantes = new List<VarianteAnomalia>
                    {
                        new VarianteAnomalia
                        {
                            nombreDescriptivo = "Fallo Eléctrico / Encendido",
                            tipo = TipoAnomalia.Electrical,
                            rendererCuadroOTV = obj.GetComponent<MeshRenderer>(),
                            audioEvento = aud,
                            sonidoEnBucle = true
                        }
                    };
                    nuevasAnomalias++;
                    break;
                }
            }
        }

        EditorUtility.DisplayDialog("¡Props Actualizados!",
            $"• {nuevasAnomalias} objetos nuevos configurados.\n• {cuartosActualizados} cuartos verificados.", "OK");
    }

    // =========================================================================
    // BOTÓN 2: CREAR CAJA DE FUSIBLES 3D EN GARAJE + LUCES INDIVIDUALES
    // =========================================================================
    [MenuItem("Herramientas VR/2. Crear Caja de Fusibles (Garaje) y Eventos Electrical")]
    public static void CrearCajaFusiblesYEventosLuces()
    {
        GameObject house = GameObject.Find("House");
        Transform[] todosLosHijos = house != null ? house.GetComponentsInChildren<Transform>(true) : new Transform[0];
        Dictionary<Transform, string> anclas = ObtenerAnclasDeCuartos(todosLosHijos);

        // 1. Buscar todos los interruptores de luz de la casa
        InterruptorLuz[] todosLosSwitches = Object.FindObjectsByType<InterruptorLuz>(FindObjectsSortMode.None);

        // Configurar cada switch para que también pueda fallar individualmente (Electrical de un solo cuarto)
        int switchesIndividuales = 0;
        foreach (InterruptorLuz sw in todosLosSwitches)
        {
            if (sw.GetComponent<ObjetoAnomalia>() == null)
            {
                ObjetoAnomalia anomLuz = Undo.AddComponent<ObjetoAnomalia>(sw.gameObject);
                anomLuz.nombreCuarto = DetectarCuartoMasCercano(sw.transform.position, anclas);
                anomLuz.posiblesVariantes = new List<VarianteAnomalia>
                {
                    new VarianteAnomalia
                    {
                        nombreDescriptivo = "Fallo de luz en este cuarto",
                        tipo = TipoAnomalia.Electrical,
                        interruptoresAfectados = new InterruptorLuz[] { sw }
                    }
                };
                switchesIndividuales++;
            }
        }

        // 2. Construir el GameObject de la Caja de Fusibles 3D y llevarlo al Garaje
        GameObject existente = GameObject.Find("Caja_De_Fusibles_VR");
        if (existente != null)
        {
            Selection.activeGameObject = existente;
            SceneView.lastActiveSceneView.FrameSelected();
            EditorUtility.DisplayDialog("Aviso", "La Caja_De_Fusibles_VR ya existe. Te la he seleccionado en la escena.", "OK");
            return;
        }

        // Buscamos la puerta del garaje o el estante de herramientas para posicionarla ahí mismo
        Transform refGaraje = null;
        foreach (Transform t in todosLosHijos)
        {
            string n = t.name.ToLower();
            if (n.StartsWith("garage_door") || n.StartsWith("tool_cabinet") || n.StartsWith("shelving"))
            {
                refGaraje = t;
                break;
            }
        }

        GameObject cajaBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cajaBase.name = "Caja_De_Fusibles_VR";
        Undo.RegisterCreatedObjectUndo(cajaBase, "Crear Caja de Fusibles");

        if (refGaraje != null)
            cajaBase.transform.position = refGaraje.position + new Vector3(0.5f, 1.4f, 0.5f);
        else
            cajaBase.transform.position = new Vector3(0, 1.5f, 0);

        cajaBase.transform.localScale = new Vector3(0.35f, 0.45f, 0.12f);

        // Color gris oscuro metálico
        Material matMetal = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        matMetal.color = new Color(0.22f, 0.24f, 0.26f);
        cajaBase.GetComponent<MeshRenderer>().sharedMaterial = matMetal;

        // Crear Palanca Roja hija
        GameObject pivotePalanca = new GameObject("Pivote_Palanca");
        pivotePalanca.transform.SetParent(cajaBase.transform, false);
        pivotePalanca.transform.localPosition = new Vector3(0f, 0f, -0.55f);

        GameObject palanca = GameObject.CreatePrimitive(PrimitiveType.Cube);
        palanca.name = "Palanca_Roja";
        DestroyImmediate(palanca.GetComponent<BoxCollider>());
        palanca.transform.SetParent(pivotePalanca.transform, false);
        palanca.transform.localPosition = new Vector3(0f, 0.15f, -0.2f);
        palanca.transform.localScale = new Vector3(0.3f, 0.1f, 0.5f);

        Material matRojo = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        matRojo.color = new Color(0.8f, 0.1f, 0.1f);
        palanca.GetComponent<MeshRenderer>().sharedMaterial = matRojo;

        // Luz indicadora (Verde = OK, Roja = Apagón)
        GameObject objLuz = new GameObject("Luz_Estado_Fusibles");
        objLuz.transform.SetParent(cajaBase.transform, false);
        objLuz.transform.localPosition = new Vector3(0f, 0.35f, -0.7f);
        Light luzInd = objLuz.AddComponent<Light>();
        luzInd.type = LightType.Point;
        luzInd.range = 1.2f;
        luzInd.intensity = 0.8f;
        luzInd.color = Color.green;

        // Agregar componentes interactivos VR y la Anomalía de Apagón Total
        AudioSource audioFusibles = cajaBase.AddComponent<AudioSource>();
        audioFusibles.playOnAwake = false;
        audioFusibles.spatialBlend = 1f;

        cajaBase.AddComponent<XRSimpleInteractable>();

        ObjetoAnomalia anomApagon = cajaBase.AddComponent<ObjetoAnomalia>();
        anomApagon.nombreCuarto = "Basement"; // Garaje / Sótano
        anomApagon.posiblesVariantes = new List<VarianteAnomalia>
        {
            new VarianteAnomalia
            {
                nombreDescriptivo = "APAGÓN TOTAL DE LA CASA (Caja de Fusibles)",
                tipo = TipoAnomalia.Electrical,
                interruptoresAfectados = todosLosSwitches,
                audioEvento = audioFusibles
            }
        };

        CajaFusiblesVR scriptCaja = cajaBase.AddComponent<CajaFusiblesVR>();
        scriptCaja.palancaVisual = pivotePalanca.transform;
        scriptCaja.luzIndicadora = luzInd;
        scriptCaja.sonidoPalanca = audioFusibles;

        // Seleccionar el objeto en el Editor para que el usuario solo lo acomode en la pared del garaje
        Selection.activeGameObject = cajaBase;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();

        EditorUtility.DisplayDialog("¡Caja de Fusibles Creada!",
            $"• Se creó 'Caja_De_Fusibles_VR' en el Garaje conectada a los {todosLosSwitches.Length} switches de la casa.\n" +
            $"• Se configuraron {switchesIndividuales} switches para fallos eléctricos individuales por cuarto.\n\n" +
            "La Caja de Fusibles quedó seleccionada en tu pantalla; solo muévela con las flechas (W) para pegarla a la pared que prefieras del garaje.",
            "¡Perfecto!");
    }

    // =========================================================================
    // BOTÓN 3: GENERAR EVENTOS 'UNKNOWN' (Ventanas con luz de día, Mensajes, Puertas)
    // =========================================================================
    [MenuItem("Herramientas VR/3. Generar Eventos UNKNOWN (Ventanas, Pared y Puertas)")]
    public static void GenerarEventosUnknown()
    {
        GameObject house = GameObject.Find("House");
        if (house == null) return;

        Transform[] todosLosHijos = house.GetComponentsInChildren<Transform>(true);
        Dictionary<Transform, string> anclas = ObtenerAnclasDeCuartos(todosLosHijos);

        int ventanasConfiguradas = 0;
        int puertasUnknown = 0;

        foreach (Transform t in todosLosHijos)
        {
            string n = t.name.ToLower();

            // 1. Luz de día entrando por algunas ventanas en plena madrugada (Unknown)
            if (n.StartsWith("window_frame") && ventanasConfiguradas < 5 && t.GetComponent<ObjetoAnomalia>() == null)
            {
                GameObject luzDiaObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                luzDiaObj.name = t.name + "_LuzDeDia_Unknown";
                DestroyImmediate(luzDiaObj.GetComponent<BoxCollider>());
                luzDiaObj.transform.SetParent(t, false);
                luzDiaObj.transform.localPosition = Vector3.zero;
                luzDiaObj.transform.localScale = new Vector3(1.2f, 1.5f, 0.05f);

                // Material brillante color cielo de mediodía
                Material matDia = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                matDia.color = new Color(0.85f, 0.95f, 1f);
                luzDiaObj.GetComponent<MeshRenderer>().sharedMaterial = matDia;

                // Luz potente que ilumina el cuarto como si fuera de día
                Light solFalso = luzDiaObj.AddComponent<Light>();
                solFalso.type = LightType.Point;
                solFalso.color = new Color(1f, 0.95f, 0.8f);
                solFalso.intensity = 6f;
                solFalso.range = 8f;

                luzDiaObj.SetActive(false);
                Undo.RegisterCreatedObjectUndo(luzDiaObj, "Crear Luz Día Unknown");

                ObjetoAnomalia anomVentana = Undo.AddComponent<ObjetoAnomalia>(t.gameObject);
                anomVentana.nombreCuarto = DetectarCuartoMasCercano(t.position, anclas);
                anomVentana.posiblesVariantes = new List<VarianteAnomalia>
                {
                    new VarianteAnomalia
                    {
                        nombreDescriptivo = "Luz de día en plena madrugada (Unknown)",
                        tipo = TipoAnomalia.Unknown,
                        objetoExtraActivable = luzDiaObj
                    }
                };
                ventanasConfiguradas++;
            }

            // 2. Puertas que se abren solas indicando que alguien entró (Unknown)
            PuertaAutoCierre puerta = t.GetComponent<PuertaAutoCierre>();
            if (puerta != null && t.GetComponent<ObjetoAnomalia>() == null)
            {
                ObjetoAnomalia anomPuerta = Undo.AddComponent<ObjetoAnomalia>(t.gameObject);
                anomPuerta.nombreCuarto = DetectarCuartoMasCercano(t.position, anclas);
                anomPuerta.posiblesVariantes = new List<VarianteAnomalia>
                {
                    new VarianteAnomalia
                    {
                        nombreDescriptivo = "Puerta se abre sola (Unknown)",
                        tipo = TipoAnomalia.Unknown,
                        puertaQueSeAbreSola = puerta
                    }
                };
                puertasUnknown++;
            }
        }

        EditorUtility.DisplayDialog("¡Eventos UNKNOWN Creados!",
            $"• {ventanasConfiguradas} Ventanas configuradas con el evento 'Luz de día en la madrugada'.\n" +
            $"• {puertasUnknown} Puertas configuradas para abrirse solas como anomalía 'Unknown'.",
            "¡Excelente!");
    }

    // --- FUNCIONES AUXILIARES DE DETECCIÓN DE CUARTOS ---
    private static Dictionary<Transform, string> ObtenerAnclasDeCuartos(Transform[] hijos)
    {
        Dictionary<Transform, string> anclas = new Dictionary<Transform, string>();
        foreach (Transform t in hijos)
        {
            string n = t.name.ToLower();
            if (n.StartsWith("refrigerator") || n.StartsWith("microwave") || n.StartsWith("stove"))
                anclas[t] = "Kitchen";
            else if (n.StartsWith("toilet_01") || n.StartsWith("bathtub") || n.StartsWith("shower") || n.StartsWith("washbasin"))
                anclas[t] = "Bathroom";
            else if (n.StartsWith("washing_machine") || n.StartsWith("softener"))
                anclas[t] = "Laundry";
            else if (n.StartsWith("bed_0") || n.StartsWith("wardrobe"))
                anclas[t] = "Bedroom";
            else if (n.StartsWith("tv_0") || n.StartsWith("video_player") || n.StartsWith("armchair"))
                anclas[t] = "LivingRoom";
            else if (n.StartsWith("tool_table") || n.StartsWith("tool_cabinet") || n.StartsWith("garage_door") || n.StartsWith("shelving"))
                anclas[t] = "Basement";
        }
        return anclas;
    }

    private static string DetectarCuartoMasCercano(Vector3 posObjeto, Dictionary<Transform, string> anclas)
    {
        string cuartoDetectado = "Kitchen";
        float menorDist = Mathf.Infinity;
        foreach (var par in anclas)
        {
            float distHorizontal = Vector2.Distance(new Vector2(posObjeto.x, posObjeto.z), new Vector2(par.Key.position.x, par.Key.position.z));
            float difAltura = Mathf.Abs(posObjeto.y - par.Key.position.y) * 3.5f;
            float distTotal = distHorizontal + difAltura;
            if (distTotal < menorDist)
            {
                menorDist = distTotal;
                cuartoDetectado = par.Value;
            }
        }
        return cuartoDetectado;
    }

    // =========================================================================
    // BOTÓN 4: IMAGERY (CUADROS), MENSAJES EN PARED Y SILUETAS DE LAS 5 ENTIDADES
    // =========================================================================
    [MenuItem("Herramientas VR/4. Configurar IMAGERY (Cuadros) + Placeholders de Entidades")]
    public static void ConfigurarImageryYEntidades()
    {
        GameObject house = GameObject.Find("House");
        if (house == null) return;

        Transform[] todosLosHijos = house.GetComponentsInChildren<Transform>(true);
        Dictionary<Transform, string> anclas = ObtenerAnclasDeCuartos(todosLosHijos);

        // 1. AUTO-CONFIGURAR IMAGERY (Cuadros, Fotos, Marcos)
        string[] clavesCuadros = new string[] { "frame", "picture", "painting", "poster", "photo", "cuadro", "pintura", "art" };
        int cuadrosConfigurados = 0;

        // Material alterado para Imagery (Tono rojizo/corrupto)
        Material matImageryAnomalo = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        matImageryAnomalo.color = new Color(0.45f, 0.05f, 0.05f);

        foreach (Transform t in todosLosHijos)
        {
            string n = t.name.ToLower();
            if (n.Contains("door_frame") || n.Contains("window_frame")) continue;

            bool esCuadro = false;
            foreach (string clave in clavesCuadros)
            {
                if (n.Contains(clave)) { esCuadro = true; break; }
            }

            if (esCuadro && t.GetComponent<ObjetoAnomalia>() == null && t.GetComponent<MeshRenderer>() != null)
            {
                ObjetoAnomalia anomCuadro = Undo.AddComponent<ObjetoAnomalia>(t.gameObject);
                anomCuadro.nombreCuarto = DetectarCuartoMasCercano(t.position, anclas);
                anomCuadro.posiblesVariantes = new List<VarianteAnomalia>
                {
                    // Variante A: Cambia la imagen/material del cuadro
                    new VarianteAnomalia
                    {
                        nombreDescriptivo = "Imagen alterada (Imagery)",
                        tipo = TipoAnomalia.Imagery,
                        rendererCuadroOTV = t.GetComponent<MeshRenderer>(),
                        materialAlterado = matImageryAnomalo
                    },
                    // Variante B: El cuadro se voltea de cabeza (180 grados)
                    new VarianteAnomalia
                    {
                        nombreDescriptivo = "Cuadro invertido de cabeza (Imagery)",
                        tipo = TipoAnomalia.Imagery,
                        girarRotacion = new Vector3(0f, 0f, 180f)
                    }
                };
                cuadrosConfigurados++;
            }
        }

        // 2. CREAR SILUETAS / PLACEHOLDERS PARA LAS 5 ENTIDADES Y MENSAJES UNKNOWN
        GameObject contenedorEntidades = GameObject.Find("Contenedor_Entidades_Anomalias");
        if (contenedorEntidades == null)
        {
            contenedorEntidades = new GameObject("Contenedor_Entidades_Anomalias");
            Undo.RegisterCreatedObjectUndo(contenedorEntidades, "Crear Contenedor Entidades");

            // Buscamos muebles de referencia para colocar cada entidad en su cuarto ideal
            Transform refSala = BuscarMueble(todosLosHijos, "tv_03");
            Transform refRecamara = BuscarMueble(todosLosHijos, "wardrobe");
            Transform refCama = BuscarMueble(todosLosHijos, "bed_01");
            Transform refBano = BuscarMueble(todosLosHijos, "washbasin_03");
            Transform refCocina = BuscarMueble(todosLosHijos, "refrigerator");

            // A) PURE FORM (Sombra negra con ojos brillantes en la Sala)
            CrearEntidadPlaceholder("Anomalia_PureForm_Sala", TipoAnomalia.PureForm, "LivingRoom",
                refSala != null ? refSala.position + new Vector3(-1.2f, 0f, 0.5f) : new Vector3(0, 0, 0),
                contenedorEntidades.transform, true, false, false);

            // B) PREACHER (Figura en rincón/clóset con sonido en bucle en Recámara)
            CrearEntidadPlaceholder("Anomalia_Preacher_Closet", TipoAnomalia.Preacher, "Bedroom",
                refRecamara != null ? refRecamara.position + new Vector3(0.8f, 0f, 0.5f) : new Vector3(2, 3, 0),
                contenedorEntidades.transform, false, false, true);

            // C) FLAWED (Figura deforme/incompleta junto a la cama)
            CrearEntidadPlaceholder("Anomalia_Flawed_Recamara", TipoAnomalia.Flawed, "Bedroom",
                refCama != null ? refCama.position + new Vector3(1.0f, 0f, -0.8f) : new Vector3(-2, 3, 0),
                contenedorEntidades.transform, false, false, false);

            // D) TULPA (Entidad en el espejo/lavabo del Baño)
            CrearEntidadPlaceholder("Anomalia_Tulpa_EspejoBano", TipoAnomalia.Tulpa, "Bathroom",
                refBano != null ? refBano.position + new Vector3(0f, 0f, -0.4f) : new Vector3(0, 0, 3),
                contenedorEntidades.transform, true, false, false);

            // E) CORPSE (Cuerpo tirado en el suelo de la Cocina)
            CrearEntidadPlaceholder("Anomalia_Corpse_Cocina", TipoAnomalia.Corpse, "Kitchen",
                refCocina != null ? refCocina.position + new Vector3(1.2f, 0.15f, 1.0f) : new Vector3(1, 0.15f, 1),
                contenedorEntidades.transform, false, true, false);

            // F) UNKNOWN EXTRA: Mensaje escrito con sangre en la pared
            CrearMensajeParedUnknown("Anomalia_MensajePared_Unknown", "LivingRoom",
                refSala != null ? refSala.position + new Vector3(0f, 1.5f, -0.2f) : new Vector3(0, 1.5f, 0),
                contenedorEntidades.transform);
        }

        EditorUtility.DisplayDialog("¡Imagery y Entidades Listas!",
            $"• Se configuraron {cuadrosConfigurados} cuadros existentes para 'Imagery' (puedes volver a pulsar este botón cuando agregues más cuadros).\n" +
            $"• Se creó el objeto 'Contenedor_Entidades_Anomalias' en la jerarquía con las 5 entidades (Pure Form, Preacher, Flawed, Tulpa, Corpse) y un mensaje de pared 'Unknown'.",
            "¡Perfecto!");
    }

    private static Transform BuscarMueble(Transform[] hijos, string prefijo)
    {
        foreach (Transform t in hijos)
            if (t.name.ToLower().StartsWith(prefijo)) return t;
        return null;
    }

    private static void CrearEntidadPlaceholder(string nombre, TipoAnomalia tipo, string cuarto, Vector3 posicion, Transform padre, bool ojosBrillantes, bool acostado, bool conAudioLoop)
    {
        GameObject ancla = new GameObject(nombre);
        ancla.transform.SetParent(padre);
        ancla.transform.position = posicion;

        // Visual que se oculta/activa (Adentro de este objeto meterás tu modelo 3D final después)
        GameObject visual = new GameObject("Modelo_Visual_(Reemplazar_Por_FBX)");
        visual.transform.SetParent(ancla.transform, false);

        GameObject cuerpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        DestroyImmediate(cuerpo.GetComponent<CapsuleCollider>());
        cuerpo.transform.SetParent(visual.transform, false);
        cuerpo.transform.localPosition = new Vector3(0, 0.9f, 0);
        cuerpo.transform.localScale = tipo == TipoAnomalia.Flawed ? new Vector3(0.35f, 1.1f, 0.2f) : new Vector3(0.45f, 0.9f, 0.3f);

        Material matNegro = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        matNegro.color = Color.black;
        cuerpo.GetComponent<MeshRenderer>().sharedMaterial = matNegro;

        if (acostado)
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        if (ojosBrillantes)
        {
            Material matOjos = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            matOjos.color = Color.white;
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject ojo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyImmediate(ojo.GetComponent<SphereCollider>());
                ojo.transform.SetParent(visual.transform, false);
                ojo.transform.localPosition = new Vector3(i * 0.08f, 1.55f, 0.2f);
                ojo.transform.localScale = Vector3.one * 0.04f;
                ojo.GetComponent<MeshRenderer>().sharedMaterial = matOjos;
            }
        }

        visual.SetActive(false);

        ObjetoAnomalia anom = ancla.AddComponent<ObjetoAnomalia>();
        anom.nombreCuarto = cuarto;

        AudioSource aud = null;
        if (conAudioLoop)
        {
            aud = ancla.AddComponent<AudioSource>();
            aud.playOnAwake = false;
            aud.spatialBlend = 1f;
        }

        anom.posiblesVariantes = new List<VarianteAnomalia>
        {
            new VarianteAnomalia
            {
                nombreDescriptivo = "Aparición de " + tipo.ToString(),
                tipo = tipo,
                objetoExtraActivable = visual,
                audioEvento = aud,
                sonidoEnBucle = conAudioLoop
            }
        };
    }

    private static void CrearMensajeParedUnknown(string nombre, string cuarto, Vector3 posicion, Transform padre)
    {
        GameObject ancla = new GameObject(nombre);
        ancla.transform.SetParent(padre);
        ancla.transform.position = posicion;

        GameObject textoObj = new GameObject("Texto_Sangre_Pared");
        textoObj.transform.SetParent(ancla.transform, false);
        TextMeshPro tmp = textoObj.AddComponent<TextMeshPro>();
        tmp.text = "NO MIRES ATRÁS";
        tmp.fontSize = 3f;
        tmp.color = new Color(0.6f, 0.02f, 0.02f);
        tmp.alignment = TextAlignmentOptions.Center;
        textoObj.SetActive(false);

        ObjetoAnomalia anom = ancla.AddComponent<ObjetoAnomalia>();
        anom.nombreCuarto = cuarto;
        anom.posiblesVariantes = new List<VarianteAnomalia>
        {
            new VarianteAnomalia
            {
                nombreDescriptivo = "Mensaje en la pared (Unknown)",
                tipo = TipoAnomalia.Unknown,
                objetoExtraActivable = textoObj
            }
        };
    }
}
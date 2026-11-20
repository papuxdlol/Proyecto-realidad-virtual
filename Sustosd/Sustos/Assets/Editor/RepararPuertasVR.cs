using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

[InitializeOnLoad]
public class RepararPuertasVR : EditorWindow
{
    private const string AutoFixFlagPath = "Temp/AutoFixSustosVR_v11.flag";

    static RepararPuertasVR()
    {
        EditorApplication.delayCall += EjecutarAutoFixSiNecesario;
    }

    private static void EjecutarAutoFixSiNecesario()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(AutoFixFlagPath)) return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.name == "cuidad")
        {
            File.WriteAllText(AutoFixFlagPath, "done");
            RepararTodoSilencioso(mostrarDialogo: false);
        }
    }

    [MenuItem("Herramientas VR/6. PONER COLLIDERS DIRECTOS Y SACAR LUCES")]
    public static void PonerCollidersYLuces()
    {
        RepararTodoSilencioso(mostrarDialogo: true);
    }

    public static void RepararTodoSilencioso(bool mostrarDialogo)
    {
        // 0. Asegurar configuración URP y reimportar modelos GLB con nuestro GlbScriptedImporter
        InterruptorLuz.HabilitarLucesAdicionalesEnURP();
        AssetDatabase.ImportAsset("Assets/Settings/Project Configuration/Performance URP Config.asset", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Settings/Project Configuration/Quality URP Config.asset", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Model/Props/dark-female-apparition-3d-model/source/PureForm_Model.obj", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Model/Props/horror-character/source/Preacher_Model.obj", ImportAssetOptions.ForceUpdate);

        // Asegurar DecompressOnLoad en los 3 audios de pisadas para recorte instantáneo sin delay
        AsegurarAudioPisadasDecompressOnLoad();

        AudioClip clipAbrirPuerta = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/dragon-studio-opening-door-450444.mp3");
        AudioClip clipCerrarPuerta = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/dragon-studio-close-door-382723.mp3");
        AudioClip clipAbrirAnomalia = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-squeaky-bathroom-door-102756.mp3");

        // Restaurar la puerta faltante de Lavandería (Door_007) antes de escanear las puertas
        VentanasYLimitesVR.RestaurarPuertaLavanderiaFaltante();
        VentanasYLimitesVR.ConfigurarNocheOscuraReal();
        VentanasYLimitesVR.AsegurarCollidersMueblesTangibles();

        Transform[] todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);

        int switchesCorregidos = 0;
        int puertasCorregidas = 0;
        int anomaliasCorregidas = 0;

        // 1. Limpiar House_Colliders
        GameObject houseColliders = GameObject.Find("House_Colliders");
        if (houseColliders != null)
        {
            foreach (Transform tc in houseColliders.GetComponentsInChildren<Transform>(true))
            {
                if (tc == null) continue;
                string tcName = tc.name.ToLowerInvariant();

                if (tcName == "hitbox_vr_positivo" || tcName == "hitbox_vr")
                {
                    Undo.DestroyObjectImmediate(tc.gameObject);
                    continue;
                }

                PuertaAutoCierre puertaFalsa = tc.GetComponent<PuertaAutoCierre>();
                if (puertaFalsa != null)
                    Undo.DestroyObjectImmediate(puertaFalsa);

                XRSimpleInteractable xriFalso = tc.GetComponent<XRSimpleInteractable>();
                if (xriFalso != null)
                    Undo.DestroyObjectImmediate(xriFalso);

                if ((tcName.StartsWith("door") || tcName.StartsWith("closet_door")) && !tcName.Contains("frame"))
                {
                    foreach (Collider col in tc.GetComponents<Collider>())
                    {
                        Undo.RecordObject(col, "Desactivar Collider Puerta Estática");
                        col.enabled = false;
                    }
                }
            }
        }

        // 2. Configurar Hitbox_VR_Positivo en todos los Switches y Puertas visibles + asignar audios reales
        foreach (Transform t in todos)
        {
            if (t == null || t.gameObject.scene.name == null) continue;
            string n = t.name.ToLowerInvariant();

            if (n == "hitbox_vr" || n == "luz_real_cuarto")
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                continue;
            }

            if (n.Contains("clonmimic") || n.Contains("hitbox_vr_positivo")) continue;
            if (PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(t)) continue;

            bool esPuerta = (n.StartsWith("door") || n.StartsWith("closet_door")) && !n.Contains("frame");
            bool esSwitch = n.Contains("switch");

            if (esPuerta || esSwitch)
            {
                BoxCollider bcDirecto = t.GetComponent<BoxCollider>();
                if (bcDirecto != null)
                {
                    Undo.RecordObject(bcDirecto, "Desactivar BoxCollider Escala Negativa");
                    bcDirecto.enabled = false;
                }

                Transform hijoHitbox = t.Find("Hitbox_VR_Positivo");
                if (hijoHitbox == null)
                {
                    GameObject objHitbox = new GameObject("Hitbox_VR_Positivo");
                    Undo.RegisterCreatedObjectUndo(objHitbox, "Crear Hitbox Positivo");
                    hijoHitbox = objHitbox.transform;
                    hijoHitbox.SetParent(t, false);
                }

                MeshRenderer mr = t.GetComponent<MeshRenderer>();
                Bounds wb = mr != null ? mr.bounds : new Bounds(t.position, Vector3.one * 0.25f);

                hijoHitbox.position = wb.center;
                hijoHitbox.rotation = Quaternion.identity;

                Vector3 ls = t.lossyScale;
                hijoHitbox.localScale = new Vector3(
                    Mathf.Abs(ls.x) > 0.0001f ? 1f / ls.x : 1f,
                    Mathf.Abs(ls.y) > 0.0001f ? 1f / ls.y : 1f,
                    Mathf.Abs(ls.z) > 0.0001f ? 1f / ls.z : 1f
                );

                BoxCollider bcPositivo = hijoHitbox.GetComponent<BoxCollider>();
                if (bcPositivo == null)
                    bcPositivo = Undo.AddComponent<BoxCollider>(hijoHitbox.gameObject);

                Undo.RecordObject(bcPositivo, "Configurar BoxCollider Positivo");
                bcPositivo.center = Vector3.zero;
                if (esSwitch)
                {
                    bcPositivo.isTrigger = true;
                    bcPositivo.size = new Vector3(0.24f, 0.24f, 0.24f);
                }
                else
                {
                    bcPositivo.isTrigger = false;
                    bcPositivo.size = new Vector3(
                        Mathf.Max(wb.size.x, 0.22f),
                        Mathf.Max(wb.size.y, 0.40f),
                        Mathf.Max(wb.size.z, 0.22f)
                    );
                }
                bcPositivo.enabled = true;

                if (esPuerta)
                {
                    PuertaAutoCierre scriptPuerta = t.GetComponent<PuertaAutoCierre>();
                    if (scriptPuerta == null)
                        scriptPuerta = Undo.AddComponent<PuertaAutoCierre>(t.gameObject);

                    Undo.RecordObject(scriptPuerta, "Asignar Sonidos y AutoCierre Puerta");
                    scriptPuerta.anguloAbierta = PuertaAutoCierre.ObtenerAnguloAperturaOptimo(t.name);
                    scriptPuerta.segundosParaCerrarse = 5.5f;
                    scriptPuerta.delayAccion = 0.14f;
                    if (clipAbrirPuerta != null) scriptPuerta.sonidoAbrir = clipAbrirPuerta;
                    if (clipCerrarPuerta != null) scriptPuerta.sonidoCerrar = clipCerrarPuerta;
                    if (clipAbrirAnomalia != null) scriptPuerta.sonidoAbrirAnomalia = clipAbrirAnomalia;
                    if (clipCerrarPuerta != null) scriptPuerta.sonidoAzotar = clipCerrarPuerta;
                    EditorUtility.SetDirty(scriptPuerta);

                    ObjetoAnomalia anomPuerta = t.GetComponent<ObjetoAnomalia>();
                    if (anomPuerta != null && anomPuerta.posiblesVariantes != null)
                    {
                        Undo.RecordObject(anomPuerta, "Vincular Puerta a ObjetoAnomalia");
                        foreach (var v in anomPuerta.posiblesVariantes)
                        {
                            if (v != null && v.tipo == TipoAnomalia.Unknown && v.puertaQueSeAbreSola == null && v.objetoExtraActivable == null)
                            {
                                v.puertaQueSeAbreSola = scriptPuerta;
                                anomaliasCorregidas++;
                            }
                        }
                        EditorUtility.SetDirty(anomPuerta);
                    }
                    puertasCorregidas++;
                }

                if (esSwitch)
                {
                    switchesCorregidos++;
                }

                XRSimpleInteractable xri = t.GetComponent<XRSimpleInteractable>();
                if (xri == null) xri = Undo.AddComponent<XRSimpleInteractable>(t.gameObject);

                Undo.RecordObject(xri, "Vincular Collider Positivo a XRI");
                xri.colliders.Clear();
                xri.colliders.Add(bcPositivo);
                EditorUtility.SetDirty(xri);
            }
        }

        // 3. Asignar luces 1 a 1 sin repetición (incluyendo focus02) y calibrar intensidad tenue realista de casa
        InterruptorLuz.AsignarLucesSinRepeticionGlobalmente();
        foreach (InterruptorLuz sw in Object.FindObjectsByType<InterruptorLuz>(FindObjectsInactive.Include))
        {
            if (sw != null)
            {
                sw.estaEncendida = true;
                EditorUtility.SetDirty(sw);
            }
        }
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l != null) EditorUtility.SetDirty(l);
        }

        // 4. Corregir Caja_De_Fusibles_VR (LED tenue + toggle encender/apagar)
        GameObject cajaFusibles = GameObject.Find("Caja_De_Fusibles_VR");
        if (cajaFusibles != null)
        {
            BoxCollider bcCaja = cajaFusibles.GetComponent<BoxCollider>();
            if (bcCaja == null) bcCaja = Undo.AddComponent<BoxCollider>(cajaFusibles);
            Undo.RecordObject(bcCaja, "Ajustar Collider Caja Fusibles");
            bcCaja.size = new Vector3(1.5f, 1.4f, 2.4f);
            bcCaja.center = new Vector3(0f, 0f, -0.3f);

            CajaFusiblesVR cf = cajaFusibles.GetComponent<CajaFusiblesVR>();
            if (cf != null && cf.luzIndicadora != null)
            {
                Undo.RecordObject(cf.luzIndicadora, "Atenuar LED Caja Fusibles");
                cf.luzIndicadora.intensity = 0.35f;
                cf.luzIndicadora.range = 0.85f;
                EditorUtility.SetDirty(cf.luzIndicadora);
            }
        }

        // 5. Reemplazar los 5 Placeholders de Entidades por sus Modelos 3D Reales y Animarlos + Espejo Tulpa
        int entidadesModeladas = ActualizarModelos3DDeEntidades();

        // 6. Hacer ventanas transparentes, construir barreras exteriores, configurar cajones/electrónicos y normalizar cuartos de anomalías
        int ventanasTransparentes = VentanasYLimitesVR.HacerVentanasTransparentes();
        VentanasYLimitesVR.ConstruirBarrerasLimitesMapa();
        InteractorUniversalVR.AsegurarPuertasYSwitchesEnEscena();

        foreach (ObjetoAnomalia anom in Object.FindObjectsByType<ObjetoAnomalia>(FindObjectsInactive.Include))
        {
            if (anom == null) continue;
            MeshRenderer mrAnom = anom.GetComponent<MeshRenderer>();
            Vector3 posAnom = mrAnom != null ? mrAnom.bounds.center : anom.transform.position;
            string cuartoReal = ObjetoAnomalia.NormalizarCuartoPorPosicion(posAnom, anom.nombreCuarto, anom.gameObject.name);
            if (anom.nombreCuarto != cuartoReal)
            {
                Undo.RecordObject(anom, "Normalizar Cuarto Anomalia");
                anom.nombreCuarto = cuartoReal;
                EditorUtility.SetDirty(anom);
            }
        }

        // 7. Configurar SonidoPisadasVR con los 3 clips de pasos (incluyendo soumages-walking-on-wood) y sin delay
        SonidoPisadasVR pisadas = Object.FindAnyObjectByType<SonidoPisadasVR>(FindObjectsInactive.Include);
        if (pisadas != null)
        {
            Undo.RecordObject(pisadas, "Actualizar Clips y Distancia Pisadas");
            AudioClip p1 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/dragon-studio-footsteps-on-wood-397989.mp3");
            AudioClip p2 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sumaga123-shoe-steps-447693.mp3");
            AudioClip p3 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/soumages-walking-on-wood-363349.mp3");
            List<AudioClip> listaPasos = new List<AudioClip>();
            if (p1 != null) listaPasos.Add(p1);
            if (p2 != null) listaPasos.Add(p2);
            if (p3 != null) listaPasos.Add(p3);
            pisadas.clipsPasos = listaPasos.ToArray();
            pisadas.distanciaEntrePasos = 0.62f;
            EditorUtility.SetDirty(pisadas);
        }

        // 8. Activar Control de cámara Ciudad para rotación fluida en Meta Quest 2 y PC
        CiudadCameraLook camLook = Object.FindAnyObjectByType<CiudadCameraLook>(FindObjectsInactive.Include);
        if (camLook != null)
        {
            Undo.RecordObject(camLook.gameObject, "Activar Control Camara Ciudad");
            camLook.gameObject.SetActive(true);
            camLook.enabled = true;
            EditorUtility.SetDirty(camLook);
        }

        // 9. Asignar automáticamente audios sintetizados y ambientales de Fase 1 y Fase 2 a Tablet y Tensión VR
        TabletSistemaVR tablet = Object.FindAnyObjectByType<TabletSistemaVR>(FindObjectsInactive.Include);
        if (tablet != null)
        {
            Undo.RecordObject(tablet, "Asignar Audios Fase 1 y 2 Tablet");
            AudioClip clipProc = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_tablet_procesando.wav");
            AudioClip clipGlitch = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_glitch_tablet.wav");
            if (clipProc != null) tablet.sonidoProcesando = clipProc;
            if (clipGlitch != null) tablet.sonidoGlitch = clipGlitch;
            EditorUtility.SetDirty(tablet);
        }

        TensionYAmbienteVR tension = Object.FindAnyObjectByType<TensionYAmbienteVR>(FindObjectsInactive.Include);
        if (tension == null)
        {
            InteractorUniversalVR inter = Object.FindAnyObjectByType<InteractorUniversalVR>(FindObjectsInactive.Include);
            if (inter != null)
                tension = Undo.AddComponent<TensionYAmbienteVR>(inter.gameObject);
        }
        if (tension != null)
        {
            Undo.RecordObject(tension, "Asignar Audios Tension y Paranoia VR");
            AudioClip clipLatido = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_latido_corazon.wav");
            AudioClip clipViento = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-wind-ambience-14720.mp3");
            AudioClip clipCrujido = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-squeaky-bathroom-door-102756.mp3");
            AudioClip clipGolpe = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/soumages-walking-on-wood-363349.mp3");
            if (clipLatido != null) tension.clipLatidoCorazon = clipLatido;
            if (clipViento != null) tension.clipVientoVentana = clipViento;
            if (clipCrujido != null) tension.clipCrujidoMadera = clipCrujido;
            if (clipGolpe != null) tension.clipGolpeMadera = clipGolpe;
            EditorUtility.SetDirty(tension);
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        if (mostrarDialogo)
        {
            EditorUtility.DisplayDialog("¡Proyecto VR Actualizado al 100%!",
                $"• Luces calibradas con brillo tenue realista (Piso 1: {InterruptorLuz.IntensidadPiso1}, Piso 2: {InterruptorLuz.IntensidadPiso2}).\n" +
                $"• {switchesCorregidos} Switches asignados 1 a 1 sin repetir luces (focus02 incluido) y con delay.\n" +
                $"• Caja de Fusibles con lógica real de APAGAR y PRENDER toda la casa.\n" +
                $"• {puertasCorregidas} Puertas con sonidos reales, auto-cierre y rechinido especial Unknown.\n" +
                $"• {entidadesModeladas} Entidades actualizadas con sus modelos 3D reales, animadas, y Espejo Tulpa activo.\n" +
                $"• {ventanasTransparentes} Ventanas transparentes + Barreras exteriores Anti-Out-of-Bounds + Pisadas sin delay.",
                "¡Excelente!");
        }
        else
        {
            Debug.Log($"[Auto-Fix VR v3] Completado: {switchesCorregidos} switches sin repetición, {puertasCorregidas} puertas con audio real, {entidadesModeladas} entidades 3D animadas, {ventanasTransparentes} ventanas transparentes y barreras de mapa.");
        }
    }

    private static void AsegurarAudioPisadasDecompressOnLoad()
    {
        string[] rutasPasos = {
            "Assets/Model/sonido/dragon-studio-footsteps-on-wood-397989.mp3",
            "Assets/Model/sonido/sumaga123-shoe-steps-447693.mp3",
            "Assets/Model/sonido/soumages-walking-on-wood-363349.mp3"
        };

        foreach (string ruta in rutasPasos)
        {
            AudioImporter imp = AssetImporter.GetAtPath(ruta) as AudioImporter;
            if (imp != null)
            {
                AudioImporterSampleSettings settings = imp.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    imp.defaultSampleSettings = settings;
                    imp.SaveAndReimport();
                }
            }
        }
    }

    private static int ActualizarModelos3DDeEntidades()
    {
        int contador = 0;
        AudioClip clipPasosMadera = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/soumages-walking-on-wood-363349.mp3");

        // 1. PURE FORM -> dark-female-apparition (OBJ nativo optimizado para VR)
        if (ReemplazarVisualEntidad(
            "Anomalia_PureForm_Sala",
            "Assets/Model/Props/dark-female-apparition-3d-model/source/PureForm_Model.obj",
            "Assets/Model/Props/dark-female-apparition-3d-model/textures/tripo_image_2acc1ccd-55f9-4be8-a988-c49b100357ff_0_1.jpeg",
            "Assets/Model/Props/dark-female-apparition-3d-model/textures/tripo_image_2acc1ccd-55f9-4be8-a988-c49b100357ff_2_0.jpeg",
            TipoAnomalia.PureForm,
            alturaDeseada: 1.72f,
            acostadoEnSuelo: false,
            clipAudio: null))
        {
            contador++;
        }

        // 2. PREACHER (MONJE) -> horror-character (OBJ nativo optimizado para VR, con susurros de ObjetoAnomalia)
        if (ReemplazarVisualEntidad(
            "Anomalia_Preacher_Closet",
            "Assets/Model/Props/horror-character/source/Preacher_Model.obj",
            "Assets/Model/Props/horror-character/textures/gltf_embedded_0.jpeg",
            null,
            TipoAnomalia.Preacher,
            alturaDeseada: 1.78f,
            acostadoEnSuelo: false,
            clipAudio: null))
        {
            contador++;
        }

        // 3. FLAWED -> smily-horror-monster (shia lebeuofsf.fbx)
        if (ReemplazarVisualEntidad(
            "Anomalia_Flawed_Recamara",
            "Assets/Model/Props/smily-horror-monster/source/shia lebeuofsf.fbx",
            "Assets/Model/Props/smily-horror-monster/textures/Material.001_Base_Color.png",
            "Assets/Model/Props/smily-horror-monster/textures/Material.001_Normal_OpenGL.png",
            TipoAnomalia.Flawed,
            alturaDeseada: 1.85f,
            acostadoEnSuelo: false,
            clipAudio: clipPasosMadera))
        {
            contador++;
        }

        // 4. TULPA -> horror-ghost-character-blood-stained-spirit (creepy+ghost+girl+3d+model.fbx) + EspejoTulpaVR
        if (ReemplazarVisualEntidad(
            "Anomalia_Tulpa_EspejoBano",
            "Assets/Model/Props/horror-ghost-character-blood-stained-spirit/source/creepy+ghost+girl+3d+model.fbx",
            "Assets/Model/Props/horror-ghost-character-blood-stained-spirit/textures/Horror_Ghost.png",
            null,
            TipoAnomalia.Tulpa,
            alturaDeseada: 1.62f,
            acostadoEnSuelo: false,
            clipAudio: null))
        {
            GameObject objTulpa = GameObject.Find("Anomalia_Tulpa_EspejoBano");
            if (objTulpa != null)
            {
                EspejoTulpaVR espejo = objTulpa.GetComponent<EspejoTulpaVR>();
                if (espejo == null)
                    espejo = Undo.AddComponent<EspejoTulpaVR>(objTulpa);
                espejo.ConfigurarCapasYConstruirEspejo();
                EditorUtility.SetDirty(espejo);
            }
            contador++;
        }

        // 5. CORPSE -> body-bag (Body.fbx)
        if (ReemplazarVisualEntidad(
            "Anomalia_Corpse_Cocina",
            "Assets/Model/Props/body-bag/source/Body.fbx",
            "Assets/Model/Props/body-bag/textures/Body.jpg",
            null,
            TipoAnomalia.Corpse,
            alturaDeseada: 1.75f,
            acostadoEnSuelo: true,
            clipAudio: null))
        {
            contador++;
        }

        return contador;
    }

    private static bool ReemplazarVisualEntidad(
        string nombreAnomaliaGO,
        string rutaModelo,
        string rutaTexturaBase,
        string rutaTexturaNormal,
        TipoAnomalia tipo,
        float alturaDeseada,
        bool acostadoEnSuelo,
        AudioClip clipAudio)
    {
        GameObject ancla = GameObject.Find(nombreAnomaliaGO);
        if (ancla == null) return false;

        GameObject prefabModelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaModelo);
        if (prefabModelo == null)
        {
            Debug.LogWarning($"[RepararPuertasVR] No se pudo cargar el modelo en: {rutaModelo}");
            return false;
        }

        Transform visual = ancla.transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");
        if (visual == null)
        {
            GameObject vObj = new GameObject("Modelo_Visual_(Reemplazar_Por_FBX)");
            vObj.transform.SetParent(ancla.transform, false);
            visual = vObj.transform;
        }

        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;

        // Eliminar primitivas placeholder anteriores (Capsule, Sphere, instancias viejas)
        for (int i = visual.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(visual.GetChild(i).gameObject);
        }

        GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefabModelo, visual);
        if (instancia == null)
            instancia = Object.Instantiate(prefabModelo, visual);

        instancia.name = "Modelo_3D_" + tipo;
        instancia.transform.localPosition = Vector3.zero;
        instancia.transform.localRotation = Quaternion.identity;
        instancia.transform.localScale = Vector3.one;

        // Eliminar colliders internos del modelo de terror para que no bloqueen el paso del jugador
        foreach (Collider c in instancia.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);

        // Aplicar material URP con su textura correspondiente
        Texture2D texBase = !string.IsNullOrEmpty(rutaTexturaBase) ? AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTexturaBase) : null;
        Texture2D texNorm = !string.IsNullOrEmpty(rutaTexturaNormal) ? AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTexturaNormal) : null;

        if (texBase != null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(urpLit)
            {
                name = "Mat_" + tipo
            };
            mat.SetTexture("_BaseMap", texBase);
            mat.mainTexture = texBase;
            mat.SetColor("_BaseColor", tipo == TipoAnomalia.Corpse ? new Color(0.55f, 0.50f, 0.50f, 1f) : Color.white);
            mat.SetFloat("_Smoothness", tipo == TipoAnomalia.Corpse ? 0.35f : 0.25f);
            if (texNorm != null)
                mat.SetTexture("_BumpMap", texNorm);

            foreach (Renderer r in instancia.GetComponentsInChildren<Renderer>(true))
            {
                Material[] arr = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                r.sharedMaterials = arr;
            }
        }

        // Normalizar escala y apoyar el modelo sobre el suelo
        NormalizarTamanoYApoyarEnSuelo(ancla.transform, visual, instancia.transform, alturaDeseada, acostadoEnSuelo);

        // Agregar AnimadorEntidadTerror para que no sea estático
        AnimadorEntidadTerror anim = visual.GetComponent<AnimadorEntidadTerror>();
        if (anim == null)
            anim = Undo.AddComponent<AnimadorEntidadTerror>(visual.gameObject);
        anim.tipoEntidad = tipo;
        anim.mirarAlJugador = (tipo != TipoAnomalia.Preacher && tipo != TipoAnomalia.Corpse);
        anim.GuardarTransformBase();

        // Configurar audio si aplica
        ObjetoAnomalia anom = ancla.GetComponent<ObjetoAnomalia>();
        if (anom != null && anom.posiblesVariantes != null)
        {
            Undo.RecordObject(anom, "Actualizar Variante Entidad");
            foreach (var v in anom.posiblesVariantes)
            {
                if (v == null) continue;
                v.objetoExtraActivable = visual.gameObject;
                if (clipAudio != null)
                {
                    AudioSource aud = ancla.GetComponent<AudioSource>();
                    if (aud == null) aud = Undo.AddComponent<AudioSource>(ancla);
                    aud.clip = clipAudio;
                    aud.playOnAwake = false;
                    aud.spatialBlend = 1f;
                    aud.volume = 0.45f;
                    aud.pitch = 0.82f;
                    aud.minDistance = 1.0f;
                    aud.maxDistance = 10.0f;
                    v.audioEvento = aud;
                    v.sonidoEnBucle = (tipo == TipoAnomalia.Preacher);
                }
            }
            EditorUtility.SetDirty(anom);
        }

        visual.gameObject.SetActive(false);
        return true;
    }

    private static void NormalizarTamanoYApoyarEnSuelo(Transform ancla, Transform visual, Transform modelo, float DimensionObjetivo, bool acostado)
    {
        bool estabaActivo = visual.gameObject.activeSelf;
        visual.gameObject.SetActive(true);

        Renderer[] renderers = modelo.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            //Si es corpse y la malla viene vertical, acostarla primero
            Bounds bInicial = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bInicial.Encapsulate(renderers[i].bounds);

            if (acostado && bInicial.size.y > Mathf.Max(bInicial.size.x, bInicial.size.z))
            {
                modelo.localRotation = Quaternion.Euler(90f, 0f, 0f);
                bInicial = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bInicial.Encapsulate(renderers[i].bounds);
            }
            else if (!acostado && bInicial.size.y < Mathf.Max(bInicial.size.x, bInicial.size.z) * 0.85f)
            {
                // Si una entidad de pie (ej. Tulpa FBX) viene exportada acostada en X o Z, enderezarla automáticamente
                modelo.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                bInicial = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bInicial.Encapsulate(renderers[i].bounds);

                if (bInicial.size.y < Mathf.Max(bInicial.size.x, bInicial.size.z) * 0.85f)
                {
                    modelo.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    bInicial = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        bInicial.Encapsulate(renderers[i].bounds);
                }
            }

            float maxDim = acostado
                ? Mathf.Max(bInicial.size.x, bInicial.size.z, bInicial.size.y)
                : bInicial.size.y;

            if (maxDim > 0.001f)
            {
                float factor = DimensionObjetivo / maxDim;
                modelo.localScale *= factor;
            }

            // Recalcular bounds ya escalados y alinear la base (min.y) con la posición Y del ancla (el suelo del cuarto)
            Bounds bFinal = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bFinal.Encapsulate(renderers[i].bounds);

            Vector3 delta = new Vector3(
                ancla.position.x - bFinal.center.x,
                ancla.position.y - bFinal.min.y,
                ancla.position.z - bFinal.center.z
            );
            modelo.position += delta;
        }

        visual.gameObject.SetActive(estabaActivo);
    }
}

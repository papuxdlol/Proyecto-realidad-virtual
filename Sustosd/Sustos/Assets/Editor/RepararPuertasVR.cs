using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
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
    private const string AutoFixFlagPath = "Temp/AutoFixSustosVR_v30.flag";

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
        ConfigurarMaterialesUnlitPermanentesEnFBXGenerados();
        ConfigurarAnimacionesMixamoYControladores();

        // Asegurar DecompressOnLoad en los 3 audios de pisadas para recorte instantáneo sin delay
        AsegurarAudioPisadasDecompressOnLoad();

        AudioClip clipAbrirPuerta = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/dragon-studio-opening-door-450444.mp3");
        AudioClip clipCerrarPuerta = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/dragon-studio-close-door-382723.mp3");
        AudioClip clipAbrirAnomalia = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-squeaky-bathroom-door-102756.mp3");

        // Restaurar la puerta faltante de Lavandería (Door_007) antes de escanear las puertas
        VentanasYLimitesVR.RestaurarPuertaLavanderiaFaltante();
        VentanasYLimitesVR.ConfigurarNocheOscuraReal();
        VentanasYLimitesVR.AsegurarCollidersMueblesTangibles();

        // Cargar las rotaciones/posiciones/escalas 1:1 exactas de los 14 Light_switch desde House.fbx para revertir cualquier rotación horizontal previa
        Dictionary<string, (Vector3 pos, Quaternion rot, Vector3 esc)> switchesOriginalesFBX = new Dictionary<string, (Vector3, Quaternion, Vector3)>(System.StringComparer.OrdinalIgnoreCase);
        GameObject fbxHouseAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/House.fbx");
        if (fbxHouseAsset != null)
        {
            foreach (Transform tf in fbxHouseAsset.GetComponentsInChildren<Transform>(true))
            {
                if (tf != null && tf.name.StartsWith("Light_switch"))
                    switchesOriginalesFBX[tf.name.Trim()] = (tf.localPosition, tf.localRotation, tf.localScale);
            }
        }

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

            if (n == "hitbox_vr" || n == "luz_real_cuarto" || n == "palanca_rocker_vr")
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                continue;
            }

            if (n.Contains("mensajepared"))
            {
                ObjetoAnomalia anomPared = t.GetComponent<ObjetoAnomalia>();
                if (anomPared != null)
                {
                    Undo.RecordObject(anomPared, "Desactivar Escritura Pared");
                    anomPared.estaActiva = false;
                    if (anomPared.posiblesVariantes != null) anomPared.posiblesVariantes.Clear();
                    EditorUtility.SetDirty(anomPared);
                }
                Undo.RecordObject(t.gameObject, "Ocultar Escritura Pared");
                t.gameObject.SetActive(false);
                continue;
            }

            if (n.Contains("clonmimic") || n.Contains("hitbox_vr_positivo")) continue;
            if (PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(t)) continue;

            bool esPuerta = (n.StartsWith("door") || n.StartsWith("closet_door")) && !n.Contains("frame");
            bool esSwitch = n.Contains("switch");

            if (esPuerta || esSwitch)
            {
                if (esSwitch)
                {
                    // Revertir overrides de rotación en el Prefab House.fbx Y aplicar el transform original 1:1 para que los switches vuelvan a estar verticales sobre el muro
                    Transform rockerHijo = t.Find("Palanca_Rocker_VR");
                    if (rockerHijo != null)
                        Undo.DestroyObjectImmediate(rockerHijo.gameObject);

                    Undo.RecordObject(t, "Restaurar Transform Original Switch");
                    SerializedObject soTr = new SerializedObject(t);
                    SerializedProperty pRot = soTr.FindProperty("m_LocalRotation");
                    SerializedProperty pEul = soTr.FindProperty("m_LocalEulerAnglesHint");
                    SerializedProperty pPos = soTr.FindProperty("m_LocalPosition");
                    SerializedProperty pEsc = soTr.FindProperty("m_LocalScale");
                    if (pRot != null) PrefabUtility.RevertPropertyOverride(pRot, InteractionMode.AutomatedAction);
                    if (pEul != null) PrefabUtility.RevertPropertyOverride(pEul, InteractionMode.AutomatedAction);
                    if (pPos != null) PrefabUtility.RevertPropertyOverride(pPos, InteractionMode.AutomatedAction);
                    if (pEsc != null) PrefabUtility.RevertPropertyOverride(pEsc, InteractionMode.AutomatedAction);

                    if (switchesOriginalesFBX.TryGetValue(t.name.Trim(), out var origSw))
                    {
                        t.localPosition = origSw.pos;
                        t.localRotation = origSw.rot;
                        t.localScale = origSw.esc;
                    }
                    EditorUtility.SetDirty(t);

                    InterruptorLuz swComp = t.GetComponent<InterruptorLuz>();
                    if (swComp == null)
                        swComp = Undo.AddComponent<InterruptorLuz>(t.gameObject);
                    AudioClip clipSwReal = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/168860__orginaljun__light-switch-01.mp3");
                    if (clipSwReal != null)
                        swComp.clipSwitchReal = clipSwReal;
                    swComp.ConfigurarColliderPositivoEInteractable();
                    if (swComp.sonidoClick != null && clipSwReal != null)
                        swComp.sonidoClick.clip = clipSwReal;
                    EditorUtility.SetDirty(swComp);
                    switchesCorregidos++;
                    continue;
                }
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
                bcPositivo.isTrigger = false;
                bcPositivo.size = new Vector3(
                    Mathf.Max(wb.size.x, 0.22f),
                    Mathf.Max(wb.size.y, 0.40f),
                    Mathf.Max(wb.size.z, 0.22f)
                );
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
            if (cf != null)
            {
                Undo.RecordObject(cf, "Asignar Fuse Box Klick");
                AudioClip clipFuse = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-fuse-box-klick-106954.mp3");
                if (clipFuse != null)
                {
                    cf.clipFuseBoxReal = clipFuse;
                    if (cf.sonidoPalanca != null) cf.sonidoPalanca.clip = clipFuse;
                }
                if (cf.luzIndicadora != null)
                {
                    Undo.RecordObject(cf.luzIndicadora, "Atenuar LED Caja Fusibles");
                    cf.luzIndicadora.intensity = 0.35f;
                    cf.luzIndicadora.range = 0.85f;
                    EditorUtility.SetDirty(cf.luzIndicadora);
                }
                EditorUtility.SetDirty(cf);
            }
        }

        // 5. Reemplazar los 5 Placeholders de Entidades por sus Modelos 3D Reales y Animarlos + Espejo Tulpa
        int entidadesModeladas = ActualizarModelos3DDeEntidades();

        // 6. Hacer ventanas transparentes, construir barreras exteriores, colocar placas de Recámaras 1-3, configurar cajones/electrónicos y normalizar cuartos
        int ventanasTransparentes = VentanasYLimitesVR.HacerVentanasTransparentes();
        VentanasYLimitesVR.ConstruirBarrerasLimitesMapa();
        VentanasYLimitesVR.ConstruirLetrerosHabitaciones();
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

        // 9. Importar y conectar los nuevos audios reales seleccionados (Switches, Teléfono, Susurros, Cajones, Electrodomésticos, TV, 3AM, Fusibles, Tablet y Victoria)
        string[] rutasCortosDecompress = {
            "Assets/Model/sonido/23newsound/168860__orginaljun__light-switch-01.mp3",
            "Assets/Model/sonido/23newsound/Telefono.mp3",
            "Assets/Model/sonido/23newsound/freesound_community-drawer-41055.mp3",
            "Assets/Model/sonido/23newsound/glitchstarting.mp3",
            "Assets/Model/sonido/freesound_community-fuse-box-klick-106954.mp3",
            "Assets/Model/sonido/freesound_community-fridge-hum-and-door-68902.mp3",
            "Assets/Model/sonido/freesound_community-microwave-door-open-90798.mp3",
            "Assets/Model/sonido/freesound_community-door-washing-machine-94161.mp3",
            "Assets/Model/sonido/freesound_community-opening-closing-cabinet-door-101970.mp3",
            "Assets/Model/sonido/yourugor-tv-static-noise-291374.mp3",
            "Assets/Model/sonido/sfx_tablet_acierto.wav",
            "Assets/Model/sonido/sfx_tablet_procesando.wav",
            "Assets/Model/sonido/sfx_tablet_fallo.wav",
            "Assets/Model/sonido/sfx_victoria_6am.wav"
        };
        foreach (string rCorto in rutasCortosDecompress)
        {
            if (!File.Exists(rCorto)) continue;
            AssetDatabase.ImportAsset(rCorto, ImportAssetOptions.ForceUpdate);
            AudioImporter aiC = AssetImporter.GetAtPath(rCorto) as AudioImporter;
            if (aiC != null)
            {
                AudioImporterSampleSettings s = aiC.defaultSampleSettings;
                if (s.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    s.loadType = AudioClipLoadType.DecompressOnLoad;
                    aiC.defaultSampleSettings = s;
                    aiC.SaveAndReimport();
                }
            }
        }

        string[] rutasLargos = {
            "Assets/Model/sonido/23newsound/whispering-voices-background.mp3",
            "Assets/Model/sonido/23newsound/heartbeat-fx.mp3",
            "Assets/Model/sonido/23newsound/tic-tac-emilly.mp3",
            "Assets/Model/sonido/homarov-hell-sounds-12417.mp3",
            "Assets/Model/sonido/sfx_preacher_original.wav"
        };
        foreach (string rLargo in rutasLargos)
        {
            if (File.Exists(rLargo))
                AssetDatabase.ImportAsset(rLargo, ImportAssetOptions.ForceUpdate);
        }

        AudioClip clipDrawerReal = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/freesound_community-drawer-41055.mp3");
        AudioClip clipFridgeReal = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-fridge-hum-and-door-68902.mp3");
        AudioClip clipMicroReal  = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-microwave-door-open-90798.mp3");
        AudioClip clipWashReal   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-door-washing-machine-94161.mp3");
        AudioClip clipCabReal    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-opening-closing-cabinet-door-101970.mp3");
        AudioClip clipTvStatReal = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/yourugor-tv-static-noise-291374.mp3");

        foreach (InteractivoMuebleYElectronicoVR me in Object.FindObjectsByType<InteractivoMuebleYElectronicoVR>(FindObjectsInactive.Include))
        {
            if (me == null) continue;
            Undo.RecordObject(me, "Asignar Sonidos Reales Muebles y Electrodomesticos");
            if (clipDrawerReal != null) me.clipCajonReal = clipDrawerReal;
            if (clipFridgeReal != null) me.clipRefrigerador = clipFridgeReal;
            if (clipMicroReal != null)  me.clipMicroondas = clipMicroReal;
            if (clipWashReal != null)   me.clipLavadora = clipWashReal;
            if (clipCabReal != null)    me.clipGabinete = clipCabReal;
            if (clipTvStatReal != null) me.clipEstaticaTV = clipTvStatReal;
            EditorUtility.SetDirty(me);
        }

        TabletSistemaVR tablet = Object.FindAnyObjectByType<TabletSistemaVR>(FindObjectsInactive.Include);
        if (tablet != null)
        {
            Undo.RecordObject(tablet, "Asignar Audios Reales Tablet");
            AudioClip clipAcierto = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_tablet_acierto.wav");
            AudioClip clipProc    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_tablet_procesando.wav");
            AudioClip clipFallo   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_tablet_fallo.wav");
            AudioClip clipGlitch  = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/glitchstarting.mp3");
            if (clipGlitch == null)
                clipGlitch = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_glitch_tablet.wav");
            if (clipAcierto != null) tablet.sonidoAcierto = clipAcierto;
            if (clipProc != null)    tablet.sonidoProcesando = clipProc;
            if (clipFallo != null)   tablet.sonidoFallo = clipFallo;
            if (clipGlitch != null)  tablet.sonidoGlitch = clipGlitch;
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
            Undo.RecordObject(tension, "Asignar Audios Tension, 3AM y Paranoia VR");
            AudioClip clipLatido = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/heartbeat-fx.mp3");
            if (clipLatido == null)
                clipLatido = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_latido_corazon.wav");
            AudioClip clipSusurros = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/whispering-voices-background.mp3");
            AudioClip clipTicTac = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/tic-tac-emilly.mp3");
            AudioClip clipHell3AM = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/homarov-hell-sounds-12417.mp3");
            AudioClip clipViento = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-wind-ambience-14720.mp3");
            AudioClip clipGolpe = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/soumages-walking-on-wood-363349.mp3");
            AudioClip clipGlitchPF = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/glitchstarting.mp3");
            if (clipLatido != null) tension.clipLatidoCorazon = clipLatido;
            if (clipSusurros != null) tension.clipVocesSusurros = clipSusurros;
            if (clipTicTac != null) tension.clipTicTacHoraMuerta = clipTicTac;
            if (clipHell3AM != null) tension.clipSonido3AM = clipHell3AM;
            if (clipViento != null) tension.clipVientoVentana = clipViento;
            if (clipGlitchPF != null) tension.clipGlitchPureForm = clipGlitchPF;
            // NUNCA asignar audio de abrir puerta a paranoia ambiental para evitar falsas alarmas de puerta
            tension.clipCrujidoMadera = null;
            if (clipGolpe != null) tension.clipGolpeMadera = clipGolpe;
            EditorUtility.SetDirty(tension);
        }

        // 9b. Adaptación física Meta Quest 2: CharacterController (escaleras 52.4°), Tablet XRGrabInteractable y Shaders APK Android
        foreach (CharacterController cc in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include))
        {
            if (cc == null) continue;
            Undo.RecordObject(cc, "Ajustar CharacterController Escaleras Quest 2");
            cc.slopeLimit = 62f;
            cc.stepOffset = Mathf.Max(cc.stepOffset, 0.42f);
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0.0005f;
            EditorUtility.SetDirty(cc);
        }

        if (tablet != null)
        {
            XRGrabInteractable grabTab = tablet.GetComponent<XRGrabInteractable>();
            if (grabTab != null)
            {
                Undo.RecordObject(grabTab, "Optimizar XRGrabInteractable Tablet Quest 2");
                grabTab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                grabTab.throwOnDetach = false;
                grabTab.retainTransformParent = false;
                EditorUtility.SetDirty(grabTab);
            }
        }

        Object graphicsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
        if (graphicsAsset != null)
        {
            SerializedObject soGfx = new SerializedObject(graphicsAsset);
            SerializedProperty propShaders = soGfx.FindProperty("m_AlwaysIncludedShaders");
            if (propShaders != null)
            {
                string[] shadersRequeridosAPK = {
                    "Universal Render Pipeline/Lit",
                    "Universal Render Pipeline/Unlit",
                    "Sprites/Default"
                };
                bool cambioShader = false;
                foreach (string sName in shadersRequeridosAPK)
                {
                    Shader sh = Shader.Find(sName);
                    if (sh == null) continue;
                    bool existe = false;
                    for (int idx = 0; idx < propShaders.arraySize; idx++)
                    {
                        if (propShaders.GetArrayElementAtIndex(idx).objectReferenceValue == sh)
                        {
                            existe = true;
                            break;
                        }
                    }
                    if (!existe)
                    {
                        int nuevoIdx = propShaders.arraySize;
                        propShaders.InsertArrayElementAtIndex(nuevoIdx);
                        propShaders.GetArrayElementAtIndex(nuevoIdx).objectReferenceValue = sh;
                        cambioShader = true;
                    }
                }
                if (cambioShader)
                {
                    soGfx.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                }
            }
        }

        // 10. Asegurar y conectar audios de Fase 3 (Screamer + Death Screen VHS + Victoria 6:00 AM)
        FinalTurnoVR finalTurno = Object.FindAnyObjectByType<FinalTurnoVR>(FindObjectsInactive.Include);
        if (finalTurno == null)
        {
            InteractorUniversalVR inter = Object.FindAnyObjectByType<InteractorUniversalVR>(FindObjectsInactive.Include);
            if (inter != null)
                finalTurno = Undo.AddComponent<FinalTurnoVR>(inter.gameObject);
        }
        if (finalTurno != null)
        {
            string[] rutasDeathScreenWav = {
                "Assets/Model/sonido/sfx_screamer_anomalia.wav",
                "Assets/Model/sonido/sfx_deathscreen_vhs_alarm.wav",
                "Assets/Model/sonido/sfx_deathscreen_flatline.wav"
            };
            foreach (string rutaWav in rutasDeathScreenWav)
            {
                if (!File.Exists(rutaWav)) continue;
                AssetDatabase.ImportAsset(rutaWav, ImportAssetOptions.ForceUpdate);
                AudioImporter ai = AssetImporter.GetAtPath(rutaWav) as AudioImporter;
                if (ai != null)
                {
                    AudioImporterSampleSettings s = ai.defaultSampleSettings;
                    if (s.loadType != AudioClipLoadType.DecompressOnLoad || s.compressionFormat != AudioCompressionFormat.PCM || ai.forceToMono)
                    {
                        ai.forceToMono = false;
                        ai.loadInBackground = false;
                        s.loadType = AudioClipLoadType.DecompressOnLoad;
                        s.compressionFormat = AudioCompressionFormat.PCM;
                        ai.defaultSampleSettings = s;
                        ai.SaveAndReimport();
                    }
                }
            }

            Undo.RecordObject(finalTurno, "Asignar Audios Fase 3 FinalTurnoVR");
            AudioClip clipScream = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_screamer_anomalia.wav");
            AudioClip clipVhs = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_deathscreen_vhs_alarm.wav");
            AudioClip clipFlat = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_deathscreen_flatline.wav");
            AudioClip clipVic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_victoria_6am.wav");
            Font fontVt323 = AssetDatabase.LoadAssetAtPath<Font>("Assets/VT323/VT323-Regular.ttf");
            if (clipScream != null) finalTurno.sonidoScreamer = clipScream;
            if (clipVhs != null) finalTurno.sonidoAlarmaVHS = clipVhs;
            if (clipFlat != null) finalTurno.sonidoFlatline = clipFlat;
            if (clipVic != null) finalTurno.sonidoVictoria6AM = clipVic;
            if (fontVt323 != null) finalTurno.fuenteTerrorVHS = fontVt323;
            EditorUtility.SetDirty(finalTurno);
        }

        // 11. Inicializar Tareas Base B (Cuadros Imagery) y Bloque 1 (Puerta Imposible Unknown, Relojes Vivos, Muebles y Aparatos)
        Bloque1AnomaliasInteractivasVR bloque1 = Object.FindAnyObjectByType<Bloque1AnomaliasInteractivasVR>(FindObjectsInactive.Include);
        if (bloque1 == null)
        {
            InteractorUniversalVR inter = Object.FindAnyObjectByType<InteractorUniversalVR>(FindObjectsInactive.Include);
            if (inter != null)
                bloque1 = Undo.AddComponent<Bloque1AnomaliasInteractivasVR>(inter.gameObject);
        }
        if (bloque1 != null)
        {
            Undo.RecordObject(bloque1, "Asignar Telefono Analogo, TicTac Reloj y Estatica TV");
            AudioClip clipTelAnalogo = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/Telefono.mp3");
            AudioClip clipTicTacReloj = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/tic-tac-emilly.mp3");
            if (clipTelAnalogo != null) bloque1.clipTelefonoAnalogo = clipTelAnalogo;
            if (clipTicTacReloj != null) bloque1.clipTicTacReloj = clipTicTacReloj;
            if (clipTvStatReal != null) bloque1.clipEstaticaTV = clipTvStatReal;
            bloque1.InicializarTodoBloque1();
            EditorUtility.SetDirty(bloque1);
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

    private static void ConfigurarMaterialesUnlitPermanentesEnFBXGenerados()
    {
        if (File.Exists("Assets/Model/Props/Generados/Preacher_Ojos_Tex.png"))
            AssetDatabase.DeleteAsset("Assets/Model/Props/Generados/Preacher_Ojos_Tex.png");

        if (AssetDatabase.IsValidFolder("Assets/Model/Props/smily-horror-monster"))
            AssetDatabase.DeleteAsset("Assets/Model/Props/smily-horror-monster");

        AssetDatabase.ImportAsset("Assets/Model/Props/Generados/Preacher_Rostro_Tex.png", ImportAssetOptions.ForceUpdate);
        TextureImporter ti = AssetImporter.GetAtPath("Assets/Model/Props/Generados/Preacher_Rostro_Tex.png") as TextureImporter;
        if (ti != null && (ti.wrapMode != TextureWrapMode.Clamp || ti.mipmapEnabled))
        {
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.SaveAndReimport();
        }

        string[] normalMapsFlawed = {
            "Assets/Model/Props/Generados/Flawed_Tela_Normal.png",
            "Assets/Model/Props/Generados/Flawed_Denim_Normal.png",
            "Assets/Model/Props/Generados/Flawed_Piel_Normal.png",
            "Assets/Model/Props/Generados/Flawed_Cabello_Normal.png"
        };
        foreach (string rutaNorm in normalMapsFlawed)
        {
            if (!File.Exists(rutaNorm)) continue;
            AssetDatabase.ImportAsset(rutaNorm, ImportAssetOptions.ForceUpdate);
            TextureImporter tin = AssetImporter.GetAtPath(rutaNorm) as TextureImporter;
            if (tin != null && (tin.textureType != TextureImporterType.NormalMap || tin.wrapMode != TextureWrapMode.Repeat))
            {
                tin.textureType = TextureImporterType.NormalMap;
                tin.wrapMode = TextureWrapMode.Repeat;
                tin.mipmapEnabled = true;
                tin.filterMode = FilterMode.Trilinear;
                tin.SaveAndReimport();
            }
        }

        string[] texsFlawed = {
            "Assets/Model/Props/Generados/Flawed_Piel_SinRostro_Tex.png",
            "Assets/Model/Props/Generados/Flawed_Piel_CuelloRoto_Tex.png",
            "Assets/Model/Props/Generados/Flawed_PlayeraGris_Tex.png",
            "Assets/Model/Props/Generados/Flawed_CamisaMoteada_Tex.png",
            "Assets/Model/Props/Generados/Flawed_Jeans_Tex.png",
            "Assets/Model/Props/Generados/Flawed_Cabello_Tex.png",
            "Assets/Model/Props/Generados/Flawed_03_PielGrito_Tex.png",
            "Assets/Model/Props/Generados/Flawed_03_BlusaMostaza_Tex.png",
            "Assets/Model/Props/Generados/Flawed_03_Bolso_Tex.png",
            "Assets/Model/Props/Generados/Flawed_03_PantalonAzul_Tex.png",
            "Assets/Model/Props/Generados/Flawed_04_PielTraje_Tex.png",
            "Assets/Model/Props/Generados/Flawed_04_CamisaInt_Tex.png",
            "Assets/Model/Props/Generados/Flawed_04_SacoTraje_Tex.png",
            "Assets/Model/Props/Generados/Flawed_04_PantalonTraje_Tex.png",
            "Assets/Model/Props/Generados/Flawed_05_PielBuho_Tex.png",
            "Assets/Model/Props/Generados/Flawed_05_BlusaJaspeada_Tex.png",
            "Assets/Model/Props/Generados/Flawed_06_PielVentana_Tex.png",
            "Assets/Model/Props/Generados/Flawed_06_AbrigoVentana_Tex.png"
        };
        foreach (string rutaTex in texsFlawed)
        {
            if (!File.Exists(rutaTex)) continue;
            AssetDatabase.ImportAsset(rutaTex, ImportAssetOptions.ForceUpdate);
            TextureImporter tif = AssetImporter.GetAtPath(rutaTex) as TextureImporter;
            bool clampTex = rutaTex.Contains("Piel");
            if (tif != null && (tif.wrapMode != (clampTex ? TextureWrapMode.Clamp : TextureWrapMode.Repeat) || tif.mipmapEnabled))
            {
                tif.wrapMode = clampTex ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                tif.mipmapEnabled = false;
                tif.filterMode = FilterMode.Bilinear;
                tif.SaveAndReimport();
            }
        }

        Shader shUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
        Shader shLitMate = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Texture2D texCaraPreacher = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Preacher_Rostro_Tex.png");

        Texture2D normTela = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_Tela_Normal.png");
        Texture2D normDenim = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_Denim_Normal.png");
        Texture2D normPiel = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_Piel_Normal.png");
        Texture2D normPelo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_Cabello_Normal.png");

        Material matNegroVacio = CrearOActualizarMaterialAsset(
            "Assets/Model/Props/Generados/Mat_SombraNegra_Unlit.mat",
            shUnlit,
            Color.black,
            null);

        Material matOjosPureForm = CrearOActualizarMaterialAsset(
            "Assets/Model/Props/Generados/Mat_OjosLuz_PureForm.mat",
            shUnlit,
            new Color(3.2f, 3.3f, 3.6f, 1f),
            null);

        Material matRostroPreacher = CrearOActualizarMaterialAsset(
            "Assets/Model/Props/Generados/Mat_Preacher_Rostro_1a1.mat",
            shUnlit,
            new Color(0.72f, 0.72f, 0.77f, 1f),
            texCaraPreacher);

        // Materiales PBR Mate con Normal Maps reales (CERO efecto plastilina!) para los 6 modelos Flawed
        Material matFlPiel1 = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed_Piel1.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_Piel_SinRostro_Tex.png"), normPiel, 0.65f, 0.10f);
        Material matFlPiel2 = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed_Piel2.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_Piel_CuelloRoto_Tex.png"), normPiel, 0.55f, 0.10f);
        Material matFlCam1 = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed_Camisa1.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_PlayeraGris_Tex.png"), normTela, 1.15f, 0.05f);
        Material matFlCam2 = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed_Camisa2.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_CamisaMoteada_Tex.png"), normTela, 1.20f, 0.05f);
        Material matFlJeans = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed_Jeans.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_Jeans_Tex.png"), normDenim, 1.20f, 0.05f);
        Material matFlCabello = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed_Cabello.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_Cabello_Tex.png"), normPelo, 1.30f, 0.14f);
        Material matFlZapatos = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed_Zapatos.mat", shLitMate, new Color(0.06f, 0.06f, 0.07f, 1f),
            null, normTela, 0.80f, 0.10f);

        Material matFl03Piel = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed03_Piel.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_03_PielGrito_Tex.png"), normPiel, 0.55f, 0.10f);
        Material matFl03Blusa = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed03_Blusa.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_03_BlusaMostaza_Tex.png"), normTela, 1.15f, 0.05f);
        Material matFl03Bolso = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed03_Bolso.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_03_Bolso_Tex.png"), normDenim, 1.35f, 0.05f);
        Material matFl03Pan = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed03_Pantalon.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_03_PantalonAzul_Tex.png"), normTela, 1.10f, 0.05f);
        Material matFl03Zap = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed03_Zapatos.mat", shLitMate, new Color(0.09f, 0.065f, 0.05f, 1f),
            null, normTela, 0.80f, 0.10f);

        Material matFl04Piel = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed04_Piel.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_04_PielTraje_Tex.png"), normPiel, 0.65f, 0.10f);
        Material matFl04CamInt = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed04_CamisaInt.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_04_CamisaInt_Tex.png"), normTela, 1.10f, 0.05f);
        Material matFl04Saco = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed04_Saco.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_04_SacoTraje_Tex.png"), normDenim, 1.15f, 0.05f);
        Material matFl04Pan = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed04_Pantalon.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_04_PantalonTraje_Tex.png"), normTela, 1.10f, 0.05f);

        Material matFl05Piel = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed05_Piel.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_05_PielBuho_Tex.png"), normPiel, 0.60f, 0.10f);
        Material matFl05Blusa = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed05_Blusa.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_05_BlusaJaspeada_Tex.png"), normTela, 1.18f, 0.05f);

        Material matFl06Piel = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed06_Piel.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_06_PielVentana_Tex.png"), normPiel, 0.65f, 0.10f);
        Material matFl06Abrigo = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Flawed06_Abrigo.mat", shLitMate, Color.white,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Flawed_06_AbrigoVentana_Tex.png"), normTela, 1.25f, 0.05f);

        AssetDatabase.ImportAsset("Assets/Model/Props/Generados/PureForm_Silueta.fbx", ImportAssetOptions.ForceUpdate);
        ModelImporter miPf = AssetImporter.GetAtPath("Assets/Model/Props/Generados/PureForm_Silueta.fbx") as ModelImporter;
        if (miPf != null)
        {
            miPf.animationType = ModelImporterAnimationType.Human;
            miPf.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            miPf.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "PF_Cuerpo"), matNegroVacio);
            miPf.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "PF_Ojos"), matOjosPureForm);
            miPf.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "PF_HaloOjos"), matOjosPureForm);
            miPf.SaveAndReimport();
        }

        AssetDatabase.ImportAsset("Assets/Model/Props/Generados/Preacher_Monje.fbx", ImportAssetOptions.ForceUpdate);
        ModelImporter miPr = AssetImporter.GetAtPath("Assets/Model/Props/Generados/Preacher_Monje.fbx") as ModelImporter;
        if (miPr != null)
        {
            miPr.animationType = ModelImporterAnimationType.Generic;
            miPr.importAnimation = true;
            ConfigurarLoopsEnModelImporter(miPr, true, false);
            miPr.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "PR_Tunica"), matNegroVacio);
            miPr.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "PR_Rostro"), matRostroPreacher);
            miPr.SaveAndReimport();
        }

        if (File.Exists("Assets/Model/Props/Generados/Tulpa_Rigged.fbx"))
        {
            Texture2D texTulpa = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/horror-ghost-character-blood-stained-spirit/textures/Horror_Ghost.png");
            Material matTulpa = CrearOActualizarMaterialMateLit("Assets/Model/Props/Generados/Mat_Tulpa_Fantasma.mat", shLitMate, Color.white, texTulpa, null, 1f, 0.12f);
            AssetDatabase.ImportAsset("Assets/Model/Props/Generados/Tulpa_Rigged.fbx", ImportAssetOptions.ForceUpdate);
            ModelImporter miTu = AssetImporter.GetAtPath("Assets/Model/Props/Generados/Tulpa_Rigged.fbx") as ModelImporter;
            if (miTu != null)
            {
                miTu.animationType = ModelImporterAnimationType.Human;
                miTu.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                miTu.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "TU_Fantasma"), matTulpa);
                miTu.SaveAndReimport();
            }
        }

        string[] fbxFlawed = {
            "Assets/Model/Props/Generados/Flawed_01_SinRostro.fbx",
            "Assets/Model/Props/Generados/Flawed_02_CuelloRoto.fbx",
            "Assets/Model/Props/Generados/Flawed_03_MandibulaEstirada.fbx",
            "Assets/Model/Props/Generados/Flawed_04_HombreTraje.fbx",
            "Assets/Model/Props/Generados/Flawed_05_CabezaGirada.fbx",
            "Assets/Model/Props/Generados/Flawed_06_Ventana.fbx"
        };
        var mapaMatsFlawed = new (string slot, Material mat)[]
        {
            ("FL_Piel_1", matFlPiel1), ("FL_Piel_1.001", matFlPiel1),
            ("FL_Piel_2", matFlPiel2), ("FL_Piel_2.001", matFlPiel2),
            ("FL_Camisa_1", matFlCam1),
            ("FL_Camisa_2", matFlCam2),
            ("FL_Pantalon", matFlJeans), ("FL_Pantalon.001", matFlJeans),
            ("FL_Cabello", matFlCabello), ("FL_Cabello.001", matFlCabello),
            ("FL_Zapatos", matFlZapatos), ("FL_Zapatos.001", matFlZapatos),
            ("FL_NegroVacio", matNegroVacio),

            ("FL03_Piel", matFl03Piel), ("FL03_PielBrazos", matFl03Piel),
            ("FL03_Blusa", matFl03Blusa),
            ("FL03_Bolso", matFl03Bolso),
            ("FL03_Pantalon", matFl03Pan),
            ("FL03_Cabello", matFlCabello),
            ("FL03_Zapatos", matFl03Zap),

            ("FL04_Piel", matFl04Piel), ("FL04_PielManos", matFl04Piel),
            ("FL04_CamisaInt", matFl04CamInt),
            ("FL04_Saco", matFl04Saco),
            ("FL04_Pantalon", matFl04Pan),
            ("FL04_Cabello", matFlCabello),
            ("FL04_Zapatos", matFlZapatos),

            ("FL05_Piel", matFl05Piel), ("FL05_PielManos", matFl05Piel),
            ("FL05_BlusaJaspeada", matFl05Blusa),
            ("FL05_Jeans", matFlJeans),
            ("FL05_Cabello", matFlCabello),
            ("FL05_Zapatos", matFlZapatos),

            ("FL06_PielVentana", matFl06Piel), ("FL06_PielManos", matFl06Piel),
            ("FL06_Abrigo", matFl06Abrigo),
            ("FL06_Pantalon", matFl04Pan),
            ("FL06_Zapatos", matFlZapatos)
        };

        foreach (string rutaFbxFl in fbxFlawed)
        {
            if (!File.Exists(rutaFbxFl)) continue;
            AssetDatabase.ImportAsset(rutaFbxFl, ImportAssetOptions.ForceUpdate);
            ModelImporter miFl = AssetImporter.GetAtPath(rutaFbxFl) as ModelImporter;
            if (miFl != null)
            {
                bool esVentanaGeneric = rutaFbxFl.Contains("Flawed_06_Ventana");
                if (esVentanaGeneric)
                {
                    miFl.animationType = ModelImporterAnimationType.Generic;
                    miFl.importAnimation = true;
                    ConfigurarLoopsEnModelImporter(miFl, true, false);
                }
                else
                {
                    miFl.animationType = ModelImporterAnimationType.Human;
                    miFl.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                }

                foreach (var par in mapaMatsFlawed)
                {
                    if (par.mat != null)
                        miFl.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), par.slot), par.mat);
                }
                miFl.SaveAndReimport();
            }
        }
    }

    private static void ConfigurarLoopsEnModelImporter(ModelImporter mi, bool loop, bool esHumanoidLockRoot, bool bloquearAlturaY = true)
    {
        if (mi == null) return;
        ModelImporterClipAnimation[] clips = mi.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = mi.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = loop;
            clips[i].loopPose = loop;
            if (esHumanoidLockRoot)
            {
                clips[i].lockRootRotation = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].lockRootHeightY = bloquearAlturaY;
                // CRÍTICO: Anclar la raíz Y en los PIES (heightFromFeet = true) y NUNCA en Center of Mass,
                // evitando al 100% que Flawed, PureForm o Tulpa se hundan ~0.8m a través del piso hacia la Cocina abajo!
                clips[i].heightFromFeet = true;
                clips[i].heightOffset = 0f;
                clips[i].keepOriginalOrientation = false;
                clips[i].keepOriginalPositionXZ = false;
                clips[i].keepOriginalPositionY = false;
            }
        }
        mi.clipAnimations = clips;
    }

    private static AnimationClip CargarClipDeFBX(string rutaFbx, string filtroNombre = null)
    {
        if (!File.Exists(rutaFbx)) return null;
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(rutaFbx);
        AnimationClip fallback = null;
        for (int i = 0; i < subAssets.Length; i++)
        {
            AnimationClip c = subAssets[i] as AnimationClip;
            if (c == null || c.name.StartsWith("__preview__")) continue;
            if (fallback == null) fallback = c;
            if (!string.IsNullOrEmpty(filtroNombre) && c.name.IndexOf(filtroNombre, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return c;
        }
        return fallback;
    }

    private static AnimatorController CrearControladorEstados(string rutaCtrl, (string estado, AnimationClip clip)[] estados)
    {
        Directory.CreateDirectory("Assets/Animations/Controllers");
        if (File.Exists(rutaCtrl))
            AssetDatabase.DeleteAsset(rutaCtrl);

        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(rutaCtrl);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        bool primero = true;
        for (int i = 0; i < estados.Length; i++)
        {
            if (estados[i].clip == null) continue;
            AnimatorState st = sm.AddState(estados[i].estado);
            st.motion = estados[i].clip;
            if (primero)
            {
                sm.defaultState = st;
                primero = false;
            }
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    private static void ConfigurarAnimacionesMixamoYControladores()
    {
        (string ruta, bool loop, bool lockY)[] clipsHumanoid = new (string, bool, bool)[]
        {
            ("Assets/Animations/Agony.fbx", true, true),
            ("Assets/Animations/Breathing Idle.fbx", true, true),
            ("Assets/Animations/Looking Around.fbx", true, true),
            ("Assets/Animations/Mutant Breathing Idle.fbx", true, true),
            ("Assets/Animations/Sit To Stand.fbx", false, false),
            ("Assets/Animations/Sitting Disbelief.fbx", true, true),
            ("Assets/Animations/Sitting Dodges.fbx", true, true),
            ("Assets/Animations/Sitting Rubbing Arm.fbx", true, true),
            ("Assets/Animations/Custom/Custom_Sitting_EerieIdle.fbx", true, true),
            ("Assets/Animations/Custom/Custom_Flawed_TwitchStare.fbx", true, true),
            ("Assets/Animations/Custom/Custom_PureForm_PeekCorner.fbx", true, true),
            ("Assets/Animations/Custom/Custom_PureForm_StalkerSnap.fbx", true, true),
            ("Assets/Animations/Custom/Custom_Tulpa_MirrorReach.fbx", true, true)
        };

        for (int i = 0; i < clipsHumanoid.Length; i++)
        {
            var item = clipsHumanoid[i];
            if (!File.Exists(item.ruta)) continue;
            AssetDatabase.ImportAsset(item.ruta, ImportAssetOptions.ForceUpdate);
            ModelImporter mi = AssetImporter.GetAtPath(item.ruta) as ModelImporter;
            if (mi != null)
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                ConfigurarLoopsEnModelImporter(mi, item.loop, true, item.lockY);
                mi.SaveAndReimport();
            }
        }

        AnimationClip cBreathing    = CargarClipDeFBX("Assets/Animations/Breathing Idle.fbx");
        AnimationClip cMutant       = CargarClipDeFBX("Assets/Animations/Mutant Breathing Idle.fbx");
        AnimationClip cLookAround   = CargarClipDeFBX("Assets/Animations/Looking Around.fbx");
        AnimationClip cAgony        = CargarClipDeFBX("Assets/Animations/Agony.fbx");
        AnimationClip cSitDisbelief = CargarClipDeFBX("Assets/Animations/Sitting Disbelief.fbx");
        AnimationClip cSitDodges    = CargarClipDeFBX("Assets/Animations/Sitting Dodges.fbx");
        AnimationClip cSitRubbing   = CargarClipDeFBX("Assets/Animations/Sitting Rubbing Arm.fbx");
        AnimationClip cSitToStand   = CargarClipDeFBX("Assets/Animations/Sit To Stand.fbx");

        AnimationClip cSitEerie     = CargarClipDeFBX("Assets/Animations/Custom/Custom_Sitting_EerieIdle.fbx");
        AnimationClip cFlawedTwitch = CargarClipDeFBX("Assets/Animations/Custom/Custom_Flawed_TwitchStare.fbx");
        AnimationClip cPfPeek       = CargarClipDeFBX("Assets/Animations/Custom/Custom_PureForm_PeekCorner.fbx");
        AnimationClip cPfStalk      = CargarClipDeFBX("Assets/Animations/Custom/Custom_PureForm_StalkerSnap.fbx");
        AnimationClip cTulpaReach   = CargarClipDeFBX("Assets/Animations/Custom/Custom_Tulpa_MirrorReach.fbx");

        AnimationClip cWinStalk     = CargarClipDeFBX("Assets/Model/Props/Generados/Flawed_06_Ventana.fbx", "Window_Stalk");
        AnimationClip cWinPress     = CargarClipDeFBX("Assets/Model/Props/Generados/Flawed_06_Ventana.fbx", "Window_Glass");
        AnimationClip cPreacherWhis = CargarClipDeFBX("Assets/Model/Props/Generados/Preacher_Monje.fbx", "Preacher_Closet");

        CrearControladorEstados("Assets/Animations/Controllers/AC_Flawed_Pie.controller", new[]
        {
            ("BreathingIdle",   cBreathing),
            ("TwitchStare",     cFlawedTwitch ?? cBreathing),
            ("MutantBreathing", cMutant ?? cBreathing),
            ("LookingAround",   cLookAround ?? cBreathing),
            ("Agony",           cAgony ?? cBreathing)
        });

        CrearControladorEstados("Assets/Animations/Controllers/AC_Flawed_Sentado.controller", new[]
        {
            ("SittingIdle",       cSitEerie ?? cSitDisbelief),
            ("SittingDisbelief",  cSitDisbelief),
            ("SittingRubbingArm", cSitRubbing ?? cSitDisbelief),
            ("SittingDodges",     cSitDodges ?? cSitDisbelief),
            ("SitToStand",        cSitToStand ?? cSitDisbelief)
        });

        CrearControladorEstados("Assets/Animations/Controllers/AC_Flawed_Ventana.controller", new[]
        {
            ("WindowStalk", cWinStalk),
            ("WindowPress", cWinPress ?? cWinStalk)
        });

        CrearControladorEstados("Assets/Animations/Controllers/AC_PureForm.controller", new[]
        {
            ("MutantBreathing", cMutant ?? cBreathing),
            ("StalkerSnap",     cPfStalk ?? cMutant),
            ("BreathingIdle",   cBreathing),
            ("PeekCorner",      cPfPeek ?? cBreathing)
        });

        CrearControladorEstados("Assets/Animations/Controllers/AC_Preacher.controller", new[]
        {
            ("ClosetWhisper", cPreacherWhis)
        });

        CrearControladorEstados("Assets/Animations/Controllers/AC_Tulpa.controller", new[]
        {
            ("BreathingIdle", cBreathing),
            ("MirrorReach",   cTulpaReach ?? cBreathing),
            ("LookingAround", cLookAround ?? cBreathing),
            ("Agony",         cAgony ?? cBreathing)
        });

        AssetDatabase.SaveAssets();
    }

    private static Material CrearOActualizarMaterialMateLit(string rutaMat, Shader sh, Color colorBase, Texture2D tex, Texture2D normalTex = null, float bumpScale = 1.0f, float smoothness = 0.06f)
    {
        Material m = CrearOActualizarMaterialAsset(rutaMat, sh, colorBase, tex);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.0f);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0.0f);
        if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0.0f);
        if (normalTex != null)
        {
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", normalTex);
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", bumpScale);
            m.EnableKeyword("_NORMALMAP");
        }
        else
        {
            m.DisableKeyword("_NORMALMAP");
        }
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material CrearOActualizarMaterialAsset(string rutaMat, Shader sh, Color colorBase, Texture2D tex)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, rutaMat);
        }
        else
        {
            m.shader = sh;
        }
        m.color = colorBase;
        m.SetColor("_BaseColor", colorBase);
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_MainTex", tex);
        m.mainTexture = tex;
        EditorUtility.SetDirty(m);
        return m;
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

        // 1. PURE FORM -> Nuevo modelo Blender PureForm_Silueta.fbx (Silueta negra esbelta con ojos blancos al ras)
        if (ReemplazarVisualEntidad(
            "Anomalia_PureForm_Sala",
            "Assets/Model/Props/Generados/PureForm_Silueta.fbx",
            null,
            null,
            TipoAnomalia.PureForm,
            alturaDeseada: 1.88f,
            acostadoEnSuelo: false,
            clipAudio: null))
        {
            contador++;
        }

        // 2. PREACHER (MONJE) -> Modelo Preacher_Monje.fbx + audio original extraído a bajo volumen con atenuación espacial por cuarto
        if (File.Exists("Assets/Model/sonido/sfx_preacher_original.wav"))
            AssetDatabase.ImportAsset("Assets/Model/sonido/sfx_preacher_original.wav", ImportAssetOptions.ForceUpdate);
        AudioClip clipPreacherOrig = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_preacher_original.wav");
        if (ReemplazarVisualEntidad(
            "Anomalia_Preacher_Closet",
            "Assets/Model/Props/Generados/Preacher_Monje.fbx",
            null,
            null,
            TipoAnomalia.Preacher,
            alturaDeseada: 1.88f,
            acostadoEnSuelo: false,
            clipAudio: clipPreacherOrig))
        {
            contador++;
        }

        // 3. FLAWED -> Primeras 2 variantes humanoides con rasgos no humanos (Flawed_01_SinRostro y Flawed_02_CuelloRoto)
        if (ConfigurarVariantesFlawed(clipPasosMadera))
        {
            contador++;
        }

        // 4. TULPA -> Nuevo modelo riggeado Tulpa_Rigged.fbx (con esqueleto Mixamo Humanoid + textura Horror_Ghost.png + EspejoTulpaVR)
        string rutaModeloTulpa = File.Exists("Assets/Model/Props/Generados/Tulpa_Rigged.fbx")
            ? "Assets/Model/Props/Generados/Tulpa_Rigged.fbx"
            : "Assets/Model/Props/horror-ghost-character-blood-stained-spirit/source/creepy+ghost+girl+3d+model.fbx";
        if (ReemplazarVisualEntidad(
            "Anomalia_Tulpa_EspejoBano",
            rutaModeloTulpa,
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

        // 5. CORPSE -> Sistema contextual con DeadBody LITE (Acostado en cama/suelo, Colgado del techo, y Bañera exclusiva del baño con tina; NUNCA variante parada)
        if (ConfigurarVariantesContextualesCorpse())
        {
            contador++;
        }

        return contador;
    }

    private static void LimpiarFisicasYStaticRecursivo(GameObject raiz)
    {
        if (raiz == null) return;
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            t.gameObject.isStatic = false;
        }
        foreach (Collider c in raiz.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
        foreach (Rigidbody rb in raiz.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(rb);
        foreach (SkinnedMeshRenderer smr in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.updateWhenOffscreen = true;
    }

    private static bool ConfigurarVariantesFlawed(AudioClip clipAudio)
    {
        GameObject ancla = GameObject.Find("Anomalia_Flawed_Recamara");
        if (ancla == null) return false;

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
        visual.gameObject.SetActive(true);

        for (int i = visual.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(visual.GetChild(i).gameObject);
        }

        string[] rutasVariantes = {
            "Assets/Model/Props/Generados/Flawed_01_SinRostro.fbx",
            "Assets/Model/Props/Generados/Flawed_02_CuelloRoto.fbx",
            "Assets/Model/Props/Generados/Flawed_03_MandibulaEstirada.fbx",
            "Assets/Model/Props/Generados/Flawed_04_HombreTraje.fbx",
            "Assets/Model/Props/Generados/Flawed_05_CabezaGirada.fbx",
            "Assets/Model/Props/Generados/Flawed_06_Ventana.fbx"
        };
        float[] alturasVariantes = { 1.72f, 1.86f, 1.83f, 1.83f, 1.73f, 1.84f };

        RuntimeAnimatorController ctrlPie = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Controllers/AC_Flawed_Pie.controller");
        RuntimeAnimatorController ctrlSentado = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Controllers/AC_Flawed_Sentado.controller");
        RuntimeAnimatorController ctrlVentana = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Controllers/AC_Flawed_Ventana.controller");

        int creadas = 0;
        for (int i = 0; i < rutasVariantes.Length; i++)
        {
            GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(rutasVariantes[i]);
            if (pf == null) continue;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, visual);
            if (inst == null)
                inst = Object.Instantiate(pf, visual);

            bool esTrajeSentado = rutasVariantes[i].Contains("HombreTraje");
            bool esVentana = rutasVariantes[i].Contains("Ventana");
            inst.name = esTrajeSentado
                ? "Flawed_Variante_3_TrajeSentado"
                : (rutasVariantes[i].Contains("CabezaGirada")
                    ? "Flawed_Variante_4_CabezaGirada"
                    : (esVentana ? "Flawed_Variante_5_Ventana" : "Flawed_Variante_" + creadas));
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            LimpiarFisicasYStaticRecursivo(inst);

            Animator animComp = inst.GetComponentInChildren<Animator>(true);
            if (animComp == null)
                animComp = inst.AddComponent<Animator>();
            animComp.runtimeAnimatorController = esTrajeSentado ? ctrlSentado : (esVentana && ctrlVentana != null ? ctrlVentana : ctrlPie);
            animComp.applyRootMotion = false;
            animComp.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Asegurar que el rostro mire hacia +Z local del contenedor visual (en CabezaGirada los zapatos apuntan de espaldas a propósito)
            Renderer rZap = null;
            Renderer rPan = null;
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name.Contains("FL_Zapatos")) rZap = r;
                else if (r.gameObject.name.Contains("FL_Pantalon")) rPan = r;
            }
            if (rZap != null && rPan != null)
            {
                float dzPuntaPie = visual.InverseTransformPoint(rZap.bounds.center).z - visual.InverseTransformPoint(rPan.bounds.center).z;
                bool cuerpoDeEspaldas = rutasVariantes[i].Contains("CabezaGirada");
                if (cuerpoDeEspaldas ? (dzPuntaPie > 0.005f) : (dzPuntaPie < -0.005f))
                    inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            NormalizarTamanoYApoyarEnSuelo(ancla.transform, visual, inst.transform, alturasVariantes[i], false);
            inst.SetActive(creadas == 0);
            creadas++;
        }

        ObjetoAnomalia.AplicarMaterialesMateFlawed(visual);

        AnimadorEntidadTerror anim = visual.GetComponent<AnimadorEntidadTerror>();
        if (anim == null)
            anim = Undo.AddComponent<AnimadorEntidadTerror>(visual.gameObject);
        anim.tipoEntidad = TipoAnomalia.Flawed;
        anim.mirarAlJugador = true;
        anim.GuardarTransformBase();

        ObjetoAnomalia anom = ancla.GetComponent<ObjetoAnomalia>();
        if (anom != null && anom.posiblesVariantes != null)
        {
            Undo.RecordObject(anom, "Actualizar Variantes Flawed");
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
                    v.sonidoEnBucle = false;
                }
            }
            EditorUtility.SetDirty(anom);
        }

        visual.gameObject.SetActive(false);
        EditorUtility.SetDirty(ancla);
        return creadas > 0;
    }

    private static bool ConfigurarVariantesContextualesCorpse()
    {
        GameObject ancla = GameObject.Find("Anomalia_Corpse_Cocina");
        if (ancla == null) return false;

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
        visual.gameObject.SetActive(true);

        for (int i = visual.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(visual.GetChild(i).gameObject);
        }

        // A) CORPSE ACOSTADO (Para camas en Bedroom1..3 y suelo en cuartos sin cama; 3 variantes visuales de DeadBody LITE)
        GameObject grpAcostado = new GameObject("Corpse_Acostado");
        grpAcostado.transform.SetParent(visual, false);
        string[] rutasAcostado = {
            "Assets/DeadBody LITE/Prefabs/Body_Only/DeadBody (1).prefab",
            "Assets/DeadBody LITE/Prefabs/Body_Only/DeadBody (3).prefab",
            "Assets/DeadBody LITE/Prefabs/Body_Only/DeadBody (5).prefab"
        };
        int posesAcostado = 0;
        for (int i = 0; i < rutasAcostado.Length; i++)
        {
            GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(rutasAcostado[i]);
            if (pf == null) continue;
            GameObject inst = Object.Instantiate(pf, grpAcostado.transform);
            inst.name = "Pose_" + posesAcostado;
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            LimpiarFisicasYStaticRecursivo(inst);

            Renderer[] rs = inst.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int r = 1; r < rs.Length; r++) b.Encapsulate(rs[r].bounds);
                if (b.size.y > Mathf.Max(b.size.x, b.size.z))
                {
                    inst.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    b = rs[0].bounds;
                    for (int r = 1; r < rs.Length; r++) b.Encapsulate(rs[r].bounds);
                }
                inst.transform.position += new Vector3(
                    grpAcostado.transform.position.x - b.center.x,
                    grpAcostado.transform.position.y - b.min.y,
                    grpAcostado.transform.position.z - b.center.z
                );
            }
            inst.SetActive(posesAcostado == 0);
            posesAcostado++;
        }

        // B) CORPSE COLGADO DEL TECHO (2 variantes de soga; pivote superior y=0 justo en la punta de la cuerda para anclar al techo)
        GameObject grpColgado = new GameObject("Corpse_Colgado");
        grpColgado.transform.SetParent(visual, false);
        string[] rutasColgado = {
            "Assets/DeadBody LITE/Prefabs/Hanging_Body/Hanging 1/Deadbody_Hanging (1).prefab",
            "Assets/DeadBody LITE/Prefabs/Hanging_Body/Hanging 2/Deadbody_HangingS2 (2).prefab"
        };
        int posesColgado = 0;
        for (int i = 0; i < rutasColgado.Length; i++)
        {
            GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(rutasColgado[i]);
            if (pf == null) continue;
            GameObject inst = Object.Instantiate(pf, grpColgado.transform);
            inst.name = "Colgado_" + posesColgado;
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            // Quitar el script ExecuteInEditMode DeadBodyHanging para que no sobreescriba la cuerda cada frame
            foreach (MonoBehaviour mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == "DeadBodyHanging")
                    Object.DestroyImmediate(mb);
            }
            LimpiarFisicasYStaticRecursivo(inst);

            Renderer[] rs = inst.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int r = 1; r < rs.Length; r++) b.Encapsulate(rs[r].bounds);
                // Ajustar altura total (~2.12m cuerda + cuerpo) para que en Piso 1 y Piso 2 los pies siempre queden suspendidos en el aire
                if (b.size.y > 0.01f)
                {
                    float factor = 2.12f / b.size.y;
                    inst.transform.localScale *= factor;
                    b = rs[0].bounds;
                    for (int r = 1; r < rs.Length; r++) b.Encapsulate(rs[r].bounds);
                }
                // Alinear el extremo SUPERIOR de la cuerda (b.max.y) en y=0 local para colgar directamente del techo
                inst.transform.position += new Vector3(
                    grpColgado.transform.position.x - b.center.x,
                    grpColgado.transform.position.y - b.max.y,
                    grpColgado.transform.position.z - b.center.z
                );
            }
            inst.SetActive(posesColgado == 0);
            posesColgado++;
        }
        grpColgado.SetActive(false);

        // C) CORPSE EN BAÑERA (Exclusivo para el baño superior con bathtub; incluye sangre + 3 cuerpos seleccionables)
        GameObject grpBanera = new GameObject("Corpse_Banera");
        grpBanera.transform.SetParent(visual, false);
        GameObject pfTub = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeadBody LITE/Prefabs/Bath_Tub/Bathtub With Bodies (1).prefab");
        if (pfTub != null)
        {
            GameObject instTub = Object.Instantiate(pfTub, grpBanera.transform);
            instTub.name = "Tina_Con_Cuerpo";
            instTub.transform.localPosition = Vector3.zero;
            instTub.transform.localRotation = Quaternion.identity;
            instTub.transform.localScale = new Vector3(0.87f, 0.98f, 0.76f);

            foreach (MonoBehaviour mb in instTub.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == "BathTub")
                    Object.DestroyImmediate(mb);
            }
            LimpiarFisicasYStaticRecursivo(instTub);

            Transform tBlood = instTub.transform.Find("Blood");
            if (tBlood != null) tBlood.gameObject.SetActive(true);

            Transform tB1 = instTub.transform.Find("deadbody_Tub1");
            Transform tB2 = instTub.transform.Find("deadbody_Tub2");
            Transform tB3 = instTub.transform.Find("deadbody_Tub3");
            if (tB1 != null) tB1.gameObject.SetActive(true);
            if (tB2 != null) tB2.gameObject.SetActive(false);
            if (tB3 != null) tB3.gameObject.SetActive(false);

            Transform tMeshTub = instTub.transform.Find("Bathtub");
            Renderer mrRef = tMeshTub != null ? tMeshTub.GetComponent<Renderer>() : instTub.GetComponentInChildren<Renderer>(true);
            if (mrRef != null)
            {
                Bounds bt = mrRef.bounds;
                instTub.transform.position += new Vector3(
                    grpBanera.transform.position.x - bt.center.x,
                    grpBanera.transform.position.y - bt.min.y,
                    grpBanera.transform.position.z - bt.center.z
                );
            }
        }
        grpBanera.SetActive(false);

        AnimadorEntidadTerror anim = visual.GetComponent<AnimadorEntidadTerror>();
        if (anim == null)
            anim = Undo.AddComponent<AnimadorEntidadTerror>(visual.gameObject);
        anim.tipoEntidad = TipoAnomalia.Corpse;
        anim.mirarAlJugador = false;
        anim.modoCorpse = ModoCorpseContextual.Acostado;
        anim.GuardarTransformBase();

        ObjetoAnomalia anom = ancla.GetComponent<ObjetoAnomalia>();
        if (anom != null && anom.posiblesVariantes != null)
        {
            Undo.RecordObject(anom, "Actualizar Variantes Corpse Contextual");
            foreach (var v in anom.posiblesVariantes)
            {
                if (v == null) continue;
                v.objetoExtraActivable = visual.gameObject;
            }
            EditorUtility.SetDirty(anom);
        }

        visual.gameObject.SetActive(false);
        EditorUtility.SetDirty(ancla);
        return posesAcostado > 0;
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

        // Eliminar colliders, rigidbodies y flags Static internos para evitar bloqueos y Static Batching roto al moverse
        LimpiarFisicasYStaticRecursivo(instancia);

        // Aplicar material URP con su textura correspondiente (o materiales de sombra/ojos para PureForm_Silueta)
        Texture2D texBase = !string.IsNullOrEmpty(rutaTexturaBase) ? AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTexturaBase) : null;
        Texture2D texNorm = !string.IsNullOrEmpty(rutaTexturaNormal) ? AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTexturaNormal) : null;

        if (tipo == TipoAnomalia.PureForm && texBase == null)
        {
            // PureForm usa material 100% Unlit negro absoluto (#000000) sin reflejos ("plastilina"/"piel") + focos de luz blancos con halo óptico
            ObjetoAnomalia.AplicarMaterialesAbismalesPureForm(visual);
        }
        else if (tipo == TipoAnomalia.Preacher && texBase == null)
        {
            Texture2D texRostroPr = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/Props/Generados/Preacher_Rostro_Tex.png");
            ObjetoAnomalia.AplicarMaterialesTerrorPreacher(visual, texRostroPr);
        }
        else if (texBase != null)
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

        // Vincular su AnimatorController esquelético (Mixamo + Custom)
        string rutaCtrlEntidad = tipo == TipoAnomalia.PureForm ? "Assets/Animations/Controllers/AC_PureForm.controller" :
                                 (tipo == TipoAnomalia.Preacher ? "Assets/Animations/Controllers/AC_Preacher.controller" :
                                 (tipo == TipoAnomalia.Tulpa ? "Assets/Animations/Controllers/AC_Tulpa.controller" : null));
        if (!string.IsNullOrEmpty(rutaCtrlEntidad))
        {
            RuntimeAnimatorController ctrlEnt = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(rutaCtrlEntidad);
            Animator animInst = instancia.GetComponentInChildren<Animator>(true);
            if (animInst == null)
                animInst = instancia.AddComponent<Animator>();
            animInst.runtimeAnimatorController = ctrlEnt;
            animInst.applyRootMotion = false;
            animInst.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // Agregar AnimadorEntidadTerror para que controle cambios de estado y Animation Rigging
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
                    if (tipo == TipoAnomalia.Preacher)
                    {
                        ObjetoAnomalia.ConfigurarAudioEspacialPreacher(aud);
                        aud.pitch = 1.0f;
                        aud.volume = 0f;
                    }
                    else
                    {
                        aud.playOnAwake = false;
                        aud.spatialBlend = 1f;
                        aud.volume = 0.45f;
                        aud.pitch = 0.82f;
                        aud.minDistance = 1.0f;
                        aud.maxDistance = 10.0f;
                    }
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

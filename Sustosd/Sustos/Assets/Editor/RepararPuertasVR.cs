using System.IO;
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
    private const string AutoFixFlagPath = "Temp/AutoFixSustosVR_v2.flag";

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
        // 0. Asegurar que Universal Render Pipeline tenga Additional Lights = Per Pixel (8) habilitado en memoria y en disco
        InterruptorLuz.HabilitarLucesAdicionalesEnURP();
        AssetDatabase.ImportAsset("Assets/Settings/Project Configuration/Performance URP Config.asset", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Settings/Project Configuration/Quality URP Config.asset", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Settings/Project Configuration/Android Preset.asset", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Settings/Project Configuration/Standalone Preset.asset", ImportAssetOptions.ForceUpdate);

        Transform[] todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int switchesCorregidos = 0;
        int puertasCorregidas = 0;
        int lucesCorregidas = 0;
        int anomaliasCorregidas = 0;

        // 1. Limpiar House_Colliders: desactivar colliders de puertas estáticas y eliminar PuertaAutoCierre que se hayan agregado ahí por error
        GameObject houseColliders = GameObject.Find("House_Colliders");
        if (houseColliders != null)
        {
            foreach (Transform tc in houseColliders.GetComponentsInChildren<Transform>(true))
            {
                if (tc == null) continue;
                string tcName = tc.name.ToLower();

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

        // 2. Sacar las Point Lights de adentro de los Focus (-95.79), bajarlas debajo de la losa del techo y encenderlas con ForcePixel
        Light[] luces = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject contenedorLuces = GameObject.Find("Contenedor_Luces_Techo");
        if (contenedorLuces == null)
        {
            contenedorLuces = new GameObject("Contenedor_Luces_Techo");
            Undo.RegisterCreatedObjectUndo(contenedorLuces, "Crear Contenedor Luces");
        }

        foreach (Light l in luces)
        {
            if (l == null || l.type != LightType.Point) continue;
            if (l.name.Contains("Fusibles") || l.name.Contains("Unknown") || l.name.Contains("Electrico") || l.name.Contains("Luz_Real_Cuarto"))
                continue;

            if (l.transform.parent != null && l.transform.parent.name.ToLower().StartsWith("focus"))
            {
                Renderer rFoco = l.transform.parent.GetComponent<Renderer>();
                Vector3 posMundialFoco = rFoco != null ? rFoco.bounds.center : l.transform.parent.position;
                Undo.SetTransformParent(l.transform, contenedorLuces.transform, "Desemparentar Luz");
                l.transform.position = posMundialFoco;
                l.transform.rotation = Quaternion.identity;
                l.transform.localScale = Vector3.one;
            }

            Undo.RecordObject(l.transform, "Ajustar Altura Luz Techo");
            Vector3 pos = l.transform.position;
            if (pos.y > 2.70f && pos.y < 4.30f)
                pos.y = 2.52f; // Debajo del techo del Piso 1 (antes estaba en 3.20m dentro de la losa entre pisos)
            else if (pos.y > 5.90f)
                pos.y = 5.78f; // Debajo del techo del Piso 2 (antes estaba en 6.51m sobre el techo)
            l.transform.position = pos;

            Undo.RecordObject(l, "Configurar Luz Techo");
            l.renderMode = LightRenderMode.ForcePixel;
            l.shadows = LightShadows.None;
            l.intensity = 11f;
            l.range = 9.5f;
            l.color = new Color(1f, 0.95f, 0.84f);
            l.cullingMask = ~0;
            l.enabled = true;
            EditorUtility.SetDirty(l);
            lucesCorregidas++;
        }

        // 3. Configurar Hitbox_VR_Positivo (escala mundial +1, +1, +1) en todos los Switches y Puertas visibles
        foreach (Transform t in todos)
        {
            if (t == null || t.gameObject.scene.name == null) continue;
            string n = t.name.ToLower();

            if (n == "hitbox_vr")
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
                // Desactivar el BoxCollider directo que hereda escala negativa (-95.79)
                BoxCollider bcDirecto = t.GetComponent<BoxCollider>();
                if (bcDirecto != null)
                {
                    Undo.RecordObject(bcDirecto, "Desactivar BoxCollider Escala Negativa");
                    bcDirecto.enabled = false;
                }

                // Crear o actualizar hijo Hitbox_VR_Positivo con lossyScale = (1, 1, 1)
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
                    bcPositivo.size = new Vector3(0.30f, 0.30f, 0.30f);
                }
                else
                {
                    bcPositivo.isTrigger = false;
                    bcPositivo.size = new Vector3(
                        Mathf.Max(wb.size.x, 0.20f),
                        Mathf.Max(wb.size.y, 0.40f),
                        Mathf.Max(wb.size.z, 0.20f)
                    );
                }
                bcPositivo.enabled = true;

                // Asegurar PuertaAutoCierre en todas las puertas visibles y vincular su anomalía Unknown
                if (esPuerta)
                {
                    PuertaAutoCierre scriptPuerta = t.GetComponent<PuertaAutoCierre>();
                    if (scriptPuerta == null)
                        scriptPuerta = Undo.AddComponent<PuertaAutoCierre>(t.gameObject);

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
                    InterruptorLuz sw = t.GetComponent<InterruptorLuz>();
                    if (sw != null)
                    {
                        Undo.RecordObject(sw, "Reasignar Luz Real al Switch");
                        sw.estaEncendida = true;
                        sw.VincularLucesRealesDelCuarto();
                        EditorUtility.SetDirty(sw);
                    }
                    switchesCorregidos++;
                }

                // Vincular el BoxCollider positivo al XRSimpleInteractable
                XRSimpleInteractable xri = t.GetComponent<XRSimpleInteractable>();
                if (xri == null) xri = Undo.AddComponent<XRSimpleInteractable>(t.gameObject);

                Undo.RecordObject(xri, "Vincular Collider Positivo a XRI");
                xri.colliders.Clear();
                xri.colliders.Add(bcPositivo);
                EditorUtility.SetDirty(xri);
            }
        }

        // 4. Corregir Caja_De_Fusibles_VR
        GameObject cajaFusibles = GameObject.Find("Caja_De_Fusibles_VR");
        if (cajaFusibles != null)
        {
            BoxCollider bcCaja = cajaFusibles.GetComponent<BoxCollider>();
            if (bcCaja == null) bcCaja = Undo.AddComponent<BoxCollider>(cajaFusibles);
            Undo.RecordObject(bcCaja, "Ajustar Collider Caja Fusibles");
            bcCaja.size = new Vector3(1.5f, 1.4f, 2.4f);
            bcCaja.center = new Vector3(0f, 0f, -0.3f);

            XRSimpleInteractable xriCaja = cajaFusibles.GetComponent<XRSimpleInteractable>();
            if (xriCaja != null)
            {
                Undo.RecordObject(xriCaja, "Vincular Collider Caja Fusibles");
                xriCaja.colliders.Clear();
                xriCaja.colliders.Add(bcCaja);
                EditorUtility.SetDirty(xriCaja);
            }
        }

        // 5. Corregir ventanas con LuzDeDia_Unknown, Texto_Sangre_Pared y las 5 Entidades
        foreach (ObjetoAnomalia anom in Object.FindObjectsByType<ObjetoAnomalia>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (anom == null) continue;
            string n = anom.name.ToLower();

            if (anom.posiblesVariantes != null)
            {
                foreach (var v in anom.posiblesVariantes)
                {
                    if (v?.objetoExtraActivable != null && v.objetoExtraActivable.name.ToLower().Contains("luzdedia_unknown"))
                    {
                        MeshRenderer mrWin = anom.GetComponent<MeshRenderer>();
                        Vector3 lossy = anom.transform.lossyScale;
                        float sx = Mathf.Abs(lossy.x) > 0.0001f ? 1f / Mathf.Abs(lossy.x) : 1f;
                        float sy = Mathf.Abs(lossy.y) > 0.0001f ? 1f / Mathf.Abs(lossy.y) : 1f;
                        float sz = Mathf.Abs(lossy.z) > 0.0001f ? 1f / Mathf.Abs(lossy.z) : 1f;

                        Undo.RecordObject(v.objetoExtraActivable.transform, "Corregir Escala LuzDeDia");
                        if (mrWin != null)
                        {
                            Bounds wb = mrWin.bounds;
                            v.objetoExtraActivable.transform.position = wb.center;
                            v.objetoExtraActivable.transform.rotation = Quaternion.identity;
                            v.objetoExtraActivable.transform.localScale = new Vector3(
                                Mathf.Clamp(wb.size.x, 0.08f, 2.2f) * sx,
                                Mathf.Clamp(wb.size.z, 0.08f, 2.2f) * sy,
                                Mathf.Clamp(wb.size.y, 0.8f, 2.2f) * sz
                            );
                        }
                        else
                        {
                            v.objetoExtraActivable.transform.localScale = new Vector3(1.1f * sx, 0.05f * sy, 1.4f * sz);
                        }
                        anomaliasCorregidas++;
                    }
                }
            }

            if (n.Contains("mensajepared"))
            {
                Undo.RecordObject(anom.transform, "Alinear Mensaje Pared");
                anom.transform.position = new Vector3(10.02f, 1.78f, 2.25f);
                anom.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                TextMeshPro tmp = anom.GetComponentInChildren<TextMeshPro>(true);
                if (tmp != null)
                {
                    Undo.RecordObject(tmp, "Ajustar Texto Pared");
                    tmp.fontSize = 2.2f;
                    RectTransform rt = tmp.GetComponent<RectTransform>();
                    if (rt != null) rt.sizeDelta = new Vector2(2.4f, 0.7f);
                }
                anomaliasCorregidas++;
            }
            else if (n.StartsWith("anomalia_pureform") || n.StartsWith("anomalia_tulpa"))
            {
                Undo.RecordObject(anom.transform, "Ajustar Altura Entidad Piso 1");
                Vector3 p = anom.transform.position;
                anom.transform.position = new Vector3(p.x, 0.41f, p.z);
                anomaliasCorregidas++;
            }
            else if (n.StartsWith("anomalia_corpse"))
            {
                Undo.RecordObject(anom.transform, "Ajustar Altura Corpse Cocina");
                Vector3 p = anom.transform.position;
                anom.transform.position = new Vector3(p.x, 0.57f, p.z);
                Transform vis = anom.transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)");
                if (vis != null && vis.childCount > 0)
                {
                    Undo.RecordObject(vis.GetChild(0), "Centrar Capsula Corpse");
                    vis.GetChild(0).localPosition = Vector3.zero;
                }
                anomaliasCorregidas++;
            }
            else if (n.StartsWith("anomalia_preacher") || n.StartsWith("anomalia_flawed"))
            {
                Undo.RecordObject(anom.transform, "Ajustar Altura Entidad Piso 2");
                Vector3 p = anom.transform.position;
                anom.transform.position = new Vector3(p.x, 3.74f, p.z);
                anomaliasCorregidas++;
            }
        }

        // 6. Limpiar InteractorUniversalVR duplicado en la Tablet y asegurar Rayos XR
        GameObject tablet = GameObject.Find("Tablet de anomalías");
        if (tablet != null)
        {
            InteractorUniversalVR interactorDuplicado = tablet.GetComponent<InteractorUniversalVR>();
            if (interactorDuplicado != null)
                Undo.DestroyObjectImmediate(interactorDuplicado);

            BoxCollider bcTablet = tablet.GetComponent<BoxCollider>();
            if (bcTablet != null)
            {
                Undo.RecordObject(bcTablet, "Ajustar Collider Tablet");
                bcTablet.center = new Vector3(0f, -0.0025f, 0f);
            }

            Transform pantalla = tablet.transform.Find("Pantalla de reportes");
            if (pantalla != null)
            {
                Undo.RecordObject(pantalla, "Ajustar Pantalla Tablet");
                pantalla.localPosition = new Vector3(0f, 0.003f, 0f);
                pantalla.localRotation = Quaternion.Euler(90f, 0f, 0f);

                GraphicRaycaster gr = pantalla.GetComponent<GraphicRaycaster>();
                if (gr != null) gr.ignoreReversedGraphics = false;

                TrackedDeviceGraphicRaycaster tdgr = pantalla.GetComponent<TrackedDeviceGraphicRaycaster>();
                if (tdgr != null)
                {
                    tdgr.ignoreReversedGraphics = false;
                    tdgr.checkFor3DOcclusion = false;
                }
            }
        }

        foreach (XRRayInteractor ray in Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ray == null) continue;
            Undo.RecordObject(ray, "Configurar XRRayInteractor");
            ray.allowHoveredActivate = true;
            ray.enableUIInteraction = true;
            ray.blockUIOnInteractableSelection = false;
            EditorUtility.SetDirty(ray);
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        if (mostrarDialogo)
        {
            EditorUtility.DisplayDialog("¡Puertas y Luces Reparadas!",
                $"• Additional Lights en URP habilitadas en modo Per Pixel (8 luces).\n" +
                $"• {lucesCorregidas} Luces de techo bajadas debajo de la losa (Piso 1: 2.52m, Piso 2: 5.78m) con ForcePixel.\n" +
                $"• {switchesCorregidos} Switches vinculados a las luces reales de su mismo piso.\n" +
                $"• {puertasCorregidas} Puertas visibles reparadas (eliminados duplicados en House_Colliders y doble toggle).",
                "¡Perfecto!");
        }
        else
        {
            Debug.Log($"[Auto-Fix VR v2] Reparación completada: URP PerPixel activo, {lucesCorregidas} luces reposicionadas, {switchesCorregidos} switches revinculados, {puertasCorregidas} puertas limpias.");
        }
    }
}

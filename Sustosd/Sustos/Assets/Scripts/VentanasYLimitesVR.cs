using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class VentanasYLimitesVR
{
    private static Material materialVidrioTransparente;

    public static void ConfigurarNocheOscuraReal()
    {
        Color colorNocheProfunda = new Color(0.012f, 0.015f, 0.024f, 1f);
        Color colorAmbienteNocturno = new Color(0.008f, 0.010f, 0.016f, 1f);

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = colorAmbienteNocturno;
        RenderSettings.ambientSkyColor = colorAmbienteNocturno;
        RenderSettings.ambientEquatorColor = colorAmbienteNocturno;
        RenderSettings.ambientGroundColor = new Color(0.005f, 0.006f, 0.010f, 1f);
        RenderSettings.ambientIntensity = 0.2f;
        RenderSettings.reflectionIntensity = 0.05f;
        RenderSettings.subtractiveShadowColor = new Color(0.02f, 0.025f, 0.04f, 1f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = colorNocheProfunda;
        RenderSettings.fogDensity = 0.045f;

        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (cam == null) continue;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = colorNocheProfunda;
        }

        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l != null && l.type == LightType.Directional)
            {
                l.color = new Color(0.04f, 0.05f, 0.09f, 1f);
                l.intensity = 0.01f;
            }
        }
    }

    public static PuertaAutoCierre RestaurarPuertaLavanderiaFaltante()
    {
        GameObject house = GameObject.Find("House");
        if (house == null) return null;

        // Verificar si ya existe Door_007
        foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name == "Door_007" && !PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(t))
            {
                PuertaAutoCierre pExistente = t.GetComponent<PuertaAutoCierre>();
                if (pExistente == null) pExistente = t.gameObject.AddComponent<PuertaAutoCierre>();
                pExistente.CalcularBisagraYHitboxPositivo();
                return pExistente;
            }
        }

        // Buscar Door_001 (misma orientación R=(-90,0,0) y escala S=(100,100,100) que el marco Door_frame_005 de Lavandería)
        Transform plantillaDoor001 = null;
        Transform marcoLavanderia = null;
        foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(t)) continue;
            if (t.name == "Door_001") plantillaDoor001 = t;
            else if (t.name == "Door_frame_005") marcoLavanderia = t;
        }

        if (plantillaDoor001 == null) return null;

        GameObject nuevaPuerta = Object.Instantiate(plantillaDoor001.gameObject, plantillaDoor001.parent);
        nuevaPuerta.name = "Door_007";
        nuevaPuerta.SetActive(true);

        // Posición exacta encajada en Door_frame_005 (X=[16.9735, 18.0009], Y=[0.3979, 2.9376], Z=-5.6047)
        if (marcoLavanderia != null)
        {
            Vector3 pMarco = marcoLavanderia.position;
            nuevaPuerta.transform.position = new Vector3(pMarco.x - 0.4391f, pMarco.y - 0.2246f, pMarco.z);
        }
        else
        {
            nuevaPuerta.transform.position = new Vector3(17.0297f, 1.6615f, -5.6047f);
        }

        nuevaPuerta.transform.localRotation = plantillaDoor001.localRotation;
        nuevaPuerta.transform.localScale = plantillaDoor001.localScale;

        PuertaAutoCierre compPuerta = nuevaPuerta.GetComponent<PuertaAutoCierre>();
        if (compPuerta == null)
            compPuerta = nuevaPuerta.AddComponent<PuertaAutoCierre>();

        PuertaAutoCierre puertaRef = plantillaDoor001.GetComponent<PuertaAutoCierre>();
        if (puertaRef != null)
        {
            compPuerta.sonidoAbrir = puertaRef.sonidoAbrir;
            compPuerta.sonidoCerrar = puertaRef.sonidoCerrar;
            compPuerta.sonidoAbrirAnomalia = puertaRef.sonidoAbrirAnomalia;
            compPuerta.sonidoAzotar = puertaRef.sonidoCerrar;
        }

        compPuerta.anguloAbierta = 90f;
        compPuerta.CalcularBisagraYHitboxPositivo();

        ObjetoAnomalia anom = nuevaPuerta.GetComponent<ObjetoAnomalia>();
        if (anom == null)
            anom = nuevaPuerta.AddComponent<ObjetoAnomalia>();

        anom.nombreCuarto = "Laundry";
        if (anom.posiblesVariantes == null)
            anom.posiblesVariantes = new List<VarianteAnomalia>();
        anom.posiblesVariantes.Clear();
        anom.posiblesVariantes.Add(new VarianteAnomalia
        {
            nombreDescriptivo = "Puerta de Lavandería se abre sola (Unknown)",
            tipo = TipoAnomalia.Unknown,
            puertaQueSeAbreSola = compPuerta
        });

        return compPuerta;
    }

    public static Material ObtenerMaterialVidrioTransparente()
    {
        if (materialVidrioTransparente != null)
            return materialVidrioTransparente;

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        materialVidrioTransparente = new Material(sh)
        {
            name = "Glass_Transparente_VR"
        };

        ConfigurarMaterialComoVidrioURP(materialVidrioTransparente);
        return materialVidrioTransparente;
    }

    public static void ConfigurarMaterialComoVidrioURP(Material mat)
    {
        if (mat == null) return;

        Color colorCristal = new Color(0.76f, 0.88f, 0.95f, 0.16f);
        mat.SetColor("_BaseColor", colorCristal);
        mat.color = colorCristal;

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_Smoothness", 0.92f);
        mat.SetFloat("_Metallic", 0.08f);
        mat.SetFloat("_Cull", 0f);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetOverrideTag("RenderType", "Transparent");
    }

    public static int HacerVentanasTransparentes()
    {
        GameObject house = GameObject.Find("House");
        if (house == null) return 0;

        Material vidrio = ObtenerMaterialVidrioTransparente();
        int cristalesActualizados = 0;

        foreach (MeshRenderer mr in house.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr == null) continue;
            string objName = mr.gameObject.name.ToLowerInvariant();
            if (objName.Contains("luzdedia_unknown")) continue;

            Material[] mats = mr.sharedMaterials;
            bool modificado = false;

            if (objName.StartsWith("glass"))
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = vidrio;
                    modificado = true;
                }
            }
            else
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string matName = mats[i].name.ToLowerInvariant();
                    if (matName.Contains("glass") || matName == "windows" || matName.StartsWith("windows "))
                    {
                        ConfigurarMaterialComoVidrioURP(mats[i]);
                        mats[i] = vidrio;
                        modificado = true;
                    }
                }
            }

            if (modificado)
            {
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                cristalesActualizados++;
            }
        }

        return cristalesActualizados;
    }

    public static void ConstruirBarrerasLimitesMapa()
    {
        GameObject house = GameObject.Find("House");
        if (house == null) return;

        // Buscar Fence_003 (la valla perimetral de la casa: X=[7.57, 28.09], Y=[-0.31, 1.86], Z=[-24.22, 10.70])
        MeshRenderer fenceRenderer = null;
        foreach (MeshRenderer mr in house.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr != null && mr.gameObject.name == "Fence_003")
            {
                fenceRenderer = mr;
                break;
            }
        }

        float minX = 7.61f;
        float maxX = 28.03f;
        float minZ = -24.17f;
        float maxZ = 9.25f; // Justo antes de los botes de basura y la calle para cerrar el jardín delantero con vallas

        float centroX = (minX + maxX) * 0.5f;
        float centroZ = (minZ + maxZ) * 0.5f;
        float anchoX = (maxX - minX);
        float largoZ = (maxZ - minZ);
        float alturaCollider = 6.0f;
        float centroY = 2.5f;
        float grosor = 0.25f;

        GameObject contenedor = GameObject.Find("Contenedor_Barreras_Mapa");
        if (contenedor == null)
            contenedor = new GameObject("Contenedor_Barreras_Mapa");

        // Limpiar barreras antiguas si tenían otros nombres
        for (int i = contenedor.transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = contenedor.transform.GetChild(i);
            if (ch != null && ch.name.StartsWith("Barrera_"))
                Object.DestroyImmediate(ch.gameObject);
        }

        // 1. Valla visual + collider en el frente (cerrando el jardín delantero y la entrada de autos de X=7.57 a X=28.09 en Z=9.25)
        CrearOActualizarVallaVisibleConCollider(
            contenedor.transform,
            "Valla_Limite_Frente",
            new Vector3(centroX, 0.775f, maxZ),
            new Vector3(anchoX + 0.12f, 2.17f, 0.09f),
            alturaCollider,
            fenceRenderer != null ? fenceRenderer.sharedMaterials : null);

        // 2. Colliders físicos alineados exactamente con las vallas reales existentes de los lados y del patio trasero
        CrearOActualizarMuroCollider(contenedor.transform, "Valla_Collider_Izquierda",
            new Vector3(minX, centroY, centroZ),
            new Vector3(grosor, alturaCollider, largoZ + 0.5f));

        CrearOActualizarMuroCollider(contenedor.transform, "Valla_Collider_Derecha",
            new Vector3(maxX, centroY, centroZ),
            new Vector3(grosor, alturaCollider, largoZ + 0.5f));

        CrearOActualizarMuroCollider(contenedor.transform, "Valla_Collider_Atras",
            new Vector3(centroX, centroY, minZ),
            new Vector3(anchoX + 0.5f, alturaCollider, grosor));
    }

    private static void CrearOActualizarVallaVisibleConCollider(
        Transform padre,
        string nombre,
        Vector3 posicion,
        Vector3 tamanoMalla,
        float alturaCollider,
        Material[] materialesValla)
    {
        Transform hijo = padre.Find(nombre);
        if (hijo == null)
        {
            GameObject obj = new GameObject(nombre);
            obj.layer = 0;
            hijo = obj.transform;
            hijo.SetParent(padre, false);
        }

        hijo.position = posicion;
        hijo.rotation = Quaternion.identity;
        hijo.localScale = Vector3.one;

        MeshFilter mf = hijo.GetComponent<MeshFilter>();
        if (mf == null) mf = hijo.gameObject.AddComponent<MeshFilter>();

        MeshRenderer mr = hijo.GetComponent<MeshRenderer>();
        if (mr == null) mr = hijo.gameObject.AddComponent<MeshRenderer>();

        mf.sharedMesh = CrearMallaVallafrontal(tamanoMalla.x, tamanoMalla.y, tamanoMalla.z);

        if (materialesValla != null && materialesValla.Length > 0)
        {
            mr.sharedMaterials = new Material[] { materialesValla[0] };
        }
        else
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material matMadera = new Material(sh);
            matMadera.SetColor("_BaseColor", new Color(0.28f, 0.20f, 0.14f, 1f));
            matMadera.color = new Color(0.28f, 0.20f, 0.14f, 1f);
            mr.sharedMaterial = matMadera;
        }

        BoxCollider bc = hijo.GetComponent<BoxCollider>();
        if (bc == null) bc = hijo.gameObject.AddComponent<BoxCollider>();
        bc.isTrigger = false;
        bc.center = new Vector3(0f, (alturaCollider - tamanoMalla.y) * 0.5f, 0f);
        bc.size = new Vector3(tamanoMalla.x, alturaCollider, Mathf.Max(tamanoMalla.z, 0.35f));
        bc.enabled = true;
    }

    private static Mesh CrearMallaVallafrontal(float anchoX, float altoY, float grosorZ)
    {
        float hx = anchoX * 0.5f;
        float hy = altoY * 0.5f;
        float hz = grosorZ * 0.5f;

        // Repetición UV horizontal proporcional al largo de la valla para que los tablones se vean idénticos a Fence_003
        float repeticionU = Mathf.Max(1f, anchoX / 2.2f);

        Vector3[] vertices = new Vector3[]
        {
            // Cara trasera (-Z, mirando hacia la casa)
            new Vector3(-hx, -hy, -hz), new Vector3( hx, -hy, -hz), new Vector3( hx,  hy, -hz), new Vector3(-hx,  hy, -hz),
            // Cara delantera (+Z, mirando hacia la calle)
            new Vector3( hx, -hy,  hz), new Vector3(-hx, -hy,  hz), new Vector3(-hx,  hy,  hz), new Vector3( hx,  hy,  hz),
            // Cara superior (+Y)
            new Vector3(-hx,  hy, -hz), new Vector3( hx,  hy, -hz), new Vector3( hx,  hy,  hz), new Vector3(-hx,  hy,  hz),
            // Lateral izquierdo (-X)
            new Vector3(-hx, -hy,  hz), new Vector3(-hx, -hy, -hz), new Vector3(-hx,  hy, -hz), new Vector3(-hx,  hy,  hz),
            // Lateral derecho (+X)
            new Vector3( hx, -hy, -hz), new Vector3( hx, -hy,  hz), new Vector3( hx,  hy,  hz), new Vector3( hx,  hy, -hz)
        };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0f, 0f), new Vector2(repeticionU, 0f), new Vector2(repeticionU, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(repeticionU, 0f), new Vector2(repeticionU, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(repeticionU, 0f), new Vector2(repeticionU, 0.1f), new Vector2(0f, 0.1f),
            new Vector2(0f, 0f), new Vector2(0.1f, 0f), new Vector2(0.1f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(0.1f, 0f), new Vector2(0.1f, 1f), new Vector2(0f, 1f)
        };

        int[] tris = new int[]
        {
            0, 3, 2,  0, 2, 1,
            4, 7, 6,  4, 6, 5,
            8, 11, 10, 8, 10, 9,
            12, 15, 14, 12, 14, 13,
            16, 19, 18, 16, 18, 17
        };

        Mesh m = new Mesh { name = "Malla_Valla_Frente_VR" };
        m.vertices = vertices;
        m.uv = uvs;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static void CrearOActualizarMuroCollider(Transform padre, string nombre, Vector3 posicion, Vector3 tamano)
    {
        Transform hijo = padre.Find(nombre);
        if (hijo == null)
        {
            GameObject obj = new GameObject(nombre);
            obj.layer = 0;
            hijo = obj.transform;
            hijo.SetParent(padre, false);
        }

        hijo.position = posicion;
        hijo.rotation = Quaternion.identity;
        hijo.localScale = Vector3.one;

        BoxCollider bc = hijo.GetComponent<BoxCollider>();
        if (bc == null)
            bc = hijo.gameObject.AddComponent<BoxCollider>();

        bc.isTrigger = false;
        bc.center = Vector3.zero;
        bc.size = tamano;
        bc.enabled = true;
    }

    /// <summary>
    /// Agrega colliders sólidos (positivos y compatibles con CharacterController en Meta Quest 2)
    /// a todos los muebles grandes de la casa (sofás, camas, mesas, encimeras, electrodomésticos,
    /// armarios, sanitarios, etc.) que no están cubiertos por House_Colliders.
    /// </summary>
    public static int AsegurarCollidersMueblesTangibles()
    {
        GameObject house = GameObject.Find("House");
        if (house == null) return 0;

        int mueblesProcesados = 0;

        foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(t)) continue;

            string n = t.name.ToLowerInvariant();
            if (n.Contains("hitbox_vr") || n.Contains("collider_mueble") || n.Contains("clonmimic") || n.Contains("espejo_"))
                continue;

            if (!EsMuebleSolidoTangible(n))
                continue;

            MeshRenderer mr = t.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled) continue;

            Bounds b = mr.bounds;
            if (b.size.x < 0.22f || b.size.y < 0.20f || b.size.z < 0.22f)
                continue;

            // Si ya tiene un MeshCollider sólido válido (p.ej. horneado en Editor para sofás/cocina en L), respetarlo
            MeshCollider mcExistente = t.GetComponent<MeshCollider>();
            if (mcExistente != null && mcExistente.enabled && !mcExistente.isTrigger && mcExistente.sharedMesh != null)
            {
                mueblesProcesados++;
                continue;
            }

            // Para sofás en L (Armchair) o encimeras de cocina en L (Kitchen), usar MeshCollider en Editor
            // (o si la malla es legible) para no bloquear pasillos ni mesas de centro dentro de la "L".
            bool esFormaEnL = n.StartsWith("armchair") || n.StartsWith("kitchen");
            MeshFilter mf = t.GetComponent<MeshFilter>();
            Vector3 ls = t.lossyScale;
            bool escalaPositiva = ls.x > 0.0001f && ls.y > 0.0001f && ls.z > 0.0001f;

            if (esFormaEnL && mf != null && mf.sharedMesh != null && escalaPositiva && (!Application.isPlaying || mf.sharedMesh.isReadable))
            {
                // Quitar BoxCollider hijo previo si existía
                Transform hijoViejo = t.Find("Collider_Mueble_Solido");
                if (hijoViejo != null)
                {
                    if (Application.isPlaying) Object.Destroy(hijoViejo.gameObject);
                    else Object.DestroyImmediate(hijoViejo.gameObject);
                }

                MeshCollider mc = t.GetComponent<MeshCollider>();
                if (mc == null) mc = t.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                mc.isTrigger = false;
                mc.enabled = true;
                mueblesProcesados++;
                continue;
            }

            // Para el resto de muebles sólidos (camas, mesas, sillones, refrigerador, estufa, lavadoras,
            // cómodas, roperos, muebles de baño, inodoros, tina, sillas, estantes), crear un BoxCollider hijo
            // con escala positiva compensada (100% inmune a escalas negativas del FBX y ultra ligero en Quest 2).
            Transform hijoCol = t.Find("Collider_Mueble_Solido");
            if (hijoCol == null)
            {
                GameObject colObj = new GameObject("Collider_Mueble_Solido");
                colObj.layer = 0; // Default (colisiona con CharacterController)
                hijoCol = colObj.transform;
                hijoCol.SetParent(t, false);
            }

            hijoCol.position = b.center;
            hijoCol.rotation = Quaternion.identity;

            float sx = Mathf.Abs(ls.x) > 0.0001f ? ls.x : 1f;
            float sy = Mathf.Abs(ls.y) > 0.0001f ? ls.y : 1f;
            float sz = Mathf.Abs(ls.z) > 0.0001f ? ls.z : 1f;
            hijoCol.localScale = new Vector3(1f / sx, 1f / sy, 1f / sz);

            BoxCollider bc = hijoCol.GetComponent<BoxCollider>();
            if (bc == null) bc = hijoCol.gameObject.AddComponent<BoxCollider>();

            // Ligero margen interno en XZ para no atorar al jugador en pasillos angostos ni tapar puertas/cajones del mismo mueble
            float margenXZ = n.StartsWith("chair") ? 0.05f : 0.03f;
            Vector3 tamano = new Vector3(
                Mathf.Max(0.18f, b.size.x - margenXZ * 2f),
                Mathf.Max(0.20f, b.size.y - 0.02f),
                Mathf.Max(0.18f, b.size.z - margenXZ * 2f)
            );

            bc.isTrigger = false;
            bc.center = Vector3.zero;
            bc.size = tamano;
            bc.enabled = true;
            mueblesProcesados++;
        }

        return mueblesProcesados;
    }

    private static bool EsMuebleSolidoTangible(string n)
    {
        // Excluir partes móviles / puertas / cajones / tapas / manijas / accesorios pequeños
        if (n.Contains("door") || n.Contains("drawer") || n.Contains("manija") ||
            n.Contains("tapa") || n.Contains("paper") || n.Contains("lamp") ||
            n.Contains("frame") || n.Contains("switch") || n.Contains("focus"))
            return false;

        // Sofás y sillones
        if (n.StartsWith("armchair") || n.StartsWith("sofa") || n.StartsWith("couch"))
            return true;

        // Camas, mesas y sillas
        if (n.StartsWith("bed_") || n == "bed" ||
            n.StartsWith("table") || n.StartsWith("flower_table") || n.StartsWith("tool_table") ||
            n.StartsWith("chair_"))
            return true;

        // Muebles grandes de almacenamiento (cómodas, roperos, alacenas, estanterías)
        if (n.StartsWith("wardrobe") || n.StartsWith("chest_drawers") ||
            n.StartsWith("cupboard") || n.StartsWith("shelving") ||
            n.StartsWith("tool_cabinet") || n.StartsWith("bathroom_cabinet"))
            return true;

        // Cocina y electrodomésticos pesados
        if (n == "kitchen" || n.StartsWith("kitchen_") ||
            n.StartsWith("refrigerator") || n == "stove" ||
            n == "washing_machine" || n == "washing_machine_02")
            return true;

        // Baño (tina, lavabos, inodoros, mamparas fijas de regadera, cestos de ropa)
        if (n == "bathtub" || n.StartsWith("washbasin_0") ||
            n.StartsWith("toilet_01") || n.StartsWith("glass_shower") ||
            n == "basket" || n == "basket_01")
            return true;

        // Garaje (podadora)
        if (n == "mower")
            return true;

        return false;
    }
}

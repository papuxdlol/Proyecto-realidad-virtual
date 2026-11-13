using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class RepararPuertasVR : EditorWindow
{
    [MenuItem("Herramientas VR/6. RESTAURAR PUERTAS DE FÁBRICA Y ARREGLAR SWITCHES")]
    public static void RestaurarPuertasDeFabrica()
    {
        // 1. Buscar TODOS los objetos de la escena (sin importar si están dentro o fuera de House)
        Transform[] todosEnEscena = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int bisagrasEliminadas = 0;
        int puertasRestauradas = 0;

        // Primero sacamos cualquier puerta que siga atrapada dentro de un objeto "Bisagra_"
        foreach (Transform t in todosEnEscena)
        {
            if (t == null) continue;
            if (t.name.StartsWith("Bisagra_"))
            {
                Transform padreOriginal = t.parent;
                for (int i = t.childCount - 1; i >= 0; i--)
                {
                    Transform hijo = t.GetChild(i);
                    Undo.SetTransformParent(hijo, padreOriginal, "Liberar Puerta");
                }
                Undo.DestroyObjectImmediate(t.gameObject);
                bisagrasEliminadas++;
            }
        }

        // 2. Buscar el archivo FBX/Prefab original en la carpeta Assets para leer las posiciones exactas de fábrica
        string[] guidsModelos = AssetDatabase.FindAssets("t:GameObject cuidad");
        if (guidsModelos.Length == 0) guidsModelos = AssetDatabase.FindAssets("t:Model");

        Dictionary<string, Transform> mapaOriginal = new Dictionary<string, Transform>();
        foreach (string guid in guidsModelos)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset != null)
            {
                foreach (Transform orig in prefabAsset.GetComponentsInChildren<Transform>(true))
                {
                    if (!mapaOriginal.ContainsKey(orig.name))
                        mapaOriginal[orig.name] = orig;
                }
            }
        }

        // 3. Devolver cada puerta y switch a su posición, rotación y escala exactas del archivo original
        todosEnEscena = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in todosEnEscena)
        {
            if (t == null || t.gameObject.scene.name == null) continue;
            string n = t.name.ToLower();

            bool esPuerta = (n.StartsWith("door") || n.StartsWith("closet_door") || n.StartsWith("refrigerator_door")) && !n.Contains("frame");
            bool esSwitch = n.Contains("switch");

            if (esPuerta || esSwitch)
            {
                Undo.RecordObject(t, "Restaurar Transform Original");

                // Si es parte de un Prefab, revertimos sus overrides de Transform
                PrefabUtility.RevertObjectOverride(t, InteractionMode.AutomatedAction);

                // Y si tenemos su gemelo en el modelo original, copiamos sus coordenadas exactas
                if (mapaOriginal.TryGetValue(t.name, out Transform origTransform))
                {
                    t.localPosition = origTransform.localPosition;
                    t.localRotation = origTransform.localRotation;
                    t.localScale = origTransform.localScale;
                }

                // Asegurar que esté activo y visible
                t.gameObject.SetActive(true);
                MeshRenderer mr = t.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = true;

                // En vez de poner el BoxCollider en el objeto con escala -95, creamos un hijo limpio con escala Positiva
                Transform hitboxAntiguo = t.Find("Hitbox_VR");
                if (hitboxAntiguo != null) Undo.DestroyObjectImmediate(hitboxAntiguo.gameObject);

                // Quitamos colliders invertidos del objeto con escala negativa
                BoxCollider colViejo = t.GetComponent<BoxCollider>();
                if (colViejo != null) Undo.DestroyObjectImmediate(colViejo);

                if (mr != null)
                {
                    GameObject hitbox = new GameObject("Hitbox_VR");
                    Undo.RegisterCreatedObjectUndo(hitbox, "Crear Hitbox Positivo");
                    hitbox.transform.position = mr.bounds.center;
                    hitbox.transform.rotation = Quaternion.identity;
                    hitbox.transform.localScale = Vector3.one; // Escala 1,1,1 SIEMPRE POSITIVA
                    hitbox.transform.SetParent(t, true);

                    // Forzar escala global positiva aunque el padre sea -95
                    Vector3 ls = hitbox.transform.localScale;
                    hitbox.transform.localScale = new Vector3(-Mathf.Abs(ls.x), -Mathf.Abs(ls.y), -Mathf.Abs(ls.z));

                    BoxCollider bc = hitbox.AddComponent<BoxCollider>();
                    // Ajustar tamaño mundial real
                    bc.size = new Vector3(
                        mr.bounds.size.x / Mathf.Abs(hitbox.transform.lossyScale.x),
                        mr.bounds.size.y / Mathf.Abs(hitbox.transform.lossyScale.y),
                        mr.bounds.size.z / Mathf.Abs(hitbox.transform.lossyScale.z)
                    );

                    if (esSwitch) bc.size *= 3f; // Hacer el área de clic del switch 3 veces más grande
                }

                if (esPuerta) puertasRestauradas++;
            }
        }

        EditorUtility.DisplayDialog("¡Restauración Completada!",
            $"• Se eliminaron {bisagrasEliminadas} bisagras rotas.\n" +
            $"• Se restauraron {puertasRestauradas} puertas a su posición original.\n" +
            $"• Se crearon Hitboxes positivos para puertas y switches.",
            "¡Listo!");
    }
}
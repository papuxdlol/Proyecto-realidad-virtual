using System.Collections.Generic;
using UnityEngine;

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
    private MeshRenderer miRenderer;

    void Awake()
    {
        posOriginal = transform.localPosition;
        rotOriginal = transform.localRotation;
        miRenderer = GetComponent<MeshRenderer>();

        // Preparamos y ocultamos los clones/monstruos de todas las variantes al iniciar
        foreach (var v in posiblesVariantes)
        {
            if (v.objetoExtraActivable != null)
                v.objetoExtraActivable.SetActive(false);

            if (v.rendererCuadroOTV != null)
                v.materialOriginalGuardado = v.rendererCuadroOTV.sharedMaterial;
        }
    }

    public void ActivarAnomalia()
    {
        if (estaActiva || posiblesVariantes.Count == 0) return;

        // Elegimos al azar cuál de sus variantes se va a ejecutar esta vez (ej. Mimic o Displacement)
        int indiceAzar = Random.Range(0, posiblesVariantes.Count);
        varianteActual = posiblesVariantes[indiceAzar];
        tipoActivoActual = varianteActual.tipo;
        estaActiva = true;

        // 1. ¿Desaparece el objeto original? (Displacement)
        if (varianteActual.desapareceElObjeto && miRenderer != null)
            miRenderer.enabled = false;

        // 2. ¿Se mueve o rota? (Displacement)
        if (varianteActual.moverPosicion != Vector3.zero || varianteActual.girarRotacion != Vector3.zero)
        {
            transform.localPosition = posOriginal + varianteActual.moverPosicion;
            transform.localRotation = rotOriginal * Quaternion.Euler(varianteActual.girarRotacion);
        }

        // 3. ¿Aparece un clon o entidad? (Mimic, Intruder, Tulpa, Corpse)
        if (varianteActual.objetoExtraActivable != null)
            varianteActual.objetoExtraActivable.SetActive(true);

        // 4. ¿Cambia la textura de un cuadro o TV? (Imagery / Electrical)
        if (varianteActual.rendererCuadroOTV != null && varianteActual.materialAlterado != null)
            varianteActual.rendererCuadroOTV.material = varianteActual.materialAlterado;

        // 5. ¿Apaga interruptores? (Electrical)
        if (varianteActual.interruptoresAfectados != null)
        {
            foreach (var sw in varianteActual.interruptoresAfectados)
            {
                if (sw != null)
                {
                    sw.CambiarEstado(false);
                    sw.bloqueadaPorAnomalia = true;
                }
            }
        }

        // 6. ¿Suena algo?
        if (varianteActual.audioEvento != null)
        {
            varianteActual.audioEvento.loop = varianteActual.sonidoEnBucle;
            varianteActual.audioEvento.Play();
        }

        // Dentro de ActivarAnomalia():
        if (varianteActual.puertaQueSeAbreSola != null)
            varianteActual.puertaQueSeAbreSola.AbrirPuerta();
    }

    public void RestaurarNormalidad()
    {
        if (!estaActiva || varianteActual == null) return;

        // Restaurar visibilidad, posición y rotación
        if (miRenderer != null) miRenderer.enabled = true;
        transform.localPosition = posOriginal;
        transform.localRotation = rotOriginal;

        // Ocultar clon o entidad
        if (varianteActual.objetoExtraActivable != null)
            varianteActual.objetoExtraActivable.SetActive(false);

        // Restaurar cuadro o TV
        if (varianteActual.rendererCuadroOTV != null && varianteActual.materialOriginalGuardado != null)
            varianteActual.rendererCuadroOTV.material = varianteActual.materialOriginalGuardado;

        // Desbloquear luces
        if (varianteActual.interruptoresAfectados != null)
        {
            foreach (var sw in varianteActual.interruptoresAfectados)
            {
                if (sw != null)
                {
                    sw.bloqueadaPorAnomalia = false;
                    sw.CambiarEstado(true);
                }
            }
        }

        // Apagar sonido
        if (varianteActual.audioEvento != null && varianteActual.audioEvento.isPlaying)
            varianteActual.audioEvento.Stop();

        estaActiva = false;
        varianteActual = null;

        // Dentro de RestaurarNormalidad():
        if (varianteActual.puertaQueSeAbreSola != null)
            varianteActual.puertaQueSeAbreSola.CerrarPuerta(false);
    }
}
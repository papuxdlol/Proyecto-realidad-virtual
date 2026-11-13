using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PuertaAutoCierre : MonoBehaviour
{
    [Header("Configuración de Giro")]
    public float anguloAbierta = 90f;
    public float velocidadGiro = 180f;
    public float segundosParaCerrarse = 5f;

    [Header("Sonidos")]
    public AudioSource audioPuerta;
    public AudioClip sonidoAbrir;
    public AudioClip sonidoCerrar;
    public AudioClip sonidoAzotar;

    [HideInInspector] public bool estaAbierta = false;

    private Vector3 puntoBisagraMundial;
    private float anguloActual = 0f;
    private Coroutine rutinaAutoCierre;
    private XRSimpleInteractable interactable;

    void Start()
    {
        // Calculamos la orilla exacta de la puerta en el mundo real para girar desde ahí
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Bounds b = mr.bounds;
            // Tomamos el borde lateral más ancho como bisagra
            if (b.extents.x > b.extents.z)
                puntoBisagraMundial = new Vector3(b.min.x, b.center.y, b.center.z);
            else
                puntoBisagraMundial = new Vector3(b.center.x, b.center.y, b.min.z);
        }
        else
        {
            puntoBisagraMundial = transform.position;
        }

        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(_ => AlternarPuerta());
            interactable.activated.AddListener(_ => AlternarPuerta());
            // También permite abrirla solo con apuntar y presionar gatillo UI
            interactable.hoverEntered.AddListener(_ => RevisarClickSimulador());
        }
    }

    private void RevisarClickSimulador() { }

    public void AlternarPuerta()
    {
        if (!estaAbierta) AbrirPuerta();
        else CerrarPuerta(false);
    }

    public void AbrirPuerta()
    {
        if (estaAbierta) return;
        estaAbierta = true;
        if (audioPuerta && sonidoAbrir) audioPuerta.PlayOneShot(sonidoAbrir);

        if (rutinaAutoCierre != null) StopCoroutine(rutinaAutoCierre);
        rutinaAutoCierre = StartCoroutine(TemporizadorCierre());
    }

    public void CerrarPuerta(bool esAzoteAnomalia)
    {
        if (!estaAbierta) return;
        estaAbierta = false;
        if (rutinaAutoCierre != null) StopCoroutine(rutinaAutoCierre);

        if (audioPuerta)
        {
            AudioClip clip = (esAzoteAnomalia && sonidoAzotar != null) ? sonidoAzotar : sonidoCerrar;
            if (clip != null) audioPuerta.PlayOneShot(clip);
        }
    }

    private IEnumerator TemporizadorCierre()
    {
        yield return new WaitForSeconds(segundosParaCerrarse);
        CerrarPuerta(false);
    }

    void Update()
    {
        float objetivo = estaAbierta ? anguloAbierta : 0f;
        if (Mathf.Abs(anguloActual - objetivo) > 0.1f)
        {
            float nuevoAngulo = Mathf.MoveTowards(anguloActual, objetivo, velocidadGiro * Time.deltaTime);
            float delta = nuevoAngulo - anguloActual;
            transform.RotateAround(puntoBisagraMundial, Vector3.up, delta);
            anguloActual = nuevoAngulo;
        }
    }
}
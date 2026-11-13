using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable))]
public class InterruptorLuz : MonoBehaviour
{
    [Header("Conexiones")]
    public Light luzDelCuarto;
    public AudioSource sonidoClick;

    [Header("Estado")]
    public bool estaEncendida = false;
    public bool bloqueadaPorAnomalia = false;

    private XRSimpleInteractable interactable;
    private bool rayoEncima = false;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (luzDelCuarto != null)
            luzDelCuarto.enabled = estaEncendida;
    }

    void OnEnable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelect);
            interactable.activated.AddListener(OnActivate);
            interactable.hoverEntered.AddListener(_ => rayoEncima = true);
            interactable.hoverExited.AddListener(_ => rayoEncima = false);
        }
    }

    void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelect);
            interactable.activated.RemoveListener(OnActivate);
        }
    }

    void Update()
    {
        // Si el láser VR está apuntando al switch y presionas Clic Izquierdo o tecla E/G en el Simulador, ¡prende!
        if (rayoEncima && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.E)))
        {
            ProcesarInteraccion(null);
        }
    }

    // También permite probar dándole clic directo con el mouse en la ventana Game
    void OnMouseDown()
    {
        ProcesarInteraccion(null);
    }

    private void OnSelect(SelectEnterEventArgs args) => ProcesarInteraccion(args.interactorObject);
    private void OnActivate(ActivateEventArgs args) => ProcesarInteraccion(args.interactorObject);

    private void ProcesarInteraccion(IXRInteractor interactor)
    {
        if (sonidoClick != null) sonidoClick.Play();

        if (interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.4f, 0.1f);

        if (bloqueadaPorAnomalia) return;

        CambiarEstado(!estaEncendida);
    }

    public void CambiarEstado(bool nuevoEstado)
    {
        estaEncendida = nuevoEstado;
        if (luzDelCuarto != null)
        {
            luzDelCuarto.gameObject.SetActive(true);
            luzDelCuarto.enabled = estaEncendida;
        }
    }

    public void AlternarDesdeRayo()
    {
        if (sonidoClick != null) sonidoClick.Play();
        if (bloqueadaPorAnomalia) return;

        // Si por alguna razón no tenía luz asignada, busca la Point Light más cercana en ese instante
        if (luzDelCuarto == null)
        {
            Light[] luces = FindObjectsByType<Light>(FindObjectsSortMode.None);
            float menorDist = Mathf.Infinity;
            foreach (Light l in luces)
            {
                if (l.type != LightType.Point) continue;
                float d = Vector3.Distance(transform.position, l.transform.position);
                if (d < menorDist) { menorDist = d; luzDelCuarto = l; }
            }
        }

        CambiarEstado(!estaEncendida);
        Debug.Log($"[SWITCH ACTIVADO] {name} -> Luz: {(luzDelCuarto != null ? luzDelCuarto.name : "Ninguna")} = {estaEncendida}");
    }
}
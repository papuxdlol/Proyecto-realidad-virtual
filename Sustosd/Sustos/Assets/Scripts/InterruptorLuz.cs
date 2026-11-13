using UnityEngine;
using Keyboard = UnityEngine.InputSystem.Keyboard;
using Mouse = UnityEngine.InputSystem.Mouse;
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
    private int ultimaInteraccionFrame = -1;

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
        Mouse mouse = Mouse.current;
        Keyboard teclado = Keyboard.current;
        bool entradaManual =
            (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
            (teclado != null && (teclado.gKey.wasPressedThisFrame || teclado.eKey.wasPressedThisFrame));

        if (rayoEncima && entradaManual)
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
        if (ultimaInteraccionFrame == Time.frameCount) return;
        ultimaInteraccionFrame = Time.frameCount;

        if (sonidoClick != null) sonidoClick.Play();

        if (interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.4f, 0.1f);

        if (bloqueadaPorAnomalia) return;

        bool estadoActual = luzDelCuarto != null ? luzDelCuarto.enabled : estaEncendida;
        CambiarEstado(!estadoActual);
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
        if (luzDelCuarto == null)
        {
            Light[] luces = FindObjectsByType<Light>(FindObjectsSortMode.None);
            float menorDist = Mathf.Infinity;
            foreach (Light luz in luces)
            {
                if (luz.type != LightType.Point) continue;

                float distancia = Vector3.Distance(transform.position, luz.transform.position);
                if (distancia < menorDist)
                {
                    menorDist = distancia;
                    luzDelCuarto = luz;
                }
            }
        }

        ProcesarInteraccion(null);
    }
}
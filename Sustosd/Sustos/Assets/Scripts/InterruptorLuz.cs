using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class InterruptorLuz : MonoBehaviour
{
    [Header("Conexiones")]
    public Light luzDelCuarto;
    public AudioSource sonidoClick;

    [Header("Estado")]
    public bool estaEncendida = false;
    public bool bloqueadaPorAnomalia = false; // Para anomalía "Electrical"

    private XRSimpleInteractable interactable;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (luzDelCuarto != null)
            luzDelCuarto.enabled = estaEncendida;
    }

    void OnEnable()
    {
        if (interactable != null)
            interactable.selectEntered.AddListener(AlPresionarSwitch);
    }

    void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(AlPresionarSwitch);
    }

    public void AlPresionarSwitch(SelectEnterEventArgs args)
    {
        if (sonidoClick != null) sonidoClick.Play();

        // Si hay una anomalía eléctrica activa, el switch no obedece
        if (bloqueadaPorAnomalia) return;

        CambiarEstado(!estaEncendida);
    }

    public void CambiarEstado(bool nuevoEstado)
    {
        estaEncendida = nuevoEstado;
        if (luzDelCuarto != null)
            luzDelCuarto.enabled = estaEncendida;
    }
}

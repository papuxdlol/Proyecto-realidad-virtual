using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(ObjetoAnomalia), typeof(XRSimpleInteractable))]
public class CajaFusiblesVR : MonoBehaviour
{
    public Transform palancaVisual;
    public Light luzIndicadora;
    public AudioSource sonidoPalanca;

    private ObjetoAnomalia anomaliaApagon;
    private XRSimpleInteractable interactable;

    void Awake()
    {
        anomaliaApagon = GetComponent<ObjetoAnomalia>();
        interactable = GetComponent<XRSimpleInteractable>();
    }

    void OnEnable()
    {
        interactable.selectEntered.AddListener(AlTirarPalanca);
        interactable.activated.AddListener(AlActivarPalanca);
    }

    void OnDisable()
    {
        interactable.selectEntered.RemoveListener(AlTirarPalanca);
        interactable.activated.RemoveListener(AlActivarPalanca);
    }

    void Update()
    {
        // Si el apagón está activo, la palanca se ve abajo y la luz indicadora se pone roja
        if (palancaVisual != null)
        {
            float anguloObjetivo = (anomaliaApagon != null && anomaliaApagon.estaActiva) ? 45f : -35f;
            palancaVisual.localRotation = Quaternion.Lerp(palancaVisual.localRotation, Quaternion.Euler(anguloObjetivo, 0, 0), Time.deltaTime * 8f);
        }

        if (luzIndicadora != null)
        {
            luzIndicadora.color = (anomaliaApagon != null && anomaliaApagon.estaActiva) ? Color.red : Color.green;
        }
    }

    private void AlTirarPalanca(SelectEnterEventArgs args) => RepararFusibles(args.interactorObject);
    private void AlActivarPalanca(ActivateEventArgs args) => RepararFusibles(args.interactorObject);

    public void RepararFusibles(IXRInteractor interactor)
    {
        if (sonidoPalanca != null) sonidoPalanca.Play();

        if (interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(0.7f, 0.2f);

        // Si había un apagón activo, al tocar la caja se restauran todas las luces de la casa
        if (anomaliaApagon != null && anomaliaApagon.estaActiva)
        {
            anomaliaApagon.RestaurarNormalidad();
            Debug.Log("¡Caja de fusibles reparada manualmente en el Garaje!");
        }
    }
}
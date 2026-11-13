using System.Collections.Generic;
using UnityEngine;
using Keyboard = UnityEngine.InputSystem.Keyboard;
using Mouse = UnityEngine.InputSystem.Mouse;
using UnityEngine.XR;

public class InteractorUniversalVR : MonoBehaviour
{
    [Header("Distancia máxima para tocar Switches, Puertas y Fusibles")]
    public float distanciaInteraccion = 4.5f;

    private InputDevice controlDerecho;
    private InputDevice controlIzquierdo;
    private bool gatilloDerechoPresionadoAntes;
    private bool gatilloIzquierdoPresionadoAntes;
    private bool gripDerechoPresionadoAntes;
    private bool gripIzquierdoPresionadoAntes;

    void Update()
    {
        bool clicDetectado = false;
        Mouse mouse = Mouse.current;
        Keyboard teclado = Keyboard.current;

        if ((mouse != null && mouse.leftButton.wasPressedThisFrame) ||
            (teclado != null && (teclado.eKey.wasPressedThisFrame || teclado.gKey.wasPressedThisFrame)))
        {
            clicDetectado = true;
        }

        // Detectar por separado las pulsaciones de gatillo y grip de cada control.
        if (!controlDerecho.isValid) controlDerecho = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!controlIzquierdo.isValid) controlIzquierdo = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool gatilloDerecho = false;
        bool gatilloIzquierdo = false;
        bool gripDerecho = false;
        bool gripIzquierdo = false;
        if (controlDerecho.isValid)
        {
            controlDerecho.TryGetFeatureValue(CommonUsages.triggerButton, out gatilloDerecho);
            controlDerecho.TryGetFeatureValue(CommonUsages.gripButton, out gripDerecho);
        }
        if (controlIzquierdo.isValid)
        {
            controlIzquierdo.TryGetFeatureValue(CommonUsages.triggerButton, out gatilloIzquierdo);
            controlIzquierdo.TryGetFeatureValue(CommonUsages.gripButton, out gripIzquierdo);
        }

        if ((gatilloDerecho && !gatilloDerechoPresionadoAntes) ||
            (gatilloIzquierdo && !gatilloIzquierdoPresionadoAntes) ||
            (gripDerecho && !gripDerechoPresionadoAntes) ||
            (gripIzquierdo && !gripIzquierdoPresionadoAntes))
            clicDetectado = true;

        gatilloDerechoPresionadoAntes = gatilloDerecho;
        gatilloIzquierdoPresionadoAntes = gatilloIzquierdo;
        gripDerechoPresionadoAntes = gripDerecho;
        gripIzquierdoPresionadoAntes = gripIzquierdo;

        if (clicDetectado)
        {
            IntentarInteractuar();
        }
    }

    private void IntentarInteractuar()
    {
        // Lanzamos rayos desde el control derecho, izquierdo y desde la mirada/mouse
        List<Ray> rayos = new List<Ray>();
        Mouse mouse = Mouse.current;

        GameObject manoDer = GameObject.Find("Right Controller");
        if (manoDer != null) rayos.Add(new Ray(manoDer.transform.position, manoDer.transform.forward));

        GameObject manoIzq = GameObject.Find("Left Controller");
        if (manoIzq != null) rayos.Add(new Ray(manoIzq.transform.position, manoIzq.transform.forward));

        if (Camera.main != null)
        {
            if (mouse != null)
                rayos.Add(Camera.main.ScreenPointToRay(mouse.position.ReadValue()));
            rayos.Add(new Ray(Camera.main.transform.position, Camera.main.transform.forward));
        }

        foreach (Ray r in rayos)
        {
            // RaycastAll atraviesa paredes invisibles de House_Colliders para encontrar el Switch, Puerta o Fusibles
            RaycastHit[] impactos = Physics.RaycastAll(r, distanciaInteraccion);
            foreach (RaycastHit hit in impactos)
            {
                // A) ¿Tocó un Switch de luz?
                InterruptorLuz sw = hit.collider.GetComponentInParent<InterruptorLuz>();
                if (sw != null)
                {
                    sw.AlternarDesdeRayo();
                    return;
                }

                // B) ¿Tocó una Puerta?
                PuertaAutoCierre puerta = hit.collider.GetComponentInParent<PuertaAutoCierre>();
                if (puerta != null)
                {
                    puerta.AlternarPuerta();
                    return;
                }

                // C) ¿Tocó la Caja de Fusibles?
                CajaFusiblesVR fusibles = hit.collider.GetComponentInParent<CajaFusiblesVR>();
                if (fusibles != null)
                {
                    fusibles.RepararFusibles(null);
                    return;
                }
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class InteractorUniversalVR : MonoBehaviour
{
    [Header("Distancia máxima para tocar Switches, Puertas y Fusibles")]
    public float distanciaInteraccion = 4.5f;

    private InputDevice controlDerecho;
    private InputDevice controlIzquierdo;
    private bool gatilloPresionadoAntes = false;

    void Update()
    {
        bool clicDetectado = false;

        // 1. Detectar Clic Izquierdo del Mouse o Tecla E / G (Para el Simulador en PC)
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.G))
        {
            clicDetectado = true;
        }

        // 2. Detectar Gatillo (Trigger) o Grip de los controles reales de Meta Quest 2
        if (!controlDerecho.isValid) controlDerecho = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!controlIzquierdo.isValid) controlIzquierdo = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool botonVR = false;
        if (controlDerecho.isValid)
        {
            controlDerecho.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigR);
            controlDerecho.TryGetFeatureValue(CommonUsages.gripButton, out bool gripR);
            botonVR |= (trigR || gripR);
        }
        if (controlIzquierdo.isValid)
        {
            controlIzquierdo.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigL);
            controlIzquierdo.TryGetFeatureValue(CommonUsages.gripButton, out bool gripL);
            botonVR |= (trigL || gripL);
        }

        if (botonVR && !gatilloPresionadoAntes)
            clicDetectado = true;

        gatilloPresionadoAntes = botonVR;

        if (clicDetectado)
        {
            IntentarInteractuar();
        }
    }

    private void IntentarInteractuar()
    {
        // Lanzamos rayos desde el control derecho, izquierdo y desde la mirada/mouse
        List<Ray> rayos = new List<Ray>();

        GameObject manoDer = GameObject.Find("Right Controller");
        if (manoDer != null) rayos.Add(new Ray(manoDer.transform.position, manoDer.transform.forward));

        GameObject manoIzq = GameObject.Find("Left Controller");
        if (manoIzq != null) rayos.Add(new Ray(manoIzq.transform.position, manoIzq.transform.forward));

        if (Camera.main != null)
        {
            rayos.Add(Camera.main.ScreenPointToRay(Input.mousePosition));
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
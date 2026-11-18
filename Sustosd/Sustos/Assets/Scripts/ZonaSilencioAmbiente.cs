using System.Collections.Generic;
using UnityEngine;

public sealed class ZonaSilencioAmbiente : MonoBehaviour
{
    [SerializeField] private AudioSource ambienteExterior;
    [SerializeField] private Transform raizJugador;

    private readonly HashSet<Collider> collidersDentro = new HashSet<Collider>();
    private Collider zona;
    private float volumenOriginal;
    private bool inicializado;

    private void Awake()
    {
        zona = GetComponent<Collider>();
        if (zona == null || !zona.isTrigger)
        {
            Debug.LogError("ZonaSilencioAmbiente necesita un Collider marcado como Is Trigger.", this);
            enabled = false;
            return;
        }

        if (ambienteExterior == null || raizJugador == null)
        {
            Debug.LogError("Asigna el AudioSource exterior y la raíz del jugador en ZonaSilencioAmbiente.", this);
            enabled = false;
            return;
        }

        volumenOriginal = ambienteExterior.volume;
        inicializado = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (EsColliderDelJugador(other))
        {
            collidersDentro.Add(other);
            ambienteExterior.volume = 0f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (collidersDentro.Remove(other) && collidersDentro.Count == 0)
        {
            ambienteExterior.volume = volumenOriginal;
        }
    }

    private void OnDisable()
    {
        if (inicializado && ambienteExterior != null)
        {
            ambienteExterior.volume = volumenOriginal;
        }

        collidersDentro.Clear();
    }

    private bool EsColliderDelJugador(Collider other)
    {
        return other.transform == raizJugador || other.transform.IsChildOf(raizJugador);
    }
}

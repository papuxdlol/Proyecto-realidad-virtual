using UnityEngine;

public class SonidoPisadasVR : MonoBehaviour
{
    public AudioSource audioPisadas;
    public AudioClip[] clipsPasos;
    public float distanciaEntrePasos = 1.8f;

    private Vector3 ultimaPosicion;
    private float distanciaRecorrida;

    void Start()
    {
        ultimaPosicion = transform.position;
    }

    void Update()
    {
        // Calculamos cuánto se movió en el plano horizontal (X, Z)
        Vector3 posActual = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 posAnterior = new Vector3(ultimaPosicion.x, 0, ultimaPosicion.z);

        distanciaRecorrida += Vector3.Distance(posActual, posAnterior);
        ultimaPosicion = transform.position;

        if (distanciaRecorrida >= distanciaEntrePasos)
        {
            distanciaRecorrida = 0f;
            if (clipsPasos.Length > 0 && audioPisadas != null)
            {
                AudioClip clip = clipsPasos[Random.Range(0, clipsPasos.Length)];
                audioPisadas.pitch = Random.Range(0.9f, 1.1f); // Variación natural
                audioPisadas.PlayOneShot(clip);
            }
        }
    }
}
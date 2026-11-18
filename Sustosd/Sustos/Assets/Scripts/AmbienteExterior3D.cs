using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class AmbienteExterior3D : MonoBehaviour
{
    [SerializeField] private AudioClip ambiente;
    [SerializeField] private float distanciaMinima = 8f;
    [SerializeField] private float distanciaMaxima = 60f;

    private void Start()
    {
        if (ambiente == null)
        {
            Debug.LogError("Asigna el clip de ambiente exterior.", this);
            enabled = false;
            return;
        }

        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.clip = ambiente;
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = distanciaMinima;
        audioSource.maxDistance = Mathf.Max(distanciaMinima, distanciaMaxima);
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.Play();
    }
}

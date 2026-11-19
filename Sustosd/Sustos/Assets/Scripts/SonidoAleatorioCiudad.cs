using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class SonidoAleatorioCiudad : MonoBehaviour
{
    private const float ProbabilidadPorSegundo = 0.005f;

    [SerializeField] private AudioClip tikiTikiTikti;
    [SerializeField] private AudioClip enrique;
    [SerializeField] private AudioClip cuffem;
    [SerializeField] private AudioClip fartExtremlyLoudMetal;

    private AudioSource audioSource;
    private AudioClip[] clips;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;

        clips = new[] { tikiTikiTikti, enrique, cuffem, fartExtremlyLoudMetal };
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
            {
                Debug.LogError("Asigna los cuatro clips de eventos aleatorios de la ciudad.", this);
                enabled = false;
                return;
            }
        }
    }

    private void OnEnable()
    {
        if (audioSource != null && clips != null && clips.Length == 4)
        {
            StartCoroutine(EvaluarEventos());
        }
    }

    private IEnumerator EvaluarEventos()
    {
        var espera = new WaitForSeconds(1f);
        while (true)
        {
            yield return espera;

            if (Random.value < ProbabilidadPorSegundo)
            {
                audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)]);
            }
        }
    }
}

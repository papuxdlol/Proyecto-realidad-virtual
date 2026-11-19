using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class SonidoAleatorioCiudad : MonoBehaviour
{
    private const float ProbabilidadPorSegundo = 0.005f;

    [SerializeField] private AudioClip tikiTikiTikti;
    [SerializeField] private AudioClip enrique;
    [SerializeField] private AudioClip cuffem;

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

        List<AudioClip> validos = new List<AudioClip>(3);
        if (tikiTikiTikti != null && !tikiTikiTikti.name.Contains("pipe")) validos.Add(tikiTikiTikti);
        if (enrique != null && !enrique.name.Contains("pipe")) validos.Add(enrique);
        if (cuffem != null && !cuffem.name.Contains("pipe")) validos.Add(cuffem);

        clips = validos.ToArray();
        if (clips.Length == 0)
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (audioSource != null && clips != null && clips.Length > 0)
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

            if (clips != null && clips.Length > 0 && Random.value < ProbabilidadPorSegundo)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    audioSource.PlayOneShot(clip, 0.45f);
                }
            }
        }
    }
}

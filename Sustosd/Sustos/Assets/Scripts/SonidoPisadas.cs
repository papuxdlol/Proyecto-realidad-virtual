using System.Collections.Generic;
using UnityEngine;

public class SonidoPisadasVR : MonoBehaviour
{
    public AudioSource audioPisadas;
    public AudioClip[] clipsPasos;
    public float distanciaEntrePasos = 0.62f;

    private readonly List<AudioClip> clipsRecortados = new List<AudioClip>();
    private Transform transformSeguimiento;
    private Vector3 ultimaPosicion;
    private float distanciaRecorrida;
    private bool estabaCaminando;
    private float tiempoSinMoverse;

    void Awake()
    {
        if (audioPisadas == null)
            audioPisadas = GetComponent<AudioSource>();

        if (audioPisadas != null)
        {
            audioPisadas.playOnAwake = false;
            audioPisadas.loop = false;
            // SpatialBlend bajo (0.15) para que los pasos se escuchen debajo del propio jugador y no como alguien detrás
            audioPisadas.spatialBlend = 0.15f;
            audioPisadas.volume = 0.72f;
        }

        // Forzar un intervalo natural de paso humano (~0.62m) si estaba en el valor antiguo de 1.8m
        if (distanciaEntrePasos > 0.95f || distanciaEntrePasos < 0.25f)
            distanciaEntrePasos = 0.62f;
    }

    void Start()
    {
        ActualizarTransformSeguimiento();
        ultimaPosicion = ObtenerPosicionHorizontal();
        PrepararClipsSinDelay();
    }

    private void ActualizarTransformSeguimiento()
    {
        if (Camera.main != null)
            transformSeguimiento = Camera.main.transform;
        else
            transformSeguimiento = transform;
    }

    private Vector3 ObtenerPosicionHorizontal()
    {
        if (transformSeguimiento == null)
            ActualizarTransformSeguimiento();

        Vector3 p = transformSeguimiento != null ? transformSeguimiento.position : transform.position;
        return new Vector3(p.x, 0f, p.z);
    }

    private void PrepararClipsSinDelay()
    {
        clipsRecortados.Clear();
        if (clipsPasos == null) return;

        for (int i = 0; i < clipsPasos.Length; i++)
        {
            AudioClip original = clipsPasos[i];
            if (original == null) continue;

            AudioClip recortado = ExtraerPasoIndividualSinSilencio(original, i);
            if (recortado != null)
                clipsRecortados.Add(recortado);
        }

        if (clipsRecortados.Count == 0)
        {
            clipsRecortados.Add(CrearPasoMaderaProcedural("PasoMadera_1", 115f));
            clipsRecortados.Add(CrearPasoMaderaProcedural("PasoMadera_2", 132f));
        }
    }

    private static AudioClip ExtraerPasoIndividualSinSilencio(AudioClip clip, int indice)
    {
        if (clip.loadState != AudioDataLoadState.Loaded)
            clip.LoadAudioData();

        int channels = Mathf.Max(1, clip.channels);
        int freq = Mathf.Max(8000, clip.frequency);
        int totalFrames = clip.samples;
        if (totalFrames <= 0) return null;

        // Leer hasta los primeros 5 segundos para localizar el primer golpe real de pisada sin silencio inicial
        int framesLectura = Mathf.Min(totalFrames, freq * 5);
        float[] raw = new float[framesLectura * channels];
        if (!clip.GetData(raw, 0))
            return null;

        // Encontrar el pico máximo de energía para calibrar el umbral de ataque
        float maxPico = 0f;
        int framePico = 0;
        for (int f = 0; f < framesLectura; f++)
        {
            float abs = Mathf.Abs(raw[f * channels]);
            if (abs > maxPico)
            {
                maxPico = abs;
                framePico = f;
            }
        }

        if (maxPico < 0.01f)
            return null;

        // Buscar el inicio exacto del ataque (umbral = 22% del pico) justo antes del pico
        float umbralAtaque = maxPico * 0.22f;
        int inicioAtaque = framePico;
        int ventanaBusqueda = Mathf.Min(framePico, Mathf.RoundToInt(freq * 0.12f));
        for (int f = framePico - ventanaBusqueda; f <= framePico; f++)
        {
            if (Mathf.Abs(raw[f * channels]) >= umbralAtaque)
            {
                inicioAtaque = Mathf.Max(0, f - Mathf.RoundToInt(freq * 0.006f)); // 6ms de pre-ataque
                break;
            }
        }

        // Extraer exactamente 0.24 segundos de un solo paso limpio (sin cola de pasos extra detrás del jugador)
        int framesPaso = Mathf.Min(Mathf.RoundToInt(freq * 0.24f), framesLectura - inicioAtaque);
        if (framesPaso < Mathf.RoundToInt(freq * 0.06f))
            return null;

        float[] monoData = new float[framesPaso];
        float norm = 0.75f / maxPico;
        for (int f = 0; f < framesPaso; f++)
        {
            float sample = raw[(inicioAtaque + f) * channels] * norm;
            // Envolvente de salida suave para que nunca haga clic ni deje eco
            float progreso = (float)f / framesPaso;
            float fadeOut = progreso > 0.65f ? (1f - ((progreso - 0.65f) / 0.35f)) : 1f;
            monoData[f] = Mathf.Clamp(sample * fadeOut, -0.95f, 0.95f);
        }

        AudioClip pasoLimpio = AudioClip.Create($"Paso_ZeroLag_{indice}", framesPaso, 1, freq, false);
        pasoLimpio.SetData(monoData, 0);
        return pasoLimpio;
    }

    private static AudioClip CrearPasoMaderaProcedural(string nombre, float freqBase)
    {
        int sampleRate = 22050;
        int samples = Mathf.RoundToInt(sampleRate * 0.16f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-i / (samples * 0.22f));
            float thump = Mathf.Sin(2f * Mathf.PI * freqBase * Mathf.Exp(-t * 12f) * t) * 0.6f;
            float wood = (Random.value * 2f - 1f) * Mathf.Exp(-i / (samples * 0.09f)) * 0.35f;
            data[i] = (thump + wood) * env;
        }
        AudioClip clip = AudioClip.Create(nombre, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void Update()
    {
        if (Time.deltaTime <= 0.0001f) return;

        Vector3 posActual = ObtenerPosicionHorizontal();
        float deltaDist = Vector3.Distance(posActual, ultimaPosicion);
        ultimaPosicion = posActual;

        // Ignorar teletransportes bruscos (> 1.5m en un solo frame)
        if (deltaDist > 1.5f)
        {
            distanciaRecorrida = 0f;
            return;
        }

        float velocidad = deltaDist / Time.deltaTime;

        if (velocidad > 0.12f)
        {
            tiempoSinMoverse = 0f;

            // Si acaba de empezar a caminar desde el reposo, sonar el primer paso casi de inmediato (a los 8cm)
            if (!estabaCaminando)
            {
                estabaCaminando = true;
                distanciaRecorrida = distanciaEntrePasos - 0.08f;
            }

            distanciaRecorrida += deltaDist;

            if (distanciaRecorrida >= distanciaEntrePasos)
            {
                distanciaRecorrida = 0f;
                ReproducirPasoInmediato();
            }
        }
        else
        {
            tiempoSinMoverse += Time.deltaTime;
            if (tiempoSinMoverse > 0.08f)
            {
                if (estabaCaminando)
                {
                    estabaCaminando = false;
                    distanciaRecorrida = 0f;
                    // Cortar cualquier cola de sonido inmediatamente al detenerse
                    if (audioPisadas != null && audioPisadas.isPlaying)
                        audioPisadas.Stop();
                }
            }
        }
    }

    private void ReproducirPasoInmediato()
    {
        if (audioPisadas == null || clipsRecortados.Count == 0) return;

        AudioClip clip = clipsRecortados[Random.Range(0, clipsRecortados.Count)];
        audioPisadas.Stop();
        audioPisadas.clip = clip;
        audioPisadas.pitch = Random.Range(0.92f, 1.08f);
        audioPisadas.Play();
    }
}

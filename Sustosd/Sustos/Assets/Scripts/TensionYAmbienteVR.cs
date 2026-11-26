using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema de tensión psicológica y atmósfera VR (Fase 2):
/// - Idea 7b: Reacción de entidades humanoides al mirarlas fijamente (> 4 s de cerca) -> cierra puerta o apaga luz.
/// - Idea 8: Latido cardíaco + pulso háptico leve en Quest 2 cuando hay 3-4 anomalías activas.
/// - Idea 9: Interferencia / glitch sutil en la Tablet al acercarse (< 2.8 m) a una entidad o fallo eléctrico.
/// - Idea 10: Paranoia ambiental (1-2 sonidos inofensivos de la casa por noche, no reportables).
/// </summary>
public class TensionYAmbienteVR : MonoBehaviour
{
    public static TensionYAmbienteVR Instancia;

    [Header("Audios de Tensión y Paranoia Ambiental")]
    public AudioClip clipLatidoCorazon;
    public AudioClip clipVientoVentana;
    public AudioClip clipCrujidoMadera;
    public AudioClip clipGolpeMadera;

    [Header("Parámetros de Tensión")]
    public float segundosMirandoParaReaccion = 4.0f;
    public float distanciaMaximaMirada = 4.8f;
    public float distanciaInterferenciaTablet = 2.8f;
    public int maxFalsosPositivosPorNoche = 2;

    private AudioSource fuenteLatido;
    private AudioSource fuenteParanoia3D;
    private GameObject nodoAudioParanoia;

    private float temporizadorEscaneo = 0f;
    private float proximoLatido = 0f;
    private float proximoGlitchPermitido = 0f;

    // Seguimiento de mirada a entidades humanoides
    private ObjetoAnomalia entidadMiradaActual;
    private float tiempoMirandoEntidad = 0f;
    private readonly Dictionary<ObjetoAnomalia, float> cooldownPorEntidad = new Dictionary<ObjetoAnomalia, float>();

    // Paranoia ambiental ("falsos positivos" inofensivos)
    private int falsosPositivosEjecutados = 0;
    private float proximoFalsoPositivo = -1f;
    private Coroutine rutinaAudioParanoia;
    private Coroutine rutinaHoraMuerta3AM;

    // Bloque 2 (Ideas 8 y 9): Puerta cerrada que tiembla y puerta que se azota si tardas > 18s en cuarto con anomalía
    private float proximoTemblorPuertaPermitido = 0f;
    private string cuartoAnomaloEstanciaActual = "";
    private float tiempoEstanciaEnCuartoAnomalo = 0f;
    private float proximoAzotePorTardanzaPermitido = 0f;

    private PuertaAutoCierre[] cachePuertas;
    private InterruptorLuz[] cacheSwitches;
    private static AudioClip clipZumbido3AM;

    private static readonly Vector3[] PuntosParanoiaCasa = new Vector3[]
    {
        new Vector3(13.2f, 1.8f,  1.2f),  // Ventana Sala
        new Vector3(13.1f, 1.8f, -7.8f),  // Cocina
        new Vector3(17.5f, 1.8f, -7.8f),  // Lavandería
        new Vector3(12.2f, 4.9f, -0.5f),  // Recámara 1 (Piso 2)
        new Vector3(16.8f, 4.9f,  0.5f),  // Recámara 2 (Piso 2)
        new Vector3(18.8f, 4.9f, -6.5f)   // Baño Piso 2
    };

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(this);
            return;
        }
        Instancia = this;

        fuenteLatido = gameObject.AddComponent<AudioSource>();
        fuenteLatido.playOnAwake = false;
        fuenteLatido.spatialBlend = 0f; // 2D interno (latido del propio jugador)
        fuenteLatido.volume = 0.5f;

        nodoAudioParanoia = new GameObject("Emisor_Paranoia_Ambiental_VR");
        nodoAudioParanoia.transform.SetParent(transform, false);
        fuenteParanoia3D = nodoAudioParanoia.AddComponent<AudioSource>();
        fuenteParanoia3D.playOnAwake = false;
        fuenteParanoia3D.spatialBlend = 1f; // 100% 3D espacializado
        fuenteParanoia3D.rolloffMode = AudioRolloffMode.Linear;
        fuenteParanoia3D.minDistance = 1.5f;
        fuenteParanoia3D.maxDistance = 18f;

        if (clipLatidoCorazon == null)
            clipLatidoCorazon = CrearLatidoProcedural();
    }

    void Start()
    {
        cachePuertas = FindObjectsByType<PuertaAutoCierre>(FindObjectsInactive.Include);
        cacheSwitches = FindObjectsByType<InterruptorLuz>(FindObjectsInactive.Include);
        proximoFalsoPositivo = Time.time + Random.Range(48f, 88f);
    }

    void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }

    void Update()
    {
        if (VRPauseMenu.IsMenuOpen || FinalTurnoVR.EstaEnSecuenciaFinal) return;

        TabletSistemaVR tablet = TabletSistemaVR.Instancia;
        if (tablet == null || tablet.EstaEnInspeccion || tablet.HoraActual >= 6)
            return;

        int activas = tablet.ContarAnomaliasActivas();

        // 1. Idea 8: Latido + pulso háptico progresivo cuando hay 3 o más anomalías activas
        ActualizarLatidoYTensionCritica(activas, tablet);

        // 2. Escaneo ligero cada 0.15 s (cero carga en Meta Quest 2) para Ideas 7b y 9
        temporizadorEscaneo += Time.deltaTime;
        if (temporizadorEscaneo >= 0.15f)
        {
            float dt = temporizadorEscaneo;
            temporizadorEscaneo = 0f;
            EvaluarCercaniaYMiradaAAnomalias(tablet, dt);
        }

        // 3. Idea 10: Paranoia ambiental (1-2 falsos positivos inofensivos por noche)
        ActualizarParanoiaAmbiental(activas);
    }

    private void ActualizarLatidoYTensionCritica(int activas, TabletSistemaVR tablet)
    {
        if (activas < 3 || clipLatidoCorazon == null) return;

        if (Time.time >= proximoLatido)
        {
            bool nivelCritico = activas >= 4;
            float intervalo = nivelCritico ? 0.78f : 1.18f;
            float volumen = nivelCritico ? 0.62f : 0.36f;
            float haptica = nivelCritico ? 0.24f : 0.12f;

            proximoLatido = Time.time + intervalo;
            fuenteLatido.pitch = nivelCritico ? 1.12f : 0.96f;
            fuenteLatido.PlayOneShot(clipLatidoCorazon, volumen);
            tablet.EnviarHapticAmbosMandos(haptica, 0.08f);
        }
    }

    private void EvaluarCercaniaYMiradaAAnomalias(TabletSistemaVR tablet, float dt)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 posCam = cam.transform.position;
        Vector3 dirCam = cam.transform.forward;

        ObjetoAnomalia entidadVista = null;
        float mejorDistMirada = distanciaMaximaMirada;
        bool hayInterferenciaCercana = false;

        List<ObjetoAnomalia> lista = tablet.todasLasAnomalias;
        for (int i = 0; i < lista.Count; i++)
        {
            ObjetoAnomalia a = lista[i];
            if (a == null || !a.estaActiva) continue;

            Vector3 posAnom = a.ObtenerPosicionVisualActiva();
            // Mismo piso
            if (Mathf.Abs(posCam.y - posAnom.y) > 2.3f) continue;

            float dist = Vector3.Distance(posCam, posAnom);

            // Idea 9: Interferencia en la Tablet cerca de entidades o fallos eléctricos
            bool causaInterferencia =
                a.tipoActivoActual == TipoAnomalia.Flawed ||
                a.tipoActivoActual == TipoAnomalia.PureForm ||
                a.tipoActivoActual == TipoAnomalia.Preacher ||
                a.tipoActivoActual == TipoAnomalia.Tulpa ||
                a.tipoActivoActual == TipoAnomalia.Electrical;

            if (causaInterferencia && dist <= distanciaInterferenciaTablet)
                hayInterferenciaCercana = true;

            // Idea 7b: Mirar fijamente a una entidad humanoide de cerca
            if (TabletSistemaVR.EsEntidadHumanoide(a.tipoActivoActual) && dist <= mejorDistMirada)
            {
                Vector3 haciaEntidad = (posAnom - posCam).normalized;
                float angulo = Vector3.Angle(dirCam, haciaEntidad);
                if (angulo <= 34f && InteractorUniversalVR.TieneLineaDeVisionDirecta(posCam, posAnom, a.transform))
                {
                    mejorDistMirada = dist;
                    entidadVista = a;
                }
            }
        }

        // Disparar glitch breve en Tablet (con cooldown para no molestar ni bloquear botones)
        if (hayInterferenciaCercana && Time.time >= proximoGlitchPermitido)
        {
            proximoGlitchPermitido = Time.time + Random.Range(7.5f, 11.5f);
            tablet.ActivarGlitchVisual(0.45f);
        }

        // Acumular tiempo mirando a la entidad humanoide
        if (entidadVista != null)
        {
            if (entidadMiradaActual == entidadVista)
            {
                tiempoMirandoEntidad += dt;
                if (tiempoMirandoEntidad >= segundosMirandoParaReaccion)
                {
                    float ultimoDisparo;
                    if (!cooldownPorEntidad.TryGetValue(entidadVista, out ultimoDisparo) || Time.time - ultimoDisparo > 14f)
                    {
                        cooldownPorEntidad[entidadVista] = Time.time;
                        tiempoMirandoEntidad = 0f;
                        EjecutarReaccionEntidad(entidadVista, tablet);
                    }
                }
            }
            else
            {
                entidadMiradaActual = entidadVista;
                tiempoMirandoEntidad = dt;
            }
        }
        else
        {
            entidadMiradaActual = null;
            tiempoMirandoEntidad = Mathf.Max(0f, tiempoMirandoEntidad - dt * 1.5f);
        }

        // Nota: Se desactivó el movimiento/sonido autónomo de puertas cuando NO son anomalía de puerta,
        // para que escuchar abrirse una puerta SIEMPRE signifique una anomalía real reportable.
    }

    private void EvaluarPuertasVivasPorAnomalia(TabletSistemaVR tablet, Vector3 posCam, float dt)
    {
        if (cachePuertas == null || cachePuertas.Length == 0) return;

        string cuartoJugador = ObjetoAnomalia.NormalizarCuartoPorPosicion(posCam, "LivingRoom");
        ObjetoAnomalia anomaliaEnMismoCuarto = null;
        List<ObjetoAnomalia> lista = tablet.todasLasAnomalias;

        for (int i = 0; i < lista.Count; i++)
        {
            ObjetoAnomalia a = lista[i];
            if (a == null || !a.estaActiva) continue;

            if (string.Equals(a.nombreCuarto, cuartoJugador, System.StringComparison.OrdinalIgnoreCase))
                anomaliaEnMismoCuarto = a;

            // Idea 8: Si hay una entidad humanoide encerrada detrás de una puerta cerrada y el jugador pasa a < 2.55 m
            if (TabletSistemaVR.EsEntidadHumanoide(a.tipoActivoActual) && Time.time >= proximoTemblorPuertaPermitido)
            {
                Vector3 posEnt = a.ObtenerPosicionVisualActiva();
                if (Mathf.Abs(posCam.y - posEnt.y) > 2.2f) continue;

                PuertaAutoCierre pCerrada = BuscarPuertaMasCercana(posEnt, requiereAbierta: false, requiereCerrada: true, radioMax: 5.5f);
                if (pCerrada != null)
                {
                    float distJugadorAPuerta = Vector3.Distance(posCam, pCerrada.ObtenerPosicionMundial());
                    if (distJugadorAPuerta <= 2.55f)
                    {
                        proximoTemblorPuertaPermitido = Time.time + Random.Range(7.5f, 11.5f);
                        pCerrada.SacudirPuertaCerradaPorEntidad();
                        tablet.EnviarHapticAmbosMandos(0.24f, 0.14f);
                    }
                }
            }
        }

        // Idea 9: Si el jugador permanece > 18 s dentro de un cuarto con anomalía activa sin reportarla y se acerca a la puerta abierta para salir, se azota sola
        if (anomaliaEnMismoCuarto != null)
        {
            if (string.Equals(cuartoAnomaloEstanciaActual, cuartoJugador, System.StringComparison.OrdinalIgnoreCase))
                tiempoEstanciaEnCuartoAnomalo += dt;
            else
            {
                cuartoAnomaloEstanciaActual = cuartoJugador;
                tiempoEstanciaEnCuartoAnomalo = dt;
            }

            if (tiempoEstanciaEnCuartoAnomalo >= 18.0f && Time.time >= proximoAzotePorTardanzaPermitido)
            {
                PuertaAutoCierre pAbierta = BuscarPuertaMasCercana(posCam, requiereAbierta: true, requiereCerrada: false, radioMax: 2.35f);
                if (pAbierta != null)
                {
                    proximoAzotePorTardanzaPermitido = Time.time + 22f;
                    tiempoEstanciaEnCuartoAnomalo = 0f;
                    pAbierta.CerrarPuerta(true);
                    tablet.EnviarHapticAmbosMandos(0.65f, 0.22f);
                }
            }
        }
        else
        {
            cuartoAnomaloEstanciaActual = "";
            tiempoEstanciaEnCuartoAnomalo = 0f;
        }
    }

    private PuertaAutoCierre BuscarPuertaMasCercana(Vector3 origen, bool requiereAbierta, bool requiereCerrada, float radioMax)
    {
        if (cachePuertas == null) return null;
        PuertaAutoCierre mejor = null;
        float menorDist = radioMax;

        for (int i = 0; i < cachePuertas.Length; i++)
        {
            PuertaAutoCierre p = cachePuertas[i];
            if (p == null) continue;
            if (requiereAbierta && !p.estaAbierta) continue;
            if (requiereCerrada && p.estaAbierta) continue;

            Vector3 posP = p.ObtenerPosicionMundial();
            if (Mathf.Abs(posP.y - origen.y) > 2.1f) continue;

            float d = Vector3.Distance(origen, posP);
            if (d < menorDist)
            {
                menorDist = d;
                mejor = p;
            }
        }
        return mejor;
    }

    /// <summary>
    /// Idea 6 (Bloque 2): Micro-evento de las 03:00 AM ("La Hora Muerta"):
    /// Caída general de tensión eléctrica en toda la casa durante 6.5 s (luces ámbar tenues + zumbido grave + pulso háptico frío).
    /// </summary>
    public void DispararEventoHoraMuerta3AM()
    {
        if (!InterruptorLuz.EnergiaGeneralCasaActiva) return;
        if (rutinaHoraMuerta3AM != null) StopCoroutine(rutinaHoraMuerta3AM);
        rutinaHoraMuerta3AM = StartCoroutine(RutinaEventoHoraMuerta3AM());
    }

    private IEnumerator RutinaEventoHoraMuerta3AM()
    {
        InterruptorLuz.EnEventoHoraMuerta3AM = true;

        if (clipZumbido3AM == null)
            clipZumbido3AM = CrearZumbidoCaidaVoltaje3AM();

        if (fuenteLatido != null && clipZumbido3AM != null)
            fuenteLatido.PlayOneShot(clipZumbido3AM, 0.55f);

        if (TabletSistemaVR.Instancia != null)
        {
            TabletSistemaVR.Instancia.EnviarHapticAmbosMandos(0.42f, 0.30f);
            TabletSistemaVR.Instancia.ActivarGlitchVisual(0.55f);
        }

        float duracion = 6.5f;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float p = t / duracion;
            // Curva de caída a ~25% con ligero temblor de filamento y recuperación gradual al final
            float baseCurva = p < 0.15f
                ? Mathf.Lerp(1.0f, 0.25f, p / 0.15f)
                : (p < 0.78f ? 0.25f : Mathf.Lerp(0.25f, 1.0f, (p - 0.78f) / 0.22f));
            float fluctuacion = Mathf.Sin(t * 24f) * 0.04f + Mathf.Cos(t * 11f) * 0.03f;
            InterruptorLuz.FactorVoltajeGlobal = Mathf.Clamp(baseCurva + fluctuacion, 0.18f, 1.0f);
            yield return null;
        }

        InterruptorLuz.FactorVoltajeGlobal = 1.0f;
        InterruptorLuz.EnEventoHoraMuerta3AM = false;

        if (cacheSwitches != null)
        {
            for (int i = 0; i < cacheSwitches.Length; i++)
            {
                if (cacheSwitches[i] != null)
                    cacheSwitches[i].RefrescarSegunEnergiaGeneral();
            }
        }
        rutinaHoraMuerta3AM = null;
    }

    private static AudioClip CrearZumbidoCaidaVoltaje3AM()
    {
        int sr = 22050;
        int n = Mathf.RoundToInt(sr * 3.2f);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Sin((t / 3.2f) * Mathf.PI);
            float f = Mathf.Lerp(92f, 54f, t / 3.2f);
            float hum = Mathf.Sin(2f * Mathf.PI * f * t) * 0.55f + Mathf.Sin(2f * Mathf.PI * (f * 2f) * t) * 0.25f;
            d[i] = hum * env * 0.65f;
        }
        AudioClip c = AudioClip.Create("Brownout3AMHum", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }

    private void EjecutarReaccionEntidad(ObjetoAnomalia entidad, TabletSistemaVR tablet)
    {
        Vector3 posEntidad = entidad.ObtenerPosicionVisualActiva();
        bool accionRealizada = false;

        // Al observar fijamente demasiado tiempo a una entidad humanoide, apagar la luz del cuarto más cercano (NUNCA mover puertas para no fingir anomalías de puerta)
        if (InterruptorLuz.EnergiaGeneralCasaActiva && cacheSwitches != null)
        {
            InterruptorLuz switchObjetivo = null;
            float mejorDistSw = 6.5f;
            for (int i = 0; i < cacheSwitches.Length; i++)
            {
                InterruptorLuz sw = cacheSwitches[i];
                if (sw == null || !sw.estaEncendida) continue;
                Vector3 posS = sw.ObtenerPosicionMundial();
                if (Mathf.Abs(posS.y - posEntidad.y) > 2.2f) continue;
                float d = Vector3.Distance(posS, posEntidad);
                if (d < mejorDistSw)
                {
                    mejorDistSw = d;
                    switchObjetivo = sw;
                }
            }

            if (switchObjetivo != null)
            {
                switchObjetivo.CambiarEstado(false);
                accionRealizada = true;
            }
        }

        if (accionRealizada)
        {
            tablet.EnviarHapticAmbosMandos(0.55f, 0.22f);
            tablet.ActivarGlitchVisual(0.40f);
            Debug.Log($"[Tensión VR] La entidad {entidad.tipoActivoActual} reaccionó tras ser observada demasiado tiempo.");
        }
    }

    private void ActualizarParanoiaAmbiental(int activas)
    {
        if (falsosPositivosEjecutados >= maxFalsosPositivosPorNoche) return;
        if (activas >= 3) return; // No competir con el latido crítico
        if (Time.time < proximoFalsoPositivo) return;

        falsosPositivosEjecutados++;
        proximoFalsoPositivo = Time.time + Random.Range(75f, 125f);

        Camera cam = Camera.main;
        Vector3 posCam = cam != null ? cam.transform.position : Vector3.zero;

        // Elegir un punto de la casa donde NO esté el jugador (> 4.5 m)
        Vector3 destino = PuntosParanoiaCasa[Random.Range(0, PuntosParanoiaCasa.Length)];
        for (int intentos = 0; intentos < 8; intentos++)
        {
            Vector3 candidato = PuntosParanoiaCasa[Random.Range(0, PuntosParanoiaCasa.Length)];
            if (Vector3.Distance(posCam, candidato) > 4.5f)
            {
                destino = candidato;
                break;
            }
        }

        nodoAudioParanoia.transform.position = destino;
        if (rutinaAudioParanoia != null) StopCoroutine(rutinaAudioParanoia);
        rutinaAudioParanoia = StartCoroutine(RutinaReproducirFalsoPositivo());
    }

    private IEnumerator RutinaReproducirFalsoPositivo()
    {
        int variante = Random.Range(0, 3);

        if (variante == 0 && clipVientoVentana != null)
        {
            // Ráfaga corta de viento contra ventana (3.2 s con fade suave)
            fuenteParanoia3D.clip = clipVientoVentana;
            fuenteParanoia3D.pitch = Random.Range(0.92f, 1.08f);
            fuenteParanoia3D.time = Random.Range(0f, Mathf.Max(0f, clipVientoVentana.length - 5f));
            fuenteParanoia3D.volume = 0f;
            fuenteParanoia3D.Play();

            float t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                fuenteParanoia3D.volume = Mathf.Lerp(0f, 0.34f, t / 1.2f);
                yield return null;
            }
            yield return new WaitForSeconds(0.9f);
            t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                fuenteParanoia3D.volume = Mathf.Lerp(0.34f, 0f, t / 1.2f);
                yield return null;
            }
            fuenteParanoia3D.Stop();
        }
        else if (variante == 1 && clipCrujidoMadera != null &&
                 !clipCrujidoMadera.name.ToLowerInvariant().Contains("door") &&
                 !clipCrujidoMadera.name.ToLowerInvariant().Contains("bathroom"))
        {
            // Crujido lento de madera vieja (NUNCA usar audios de puerta aquí)
            fuenteParanoia3D.pitch = Random.Range(0.68f, 0.78f);
            fuenteParanoia3D.PlayOneShot(clipCrujidoMadera, 0.22f);
        }
        else if (clipGolpeMadera != null)
        {
            // Golpe sordo / paso aislado en madera arriba
            fuenteParanoia3D.clip = clipGolpeMadera;
            fuenteParanoia3D.pitch = Random.Range(0.70f, 0.80f);
            fuenteParanoia3D.time = 0f;
            fuenteParanoia3D.volume = 0.32f;
            fuenteParanoia3D.Play();
            yield return new WaitForSeconds(0.38f);
            fuenteParanoia3D.Stop();
        }

        rutinaAudioParanoia = null;
    }

    private static AudioClip CrearLatidoProcedural()
    {
        int sr = 22050;
        int samples = Mathf.RoundToInt(sr * 0.55f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float val = 0f;
            if (t < 0.15f)
            {
                float p = t / 0.15f;
                float env = Mathf.Pow(Mathf.Sin(p * Mathf.PI), 1.6f) * Mathf.Exp(-p * 2.2f);
                val += Mathf.Sin(2f * Mathf.PI * (68f - 20f * p) * t) * env * 0.90f;
            }
            if (t >= 0.19f && t < 0.36f)
            {
                float dt = t - 0.19f;
                float p = dt / 0.17f;
                float env = Mathf.Pow(Mathf.Sin(p * Mathf.PI), 1.5f) * Mathf.Exp(-p * 2.5f);
                val += Mathf.Sin(2f * Mathf.PI * (58f - 16f * p) * dt) * env * 0.70f;
            }
            data[i] = val;
        }
        AudioClip clip = AudioClip.Create("HeartbeatProcedural", samples, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}

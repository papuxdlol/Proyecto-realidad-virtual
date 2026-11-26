using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Fase 3 — Sistema de Fin de Turno (Derrota con Screamer + Death Screen 1:1 estilo Graveyard Shift at Freddy's
/// con audio real de alarma VHS + flatline, encuadre close-up de rostro sobre negro puro, aberración cromática RGB,
/// cortes de cinta analógica y aislamiento total de cámara; más Pantalla de Victoria a las 06:00 AM).
/// </summary>
public class FinalTurnoVR : MonoBehaviour
{
    public static FinalTurnoVR Instancia;
    public static bool EstaEnSecuenciaFinal { get; private set; }

    private const string EscenaMenuPrincipal = "MenuPrincipal";
    private const int CapaScreamer = 28;
    private const int CapaUI = 5;

    [Header("Condiciones de Derrota")]
    public int maxAnomaliasActivasParaPerder = 5;
    public int maxReportesFallidosParaPerder = 4;

    [Header("Fuente de Terror VHS (VT323-Regular)")]
    public Font fuenteTerrorVHS;

    [Header("Audios de Fase 3 (Reemplazables en Inspector)")]
    public AudioClip sonidoScreamer;
    public AudioClip sonidoAlarmaVHS;
    public AudioClip sonidoFlatline;
    public AudioClip sonidoVictoria6AM;

    private AudioSource fuenteAudioFinal;
    private Canvas canvasFinal;
    private GraphicRaycaster raycasterCanvas;
    private TrackedDeviceGraphicRaycaster raycasterXRCanvas;

    private RawImage imgRetratoVHS;
    private RawImage imgScanlinesVHS;
    private Text txtMonologoDeathScreen;
    private Text txtMonologoCanalAzul;
    private Text txtMonologoCanalRojo;
    private RectTransform rectMonologoDeathScreen;
    private RectTransform rectMonologoAzul;
    private RectTransform rectMonologoRojo;
    private RectTransform barraTrackingVHS;
    private Image imgBarraTracking;

    private GameObject panelBotonesDerrota;
    private GameObject panelVictoria;
    private string textoMonologoBase = "";

    private readonly List<Texture2D> texturasTemporalesVHS = new List<Texture2D>();
    private Coroutine rutinaSecuencia;

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(this);
            return;
        }
        Instancia = this;
        EstaEnSecuenciaFinal = false;

        fuenteAudioFinal = gameObject.AddComponent<AudioSource>();
        fuenteAudioFinal.playOnAwake = false;
        fuenteAudioFinal.spatialBlend = 0f;
        fuenteAudioFinal.priority = 0;

        AsegurarFuenteTerror();
        AsegurarAudiosFallback();
    }

    void OnDestroy()
    {
        LimpiarTexturasTemporalesVHS();
        if (Instancia == this)
        {
            EstaEnSecuenciaFinal = false;
            Instancia = null;
        }
    }

    private void LimpiarTexturasTemporalesVHS()
    {
        for (int i = 0; i < texturasTemporalesVHS.Count; i++)
        {
            if (texturasTemporalesVHS[i] != null)
                Destroy(texturasTemporalesVHS[i]);
        }
        texturasTemporalesVHS.Clear();
    }

    void Update()
    {
        if (!EstaEnSecuenciaFinal) return;

        // Atajos de teclado para PC Simulator además del clic/rayo VR
        if (Keyboard.current != null)
        {
            if (panelBotonesDerrota != null && panelBotonesDerrota.activeInHierarchy)
            {
                if (Keyboard.current.rKey.wasPressedThisFrame)
                    ReintentarTurno();
                else if (Keyboard.current.mKey.wasPressedThisFrame)
                    IrAlMenuPrincipal();
            }
            else if (panelVictoria != null && panelVictoria.activeInHierarchy)
            {
                if (Keyboard.current.mKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
                    IrAlMenuPrincipal();
            }
        }
    }

    private Font ObtenerFuenteTerror()
    {
        AsegurarFuenteTerror();
        return fuenteTerrorVHS != null ? fuenteTerrorVHS : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void AsegurarFuenteTerror()
    {
        if (fuenteTerrorVHS != null) return;

#if UNITY_EDITOR
        fuenteTerrorVHS = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/VT323/VT323-Regular.ttf");
#endif
        if (fuenteTerrorVHS == null)
        {
            Font osFont = Font.CreateDynamicFontFromOSFont(new string[] { "VT323", "Times New Roman", "Georgia", "Courier New" }, 26);
            if (osFont != null)
                fuenteTerrorVHS = osFont;
        }
    }

    public void ActivarDerrota(TipoAnomalia entidadAtacante, string motivoDerrota)
    {
        if (EstaEnSecuenciaFinal) return;
        EstaEnSecuenciaFinal = true;

        if (VRPauseMenu.IsMenuOpen && VRPauseMenu.Instance != null)
            VRPauseMenu.Instance.ToggleMenu();

        if (rutinaSecuencia != null) StopCoroutine(rutinaSecuencia);
        rutinaSecuencia = StartCoroutine(RutinaScreamerYDeathScreen(entidadAtacante, motivoDerrota));
    }

    public void ActivarVictoria(int anomaliasContenidas, int reportesFallidos)
    {
        if (EstaEnSecuenciaFinal) return;
        EstaEnSecuenciaFinal = true;

        if (VRPauseMenu.IsMenuOpen && VRPauseMenu.Instance != null)
            VRPauseMenu.Instance.ToggleMenu();

        if (rutinaSecuencia != null) StopCoroutine(rutinaSecuencia);
        rutinaSecuencia = StartCoroutine(RutinaPantallaVictoria(anomaliasContenidas, reportesFallidos));
    }

    // =========================================================================
    // SECUENCIA 1:1 — SCREAMER + DEATH SCREEN VHS ANALÓGICA CON CORTES Y FLATLINE
    // =========================================================================
    private IEnumerator RutinaScreamerYDeathScreen(TipoAnomalia entidadAtacante, string motivoDerrota)
    {
        Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        SilenciarAudiosDeLaCasa();
        DeshabilitarRayosUIDuranteCinematica();
        LimpiarTexturasTemporalesVHS();

        // Ocultar la Tablet durante la secuencia final para que nunca se atraviese frente a la cámara
        if (TabletSistemaVR.Instancia != null)
            TabletSistemaVR.Instancia.gameObject.SetActive(false);

        // Enderezar cualquier inclinación lateral (roll en Z) que haya dejado el simulador de PC
        if (cam != null)
        {
            Vector3 eul = cam.transform.localEulerAngles;
            cam.transform.localEulerAngles = new Vector3(0f, eul.y, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = (1 << CapaScreamer);
        }

        // 1. Instanciar el modelo 3D de la anomalía en la capa exclusiva del Screamer encuadrando su rostro
        GameObject modeloScreamer = InstanciarModeloEntidadParaScreamer(entidadAtacante, cam != null ? cam.transform : transform);

        // Capturar 4 frames VHS posterizados con desgarros analógicos reales del ROSTRO sobre negro puro (#000000)
        Texture2D[] framesRetratoVHS = CapturarFramesRetratoVHSPosterizado(modeloScreamer, entidadAtacante);

        // Luz puntual intensa sobre el rostro del screamer (desactivada en PureForm para que sea sombra negra pura en 3D)
        GameObject objLuzScreamer = new GameObject("Luz_Screamer_VR");
        objLuzScreamer.layer = CapaScreamer;
        if (cam != null) objLuzScreamer.transform.SetParent(cam.transform, false);
        objLuzScreamer.transform.localPosition = new Vector3(0f, 0.05f, 0.28f);
        Light luzScreamer = objLuzScreamer.AddComponent<Light>();
        luzScreamer.type = LightType.Point;
        luzScreamer.color = entidadAtacante == TipoAnomalia.Preacher
            ? new Color(0.88f, 0.90f, 0.96f, 1f)
            : new Color(1f, 0.90f, 0.88f, 1f);
        bool esEntidadUnlitOscura = entidadAtacante == TipoAnomalia.PureForm || entidadAtacante == TipoAnomalia.Preacher;
        luzScreamer.intensity = esEntidadUnlitOscura ? 0f : (entidadAtacante == TipoAnomalia.Flawed ? 2.25f : 4.8f);
        luzScreamer.range = 2.6f;
        luzScreamer.shadows = LightShadows.None;
        luzScreamer.renderMode = LightRenderMode.ForcePixel;
        luzScreamer.cullingMask = (1 << CapaScreamer);
        if (esEntidadUnlitOscura && cam != null)
            cam.backgroundColor = new Color(0.045f, 0.048f, 0.058f, 1f);

        if (fuenteAudioFinal != null && sonidoScreamer != null)
        {
            fuenteAudioFinal.Stop();
            fuenteAudioFinal.clip = sonidoScreamer;
            fuenteAudioFinal.volume = 0.95f;
            fuenteAudioFinal.loop = false;
            fuenteAudioFinal.Play();
        }

        // Sacudida violenta frente al visor (0.00 s -> 1.12 s)
        float duracionScreamer = 1.12f;
        float tScreamer = 0f;
        Vector3 posBaseScreamer = modeloScreamer != null ? modeloScreamer.transform.localPosition : new Vector3(0f, -0.5f, 0.66f);
        Quaternion rotBaseScreamer = modeloScreamer != null ? modeloScreamer.transform.localRotation : Quaternion.Euler(0f, 180f, 0f);

        while (tScreamer < duracionScreamer)
        {
            tScreamer += Time.unscaledDeltaTime;

            if (TabletSistemaVR.Instancia != null)
                TabletSistemaVR.Instancia.EnviarHapticAmbosMandos(1.0f, 0.1f);

            if (modeloScreamer != null)
            {
                float progreso = Mathf.Clamp01(tScreamer / duracionScreamer);
                float acercamientoZ = Mathf.Lerp(0.12f, -0.06f, progreso);
                Vector3 sacudidaPos = new Vector3(
                    Random.Range(-0.042f, 0.042f),
                    Random.Range(-0.035f, 0.035f),
                    acercamientoZ + Random.Range(-0.02f, 0.02f)
                );
                Quaternion sacudidaRot = Quaternion.Euler(
                    Random.Range(-10f, 10f),
                    Random.Range(-14f, 14f),
                    Random.Range(-12f, 12f)
                );
                modeloScreamer.transform.localPosition = posBaseScreamer + sacudidaPos;
                modeloScreamer.transform.localRotation = rotBaseScreamer * sacudidaRot;
            }

            if (esEntidadUnlitOscura)
            {
                if (cam != null)
                {
                    float penumbra = Random.value < 0.28f ? Random.Range(0.09f, 0.14f) : Random.Range(0.038f, 0.065f);
                    cam.backgroundColor = new Color(penumbra * 0.92f, penumbra * 0.95f, penumbra * 1.08f, 1f);
                }
            }
            else if (luzScreamer != null)
            {
                luzScreamer.intensity = entidadAtacante == TipoAnomalia.Flawed
                    ? Random.Range(1.35f, 2.55f)
                    : Random.Range(2.2f, 5.8f);
            }

            yield return null;
        }

        // 2. CORTE EN SECO A NEGRO ABSOLUTO + DEATH SCREEN VHS SINCRONIZADA AL CLIP REAL DE ALARMA
        if (modeloScreamer != null) Destroy(modeloScreamer);
        if (objLuzScreamer != null) Destroy(objLuzScreamer);
        if (fuenteAudioFinal != null) fuenteAudioFinal.Stop();

        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = (1 << CapaUI);
        }

        ConstruirCanvasCentradoEnCamara(cam, habilitarInteraccionInmediata: false);
        Texture2D texInicial = (framesRetratoVHS != null && framesRetratoVHS.Length > 0) ? framesRetratoVHS[0] : null;
        ConstruirLayoutDeathScreenVHS(texInicial, entidadAtacante, motivoDerrota);

        float duracionTotalAlarma = (sonidoAlarmaVHS != null && sonidoAlarmaVHS.length > 2.5f)
            ? sonidoAlarmaVHS.length
            : 5.94f;

        if (fuenteAudioFinal != null && sonidoAlarmaVHS != null)
        {
            fuenteAudioFinal.clip = sonidoAlarmaVHS;
            fuenteAudioFinal.volume = 1.0f;
            fuenteAudioFinal.pitch = 1.0f;
            fuenteAudioFinal.loop = (sonidoAlarmaVHS.length < 2.5f);
            fuenteAudioFinal.Play();
        }

        Vector2 posBaseRetrato = new Vector2(228f, 12f);
        Vector2 posTextoMacro = new Vector2(85f, -38f);
        Vector2 posTextoNormal = new Vector2(-202f, 8f);
        RectTransform rectRetrato = imgRetratoVHS != null ? imgRetratoVHS.rectTransform : null;

        // Tiempos exactos 1:1 con el video de referencia (~5.94 s total de alarma):
        // [0.00 - 0.94s]: Solo el rostro B&W desgarrado a la derecha sobre negro absoluto.
        // [0.94 - 2.18s]: Corte macro agresivo del texto gigante superpuesto al rostro con estática fuerte.
        // [2.18 - 4.28s]: Corte al plano completo en 2 columnas (monólogo con aberración RGB azul/roja + rostro).
        // [4.28 - fin]:   El rostro se apaga (con destellos subliminales de 1 frame) y queda el monólogo vibrando.
        float corteMacro = duracionTotalAlarma * 0.158f;
        float corteDosColumnas = duracionTotalAlarma * 0.367f;
        float corteSoloTexto = duracionTotalAlarma * 0.720f;

        ActivarCapasTextoMonologo(false);

        float tAlarma = 0f;
        float timerCambioFrameRetrato = 0f;

        while (tAlarma < duracionTotalAlarma)
        {
            float dt = Time.unscaledDeltaTime;
            tAlarma += dt;
            timerCambioFrameRetrato -= dt;

            // Alternar entre los 4 frames desgarrados del retrato para que la cara se deforme como cinta dañada
            if (timerCambioFrameRetrato <= 0f && imgRetratoVHS != null && framesRetratoVHS != null && framesRetratoVHS.Length > 0)
            {
                imgRetratoVHS.texture = framesRetratoVHS[Random.Range(0, framesRetratoVHS.Length)];
                timerCambioFrameRetrato = Random.Range(0.035f, 0.085f);
            }

            // Pulso rítmico sincronizado con los golpes ásperos de la alarma VHS (~7.5 Hz + picos aleatorios)
            float pulsoAlarma = Mathf.Abs(Mathf.Sin(tAlarma * Mathf.PI * 7.6f));
            bool picoGlitch = pulsoAlarma > 0.86f || Random.value < 0.14f;

            if (TabletSistemaVR.Instancia != null && picoGlitch && Random.value < 0.35f)
                TabletSistemaVR.Instancia.EnviarHapticAmbosMandos(0.28f, 0.05f);

            if (tAlarma < corteMacro)
            {
                // BEAT 1: Solo rostro desgarrado en la oscuridad
                ActivarCapasTextoMonologo(false);
                if (imgRetratoVHS != null) imgRetratoVHS.gameObject.SetActive(true);
                AplicarGlitchContinuoVHS(rectRetrato, posBaseRetrato, picoGlitch, 1.0f, 0.38f);
            }
            else if (tAlarma < corteDosColumnas)
            {
                // BEAT 2: Corte macro agresivo (texto gigante superpuesto sobre el rostro como en el segundo 00:02)
                ActivarCapasTextoMonologo(true);
                if (imgRetratoVHS != null) imgRetratoVHS.gameObject.SetActive(true);
                AplicarGlitchContinuoVHS(rectRetrato, posBaseRetrato + new Vector2(18f, 6f), true, 0.92f, 0.50f);

                AplicarTransformYCromaticaTexto(
                    posTextoMacro,
                    new Vector2(2.22f, 2.58f),
                    picoGlitch,
                    amplitudX: 18f,
                    amplitudY: 7f,
                    splitRGB: 4.8f,
                    corromperCaracteres: true);
            }
            else if (tAlarma < corteSoloTexto)
            {
                // BEAT 3: Plano completo de 2 columnas (monólogo condensado a la izquierda + rostro a la derecha)
                ActivarCapasTextoMonologo(true);
                if (imgRetratoVHS != null) imgRetratoVHS.gameObject.SetActive(true);
                AplicarGlitchContinuoVHS(rectRetrato, posBaseRetrato, picoGlitch, 0.96f, 0.34f);

                AplicarTransformYCromaticaTexto(
                    posTextoNormal,
                    new Vector2(0.98f, 1.32f),
                    picoGlitch,
                    amplitudX: 12f,
                    amplitudY: 2.5f,
                    splitRGB: 3.4f,
                    corromperCaracteres: true);
            }
            else
            {
                // BEAT 4: El retrato desaparece en la negrura (con breves flashes subliminales) y queda el monólogo
                ActivarCapasTextoMonologo(true);
                bool flashSubliminal = picoGlitch && Random.value < 0.10f;
                if (imgRetratoVHS != null) imgRetratoVHS.gameObject.SetActive(flashSubliminal);
                AplicarGlitchContinuoVHS(rectRetrato, posBaseRetrato, picoGlitch, flashSubliminal ? 0.55f : 0f, 0.42f);

                AplicarTransformYCromaticaTexto(
                    posTextoNormal,
                    new Vector2(0.98f, 1.32f),
                    picoGlitch,
                    amplitudX: 15f,
                    amplitudY: 3.0f,
                    splitRGB: 3.8f,
                    corromperCaracteres: true);
            }

            yield return null;
        }

        // 3. CORTE EN SECO A PITIDO FLATLINE + TEXTO ESTIRADO EXTREMO CON SANGRADO CROMÁTICO AZUL/ROJO
        float duracionFlatline = (sonidoFlatline != null && sonidoFlatline.length > 0.8f)
            ? sonidoFlatline.length
            : 2.48f;

        if (fuenteAudioFinal != null)
        {
            fuenteAudioFinal.Stop();
            fuenteAudioFinal.loop = false;
            fuenteAudioFinal.pitch = 1.0f;
            if (sonidoFlatline != null)
            {
                fuenteAudioFinal.clip = sonidoFlatline;
                fuenteAudioFinal.volume = 0.92f;
                fuenteAudioFinal.Play();
            }
        }

        SincronizarContenidoTextoMonologo(textoMonologoBase);
        if (imgRetratoVHS != null)
        {
            imgRetratoVHS.gameObject.SetActive(true);
            if (framesRetratoVHS != null && framesRetratoVHS.Length > 0)
                imgRetratoVHS.texture = framesRetratoVHS[0];
        }

        if (TabletSistemaVR.Instancia != null)
            TabletSistemaVR.Instancia.EnviarHapticAmbosMandos(0.55f, 0.30f);

        float tFlatline = 0f;
        while (tFlatline < duracionFlatline)
        {
            tFlatline += Time.unscaledDeltaTime;

            // En el segundo ~1.00 del flatline real hay un pequeño salto analógico de cinta (como en el video original)
            bool segundaMitadFlatline = tFlatline >= (duracionFlatline * 0.42f);
            Vector2 posFlatline = segundaMitadFlatline ? new Vector2(-18f, 48f) : new Vector2(42f, -64f);

            // Impresión fantasma ultra tenue (7%) del rostro al fondo derecho durante el flatline
            AplicarGlitchContinuoVHS(rectRetrato, posBaseRetrato + new Vector2(10f, -8f), false, 0.075f, 0.18f);

            if (rectMonologoDeathScreen != null)
            {
                Vector2 jitter = new Vector2(Random.Range(-1.8f, 1.8f), Random.Range(-2.4f, 2.4f));
                Vector3 escalaEstirada = new Vector3(2.05f, 4.80f + Random.Range(-0.05f, 0.05f), 1f);

                rectMonologoDeathScreen.anchoredPosition = posFlatline + jitter;
                rectMonologoDeathScreen.localScale = escalaEstirada;

                if (txtMonologoDeathScreen != null)
                    txtMonologoDeathScreen.color = new Color(0.93f, 0.93f, 0.87f, 1f);

                if (rectMonologoAzul != null)
                {
                    rectMonologoAzul.anchoredPosition = rectMonologoDeathScreen.anchoredPosition + new Vector2(-7.2f, 0f);
                    rectMonologoAzul.localScale = escalaEstirada;
                }
                if (txtMonologoCanalAzul != null)
                    txtMonologoCanalAzul.color = new Color(0.12f, 0.32f, 1.0f, 0.94f);

                if (rectMonologoRojo != null)
                {
                    rectMonologoRojo.anchoredPosition = rectMonologoDeathScreen.anchoredPosition + new Vector2(6.2f, 1.8f);
                    rectMonologoRojo.localScale = escalaEstirada;
                }
                if (txtMonologoCanalRojo != null)
                    txtMonologoCanalRojo.color = new Color(0.94f, 0.15f, 0.10f, 0.88f);
            }

            yield return null;
        }

        // 4. CORTE ABRUPTO A NEGRO EN SILENCIO ABSOLUTO Y APARICIÓN DE BOTONES
        if (fuenteAudioFinal != null)
            fuenteAudioFinal.Stop();

        ActivarCapasTextoMonologo(false);
        if (imgRetratoVHS != null) imgRetratoVHS.gameObject.SetActive(false);
        if (imgScanlinesVHS != null) imgScanlinesVHS.gameObject.SetActive(false);
        if (barraTrackingVHS != null) barraTrackingVHS.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(0.75f);

        if (panelBotonesDerrota != null)
            panelBotonesDerrota.SetActive(true);

        HabilitarRayosUIMandos();
    }

    private void ActivarCapasTextoMonologo(bool activo)
    {
        if (txtMonologoCanalAzul != null && txtMonologoCanalAzul.gameObject.activeSelf != activo)
            txtMonologoCanalAzul.gameObject.SetActive(activo);
        if (txtMonologoCanalRojo != null && txtMonologoCanalRojo.gameObject.activeSelf != activo)
            txtMonologoCanalRojo.gameObject.SetActive(activo);
        if (txtMonologoDeathScreen != null && txtMonologoDeathScreen.gameObject.activeSelf != activo)
            txtMonologoDeathScreen.gameObject.SetActive(activo);
    }

    private void SincronizarContenidoTextoMonologo(string contenido)
    {
        if (txtMonologoDeathScreen != null) txtMonologoDeathScreen.text = contenido;
        if (txtMonologoCanalAzul != null) txtMonologoCanalAzul.text = contenido;
        if (txtMonologoCanalRojo != null) txtMonologoCanalRojo.text = contenido;
    }

    private void AplicarTransformYCromaticaTexto(
        Vector2 posBase,
        Vector2 escalaBase,
        bool saltoTracking,
        float amplitudX,
        float amplitudY,
        float splitRGB,
        bool corromperCaracteres)
    {
        if (rectMonologoDeathScreen == null) return;

        float tearX = saltoTracking ? Random.Range(-amplitudX, amplitudX) : Random.Range(-1.8f, 1.8f);
        float tearY = saltoTracking ? Random.Range(-amplitudY * 1.8f, amplitudY * 1.8f) : Random.Range(-amplitudY * 0.5f, amplitudY * 0.5f);
        Vector2 posFinal = posBase + new Vector2(tearX, tearY);

        float escY = escalaBase.y + (saltoTracking ? Random.Range(-0.08f, 0.14f) : Random.Range(-0.015f, 0.015f));
        Vector3 escFinal = new Vector3(escalaBase.x, escY, 1f);

        rectMonologoDeathScreen.anchoredPosition = posFinal;
        rectMonologoDeathScreen.localScale = escFinal;

        float desfaseExtra = saltoTracking ? Random.Range(1.6f, 2.5f) : 1.0f;
        if (rectMonologoAzul != null)
        {
            rectMonologoAzul.anchoredPosition = posFinal + new Vector2(-splitRGB * desfaseExtra, -0.5f);
            rectMonologoAzul.localScale = escFinal;
        }
        if (rectMonologoRojo != null)
        {
            rectMonologoRojo.anchoredPosition = posFinal + new Vector2(splitRGB * 0.92f * desfaseExtra, 1.1f);
            rectMonologoRojo.localScale = escFinal;
        }

        string textoActual = (corromperCaracteres && saltoTracking && Random.value < 0.45f)
            ? CorromperTextoBreveVHS(textoMonologoBase)
            : textoMonologoBase;
        SincronizarContenidoTextoMonologo(textoActual);

        if (txtMonologoDeathScreen != null)
        {
            float parpadeo = saltoTracking ? Random.Range(0.76f, 0.98f) : 0.95f;
            txtMonologoDeathScreen.color = new Color(parpadeo, parpadeo, parpadeo * 0.94f, 1f);
        }
    }

    private void AplicarGlitchContinuoVHS(RectTransform rectRetrato, Vector2 posBaseRetrato, bool intenso, float brilloBase, float intensidadScanlines)
    {
        if (rectRetrato != null)
        {
            float amp = intenso ? 7.5f : 2.8f;
            float saltoX = (intenso && Random.value < 0.35f) ? Random.Range(-22f, 22f) : Random.Range(-amp, amp);
            float saltoY = Random.Range(-amp * 0.65f, amp * 0.65f);
            rectRetrato.anchoredPosition = posBaseRetrato + new Vector2(saltoX, saltoY);
            float escJitter = intenso ? Random.Range(0.98f, 1.05f) : 1.0f;
            rectRetrato.localScale = new Vector3(escJitter, escJitter, 1f);
        }

        if (imgRetratoVHS != null)
        {
            float flicker = (intenso && Random.value < 0.30f) ? brilloBase * Random.Range(0.60f, 1.18f) : brilloBase;
            flicker = Mathf.Clamp01(flicker);
            imgRetratoVHS.color = new Color(flicker, flicker, flicker, 1f);
        }

        if (imgScanlinesVHS != null)
        {
            float uvX = intenso ? Random.Range(-0.08f, 0.08f) : 0f;
            float uvY = Mathf.Repeat(Time.unscaledTime * 1.85f + (intenso ? Random.Range(-0.18f, 0.18f) : 0f), 1f);
            imgScanlinesVHS.uvRect = new Rect(uvX, uvY, 1f, 1.35f);
            float alfaRuido = intenso ? Mathf.Clamp01(intensidadScanlines * Random.Range(1.05f, 1.55f)) : intensidadScanlines;
            imgScanlinesVHS.color = new Color(1f, 1f, 1f, alfaRuido);
        }

        if (barraTrackingVHS != null && imgBarraTracking != null)
        {
            float y = Mathf.Repeat(Time.unscaledTime * 340f, 640f) - 320f;
            barraTrackingVHS.anchoredPosition = new Vector2(0f, y);
            imgBarraTracking.color = new Color(1f, 1f, 1f, intenso ? Random.Range(0.04f, 0.11f) : 0.02f);
        }
    }

    private static string CorromperTextoBreveVHS(string original)
    {
        if (string.IsNullOrEmpty(original)) return original;
        char[] arr = original.ToCharArray();
        char[] glitched = { '#', '%', '&', '?', '/', '!', 'X', '_' };
        int cambios = Random.Range(2, 6);
        for (int i = 0; i < cambios; i++)
        {
            int idx = Random.Range(0, arr.Length);
            if (arr[idx] != '\n' && arr[idx] != ' ')
                arr[idx] = glitched[Random.Range(0, glitched.Length)];
        }
        return new string(arr);
    }

    // =========================================================================
    // PANTALLA DE VICTORIA A LAS 06:00 AM (CENTRADA, DERECHA Y SIN OBSTÁCULOS)
    // =========================================================================
    private IEnumerator RutinaPantallaVictoria(int anomaliasContenidas, int reportesFallidos)
    {
        Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        SilenciarAudiosDeLaCasa();

        if (TabletSistemaVR.Instancia != null)
            TabletSistemaVR.Instancia.gameObject.SetActive(false);

        if (cam != null)
        {
            Vector3 eul = cam.transform.localEulerAngles;
            cam.transform.localEulerAngles = new Vector3(0f, eul.y, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = (1 << CapaUI);
        }

        if (fuenteAudioFinal != null && sonidoVictoria6AM != null)
        {
            fuenteAudioFinal.Stop();
            fuenteAudioFinal.clip = sonidoVictoria6AM;
            fuenteAudioFinal.volume = 0.85f;
            fuenteAudioFinal.loop = false;
            fuenteAudioFinal.Play();
        }

        if (TabletSistemaVR.Instancia != null)
            TabletSistemaVR.Instancia.EnviarHapticAmbosMandos(0.55f, 0.22f);

        ConstruirCanvasCentradoEnCamara(cam, habilitarInteraccionInmediata: true);
        ConstruirLayoutVictoria6AM(anomaliasContenidas, reportesFallidos);
        HabilitarRayosUIMandos();

        yield break;
    }

    // =========================================================================
    // CLONADO 3D Y CAPTURA DE ROSTRO CLOSE-UP VHS EN NEGRO PURO (#000000)
    // =========================================================================
    private static Transform BuscarHuesoCabeza(Transform raiz)
    {
        if (raiz == null) return null;
        string[] nombresCabeza = { "mixamorig:Head", "PR_Head", "FL06_Head", "Head", "head", "Cabeza" };
        Transform[] todos = raiz.GetComponentsInChildren<Transform>(true);
        for (int n = 0; n < nombresCabeza.Length; n++)
        {
            string buscado = nombresCabeza[n];
            for (int i = 0; i < todos.Length; i++)
            {
                if (todos[i] != null && string.Equals(todos[i].name, buscado, System.StringComparison.OrdinalIgnoreCase))
                    return todos[i];
            }
        }
        return null;
    }

    private GameObject InstanciarModeloEntidadParaScreamer(TipoAnomalia tipo, Transform camTransform)
    {
        string nombreObjetoEscena = ObtenerNombreObjetoEntidad(tipo);
        GameObject original = GameObject.Find(nombreObjetoEscena);
        Transform visualOriginal = original != null ? original.transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)") : null;

        GameObject contenedor = new GameObject("Screamer_Entidad_3D_VR");
        contenedor.layer = CapaScreamer;
        contenedor.transform.SetParent(camTransform, false);
        contenedor.transform.localPosition = new Vector3(0f, -0.60f, 0.66f);
        contenedor.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        contenedor.transform.localScale = Vector3.one;

        if (visualOriginal != null)
        {
            GameObject clon = Instantiate(visualOriginal.gameObject, contenedor.transform);
            clon.name = "Visual_Screamer";
            clon.SetActive(true);
            clon.transform.localPosition = Vector3.zero;
            clon.transform.localRotation = Quaternion.identity;

            AnimadorEntidadTerror anim = clon.GetComponent<AnimadorEntidadTerror>();
            if (anim != null) Destroy(anim);

            if (tipo == TipoAnomalia.PureForm)
                ObjetoAnomalia.AplicarMaterialesAbismalesPureForm(clon.transform);
            else if (tipo == TipoAnomalia.Preacher)
                ObjetoAnomalia.AplicarMaterialesTerrorPreacher(clon.transform);
            else if (tipo == TipoAnomalia.Flawed)
            {
                if (clon.transform.childCount > 1)
                {
                    bool algunHijoActivo = false;
                    for (int c = 0; c < clon.transform.childCount; c++)
                    {
                        if (clon.transform.GetChild(c).gameObject.activeSelf)
                        {
                            if (!algunHijoActivo) algunHijoActivo = true;
                            else clon.transform.GetChild(c).gameObject.SetActive(false);
                        }
                    }
                    if (!algunHijoActivo)
                    {
                        int idxRand = Random.Range(0, clon.transform.childCount);
                        clon.transform.GetChild(idxRand).gameObject.SetActive(true);
                    }
                }
                ObjetoAnomalia.AplicarMaterialesMateFlawed(clon.transform);
            }

            AsignarCapaRecursiva(clon.transform, CapaScreamer);

            if (tipo == TipoAnomalia.Corpse)
            {
                Transform grpAcostado = clon.transform.Find("Corpse_Acostado");
                Transform grpColgado = clon.transform.Find("Corpse_Colgado");
                Transform grpBanera = clon.transform.Find("Corpse_Banera");
                bool algunoActivo = (grpAcostado != null && grpAcostado.gameObject.activeSelf) ||
                                    (grpColgado != null && grpColgado.gameObject.activeSelf) ||
                                    (grpBanera != null && grpBanera.gameObject.activeSelf);
                if (!algunoActivo && grpAcostado != null)
                    grpAcostado.gameObject.SetActive(true);

                if (grpBanera != null && grpBanera.gameObject.activeSelf)
                {
                    Transform tinaMesh = grpBanera.Find("Tina_Con_Cuerpo/Bathtub");
                    if (tinaMesh != null) tinaMesh.gameObject.SetActive(false);
                }
                else if (grpAcostado != null && grpAcostado.gameObject.activeSelf)
                {
                    clon.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                }
            }

            Renderer[] rends = clon.GetComponentsInChildren<Renderer>(false);
            if (rends.Length == 0)
                rends = clon.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

                Transform huesoCabeza = BuscarHuesoCabeza(clon.transform);
                float alturaRostro = (huesoCabeza != null)
                    ? huesoCabeza.position.y + 0.03f
                    : (b.min.y + b.size.y * 0.88f);
                Vector3 centroHorizontal = (huesoCabeza != null)
                    ? new Vector3(huesoCabeza.position.x, alturaRostro, huesoCabeza.position.z)
                    : new Vector3(b.center.x, alturaRostro, b.center.z);

                Vector3 posFrenteCam = camTransform.position + camTransform.forward * 0.66f;
                Vector3 delta = new Vector3(
                    posFrenteCam.x - centroHorizontal.x,
                    posFrenteCam.y - alturaRostro,
                    posFrenteCam.z - centroHorizontal.z
                );
                clon.transform.position += delta;
            }
        }
        else
        {
            GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            esfera.layer = CapaScreamer;
            esfera.transform.SetParent(contenedor.transform, false);
            esfera.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            esfera.transform.localScale = new Vector3(0.36f, 0.46f, 0.32f);
        }

        return contenedor;
    }

    private Texture2D[] CapturarFramesRetratoVHSPosterizado(GameObject modeloScreamer, TipoAnomalia tipo)
    {
        int w = 256;
        int h = 320;
        Color[] pixelesCrudos = null;
        bool capturaExitosa = false;

        if (modeloScreamer != null)
        {
            AsignarCapaRecursiva(modeloScreamer.transform, CapaScreamer);

            // Para entidades con cuerpo Unlit negro absoluto (PureForm / túnica de Preacher), asignar temporalmente
            // un material Lit gris carbón SOLO durante el milisegundo del snapshot offscreen para esculpir su cráneo,
            // cuencas y hombros con claroscuro sobre fondo #000000 (jamás un rectángulo gris o blanco).
            List<(Renderer rend, Material[] matsOriginales)> respaldoMateriales = new List<(Renderer, Material[])>();
            Material matEsculturaTemporal = null;

            if (tipo == TipoAnomalia.PureForm || tipo == TipoAnomalia.Preacher)
            {
                Shader shLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shLit != null)
                {
                    matEsculturaTemporal = new Material(shLit) { name = "Temp_VHS_Sculpt" };
                    matEsculturaTemporal.SetColor("_BaseColor", new Color(0.46f, 0.46f, 0.49f, 1f));
                    matEsculturaTemporal.color = new Color(0.46f, 0.46f, 0.49f, 1f);
                    matEsculturaTemporal.SetFloat("_Smoothness", 0.52f);

                    foreach (Renderer r in modeloScreamer.GetComponentsInChildren<Renderer>(false))
                    {
                        if (r == null || r.sharedMaterials == null) continue;
                        Material[] orig = r.sharedMaterials;
                        Material[] mod = (Material[])orig.Clone();
                        bool cambio = false;
                        for (int m = 0; m < mod.Length; m++)
                        {
                            string mName = mod[m] != null ? mod[m].name : "";
                            string rName = r.gameObject.name;
                            bool esOjoORostroBrillante = mName.Contains("Ojos") || mName.Contains("Rostro") ||
                                                         rName.Contains("Ojo") || rName.Contains("Mascara") || rName.Contains("Rostro");
                            if (!esOjoORostroBrillante)
                            {
                                mod[m] = matEsculturaTemporal;
                                cambio = true;
                            }
                        }
                        if (cambio)
                        {
                            respaldoMateriales.Add((r, orig));
                            r.sharedMaterials = mod;
                        }
                    }
                }
            }

            // Determinar el centro exacto de la CABEZA para un close-up de terror estilo cámara de seguridad
            Renderer[] rends = modeloScreamer.GetComponentsInChildren<Renderer>(false);
            Bounds b = rends.Length > 0 ? rends[0].bounds : new Bounds(modeloScreamer.transform.position, Vector3.one * 0.5f);
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            Transform huesoCabeza = BuscarHuesoCabeza(modeloScreamer.transform);
            Vector3 focoRostro = (huesoCabeza != null)
                ? huesoCabeza.position + Vector3.up * 0.035f
                : new Vector3(b.center.x, b.min.y + b.size.y * 0.89f, b.center.z);

            Transform refDir = Camera.main != null ? Camera.main.transform : transform;
            Vector3 haciaCamara = -refDir.forward;
            Vector3 derechaCam = refDir.right;
            Vector3 arribaCam = refDir.up;

            // Encuadre close-up ligeramente angulado y con leve inclinación holandesa como en el video original
            GameObject camObj = new GameObject("Cam_Snapshot_VHS");
            camObj.transform.position = focoRostro + haciaCamara * 0.42f + derechaCam * 0.055f + arribaCam * 0.02f;
            camObj.transform.rotation = Quaternion.LookRotation((focoRostro - Vector3.up * 0.02f) - camObj.transform.position, arribaCam)
                                      * Quaternion.Euler(0f, 0f, 6.5f);

            // Luz principal rasante lateral-superior + contraluz tenue para recortar facciones sobre fondo #000000
            GameObject luzKeyObj = new GameObject("Luz_Key_Snapshot_VHS");
            luzKeyObj.transform.position = focoRostro + haciaCamara * 0.34f + derechaCam * 0.24f + arribaCam * 0.16f;
            Light lKey = luzKeyObj.AddComponent<Light>();
            lKey.type = LightType.Point;
            lKey.color = Color.white;
            lKey.intensity = (tipo == TipoAnomalia.PureForm) ? 7.5f : 5.6f;
            lKey.range = 2.4f;
            lKey.renderMode = LightRenderMode.ForcePixel;
            lKey.cullingMask = (1 << CapaScreamer);

            GameObject luzRimObj = new GameObject("Luz_Rim_Snapshot_VHS");
            luzRimObj.transform.position = focoRostro + haciaCamara * 0.10f - derechaCam * 0.28f + arribaCam * 0.18f;
            Light lRim = luzRimObj.AddComponent<Light>();
            lRim.type = LightType.Point;
            lRim.color = Color.white;
            lRim.intensity = 2.4f;
            lRim.range = 2.0f;
            lRim.renderMode = LightRenderMode.ForcePixel;
            lRim.cullingMask = (1 << CapaScreamer);

            RenderTexture rt = RenderTexture.GetTemporary(w, h, 16, RenderTextureFormat.ARGB32);
            Camera camSnap = camObj.AddComponent<Camera>();
            camSnap.clearFlags = CameraClearFlags.SolidColor;
            // SIEMPRE negro absoluto (#000000): cero recuadros blancos o grises alrededor de la entidad
            camSnap.backgroundColor = Color.black;
            camSnap.cullingMask = (1 << CapaScreamer);
            camSnap.fieldOfView = 31f;
            camSnap.nearClipPlane = 0.06f;
            camSnap.farClipPlane = 2.5f;
            camSnap.targetTexture = rt;
            camSnap.Render();

            Texture2D texLectura = new Texture2D(w, h, TextureFormat.RGBA32, false);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            texLectura.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;

            pixelesCrudos = texLectura.GetPixels();
            Destroy(texLectura);

            camSnap.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(luzKeyObj);
            Destroy(luzRimObj);
            Destroy(camObj);

            // Restaurar materiales originales del Screamer inmediatamente
            for (int i = 0; i < respaldoMateriales.Count; i++)
            {
                if (respaldoMateriales[i].rend != null)
                    respaldoMateriales[i].rend.sharedMaterials = respaldoMateriales[i].matsOriginales;
            }
            if (matEsculturaTemporal != null)
                Destroy(matEsculturaTemporal);

            // Verificar que el rostro capturado tenga suficientes píxeles iluminados
            int conteoLuz = 0;
            for (int i = 0; i < pixelesCrudos.Length; i++)
            {
                float lum = pixelesCrudos[i].r * 0.299f + pixelesCrudos[i].g * 0.587f + pixelesCrudos[i].b * 0.114f;
                if (lum > 0.05f) conteoLuz++;
            }
            capturaExitosa = conteoLuz > (w * h) / 45;
        }

        // Posterizar imagen base con viñeteado orgánico hacia #000000 para que el rostro emerja de la oscuridad
        Color[] basePosterizada = new Color[w * h];
        if (capturaExitosa && pixelesCrudos != null)
        {
            for (int y = 0; y < h; y++)
            {
                float ny = (y / (float)h) * 2f - 1f;
                float scan = (y % 2 == 0) ? 0.85f : 1.0f;
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)w) * 2f - 1f;
                    // Viñeta elíptica suave que funde cualquier borde exterior al negro puro
                    float distBorde = nx * nx * 0.92f + ny * ny * 0.78f;
                    float mascaraVignette = Mathf.Clamp01(1.18f - distBorde * 0.95f);

                    // Cuantización en bloques de 2x2 píxeles estilo compresión VHS de baja resolución
                    int bx = (x / 2) * 2;
                    int by = (y / 2) * 2;
                    Color c = pixelesCrudos[by * w + bx];
                    float lum = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) * mascaraVignette;
                    lum = Mathf.Pow(Mathf.Clamp01(lum * 2.55f), 0.72f) * scan;

                    Color outCol;
                    if (lum < 0.15f)
                        outCol = Color.black;
                    else if (lum < 0.36f)
                        outCol = new Color(0.28f * scan, 0.28f * scan, 0.30f * scan, 1f);
                    else if (lum < 0.62f)
                        outCol = new Color(0.58f * scan, 0.58f * scan, 0.60f * scan, 1f);
                    else
                        outCol = new Color(0.94f * scan, 0.94f * scan, 0.92f * scan, 1f);

                    basePosterizada[y * w + x] = outCol;
                }
            }
        }
        else
        {
            GenerarPixelesSiluetaFallback(basePosterizada, w, h);
        }

        // Generar 4 variantes con desgarros horizontales de cinta VHS y macrobloques distintos
        Texture2D[] variantes = new Texture2D[4];
        for (int v = 0; v < 4; v++)
        {
            Texture2D texVar = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = $"Tex_Retrato_VHS_{tipo}_F{v}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] copia = (Color[])basePosterizada.Clone();
            int numDesgarros = 4 + v * 2;
            for (int d = 0; d < numDesgarros; d++)
            {
                int yInicio = Random.Range(8, h - 26);
                int altoBanda = Random.Range(5, 22);
                int shiftX = Random.Range(-24, 25);
                if (Mathf.Abs(shiftX) < 5) shiftX = (shiftX >= 0 ? 9 : -9);

                for (int yy = yInicio; yy < Mathf.Min(h, yInicio + altoBanda); yy++)
                {
                    bool lineaNieve = (yy == yInicio || yy == yInicio + altoBanda - 1) && Random.value < 0.45f;
                    for (int xx = 0; xx < w; xx++)
                    {
                        int srcX = xx - shiftX;
                        Color colDesgarrado = (srcX >= 0 && srcX < w) ? basePosterizada[yy * w + srcX] : Color.black;
                        if (lineaNieve && colDesgarrado.r > 0.05f && Random.value < 0.35f)
                        {
                            float g = Random.Range(0.55f, 0.95f);
                            colDesgarrado = new Color(g, g, g, 1f);
                        }
                        copia[yy * w + xx] = colDesgarrado;
                    }
                }
            }

            texVar.SetPixels(copia);
            texVar.Apply();
            texturasTemporalesVHS.Add(texVar);
            variantes[v] = texVar;
        }

        return variantes;
    }

    private static void GenerarPixelesSiluetaFallback(Color[] pixels, int w, int h)
    {
        for (int y = 0; y < h; y++)
        {
            float ny = (y / (float)h) * 2f - 1f;
            float scan = (y % 2 == 0) ? 0.84f : 1f;
            for (int x = 0; x < w; x++)
            {
                float nx = (x / (float)w) * 2f - 1f;
                float cabeza = (nx * nx) / 0.36f + ((ny - 0.10f) * (ny - 0.10f)) / 0.50f;
                float ojoIzq = ((nx + 0.23f) * (nx + 0.23f)) / 0.020f + ((ny - 0.16f) * (ny - 0.16f)) / 0.026f;
                float ojoDer = ((nx - 0.23f) * (nx - 0.23f)) / 0.020f + ((ny - 0.16f) * (ny - 0.16f)) / 0.026f;
                float boca   = (nx * nx) / 0.10f + ((ny + 0.24f) * (ny + 0.24f)) / 0.055f;

                float ruido = Mathf.PerlinNoise(nx * 5.0f + 2.1f, ny * 5.8f + 3.7f);
                Color c = Color.black;

                if (cabeza <= 1.0f + (ruido - 0.5f) * 0.24f)
                {
                    if (ojoIzq < 1f || ojoDer < 1f || boca < 1f)
                    {
                        c = (ojoIzq < 0.26f || ojoDer < 0.26f) ? new Color(0.95f, 0.95f, 0.95f, 1f) : Color.black;
                    }
                    else
                    {
                        float val = (1.0f - cabeza * 0.78f) + ruido * 0.48f;
                        if (val > 0.72f) c = new Color(0.92f * scan, 0.92f * scan, 0.92f * scan, 1f);
                        else if (val > 0.44f) c = new Color(0.48f * scan, 0.48f * scan, 0.50f * scan, 1f);
                    }
                }
                pixels[y * w + x] = c;
            }
        }
    }

    private Texture2D GenerarTexturaScanlinesYRuidoVHS()
    {
        int w = 256;
        int h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Tex_Scanlines_Ruido_VHS",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        Color[] cols = new Color[w * h];
        int banda1 = 48;
        int banda2 = 176;

        for (int y = 0; y < h; y++)
        {
            bool esScanOscura = (y % 2 == 0);
            float distB1 = Mathf.Abs(y - banda1);
            float distB2 = Mathf.Abs(y - banda2);
            float fuerzaBanda = Mathf.Max(
                Mathf.Clamp01(1f - distB1 / 14f),
                Mathf.Clamp01(1f - distB2 / 9f) * 0.75f
            );

            for (int x = 0; x < w; x++)
            {
                if (fuerzaBanda > 0.05f && Random.value < fuerzaBanda * 0.42f)
                {
                    // Motas horizontales de estática VHS con sutil tinte cromático analógico
                    float v = Random.Range(0.65f, 1.0f);
                    float a = fuerzaBanda * Random.Range(0.18f, 0.52f);
                    cols[y * w + x] = new Color(v, v * Random.Range(0.90f, 1.0f), v, a);
                }
                else if (Random.value < 0.008f)
                {
                    cols[y * w + x] = new Color(0.9f, 0.9f, 0.95f, 0.16f);
                }
                else if (esScanOscura)
                {
                    cols[y * w + x] = new Color(0f, 0f, 0f, 0.22f);
                }
                else
                {
                    cols[y * w + x] = new Color(0f, 0f, 0f, 0f);
                }
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        texturasTemporalesVHS.Add(tex);
        return tex;
    }

    private static string ObtenerNombreObjetoEntidad(TipoAnomalia tipo)
    {
        switch (tipo)
        {
            case TipoAnomalia.Flawed:   return "Anomalia_Flawed_Recamara";
            case TipoAnomalia.PureForm: return "Anomalia_PureForm_Sala";
            case TipoAnomalia.Preacher: return "Anomalia_Preacher_Closet";
            case TipoAnomalia.Tulpa:    return "Anomalia_Tulpa_EspejoBano";
            case TipoAnomalia.Corpse:   return "Anomalia_Corpse_Cocina";
            default:                    return "Anomalia_Flawed_Recamara";
        }
    }

    // =========================================================================
    // CANVAS CENTRADO Y AISLADO EN CAPA UI (SIN PUNTERO VR DURANTE LA CINEMÁTICA)
    // =========================================================================
    private void ConstruirCanvasCentradoEnCamara(Camera cam, bool habilitarInteraccionInmediata)
    {
        if (canvasFinal != null)
            Destroy(canvasFinal.gameObject);

        GameObject canvasObj = new GameObject("Canvas_Final_Turno_VR",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
        canvasObj.layer = CapaUI;

        canvasFinal = canvasObj.GetComponent<Canvas>();
        canvasFinal.renderMode = RenderMode.WorldSpace;
        canvasFinal.worldCamera = cam;
        canvasFinal.sortingOrder = 1000;

        // Mantener los raycasters apagados durante la cinemática de terror para que no aparezca el círculo del cursor VR en pantalla
        raycasterCanvas = canvasObj.GetComponent<GraphicRaycaster>();
        raycasterXRCanvas = canvasObj.GetComponent<TrackedDeviceGraphicRaycaster>();
        if (raycasterCanvas != null) raycasterCanvas.enabled = habilitarInteraccionInmediata;
        if (raycasterXRCanvas != null) raycasterXRCanvas.enabled = habilitarInteraccionInmediata;

        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(960f, 560f);

        if (cam != null)
        {
            canvasObj.transform.SetParent(cam.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 0f, 1.05f);
            canvasObj.transform.localRotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.one * 0.00125f;

            // Añadir RectMask2D al marco 16:9 del monitor VHS para recortar limpiamente los saltos macro y el estiramiento flatline
            canvasObj.AddComponent<RectMask2D>();
        }

        CrearImagenUI(canvasFinal.transform, "Bg_Negro_Puro", new Vector2(1800f, 1100f), Vector2.zero, Color.black).raycastTarget = false;
    }

    private void ConstruirLayoutDeathScreenVHS(Texture2D texRetrato, TipoAnomalia entidad, string motivo)
    {
        string nombreEntidad = TabletSistemaVR.ObtenerNombreTipoEspanol(entidad);
        Font fontTerror = ObtenerFuenteTerror();

        // 1. Retrato close-up VHS en blanco y negro a la derecha sobre negro absoluto (#000000)
        GameObject objRaw = new GameObject("Retrato_Anomalia_VHS", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        objRaw.layer = CapaUI;
        objRaw.transform.SetParent(canvasFinal.transform, false);
        RectTransform rtRaw = objRaw.GetComponent<RectTransform>();
        rtRaw.sizeDelta = new Vector2(420f, 495f);
        rtRaw.anchoredPosition = new Vector2(228f, 12f);

        imgRetratoVHS = objRaw.GetComponent<RawImage>();
        imgRetratoVHS.texture = texRetrato;
        imgRetratoVHS.color = Color.white;
        imgRetratoVHS.raycastTarget = false;

        // 2. Monólogo en 3 párrafos con aberración cromática RGB real (canal azul izquierdo + canal rojo derecho + fósforo blanco)
        textoMonologoBase =
            "Cuando te encontramos, apenas eras reconocible.\n" +
            "Tus ojos habían sido arrancados por completo, tu\n" +
            "caja torácica expuesta, una de tus manos había\n" +
            "desaparecido. Si no fuera por el registro de tu\n" +
            $"turno, no habríamos sabido quién eras antes de\n" +
            $"que la anomalía ({nombreEntidad}) te arruinara.\n\n" +
            "Seré honesto, fue bastante difícil limpiar lo que\n" +
            "quedó de ti. Realmente se ensañaron contigo, pero\n" +
            "supongo que eso pasa cuando las entidades no son\n" +
            "contenidas a tiempo. No merecías lo que te pasó,\n" +
            "es mi culpa que estuvieras en esta posición.\n\n" +
            "Tu sacrificio no habrá sido en vano, ya que nos\n" +
            "has comprado tiempo. Tiempo con el que podemos\n" +
            "trabajar para terminar esta locura en la casa.\n" +
            "Lo siento, vigilante. Desearía haber podido\n" +
            "hacer algo más por ti.";

        Vector2 tamTexto = new Vector2(520f, 460f);
        Vector2 posTexto = new Vector2(-202f, 8f);

        txtMonologoCanalAzul = CrearTextoUI(
            canvasFinal.transform,
            "Txt_Monologo_CanalAzul_VHS",
            textoMonologoBase,
            posTexto + new Vector2(-3.4f, -0.5f),
            tamTexto,
            22,
            TextAnchor.MiddleLeft,
            new Color(0.14f, 0.34f, 1.0f, 0.88f),
            FontStyle.Normal,
            fontTerror);
        rectMonologoAzul = txtMonologoCanalAzul.GetComponent<RectTransform>();
        rectMonologoAzul.localScale = new Vector3(0.98f, 1.32f, 1f);

        txtMonologoCanalRojo = CrearTextoUI(
            canvasFinal.transform,
            "Txt_Monologo_CanalRojo_VHS",
            textoMonologoBase,
            posTexto + new Vector2(3.1f, 1.0f),
            tamTexto,
            22,
            TextAnchor.MiddleLeft,
            new Color(0.94f, 0.16f, 0.12f, 0.82f),
            FontStyle.Normal,
            fontTerror);
        rectMonologoRojo = txtMonologoCanalRojo.GetComponent<RectTransform>();
        rectMonologoRojo.localScale = new Vector3(0.98f, 1.32f, 1f);

        txtMonologoDeathScreen = CrearTextoUI(
            canvasFinal.transform,
            "Txt_Monologo_VHS",
            textoMonologoBase,
            posTexto,
            tamTexto,
            22,
            TextAnchor.MiddleLeft,
            new Color(0.94f, 0.94f, 0.89f, 1f),
            FontStyle.Normal,
            fontTerror);
        rectMonologoDeathScreen = txtMonologoDeathScreen.GetComponent<RectTransform>();
        rectMonologoDeathScreen.localScale = new Vector3(0.98f, 1.32f, 1f);

        // 3. Capa global de scanlines CRT + bandas de nieve analógica VHS en movimiento
        GameObject objScan = new GameObject("Overlay_Scanlines_VHS", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        objScan.layer = CapaUI;
        objScan.transform.SetParent(canvasFinal.transform, false);
        RectTransform rtScan = objScan.GetComponent<RectTransform>();
        rtScan.sizeDelta = new Vector2(980f, 580f);
        rtScan.anchoredPosition = Vector2.zero;
        imgScanlinesVHS = objScan.GetComponent<RawImage>();
        imgScanlinesVHS.texture = GenerarTexturaScanlinesYRuidoVHS();
        imgScanlinesVHS.color = new Color(1f, 1f, 1f, 0.36f);
        imgScanlinesVHS.raycastTarget = false;

        // Pliegue sutil de tracking analógico (nunca una línea blanca rígida)
        imgBarraTracking = CrearImagenUI(canvasFinal.transform, "Barra_Tracking_VHS", new Vector2(980f, 16f), Vector2.zero,
            new Color(1f, 1f, 1f, 0.03f));
        imgBarraTracking.raycastTarget = false;
        barraTrackingVHS = imgBarraTracking.rectTransform;

        // 4. Panel de botones finales (aparece únicamente tras el corte a negro del flatline)
        panelBotonesDerrota = new GameObject("Panel_Botones_Derrota", typeof(RectTransform));
        panelBotonesDerrota.layer = CapaUI;
        panelBotonesDerrota.transform.SetParent(canvasFinal.transform, false);
        RectTransform rtPanel = panelBotonesDerrota.GetComponent<RectTransform>();
        rtPanel.sizeDelta = new Vector2(760f, 380f);
        rtPanel.anchoredPosition = Vector2.zero;

        string expediente = TabletSistemaVR.Instancia != null
            ? TabletSistemaVR.Instancia.ObtenerResumenExpedienteCognitivo()
            : "Hora del registro: Madrugada  |  Sector crítico: CASA";

        CrearTextoUI(panelBotonesDerrota.transform, "Titulo_Fin", "TURNO FALLIDO",
            new Vector2(0f, 130f), new Vector2(740f, 65f), 46, TextAnchor.MiddleCenter,
            new Color(0.90f, 0.18f, 0.18f, 1f), FontStyle.Bold, fontTerror);

        CrearTextoUI(panelBotonesDerrota.transform, "Subtitulo_Motivo",
            $"{motivo}\nEntidad atacante: {nombreEntidad}\n[EXPEDIENTE]: {expediente}",
            new Vector2(0f, 42f), new Vector2(820f, 96f), 23, TextAnchor.MiddleCenter,
            new Color(0.82f, 0.82f, 0.84f, 1f), FontStyle.Normal, fontTerror);

        CrearBotonUI(panelBotonesDerrota.transform, "Btn_Reintentar", "[ REINTENTAR TURNO ]  (Tecla R)",
            new Vector2(0f, -45f), new Vector2(460f, 64f),
            new Color(0.50f, 0.12f, 0.15f, 1f), Color.white, 26, ReintentarTurno, fontTerror);

        CrearBotonUI(panelBotonesDerrota.transform, "Btn_MenuPrincipal", "[ IR AL MENÚ PRINCIPAL ]  (Tecla M)",
            new Vector2(0f, -125f), new Vector2(460f, 64f),
            new Color(0.14f, 0.16f, 0.20f, 1f), new Color(0.92f, 0.92f, 0.94f, 1f), 26, IrAlMenuPrincipal, fontTerror);

        panelBotonesDerrota.SetActive(false);
    }

    private void ConstruirLayoutVictoria6AM(int contenidas, int fallidos)
    {
        Font fontTerror = ObtenerFuenteTerror();

        panelVictoria = new GameObject("Panel_Victoria_6AM", typeof(RectTransform));
        panelVictoria.layer = CapaUI;
        panelVictoria.transform.SetParent(canvasFinal.transform, false);
        RectTransform rt = panelVictoria.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(780f, 460f);
        rt.anchoredPosition = Vector2.zero;

        CrearImagenUI(panelVictoria.transform, "Marco_Victoria", new Vector2(760f, 440f), Vector2.zero,
            new Color(0.05f, 0.08f, 0.10f, 0.98f)).raycastTarget = false;

        int totalIntentos = contenidas + fallidos;
        int precision = totalIntentos > 0 ? Mathf.RoundToInt((contenidas * 100f) / totalIntentos) : 100;
        string rango = (precision >= 90 && fallidos <= 1) ? "S (MEMORIA PERFECTA)"
                     : (precision >= 75) ? "A (VIGILANTE EXPERTO)"
                     : "B (SOBREVIVIENTE)";

        CrearTextoUI(panelVictoria.transform, "Txt_6AM", "06:00 AM — ¡TURNO COMPLETADO!",
            new Vector2(0f, 145f), new Vector2(720f, 64f), 44, TextAnchor.MiddleCenter,
            new Color(0.45f, 0.95f, 0.55f, 1f), FontStyle.Bold, fontTerror);

        string expediente = TabletSistemaVR.Instancia != null
            ? TabletSistemaVR.Instancia.ObtenerResumenExpedienteCognitivo()
            : "Hora del registro: 06:00 AM";

        string resumen =
            $"Anomalías Contenidas:   {contenidas}\n" +
            $"Reportes Fallidos:      {fallidos}\n" +
            $"Precisión Cognitiva:    {precision}%\n" +
            $"{expediente}\n\n" +
            $"RANGO OBTENIDO:  {rango}";

        CrearTextoUI(panelVictoria.transform, "Txt_Estadisticas", resumen,
            new Vector2(0f, 12f), new Vector2(740f, 215f), 25, TextAnchor.MiddleCenter,
            Color.white, FontStyle.Normal, fontTerror);

        CrearBotonUI(panelVictoria.transform, "Btn_SalirMenu_Victoria", "[ IR AL MENÚ PRINCIPAL ]  (Tecla M)",
            new Vector2(0f, -140f), new Vector2(480f, 66f),
            new Color(0.15f, 0.44f, 0.22f, 1f), Color.white, 27, IrAlMenuPrincipal, fontTerror);
    }

    public void ReintentarTurno()
    {
        EstaEnSecuenciaFinal = false;
        Time.timeScale = 1f;
        Scene actual = SceneManager.GetActiveScene();
        SceneManager.LoadScene(actual.name);
    }

    public void IrAlMenuPrincipal()
    {
        EstaEnSecuenciaFinal = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(EscenaMenuPrincipal);
    }

    private void SilenciarAudiosDeLaCasa()
    {
        AudioSource[] todos = FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
        for (int i = 0; i < todos.Length; i++)
        {
            if (todos[i] != null && todos[i] != fuenteAudioFinal)
                todos[i].Stop();
        }
    }

    private void DeshabilitarRayosUIDuranteCinematica()
    {
        foreach (XRRayInteractor ray in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include))
        {
            if (ray == null) continue;
            ray.enableUIInteraction = false;
        }
    }

    private void HabilitarRayosUIMandos()
    {
        if (raycasterCanvas != null) raycasterCanvas.enabled = true;
        if (raycasterXRCanvas != null) raycasterXRCanvas.enabled = true;

        if (EventSystem.current == null)
        {
            new GameObject("EventSystem VR", typeof(EventSystem), typeof(XRUIInputModule));
        }
        foreach (XRRayInteractor ray in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include))
        {
            if (ray == null) continue;
            ray.enableUIInteraction = true;
            ray.blockUIOnInteractableSelection = false;
            ray.allowHoveredActivate = true;
        }
    }

    private static Image CrearImagenUI(Transform padre, string nombre, Vector2 size, Vector2 pos, Color col)
    {
        GameObject obj = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = CapaUI;
        obj.transform.SetParent(padre, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        Image img = obj.GetComponent<Image>();
        img.color = col;
        return img;
    }

    private static Text CrearTextoUI(Transform padre, string nombre, string contenido, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color col, FontStyle style, Font customFont)
    {
        GameObject obj = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        obj.layer = CapaUI;
        obj.transform.SetParent(padre, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        Text t = obj.GetComponent<Text>();
        t.font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = contenido;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = align;
        t.color = col;
        t.lineSpacing = 0.98f;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static Button CrearBotonUI(Transform padre, string nombre, string texto, Vector2 pos, Vector2 size, Color bgCol, Color txtCol, int fontSize, UnityEngine.Events.UnityAction onClick, Font customFont)
    {
        Image img = CrearImagenUI(padre, nombre, size, pos, bgCol);
        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(onClick);

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.88f, 0.88f, 0.94f, 1f);
        cb.pressedColor = new Color(0.65f, 0.95f, 0.65f, 1f);
        btn.colors = cb;

        CrearTextoUI(img.transform, "Texto", texto, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, txtCol, FontStyle.Bold, customFont);
        return btn;
    }

    private static void AsignarCapaRecursiva(Transform raiz, int capa)
    {
        if (raiz == null) return;
        raiz.gameObject.layer = capa;
        for (int i = 0; i < raiz.childCount; i++)
            AsignarCapaRecursiva(raiz.GetChild(i), capa);
    }

    private void AsegurarAudiosFallback()
    {
#if UNITY_EDITOR
        if (sonidoScreamer == null)
            sonidoScreamer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_screamer_anomalia.wav");
        if (sonidoAlarmaVHS == null)
            sonidoAlarmaVHS = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_deathscreen_vhs_alarm.wav");
        if (sonidoFlatline == null)
            sonidoFlatline = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_deathscreen_flatline.wav");
        if (sonidoVictoria6AM == null)
            sonidoVictoria6AM = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/sfx_victoria_6am.wav");
#endif
        int sr = 22050;

        if (sonidoScreamer == null)
        {
            int n = Mathf.RoundToInt(sr * 1.1f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t * 1.2f);
                float f = 540f + 350f * Mathf.Exp(-t * 3f) + Mathf.Sin(2f * Mathf.PI * 24f * t) * 35f;
                float s = Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * f * 1.48f * t) * 0.35f + (Random.value * 2f - 1f) * 0.45f;
                d[i] = Mathf.Clamp(s * 2.2f, -1f, 1f) * env * 0.85f;
            }
            sonidoScreamer = AudioClip.Create("ScreamerFallback", n, 1, sr, false);
            sonidoScreamer.SetData(d, 0);
        }

        if (sonidoAlarmaVHS == null)
        {
            int n = Mathf.RoundToInt(sr * 5.9f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float pulse = 0.55f + 0.45f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 5.8f * t));
                float saw = 2f * ((t * 310f) - Mathf.Floor(0.5f + t * 310f));
                d[i] = Mathf.Clamp((saw * 0.55f + (Random.value * 2f - 1f) * 0.45f) * pulse, -1f, 1f) * 0.7f;
            }
            sonidoAlarmaVHS = AudioClip.Create("VHSAlarmFallback", n, 1, sr, false);
            sonidoAlarmaVHS.SetData(d, 0);
        }

        if (sonidoFlatline == null)
        {
            int n = Mathf.RoundToInt(sr * 2.48f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 1005f * t) * 0.78f + Mathf.Sin(2f * Mathf.PI * 2010f * t) * 0.2f) * 0.65f;
            }
            sonidoFlatline = AudioClip.Create("FlatlineFallback", n, 1, sr, false);
            sonidoFlatline.SetData(d, 0);
        }

        if (sonidoVictoria6AM == null)
        {
            int n = Mathf.RoundToInt(sr * 2.2f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Exp(-t * 1.5f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.45f + Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.25f) * env * 0.7f;
            }
            sonidoVictoria6AM = AudioClip.Create("VictoriaFallback", n, 1, sr, false);
            sonidoVictoria6AM.SetData(d, 0);
        }
    }
}

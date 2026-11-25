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
/// con fuente de terror VHS, glitch constante en texto/retrato y aislamiento total de cámara para que ni muebles
/// ni la Tablet tapen la pantalla; más Pantalla de Victoria a las 06:00 AM).
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
    private RawImage imgRetratoVHS;
    private RawImage imgScanlinesVHS;
    private Text txtMonologoDeathScreen;
    private Text txtMonologoSombraGlitch;
    private RectTransform rectMonologoDeathScreen;
    private RectTransform rectMonologoSombra;
    private RectTransform barraTrackingVHS;
    private GameObject panelBotonesDerrota;
    private GameObject panelVictoria;
    private string textoMonologoBase = "";

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
        if (Instancia == this)
        {
            EstaEnSecuenciaFinal = false;
            Instancia = null;
        }
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
    // SECUENCIA 1:1 — SCREAMER + DEATH SCREEN VHS CON GLITCH CONSTANTE
    // =========================================================================
    private IEnumerator RutinaScreamerYDeathScreen(TipoAnomalia entidadAtacante, string motivoDerrota)
    {
        Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        SilenciarAudiosDeLaCasa();

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
            // Aislar la cámara en la capa del Screamer para que salga de la oscuridad pura sin que el sofá ni los controles lo tapen
            cam.cullingMask = (1 << CapaScreamer);
        }

        // 1. Instanciar el modelo 3D de la anomalía en la capa exclusiva del Screamer, encuadrando rostro y torso
        GameObject modeloScreamer = InstanciarModeloEntidadParaScreamer(entidadAtacante, cam != null ? cam.transform : transform);

        // Capturar retrato VHS en blanco y negro de altísimo contraste de esta misma entidad
        Texture2D texturaRetratoVHS = CapturarRetratoVHSPosterizado(modeloScreamer, entidadAtacante);

        // Luz puntual intensa sobre el rostro del screamer
        GameObject objLuzScreamer = new GameObject("Luz_Screamer_VR");
        objLuzScreamer.layer = CapaScreamer;
        if (cam != null) objLuzScreamer.transform.SetParent(cam.transform, false);
        objLuzScreamer.transform.localPosition = new Vector3(0f, 0.05f, 0.28f);
        Light luzScreamer = objLuzScreamer.AddComponent<Light>();
        luzScreamer.type = LightType.Point;
        luzScreamer.color = new Color(1f, 0.90f, 0.88f, 1f);
        luzScreamer.intensity = 4.8f;
        luzScreamer.range = 2.6f;
        luzScreamer.shadows = LightShadows.None;
        luzScreamer.renderMode = LightRenderMode.ForcePixel;
        luzScreamer.cullingMask = (1 << CapaScreamer);

        if (fuenteAudioFinal != null && sonidoScreamer != null)
        {
            fuenteAudioFinal.Stop();
            fuenteAudioFinal.clip = sonidoScreamer;
            fuenteAudioFinal.volume = 0.95f;
            fuenteAudioFinal.loop = false;
            fuenteAudioFinal.Play();
        }

        // Sacudida violenta frente al visor (0.00 s -> 1.15 s)
        float duracionScreamer = 1.15f;
        float tScreamer = 0f;
        Vector3 posBaseScreamer = modeloScreamer != null ? modeloScreamer.transform.localPosition : new Vector3(0f, -0.5f, 0.72f);
        Quaternion rotBaseScreamer = modeloScreamer != null ? modeloScreamer.transform.localRotation : Quaternion.Euler(0f, 180f, 0f);

        while (tScreamer < duracionScreamer)
        {
            tScreamer += Time.unscaledDeltaTime;

            if (TabletSistemaVR.Instancia != null)
                TabletSistemaVR.Instancia.EnviarHapticAmbosMandos(1.0f, 0.1f);

            if (modeloScreamer != null)
            {
                float progreso = Mathf.Clamp01(tScreamer / duracionScreamer);
                float acercamientoZ = Mathf.Lerp(0.14f, -0.05f, progreso);
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

            if (luzScreamer != null)
                luzScreamer.intensity = Random.Range(2.2f, 5.8f);

            yield return null;
        }

        // 2. CORTE BRUSCO A NEGRO ABSOLUTO + DEATH SCREEN VHS (00:01 -> 00:07 del video)
        if (modeloScreamer != null) Destroy(modeloScreamer);
        if (objLuzScreamer != null) Destroy(objLuzScreamer);
        if (fuenteAudioFinal != null) fuenteAudioFinal.Stop();

        // Cambiar cullingMask exclusivamente a UI (Capa 5) sobre fondo negro puro:
        // Así NINGÚN mueble, sofá, pared ni mando 3D puede tapar la Death Screen
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = (1 << CapaUI);
        }

        ConstruirCanvasCentradoEnCamara(cam);
        ConstruirLayoutDeathScreenVHS(texturaRetratoVHS, entidadAtacante, motivoDerrota);

        if (fuenteAudioFinal != null && sonidoAlarmaVHS != null)
        {
            fuenteAudioFinal.clip = sonidoAlarmaVHS;
            fuenteAudioFinal.volume = 0.80f;
            fuenteAudioFinal.loop = true;
            fuenteAudioFinal.Play();
        }

        // 00:01 a 00:02 (0.85 s): solo se ve el retrato VHS a la derecha con glitch analógico
        if (txtMonologoDeathScreen != null) txtMonologoDeathScreen.gameObject.SetActive(false);
        if (txtMonologoSombraGlitch != null) txtMonologoSombraGlitch.gameObject.SetActive(false);

        Vector2 posBaseRetrato = new Vector2(240f, -5f);
        Vector2 posBaseTexto = new Vector2(-185f, 10f);
        RectTransform rectRetrato = imgRetratoVHS != null ? imgRetratoVHS.rectTransform : null;

        float tFaseRetrato = 0f;
        while (tFaseRetrato < 0.85f)
        {
            tFaseRetrato += Time.unscaledDeltaTime;
            AplicarGlitchContinuoVHS(rectRetrato, posBaseRetrato, false, 1f);
            yield return null;
        }

        // 00:02 a 00:06.8 (4.85 s): Entra el monólogo con fuente de terror VHS y GLITCH CONSTANTE en texto y retrato
        if (txtMonologoDeathScreen != null) txtMonologoDeathScreen.gameObject.SetActive(true);
        if (txtMonologoSombraGlitch != null) txtMonologoSombraGlitch.gameObject.SetActive(true);

        float duracionMonologo = 4.85f;
        float tMonologo = 0f;
        while (tMonologo < duracionMonologo)
        {
            tMonologo += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(tMonologo / duracionMonologo);
            float brilloRetrato = p < 0.45f ? 1f : Mathf.Lerp(1f, 0.20f, (p - 0.45f) / 0.55f);

            AplicarGlitchContinuoVHS(rectRetrato, posBaseRetrato, true, brilloRetrato);

            // Vibración constante del texto + desgarros horizontales de tracking VHS cada pocos frames
            if (rectMonologoDeathScreen != null)
            {
                bool saltoTracking = Random.value < 0.22f;
                float tearX = saltoTracking ? Random.Range(-14f, 14f) : Random.Range(-2.2f, 2.2f);
                float tearY = Random.Range(-1.8f, 1.8f);
                rectMonologoDeathScreen.anchoredPosition = posBaseTexto + new Vector2(tearX, tearY);

                // Ligera fluctuación de escala vertical como cinta VHS inestable
                float escY = 1.24f + (saltoTracking ? Random.Range(-0.08f, 0.12f) : Random.Range(-0.02f, 0.02f));
                rectMonologoDeathScreen.localScale = new Vector3(1f, escY, 1f);

                if (rectMonologoSombra != null)
                {
                    rectMonologoSombra.anchoredPosition = rectMonologoDeathScreen.anchoredPosition + new Vector2(saltoTracking ? Random.Range(-8f, 8f) : 3.5f, -1.5f);
                    rectMonologoSombra.localScale = rectMonologoDeathScreen.localScale;
                }
            }

            // Corrupción breve de caracteres en el texto VHS
            if (txtMonologoDeathScreen != null)
            {
                if (Random.value < 0.28f)
                    txtMonologoDeathScreen.text = CorromperTextoBreveVHS(textoMonologoBase);
                else
                    txtMonologoDeathScreen.text = textoMonologoBase;

                float parpadeoAlfa = Random.value < 0.15f ? Random.Range(0.68f, 0.88f) : 0.96f;
                txtMonologoDeathScreen.color = new Color(parpadeoAlfa, parpadeoAlfa, parpadeoAlfa, 1f);
            }

            yield return null;
        }

        // 3. 00:06.8 a 00:08.5 (1.65 s): CORTE EN SECO A PITIDO FLATLINE + TEXTO ESTIRADO VERTICALMENTE CON TEMBLOR
        if (fuenteAudioFinal != null)
        {
            fuenteAudioFinal.Stop();
            fuenteAudioFinal.loop = false;
            if (sonidoFlatline != null)
            {
                fuenteAudioFinal.clip = sonidoFlatline;
                fuenteAudioFinal.volume = 0.72f;
                fuenteAudioFinal.Play();
            }
        }

        if (txtMonologoDeathScreen != null)
            txtMonologoDeathScreen.text = textoMonologoBase;

        if (TabletSistemaVR.Instancia != null)
            TabletSistemaVR.Instancia.EnviarHapticAmbosMandos(0.45f, 0.25f);

        float tFlatline = 0f;
        while (tFlatline < 1.65f)
        {
            tFlatline += Time.unscaledDeltaTime;
            AplicarGlitchContinuoVHS(rectRetrato, posBaseRetrato, true, 0.12f);

            if (rectMonologoDeathScreen != null)
            {
                rectMonologoDeathScreen.anchoredPosition = new Vector2(-110f + Random.Range(-3.5f, 3.5f), -30f + Random.Range(-3f, 3f));
                rectMonologoDeathScreen.localScale = new Vector3(1.52f, 2.75f + Random.Range(-0.06f, 0.06f), 1f);
                if (rectMonologoSombra != null)
                {
                    rectMonologoSombra.anchoredPosition = rectMonologoDeathScreen.anchoredPosition + new Vector2(5f, -2f);
                    rectMonologoSombra.localScale = rectMonologoDeathScreen.localScale;
                }
            }
            yield return null;
        }

        // 4. 00:08.5: CORTE A NEGRO EN SILENCIO Y APARICIÓN DE BOTONES (REINTENTAR / IR AL MENÚ PRINCIPAL)
        if (fuenteAudioFinal != null)
            fuenteAudioFinal.Stop();

        if (txtMonologoDeathScreen != null) txtMonologoDeathScreen.gameObject.SetActive(false);
        if (txtMonologoSombraGlitch != null) txtMonologoSombraGlitch.gameObject.SetActive(false);
        if (imgRetratoVHS != null) imgRetratoVHS.gameObject.SetActive(false);
        if (barraTrackingVHS != null) barraTrackingVHS.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(0.65f);

        if (panelBotonesDerrota != null)
            panelBotonesDerrota.SetActive(true);

        HabilitarRayosUIMandos();
    }

    private void AplicarGlitchContinuoVHS(RectTransform rectRetrato, Vector2 posBaseRetrato, bool intenso, float brilloBase)
    {
        if (rectRetrato != null)
        {
            float amp = intenso ? 5.0f : 3.2f;
            float saltoX = (Random.value < 0.18f) ? Random.Range(-14f, 14f) : Random.Range(-amp, amp);
            float saltoY = Random.Range(-amp * 0.7f, amp * 0.7f);
            rectRetrato.anchoredPosition = posBaseRetrato + new Vector2(saltoX, saltoY);
        }

        if (imgRetratoVHS != null)
        {
            float flicker = (Random.value < 0.20f) ? brilloBase * Random.Range(0.65f, 1.15f) : brilloBase;
            imgRetratoVHS.color = new Color(flicker, flicker, flicker, 1f);
        }

        if (barraTrackingVHS != null)
        {
            float y = Mathf.Repeat(Time.unscaledTime * 260f, 620f) - 310f;
            barraTrackingVHS.anchoredPosition = new Vector2(0f, y);
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

        ConstruirCanvasCentradoEnCamara(cam);
        ConstruirLayoutVictoria6AM(anomaliasContenidas, reportesFallidos);
        HabilitarRayosUIMandos();

        yield break;
    }

    // =========================================================================
    // CLONADO 3D Y CAPTURA DE RETRATO VHS POSTERIZADO (BLANCO Y NEGRO 1:1)
    // =========================================================================
    private GameObject InstanciarModeloEntidadParaScreamer(TipoAnomalia tipo, Transform camTransform)
    {
        string nombreObjetoEscena = ObtenerNombreObjetoEntidad(tipo);
        GameObject original = GameObject.Find(nombreObjetoEscena);
        Transform visualOriginal = original != null ? original.transform.Find("Modelo_Visual_(Reemplazar_Por_FBX)") : null;

        GameObject contenedor = new GameObject("Screamer_Entidad_3D_VR");
        contenedor.layer = CapaScreamer;
        contenedor.transform.SetParent(camTransform, false);
        contenedor.transform.localPosition = new Vector3(0f, -0.65f, 0.76f);
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

            AsignarCapaRecursiva(clon.transform, CapaScreamer);

            Renderer[] rends = clon.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

                // Encuadrar rostro y pecho (82% de la altura total del modelo) directo frente al centro del lente de la cámara
                Vector3 posFrenteCam = camTransform.position + camTransform.forward * 0.76f;
                float alturaRostro = b.min.y + b.size.y * 0.80f;
                Vector3 delta = new Vector3(
                    posFrenteCam.x - b.center.x,
                    posFrenteCam.y - alturaRostro,
                    posFrenteCam.z - b.center.z
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

    private Texture2D CapturarRetratoVHSPosterizado(GameObject modeloScreamer, TipoAnomalia tipo)
    {
        int w = 256;
        int h = 320;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Tex_Retrato_VHS_" + tipo,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        bool capturaExitosa = false;

        if (modeloScreamer != null && Camera.main != null)
        {
            AsignarCapaRecursiva(modeloScreamer.transform, CapaScreamer);

            RenderTexture rt = RenderTexture.GetTemporary(w, h, 16, RenderTextureFormat.ARGB32);
            GameObject camObj = new GameObject("Cam_Snapshot_VHS");
            camObj.transform.position = Camera.main.transform.position;
            camObj.transform.rotation = Camera.main.transform.rotation;

            GameObject luzObj = new GameObject("Luz_Snapshot_VHS");
            luzObj.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 0.25f + Camera.main.transform.right * 0.16f;
            Light l = luzObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Color.white;
            l.intensity = 6.0f;
            l.range = 2.8f;
            l.cullingMask = (1 << CapaScreamer);

            Camera camSnap = camObj.AddComponent<Camera>();
            camSnap.clearFlags = CameraClearFlags.SolidColor;
            camSnap.backgroundColor = Color.black;
            camSnap.cullingMask = (1 << CapaScreamer);
            camSnap.fieldOfView = 44f;
            camSnap.nearClipPlane = 0.1f;
            camSnap.farClipPlane = 3.0f;
            camSnap.targetTexture = rt;
            camSnap.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;

            camSnap.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(luzObj);
            Destroy(camObj);

            Color[] pixels = tex.GetPixels();
            int pixelesBrillantes = 0;

            for (int y = 0; y < h; y++)
            {
                float scanline = (y % 3 == 0) ? 0.80f : 1.0f;
                int offsetJitter = (y % 17 == 0) ? Random.Range(-4, 5) : 0;

                for (int x = 0; x < w; x++)
                {
                    int sx = Mathf.Clamp(x + offsetJitter, 0, w - 1);
                    Color c = pixels[y * w + sx];
                    float lum = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) * scanline;
                    lum = Mathf.Pow(Mathf.Clamp01(lum * 2.45f), 0.75f);

                    Color outCol;
                    if (lum < 0.14f)
                    {
                        outCol = Color.black;
                    }
                    else if (lum < 0.46f)
                    {
                        pixelesBrillantes++;
                        float g = 0.44f * scanline;
                        outCol = new Color(g, g, g * 1.04f, 1f);
                    }
                    else
                    {
                        pixelesBrillantes++;
                        float b = 0.95f * scanline;
                        outCol = new Color(b, b, b, 1f);
                    }

                    pixels[y * w + x] = outCol;
                }
            }

            if (pixelesBrillantes > (w * h) / 30)
            {
                tex.SetPixels(pixels);
                tex.Apply();
                capturaExitosa = true;
            }
        }

        if (!capturaExitosa)
        {
            GenerarRetratoVHSSiluetaFallback(tex, w, h);
        }

        return tex;
    }

    private static void GenerarRetratoVHSSiluetaFallback(Texture2D tex, int w, int h)
    {
        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float ny = (y / (float)h) * 2f - 1f;
            float scan = (y % 3 == 0) ? 0.80f : 1f;
            for (int x = 0; x < w; x++)
            {
                float nx = (x / (float)w) * 2f - 1f;
                float cabeza = (nx * nx) / 0.38f + ((ny - 0.12f) * (ny - 0.12f)) / 0.52f;
                float ojoIzq = ((nx + 0.24f) * (nx + 0.24f)) / 0.018f + ((ny - 0.18f) * (ny - 0.18f)) / 0.024f;
                float ojoDer = ((nx - 0.24f) * (nx - 0.24f)) / 0.018f + ((ny - 0.18f) * (ny - 0.18f)) / 0.024f;
                float boca   = (nx * nx) / 0.11f + ((ny + 0.22f) * (ny + 0.22f)) / 0.045f;

                float ruido = Mathf.PerlinNoise(nx * 4.5f + 2.1f, ny * 5.2f + 3.7f);
                Color c = Color.black;

                if (cabeza <= 1.0f + (ruido - 0.5f) * 0.22f)
                {
                    if (ojoIzq < 1f || ojoDer < 1f || boca < 1f)
                    {
                        c = (ojoIzq < 0.28f || ojoDer < 0.28f) ? new Color(0.95f, 0.95f, 0.95f, 1f) : Color.black;
                    }
                    else
                    {
                        float val = (1.0f - cabeza * 0.75f) + ruido * 0.45f;
                        if (val > 0.72f) c = new Color(0.92f * scan, 0.92f * scan, 0.92f * scan, 1f);
                        else if (val > 0.42f) c = new Color(0.45f * scan, 0.45f * scan, 0.48f * scan, 1f);
                    }
                }
                pixels[y * w + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
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
    // CANVAS CENTRADO, DERECHO Y AISLADO EN CAPA UI (100% VISIBLE EN SIMULADOR Y QUEST 2)
    // =========================================================================
    private void ConstruirCanvasCentradoEnCamara(Camera cam)
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

        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(960f, 560f);

        if (cam != null)
        {
            canvasObj.transform.SetParent(cam.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 0f, 1.05f);
            canvasObj.transform.localRotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.one * 0.00125f;
        }

        CrearImagenUI(canvasFinal.transform, "Bg_Negro_Puro", new Vector2(1600f, 1000f), Vector2.zero, Color.black).raycastTarget = false;
    }

    private void ConstruirLayoutDeathScreenVHS(Texture2D texRetrato, TipoAnomalia entidad, string motivo)
    {
        string nombreEntidad = TabletSistemaVR.ObtenerNombreTipoEspanol(entidad);
        Font fontTerror = ObtenerFuenteTerror();

        // Retrato VHS en blanco y negro a la derecha (x = +240)
        GameObject objRaw = new GameObject("Retrato_Anomalia_VHS", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        objRaw.layer = CapaUI;
        objRaw.transform.SetParent(canvasFinal.transform, false);
        RectTransform rtRaw = objRaw.GetComponent<RectTransform>();
        rtRaw.sizeDelta = new Vector2(345f, 435f);
        rtRaw.anchoredPosition = new Vector2(240f, -5f);

        imgRetratoVHS = objRaw.GetComponent<RawImage>();
        imgRetratoVHS.texture = texRetrato;
        imgRetratoVHS.color = Color.white;
        imgRetratoVHS.raycastTarget = false;

        // Monólogo en 3 párrafos a la izquierda (x = -185) con fuente de terror VHS
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

        // Sombra fantasma cromática VHS detrás del texto para dar el efecto de sangrado analógico
        txtMonologoSombraGlitch = CrearTextoUI(
            canvasFinal.transform,
            "Txt_Monologo_Sombra_VHS",
            textoMonologoBase,
            new Vector2(-181f, 8f),
            new Vector2(510f, 450f),
            22,
            TextAnchor.MiddleLeft,
            new Color(0.55f, 0.14f, 0.16f, 0.48f),
            FontStyle.Normal,
            fontTerror);
        rectMonologoSombra = txtMonologoSombraGlitch.GetComponent<RectTransform>();
        rectMonologoSombra.localScale = new Vector3(1f, 1.24f, 1f);

        txtMonologoDeathScreen = CrearTextoUI(
            canvasFinal.transform,
            "Txt_Monologo_VHS",
            textoMonologoBase,
            new Vector2(-185f, 10f),
            new Vector2(510f, 450f),
            22,
            TextAnchor.MiddleLeft,
            new Color(0.94f, 0.94f, 0.94f, 1f),
            FontStyle.Normal,
            fontTerror);
        rectMonologoDeathScreen = txtMonologoDeathScreen.GetComponent<RectTransform>();
        rectMonologoDeathScreen.localScale = new Vector3(1f, 1.24f, 1f);

        // Barra horizontal de tracking VHS que recorre la pantalla
        Image imgBarra = CrearImagenUI(canvasFinal.transform, "Barra_Tracking_VHS", new Vector2(960f, 6f), Vector2.zero,
            new Color(1f, 1f, 1f, 0.10f));
        imgBarra.raycastTarget = false;
        barraTrackingVHS = imgBarra.rectTransform;

        // Panel de botones finales (aparece al terminar el pitido flatline)
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

    private void HabilitarRayosUIMandos()
    {
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
            int n = Mathf.RoundToInt(sr * 2.0f);
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
            int n = Mathf.RoundToInt(sr * 1.7f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 1046.5f * t) * 0.75f + Mathf.Sin(2f * Mathf.PI * 2093f * t) * 0.2f) * 0.6f;
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

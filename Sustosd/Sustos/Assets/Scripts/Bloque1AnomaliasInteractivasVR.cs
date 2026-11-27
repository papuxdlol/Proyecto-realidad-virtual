using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Tareas Base + Bloque 1 de Anomalías Interactivas (Ronda 2):
/// - Base B: Cuadros y pósters reales en las paredes para activar CUADROS / PINTURA (Imagery).
/// - Idea 1: Muebles interactivos (Cajones/Gabinetes) -> Displacement | Aparatos (TV, Refri, Microondas) -> Electrical.
/// - Idea 2: Relojes despertadores (Alarm_clock*) vivos con hora real del turno y anomalía 66:66 -> Electrical.
/// - Idea 3: Teléfonos (Phone*) y Radio (Radio_01.001) que se encienden/suenan solos con interferencia -> Electrical.
/// - Evento Nuevo: Puerta Imposible (Unknown) que se abre lentamente al mirarla hacia un pasillo negro abismal y mata si entras.
/// </summary>
public class Bloque1AnomaliasInteractivasVR : MonoBehaviour
{
    public static Bloque1AnomaliasInteractivasVR Instancia;

    [Header("Audios Reales (Teléfonos Análogos, Tic-Tac de Relojes y Estática de TV)")]
    public AudioClip clipTelefonoAnalogo;
    public AudioClip clipTicTacReloj;
    public AudioClip clipEstaticaTV;

    private struct RelojDespertadorVivo
    {
        public ObjetoAnomalia anomalia;
        public TextMeshPro textoLED;
        public AudioSource audioTicTac;
    }

    private struct TelefonoAnalogoVivo
    {
        public ObjetoAnomalia anomalia;
        public AudioSource audioTimbre;
        public float proximoTimbrazo;
    }

    private readonly List<RelojDespertadorVivo> relojesEnCasa = new List<RelojDespertadorVivo>();
    private readonly List<TelefonoAnalogoVivo> telefonosEnCasa = new List<TelefonoAnalogoVivo>();
    private float temporizadorActualizacionReloj = 0f;

    private static Material materialPinturaNormal;
    private static Material materialPinturaCorrupta;
    private static AudioClip clipAlarmaDespertador;
    private static AudioClip clipTimbreTelefonoInterferencia;

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(this);
            return;
        }
        Instancia = this;
        AsegurarClipsRealesPorDefecto();
        InicializarTodoBloque1();
    }

    private void AsegurarClipsRealesPorDefecto()
    {
#if UNITY_EDITOR
        if (clipTelefonoAnalogo == null)
            clipTelefonoAnalogo = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/Telefono.mp3");
        if (clipTicTacReloj == null)
            clipTicTacReloj = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/23newsound/tic-tac-emilly.mp3");
        if (clipEstaticaTV == null)
            clipEstaticaTV = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/yourugor-tv-static-noise-291374.mp3");
#endif
    }

    void Start()
    {
        if (TabletSistemaVR.Instancia != null)
            TabletSistemaVR.Instancia.RefrescarListaAnomalias();
    }

    void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }

    public void InicializarTodoBloque1()
    {
        AsegurarClipsRealesPorDefecto();
        AsegurarCuadrosImageryEnParedes();
        AsegurarAnomaliaPuertaAbismalUnknown();
        VentanasYLimitesVR.ConstruirLetrerosHabitaciones();
        ConfigurarMueblesAparatosRelojesYTelefonos();
    }

    void Update()
    {
        if (VRPauseMenu.IsMenuOpen || FinalTurnoVR.EstaEnSecuenciaFinal) return;

        bool hayEnergia = InterruptorLuz.EnergiaGeneralCasaActiva;

        // Cadencia realista de teléfono análogo (timbrazo de ~1.08s seguido de ~1.75s de silencio)
        for (int t = 0; t < telefonosEnCasa.Count; t++)
        {
            TelefonoAnalogoVivo tel = telefonosEnCasa[t];
            if (tel.anomalia == null || tel.audioTimbre == null) continue;

            bool sonando = tel.anomalia.estaActiva && tel.anomalia.tipoActivoActual == TipoAnomalia.Electrical;
            if (sonando)
            {
                tel.audioTimbre.volume = 0.52f;
                if (Time.time >= tel.proximoTimbrazo)
                {
                    AudioClip cTel = clipTelefonoAnalogo != null ? clipTelefonoAnalogo : ObtenerClipTimbreTelefono();
                    if (cTel != null)
                        tel.audioTimbre.PlayOneShot(cTel, 0.52f);
                    tel.proximoTimbrazo = Time.time + 2.85f;
                    telefonosEnCasa[t] = tel;
                }
            }
            else if (tel.proximoTimbrazo != 0f || tel.audioTimbre.isPlaying)
            {
                if (tel.audioTimbre.isPlaying)
                {
                    tel.audioTimbre.volume = Mathf.MoveTowards(tel.audioTimbre.volume, 0f, Time.deltaTime * 2.8f);
                    if (tel.audioTimbre.volume <= 0.01f)
                        tel.audioTimbre.Stop();
                }
                tel.proximoTimbrazo = 0f;
                telefonosEnCasa[t] = tel;
            }
        }

        if (relojesEnCasa.Count == 0) return;

        temporizadorActualizacionReloj += Time.deltaTime;
        if (temporizadorActualizacionReloj < 0.25f) return;
        temporizadorActualizacionReloj = 0f;

        TabletSistemaVR tablet = TabletSistemaVR.Instancia;
        string textoHoraNormal = "12:00";

        if (tablet != null)
        {
            if (tablet.EstaEnInspeccion)
                textoHoraNormal = "11:55";
            else
                textoHoraNormal = (tablet.HoraActual == 0) ? "12:00" : $"0{tablet.HoraActual}:00";
        }

        bool parpadeo = (Mathf.FloorToInt(Time.time * 4.5f) % 2) == 0;
        string[] textosCorruptos = { "66:66", "ER:OR", "88:88", "00:00" };
        string txtCorruptoActual = textosCorruptos[Mathf.FloorToInt(Time.time * 3f) % textosCorruptos.Length];

        for (int i = 0; i < relojesEnCasa.Count; i++)
        {
            RelojDespertadorVivo r = relojesEnCasa[i];
            bool relojAnomalo = r.anomalia != null && r.anomalia.estaActiva && r.anomalia.tipoActivoActual == TipoAnomalia.Electrical;

            // Tic-tac 3D sutil en los relojes de las recámaras; al corromperse a 66:66 acelera/distorsiona su compás
            if (r.audioTicTac != null && r.audioTicTac.clip != null)
            {
                if (!r.audioTicTac.isPlaying)
                {
                    r.audioTicTac.time = Random.Range(0f, Mathf.Max(0f, r.audioTicTac.clip.length - 2f));
                    r.audioTicTac.Play();
                }

                if (relojAnomalo && hayEnergia)
                {
                    r.audioTicTac.volume = 0.24f;
                    r.audioTicTac.maxDistance = 6.2f;
                    r.audioTicTac.pitch = 1.36f + Mathf.Sin(Time.time * 5.2f) * 0.22f;
                }
                else
                {
                    r.audioTicTac.volume = 0.065f;
                    r.audioTicTac.maxDistance = 3.4f;
                    r.audioTicTac.pitch = 1.0f;
                }
            }

            if (r.textoLED == null) continue;

            if (!hayEnergia)
            {
                r.textoLED.text = "";
                continue;
            }

            if (relojAnomalo)
            {
                r.textoLED.text = parpadeo ? txtCorruptoActual : "66:66";
                r.textoLED.color = parpadeo ? new Color(1f, 0.12f, 0.12f, 1f) : new Color(0.65f, 0.05f, 0.05f, 1f);
            }
            else
            {
                r.textoLED.text = textoHoraNormal;
                r.textoLED.color = new Color(0.22f, 0.95f, 0.42f, 0.95f);
            }
        }
    }

    // =========================================================================
    // BASE B: CUADROS REALES EN PAREDES PARA ACTIVAR "CUADROS / PINTURA (Imagery)"
    // =========================================================================
    public static void AsegurarCuadrosImageryEnParedes()
    {
        GameObject house = GameObject.Find("House");
        if (house == null) return;

        // 5 ubicaciones sobre muros interiores reales (Sala, Cocina, Recámara 1, Recámara 2, Recámara 3)
        (string nombre, string cuarto, Vector3 centroBusqueda, Vector3 dirHaciaPared)[] cuadros = new[]
        {
            ("Cuadro_Imagery_Sala",      "LivingRoom", new Vector3(13.20f, 1.78f,  0.50f), Vector3.left),
            ("Cuadro_Imagery_Cocina",    "Kitchen",    new Vector3(14.55f, 1.78f, -6.30f), Vector3.right),
            ("Cuadro_Imagery_Recamara1", "Bedroom1",   new Vector3(12.20f, 5.10f, -0.50f), Vector3.left),
            ("Cuadro_Imagery_Recamara2", "Bedroom2",   new Vector3(16.80f, 5.10f,  0.50f), Vector3.forward),
            ("Cuadro_Imagery_Recamara3", "Bedroom3",   new Vector3(16.20f, 5.10f, -6.50f), Vector3.back)
        };

        Shader shLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material matMarco = new Material(shLit) { name = "Mat_Marco_Cuadro_VR", color = new Color(0.18f, 0.12f, 0.08f, 1f) };
        Material matNormal = ObtenerMaterialPinturaNormal();
        Material matCorrupto = ObtenerMaterialPinturaCorrupta();

        for (int i = 0; i < cuadros.Length; i++)
        {
            var info = cuadros[i];
            Transform existente = house.transform.Find(info.nombre);
            GameObject objCuadro = existente != null ? existente.gameObject : new GameObject(info.nombre);
            objCuadro.transform.SetParent(house.transform, true);

            // Buscar el muro arquitectónico despejado mediante Raycast (sin ventanas, puertas, switches ni muebles altos)
            Vector3 posFinal = info.centroBusqueda + info.dirHaciaPared * 1.6f;
            Quaternion rotFinal = Quaternion.LookRotation(-info.dirHaciaPared, Vector3.up);
            Vector3 perp = Vector3.Cross(Vector3.up, info.dirHaciaPared).normalized;
            Vector3[] dirsCandidatas = { info.dirHaciaPared, perp, -perp, -info.dirHaciaPared };
            bool halladoLimpio = false;

            for (int dIdx = 0; dIdx < dirsCandidatas.Length && !halladoLimpio; dIdx++)
            {
                Vector3 dirP = dirsCandidatas[dIdx];
                RaycastHit[] hits = Physics.RaycastAll(info.centroBusqueda, dirP, 5.2f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int h = 0; h < hits.Length; h++)
                {
                    if (hits[h].collider == null || hits[h].collider.isTrigger) continue;
                    if (Mathf.Abs(hits[h].normal.y) > 0.2f) continue;

                    Transform ct = hits[h].collider.transform;
                    string cn = ct.name.ToLowerInvariant();
                    bool esMuro = cn.StartsWith("colliders") || (ct.parent != null && ct.parent.name == "House_Colliders");
                    if (!esMuro) continue;

                    Vector3 candidatoPos = hits[h].point + hits[h].normal * 0.018f;
                    Quaternion candidatoRot = Quaternion.LookRotation(-hits[h].normal, Vector3.up);

                    // Verificar en una caja de 0.76m x 0.58m x 0.18m frente a la pared que no choque con ventanas, switches, puertas ni muebles
                    Collider[] solapados = Physics.OverlapBox(
                        hits[h].point + hits[h].normal * 0.11f,
                        new Vector3(0.38f, 0.29f, 0.09f),
                        candidatoRot,
                        ~0,
                        QueryTriggerInteraction.Ignore);

                    bool obstruido = false;
                    for (int s = 0; s < solapados.Length; s++)
                    {
                        if (solapados[s] == null) continue;
                        Transform st = solapados[s].transform;
                        if (st == objCuadro.transform || st.IsChildOf(objCuadro.transform)) continue;
                        if (st == ct || (st.parent != null && st.parent.name == "House_Colliders")) continue;
                        obstruido = true;
                        break;
                    }

                    if (dIdx == 0 && !halladoLimpio)
                    {
                        posFinal = candidatoPos;
                        rotFinal = candidatoRot;
                    }
                    if (!obstruido)
                    {
                        posFinal = candidatoPos;
                        rotFinal = candidatoRot;
                        halladoLimpio = true;
                    }
                    break;
                }
            }

            objCuadro.transform.position = posFinal;
            objCuadro.transform.rotation = rotFinal;
            objCuadro.transform.localScale = Vector3.one;

            MeshFilter mfMarco = objCuadro.GetComponent<MeshFilter>();
            if (mfMarco == null)
            {
                GameObject prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Mesh cuboMesh = prim.GetComponent<MeshFilter>().sharedMesh;
                if (Application.isPlaying) Destroy(prim);
                else DestroyImmediate(prim);

                mfMarco = objCuadro.AddComponent<MeshFilter>();
                mfMarco.sharedMesh = cuboMesh;
            }

            MeshRenderer mrMarco = objCuadro.GetComponent<MeshRenderer>();
            if (mrMarco == null) mrMarco = objCuadro.AddComponent<MeshRenderer>();
            mrMarco.sharedMaterial = matMarco;
            objCuadro.transform.localScale = new Vector3(0.72f, 0.54f, 0.025f);

            // Lienzo frontal con la pintura
            Transform lienzoT = objCuadro.transform.Find("Lienzo_Pintura");
            GameObject objLienzo;
            if (lienzoT == null)
            {
                objLienzo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                objLienzo.name = "Lienzo_Pintura";
                Collider c = objLienzo.GetComponent<Collider>();
                if (c != null)
                {
                    if (Application.isPlaying) Destroy(c);
                    else DestroyImmediate(c);
                }
                objLienzo.transform.SetParent(objCuadro.transform, false);
            }
            else
            {
                objLienzo = lienzoT.gameObject;
            }

            objLienzo.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            objLienzo.transform.localRotation = Quaternion.identity;
            objLienzo.transform.localScale = new Vector3(0.88f, 0.86f, 1f);

            MeshRenderer mrLienzo = objLienzo.GetComponent<MeshRenderer>();
            mrLienzo.sharedMaterial = matNormal;

            ObjetoAnomalia anom = objCuadro.GetComponent<ObjetoAnomalia>();
            if (anom == null) anom = objCuadro.AddComponent<ObjetoAnomalia>();
            anom.nombreCuarto = info.cuarto;
            if (anom.posiblesVariantes == null)
                anom.posiblesVariantes = new List<VarianteAnomalia>();
            anom.posiblesVariantes.Clear();

            anom.posiblesVariantes.Add(new VarianteAnomalia
            {
                nombreDescriptivo = "Cuadro invertido de cabeza (Imagery)",
                tipo = TipoAnomalia.Imagery,
                girarRotacion = new Vector3(0f, 0f, 180f)
            });

            anom.posiblesVariantes.Add(new VarianteAnomalia
            {
                nombreDescriptivo = "Pintura distorsionada (Imagery)",
                tipo = TipoAnomalia.Imagery,
                rendererCuadroOTV = mrLienzo,
                materialAlterado = matCorrupto,
                materialOriginalGuardado = matNormal
            });
        }
    }

    // =========================================================================
    // EVENTO NUEVO: PUERTA IMPOSIBLE UNKNOWN HACIA PASILLO NEGRO ABISMAL
    // =========================================================================
    public static void AsegurarAnomaliaPuertaAbismalUnknown()
    {
        GameObject obj = GameObject.Find("Anomalia_PuertaAbismal_Unknown");
        if (obj == null)
        {
            obj = new GameObject("Anomalia_PuertaAbismal_Unknown");
            obj.transform.position = new Vector3(13.20f, 0.41f, 0.50f);
        }

        Transform visualT = obj.transform.Find("Visual_Puerta_Abismal");
        GameObject visualObj;
        if (visualT == null)
        {
            visualObj = new GameObject("Visual_Puerta_Abismal");
            visualObj.transform.SetParent(obj.transform, false);
        }
        else
        {
            visualObj = visualT.gameObject;
        }

        PuertaAbismalUnknownVR compPuerta = visualObj.GetComponent<PuertaAbismalUnknownVR>();
        if (compPuerta == null)
            compPuerta = visualObj.AddComponent<PuertaAbismalUnknownVR>();

        AudioClip clipRechinido = null;
#if UNITY_EDITOR
        clipRechinido = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Model/sonido/freesound_community-squeaky-bathroom-door-102756.mp3");
#endif
        if (clipRechinido == null)
        {
            PuertaAutoCierre pRef = FindAnyObjectByType<PuertaAutoCierre>(FindObjectsInactive.Include);
            if (pRef != null) clipRechinido = pRef.sonidoAbrirAnomalia != null ? pRef.sonidoAbrirAnomalia : pRef.sonidoAbrir;
        }

        compPuerta.ConfigurarVisualesPuertaAbismal(clipRechinido);
        visualObj.SetActive(false);

        ObjetoAnomalia anom = obj.GetComponent<ObjetoAnomalia>();
        if (anom == null) anom = obj.AddComponent<ObjetoAnomalia>();
        compPuerta.anomaliaAsociada = anom;
        anom.nombreCuarto = "LivingRoom";
        if (anom.posiblesVariantes == null)
            anom.posiblesVariantes = new List<VarianteAnomalia>();
        anom.posiblesVariantes.Clear();
        anom.posiblesVariantes.Add(new VarianteAnomalia
        {
            nombreDescriptivo = "Puerta imposible hacia el abismo oscuro (Unknown)",
            tipo = TipoAnomalia.Unknown,
            objetoExtraActivable = visualObj
        });
    }

    // =========================================================================
    // IDEAS 1, 2 Y 3: MUEBLES (DISPLACEMENT), APARATOS / RELOJES / TELÉFONOS (ELECTRICAL)
    // =========================================================================
    private void ConfigurarMueblesAparatosRelojesYTelefonos()
    {
        relojesEnCasa.Clear();
        telefonosEnCasa.Clear();
        GameObject house = GameObject.Find("House");
        if (house == null) return;

        foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || PuertaAutoCierre.EsObjetoDeHouseCollidersOInvisible(t)) continue;
            string n = t.name.ToLowerInvariant();
            if (n.Contains("clonmimic") || n.Contains("hitbox") || n.Contains("collider_mueble")) continue;

            MeshRenderer mr = t.GetComponent<MeshRenderer>();
            Vector3 pos = mr != null ? mr.bounds.center : t.position;
            string cuarto = ObjetoAnomalia.NormalizarCuartoPorPosicion(pos, "LivingRoom", t.name);

            // 1. MUEBLES INTERACTIVOS (Cajones de cómoda / baño / gabinete garaje) -> DISPLACEMENT
            if (n.StartsWith("chest_drawers") || n.StartsWith("washbasin_drawer") || n.StartsWith("tool_cabinet"))
            {
                ObjetoAnomalia anom = AsegurarComponenteAnomalia(t.gameObject, cuarto);
                if (!anom.posiblesVariantes.Exists(v => v != null && v.tipo == TipoAnomalia.Displacement))
                {
                    anom.posiblesVariantes.Add(new VarianteAnomalia
                    {
                        nombreDescriptivo = "Cajones del mueble abiertos solos (Displacement)",
                        tipo = TipoAnomalia.Displacement
                    });
                }
            }
            // 2a. TELEVISORES INTERACTIVOS (TVs) -> Estática real yourugor-tv-static-noise (ELECTRICAL)
            else if (n.StartsWith("tv_0"))
            {
                ObjetoAnomalia anom = AsegurarComponenteAnomalia(t.gameObject, cuarto);
                AudioSource aud = AsegurarAudio3D(t.gameObject, clipEstaticaTV, true, 0.36f);
                anom.posiblesVariantes.Clear();
                anom.posiblesVariantes.Add(new VarianteAnomalia
                {
                    nombreDescriptivo = "Televisor encendido solo con estática (Electrical)",
                    tipo = TipoAnomalia.Electrical,
                    audioEvento = aud,
                    sonidoEnBucle = true
                });
            }
            // 2b. PUERTAS DE ELECTRODOMÉSTICOS (Refrigerador y Microondas) -> Se abren solas con su sonido real de puerta (ELECTRICAL)
            else if (n.StartsWith("refrigerator_door") || n.StartsWith("microwave_door"))
            {
                ObjetoAnomalia anom = AsegurarComponenteAnomalia(t.gameObject, cuarto);
                anom.posiblesVariantes.Clear();
                anom.posiblesVariantes.Add(new VarianteAnomalia
                {
                    nombreDescriptivo = "Electrodoméstico abierto solo (Electrical)",
                    tipo = TipoAnomalia.Electrical,
                    audioEvento = null,
                    sonidoEnBucle = false
                });
            }
            // 3. RELOJES VIVOS (Alarm_clock*) -> Tic-tac sutil 3D (tic-tac-emilly) + Anomalía 66:66 (ELECTRICAL)
            else if (n.StartsWith("alarm_clock"))
            {
                ObjetoAnomalia anom = AsegurarComponenteAnomalia(t.gameObject, cuarto);
                TextMeshPro tmp = AsegurarDisplayLEDDespertador(t, mr);

                AudioClip clipReloj = clipTicTacReloj != null ? clipTicTacReloj : ObtenerClipAlarmaDespertador();
                AudioSource aud = AsegurarAudio3D(t.gameObject, clipReloj, true, 0.065f);
                aud.rolloffMode = AudioRolloffMode.Linear;
                aud.minDistance = 0.35f;
                aud.maxDistance = 3.4f;

                anom.posiblesVariantes.Clear();
                anom.posiblesVariantes.Add(new VarianteAnomalia
                {
                    nombreDescriptivo = "Reloj despertador alterado 66:66 con tic-tac distorsionado (Electrical)",
                    tipo = TipoAnomalia.Electrical,
                    audioEvento = clipTicTacReloj != null ? null : aud,
                    sonidoEnBucle = true
                });

                relojesEnCasa.Add(new RelojDespertadorVivo { anomalia = anom, textoLED = tmp, audioTicTac = aud });
            }
            // 4. TELÉFONOS ANÁLOGOS (Phone, Phone_01) -> Timbre real Telefono.mp3 con cadencia analógica (ELECTRICAL)
            else if (n == "phone" || n == "phone_01")
            {
                ObjetoAnomalia anom = AsegurarComponenteAnomalia(t.gameObject, cuarto);
                AudioClip cTel = clipTelefonoAnalogo != null ? clipTelefonoAnalogo : ObtenerClipTimbreTelefono();
                AudioSource aud = AsegurarAudio3D(t.gameObject, cTel, false, 0.52f);
                aud.rolloffMode = AudioRolloffMode.Linear;
                aud.minDistance = 0.75f;
                aud.maxDistance = 9.0f;

                anom.posiblesVariantes.Clear();
                anom.posiblesVariantes.Add(new VarianteAnomalia
                {
                    nombreDescriptivo = "Teléfono análogo sonando solo (Electrical)",
                    tipo = TipoAnomalia.Electrical,
                    audioEvento = null,
                    sonidoEnBucle = false
                });

                telefonosEnCasa.Add(new TelefonoAnalogoVivo { anomalia = anom, audioTimbre = aud, proximoTimbrazo = 0f });
            }
            // 5. RADIO (Radio_01.001) -> Estática sola (ELECTRICAL)
            else if (n.StartsWith("radio_0"))
            {
                ObjetoAnomalia anom = AsegurarComponenteAnomalia(t.gameObject, cuarto);
                AudioSource aud = AsegurarAudio3D(t.gameObject, clipEstaticaTV, true, 0.34f);
                anom.posiblesVariantes.Clear();
                anom.posiblesVariantes.Add(new VarianteAnomalia
                {
                    nombreDescriptivo = "Radio encendida sola con estática (Electrical)",
                    tipo = TipoAnomalia.Electrical,
                    audioEvento = aud,
                    sonidoEnBucle = true
                });
            }
        }
    }

    private static ObjetoAnomalia AsegurarComponenteAnomalia(GameObject go, string cuarto)
    {
        ObjetoAnomalia anom = go.GetComponent<ObjetoAnomalia>();
        if (anom == null) anom = go.AddComponent<ObjetoAnomalia>();
        anom.nombreCuarto = cuarto;
        if (anom.posiblesVariantes == null)
            anom.posiblesVariantes = new List<VarianteAnomalia>();
        return anom;
    }

    private static AudioSource AsegurarAudio3D(GameObject go, AudioClip clip, bool loop, float vol)
    {
        AudioSource aud = go.GetComponent<AudioSource>();
        if (aud == null) aud = go.AddComponent<AudioSource>();
        aud.playOnAwake = false;
        aud.spatialBlend = 1f;
        aud.minDistance = 0.7f;
        aud.maxDistance = 8.5f;
        aud.volume = vol;
        aud.loop = loop;
        if (clip != null) aud.clip = clip;
        return aud;
    }

    private static TextMeshPro AsegurarDisplayLEDDespertador(Transform relojTransform, MeshRenderer mr)
    {
        Transform ledT = relojTransform.Find("Display_LED_Hora_VR");
        GameObject ledObj;
        if (ledT == null)
        {
            ledObj = new GameObject("Display_LED_Hora_VR");
            ledObj.transform.SetParent(relojTransform, true);
        }
        else
        {
            ledObj = ledT.gameObject;
        }

        Bounds b = mr != null ? mr.bounds : new Bounds(relojTransform.position, Vector3.one * 0.14f);
        Vector3 centroCuarto = new Vector3(14.5f, b.center.y, -3.0f);
        Vector3 haciaFrente = (centroCuarto - b.center);
        haciaFrente.y = 0f;
        if (haciaFrente.sqrMagnitude < 0.001f) haciaFrente = Vector3.forward;
        haciaFrente.Normalize();

        ledObj.transform.position = b.center + haciaFrente * (Mathf.Max(b.extents.x, b.extents.z) + 0.012f) + Vector3.up * 0.01f;
        ledObj.transform.rotation = Quaternion.LookRotation(-haciaFrente, Vector3.up);

        Vector3 ls = relojTransform.lossyScale;
        float sx = Mathf.Abs(ls.x) > 0.0001f ? 1f / Mathf.Abs(ls.x) : 1f;
        float sy = Mathf.Abs(ls.y) > 0.0001f ? 1f / Mathf.Abs(ls.y) : 1f;
        float sz = Mathf.Abs(ls.z) > 0.0001f ? 1f / Mathf.Abs(ls.z) : 1f;
        ledObj.transform.localScale = new Vector3(sx, sy, sz);

        TextMeshPro tmp = ledObj.GetComponent<TextMeshPro>();
        if (tmp == null) tmp = ledObj.AddComponent<TextMeshPro>();

        RectTransform rt = tmp.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(0.22f, 0.10f);

        tmp.text = "12:00";
        tmp.fontSize = 0.55f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.22f, 0.95f, 0.42f, 0.95f);
        return tmp;
    }

    private static Material ObtenerMaterialPinturaNormal()
    {
        if (materialPinturaNormal != null) return materialPinturaNormal;

        int w = 128, h = 96;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false) { name = "Tex_Pintura_Normal_VR" };

        // Paisaje clásico nocturno sereno (montañas suaves y luna cálida)
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                Color cielo = Color.Lerp(new Color(0.18f, 0.24f, 0.36f), new Color(0.08f, 0.11f, 0.20f), v);
                float montana = 0.36f + Mathf.Sin(u * 4.5f) * 0.08f + Mathf.Cos(u * 9.2f) * 0.03f;
                float distLuna = Vector2.Distance(new Vector2(u, v), new Vector2(0.72f, 0.72f));

                Color c = v < montana
                    ? Color.Lerp(new Color(0.07f, 0.09f, 0.08f), new Color(0.14f, 0.18f, 0.15f), v / montana)
                    : (distLuna < 0.09f ? new Color(0.95f, 0.90f, 0.72f) : cielo);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        materialPinturaNormal = new Material(sh) { name = "Mat_Pintura_Normal_VR", color = Color.white };
        materialPinturaNormal.SetTexture("_BaseMap", tex);
        materialPinturaNormal.mainTexture = tex;
        return materialPinturaNormal;
    }

    private static Material ObtenerMaterialPinturaCorrupta()
    {
        if (materialPinturaCorrupta != null) return materialPinturaCorrupta;

        int w = 128, h = 96;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false) { name = "Tex_Pintura_Corrupta_VR" };

        // Versión distorsionada / rostro sangriento observando desde el cuadro
        for (int y = 0; y < h; y++)
        {
            float ny = (y / (float)(h - 1)) * 2f - 1f;
            for (int x = 0; x < w; x++)
            {
                float nx = (x / (float)(w - 1)) * 2f - 1f;
                float cara = (nx * nx) / 0.28f + (ny * ny) / 0.55f;
                float ojoI = ((nx + 0.18f) * (nx + 0.18f) + (ny - 0.15f) * (ny - 0.15f));
                float ojoD = ((nx - 0.18f) * (nx - 0.18f) + (ny - 0.15f) * (ny - 0.15f));
                float boca = (nx * nx) / 0.06f + ((ny + 0.25f) * (ny + 0.25f)) / 0.08f;

                Color c = new Color(0.14f, 0.02f, 0.03f);
                if (cara < 1f)
                {
                    if (ojoI < 0.012f || ojoD < 0.012f)
                        c = (ojoI < 0.003f || ojoD < 0.003f) ? Color.white : Color.black;
                    else if (boca < 1f)
                        c = Color.black;
                    else
                        c = new Color(0.65f, 0.12f, 0.12f);
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        materialPinturaCorrupta = new Material(sh) { name = "Mat_Pintura_Corrupta_VR", color = Color.white };
        materialPinturaCorrupta.SetTexture("_BaseMap", tex);
        materialPinturaCorrupta.mainTexture = tex;
        return materialPinturaCorrupta;
    }

    private static AudioClip ObtenerClipAlarmaDespertador()
    {
        if (clipAlarmaDespertador != null) return clipAlarmaDespertador;
        int sr = 22050;
        int n = sr; // 1.0s loop (beep-beep-pause)
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            bool activo = (t < 0.12f) || (t >= 0.20f && t < 0.32f);
            d[i] = activo ? Mathf.Sin(2f * Mathf.PI * 1950f * t) * 0.35f : 0f;
        }
        clipAlarmaDespertador = AudioClip.Create("AlarmClockBeep6666", n, 1, sr, false);
        clipAlarmaDespertador.SetData(d, 0);
        return clipAlarmaDespertador;
    }

    private static AudioClip ObtenerClipTimbreTelefono()
    {
        if (clipTimbreTelefonoInterferencia != null) return clipTimbreTelefonoInterferencia;
        int sr = 22050;
        int n = Mathf.RoundToInt(sr * 1.4f);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            bool ring = t < 0.65f;
            float mod = Mathf.Sin(2f * Mathf.PI * 18f * t);
            float tono = (Mathf.Sin(2f * Mathf.PI * 440f * t) + Mathf.Sin(2f * Mathf.PI * 480f * t)) * 0.25f * mod;
            float ruido = (Random.value * 2f - 1f) * 0.15f;
            d[i] = ring ? (tono + ruido) * 0.5f : ruido * 0.25f;
        }
        clipTimbreTelefonoInterferencia = AudioClip.Create("PhoneRadioStaticRing", n, 1, sr, false);
        clipTimbreTelefonoInterferencia.SetData(d, 0);
        return clipTimbreTelefonoInterferencia;
    }
}

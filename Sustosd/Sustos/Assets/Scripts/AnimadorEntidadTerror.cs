using UnityEngine;
using UnityEngine.Animations.Rigging;

public enum ModoCorpseContextual
{
    Acostado = 0,
    Colgado = 1,
    EnBanera = 2
}

public sealed class AnimadorEntidadTerror : MonoBehaviour
{
    public TipoAnomalia tipoEntidad = TipoAnomalia.PureForm;
    public bool mirarAlJugador = true;

    [Header("Variantes Contextuales (PureForm, Flawed y Corpse)")]
    public bool modoPureFormEscondido = false;
    public float inclinacionAsomoZ = 0f;
    public Vector3 offsetRetraccionLocal = new Vector3(-0.18f, -0.03f, -0.08f);
    public ModoCorpseContextual modoCorpse = ModoCorpseContextual.Acostado;

    [Header("Variantes Esqueléticas Flawed")]
    public bool modoFlawedSentado = false;
    public bool modoFlawedEspaldas = false;
    public bool modoFlawedVentana = false;

    private Vector3 posBaseLocal;
    private Quaternion rotBaseLocal;
    private Vector3 escalaBaseLocal;
    private Transform camTransform;

    private Animator animadorActivo;
    private Rig rigMiradaActiva;
    private Transform nodoObjetivoMirada;
    private float pesoMaximoMirada = 0.60f;

    private float proximoCambioClip;
    private int indiceClipAnterior = -1;
    private bool enSecuenciaSitToStand = false;
    private bool enReaccionReporte = false;

    private float tiempoProximoEspasmo;
    private float duracionEspasmoActual;
    private Quaternion rotEspasmo = Quaternion.identity;
    private float semillaAleatoria;
    private float tiempoMiradoAcecho;
    private float factorRetraccionAcecho;
    private Renderer[] renderersCuerpoCache;

    private static readonly string[] EstadosFlawedPie =
    {
        "BreathingIdle",
        "TwitchStare",
        "MutantBreathing",
        "LookingAround",
        "Agony"
    };

    private static readonly string[] EstadosFlawedSentado =
    {
        "SittingIdle",
        "SittingDisbelief",
        "SittingRubbingArm",
        "SittingDodges"
    };

    private static readonly string[] EstadosFlawedVentana =
    {
        "WindowStalk",
        "WindowPress"
    };

    private static readonly string[] EstadosPureFormDirecto =
    {
        "MutantBreathing",
        "StalkerSnap",
        "BreathingIdle"
    };

    private static readonly string[] EstadosTulpaEspejo =
    {
        "BreathingIdle",
        "MirrorReach",
        "LookingAround",
        "Agony"
    };

    private void Awake()
    {
        GuardarTransformBase();
        semillaAleatoria = Random.Range(0f, 100f);
    }

    private void OnEnable()
    {
        GuardarTransformBase();
        enSecuenciaSitToStand = false;
        enReaccionReporte = false;
        tiempoMiradoAcecho = 0f;
        factorRetraccionAcecho = 0f;
        tiempoProximoEspasmo = Time.time + Random.Range(1.2f, 2.8f);
        duracionEspasmoActual = 0f;

        InicializarAnimatorYAnimationRigging();
        ProgramarEstadoInicial();
    }

    public void GuardarTransformBase()
    {
        posBaseLocal = transform.localPosition;
        rotBaseLocal = transform.localRotation;
        escalaBaseLocal = transform.localScale;
        if (escalaBaseLocal.sqrMagnitude < 0.00001f)
            escalaBaseLocal = Vector3.one;
    }

    public void ConfigurarModoVarianteFlawed(bool esSentado, bool esEspaldas, bool esVentana = false)
    {
        modoFlawedSentado = esSentado;
        modoFlawedEspaldas = esEspaldas;
        modoFlawedVentana = esVentana;
        enSecuenciaSitToStand = false;
        enReaccionReporte = false;
        if (isActiveAndEnabled)
        {
            InicializarAnimatorYAnimationRigging();
            ProgramarEstadoInicial();
        }
    }

    public void DispararSitToStandPorReporte()
    {
        DispararReaccionPorReporte();
    }

    public void DispararReaccionPorReporte()
    {
        if (enReaccionReporte) return;
        enReaccionReporte = true;

        if (animadorActivo == null)
            InicializarAnimatorYAnimationRigging();

        if (animadorActivo == null || animadorActivo.runtimeAnimatorController == null)
            return;

        proximoCambioClip = float.MaxValue;
        animadorActivo.speed = 1.16f;

        switch (tipoEntidad)
        {
            case TipoAnomalia.Flawed:
                if (modoFlawedSentado)
                {
                    enSecuenciaSitToStand = true;
                    animadorActivo.CrossFadeInFixedTime("SitToStand", 0.14f, 0, 0f);
                }
                else if (modoFlawedVentana)
                {
                    animadorActivo.CrossFadeInFixedTime("WindowPress", 0.12f, 0, 0f);
                }
                else
                {
                    if (modoFlawedEspaldas)
                    {
                        modoFlawedEspaldas = false;
                        mirarAlJugador = true;
                    }
                    animadorActivo.CrossFadeInFixedTime("Agony", 0.14f, 0, 0f);
                }
                break;

            case TipoAnomalia.PureForm:
                if (modoPureFormEscondido)
                {
                    modoPureFormEscondido = false;
                    factorRetraccionAcecho = 0f;
                    transform.localScale = new Vector3(Mathf.Abs(escalaBaseLocal.x), escalaBaseLocal.y, escalaBaseLocal.z);
                    transform.localPosition = posBaseLocal;
                }
                animadorActivo.CrossFadeInFixedTime("StalkerSnap", 0.10f, 0, 0f);
                break;

            case TipoAnomalia.Tulpa:
                animadorActivo.CrossFadeInFixedTime("MirrorReach", 0.12f, 0, 0f);
                break;
        }
    }

    public void InicializarAnimatorYAnimationRigging()
    {
        animadorActivo = null;
        rigMiradaActiva = null;

        if (tipoEntidad == TipoAnomalia.Corpse)
            return;

        // Buscar el Animator del submodelo actualmente activo (ej. Flawed_Variante_0..5 o Modelo_3D_*)
        Animator[] anims = GetComponentsInChildren<Animator>(false);
        if (anims != null && anims.Length > 0)
            animadorActivo = anims[0];

        if (animadorActivo == null) return;

        animadorActivo.applyRootMotion = false;
        animadorActivo.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animadorActivo.speed = Random.Range(0.94f, 1.05f);
        animadorActivo.transform.localPosition = new Vector3(animadorActivo.transform.localPosition.x, 0f, animadorActivo.transform.localPosition.z);
        renderersCuerpoCache = animadorActivo.GetComponentsInChildren<Renderer>(false);

        // Configurar Animation Rigging (MultiAimConstraint sobre la cabeza hacia la cámara VR del jugador)
        bool usarAimRig = (tipoEntidad == TipoAnomalia.Flawed && !modoFlawedEspaldas) ||
                          (tipoEntidad == TipoAnomalia.PureForm && !modoPureFormEscondido) ||
                          (tipoEntidad == TipoAnomalia.Tulpa);

        pesoMaximoMirada = tipoEntidad == TipoAnomalia.PureForm ? 0.72f :
                           (modoFlawedSentado ? 0.65f : 0.55f);

        ConfigurarMultiAimRigEnModelo(animadorActivo, usarAimRig);
    }

    private void ConfigurarMultiAimRigEnModelo(Animator anim, bool habilitar)
    {
        if (anim == null) return;

        Transform rigExistente = anim.transform.Find("Rig_Mirada_VR");
        if (!habilitar)
        {
            if (rigExistente != null)
            {
                Rig rOff = rigExistente.GetComponent<Rig>();
                if (rOff != null) rOff.weight = 0f;
            }
            return;
        }

        Transform huesoCabeza = BuscarHuesoRecursivo(anim.transform, "mixamorig:Head");
        if (huesoCabeza == null) return;

        Transform objTarget = anim.transform.Find("Objetivo_Mirada_VR");
        if (objTarget == null)
        {
            GameObject goTarget = new GameObject("Objetivo_Mirada_VR");
            goTarget.layer = anim.gameObject.layer;
            objTarget = goTarget.transform;
            objTarget.SetParent(anim.transform, false);
            objTarget.localPosition = new Vector3(0f, 1.6f, 2.0f);
        }
        nodoObjetivoMirada = objTarget;

        GameObject goRig;
        if (rigExistente == null)
        {
            goRig = new GameObject("Rig_Mirada_VR");
            goRig.layer = anim.gameObject.layer;
            goRig.transform.SetParent(anim.transform, false);
        }
        else
        {
            goRig = rigExistente.gameObject;
        }

        rigMiradaActiva = goRig.GetComponent<Rig>();
        if (rigMiradaActiva == null)
            rigMiradaActiva = goRig.AddComponent<Rig>();
        rigMiradaActiva.weight = Application.isPlaying ? 0f : pesoMaximoMirada;

        Transform aimNode = goRig.transform.Find("Aim_Cabeza");
        GameObject goAim;
        if (aimNode == null)
        {
            goAim = new GameObject("Aim_Cabeza");
            goAim.layer = anim.gameObject.layer;
            goAim.transform.SetParent(goRig.transform, false);
        }
        else
        {
            goAim = aimNode.gameObject;
        }

        MultiAimConstraint mac = goAim.GetComponent<MultiAimConstraint>();
        if (mac == null)
        {
            mac = goAim.AddComponent<MultiAimConstraint>();
            mac.Reset();
        }

        MultiAimConstraintData d = mac.data;
        d.constrainedObject = huesoCabeza;
        d.aimAxis = MultiAimConstraintData.Axis.Z;
        d.upAxis = MultiAimConstraintData.Axis.Y;
        d.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
        d.maintainOffset = true;
        d.constrainedXAxis = true;
        d.constrainedYAxis = true;
        d.constrainedZAxis = false;
        d.limits = new Vector2(-56f, 56f);

        WeightedTransformArray sources = new WeightedTransformArray(0);
        sources.Add(new WeightedTransform(nodoObjetivoMirada, 1f));
        d.sourceObjects = sources;
        mac.data = d;

        RigBuilder rb = anim.GetComponent<RigBuilder>();
        if (rb == null)
            rb = anim.gameObject.AddComponent<RigBuilder>();

        rb.layers.Clear();
        rb.layers.Add(new RigLayer(rigMiradaActiva, true));

        if (Application.isPlaying && anim.gameObject.activeInHierarchy)
        {
            rb.Build();
        }
    }

    private static Transform BuscarHuesoRecursivo(Transform raiz, string nombreExacto)
    {
        if (raiz == null) return null;
        if (raiz.name == nombreExacto) return raiz;
        for (int i = 0; i < raiz.childCount; i++)
        {
            Transform enc = BuscarHuesoRecursivo(raiz.GetChild(i), nombreExacto);
            if (enc != null) return enc;
        }
        return null;
    }

    private void ProgramarEstadoInicial()
    {
        if (animadorActivo == null || animadorActivo.runtimeAnimatorController == null) return;

        indiceClipAnterior = -1;
        switch (tipoEntidad)
        {
            case TipoAnomalia.Flawed:
                if (modoFlawedSentado)
                {
                    CambiarAEstadoAleatorio(EstadosFlawedSentado, 0.05f);
                    proximoCambioClip = Time.time + Random.Range(4.8f, 8.2f);
                }
                else if (modoFlawedVentana)
                {
                    animadorActivo.Play("WindowStalk", 0, Random.Range(0f, 0.6f));
                    indiceClipAnterior = 0;
                    proximoCambioClip = Time.time + Random.Range(5.0f, 8.5f);
                }
                else
                {
                    // Empezar en postura inquietante base y luego alternar con Agony, LookingAround y TwitchStare
                    animadorActivo.Play("BreathingIdle", 0, Random.Range(0f, 0.8f));
                    indiceClipAnterior = 0;
                    proximoCambioClip = Time.time + Random.Range(4.0f, 7.2f);
                }
                break;

            case TipoAnomalia.PureForm:
                if (modoPureFormEscondido)
                {
                    // Espejar en X según el lado de asomo para que siempre incline el torso hacia afuera del mueble
                    float signoEscalaX = inclinacionAsomoZ < 0f ? -1f : 1f;
                    transform.localScale = new Vector3(Mathf.Abs(escalaBaseLocal.x) * signoEscalaX, escalaBaseLocal.y, escalaBaseLocal.z);
                    animadorActivo.Play("PeekCorner", 0, Random.Range(0f, 0.7f));
                    proximoCambioClip = float.MaxValue;
                }
                else
                {
                    transform.localScale = escalaBaseLocal;
                    CambiarAEstadoAleatorio(EstadosPureFormDirecto, 0.05f);
                    proximoCambioClip = Time.time + Random.Range(4.5f, 8.0f);
                }
                break;

            case TipoAnomalia.Preacher:
                animadorActivo.Play("ClosetWhisper", 0, Random.Range(0f, 0.9f));
                proximoCambioClip = float.MaxValue;
                break;

            case TipoAnomalia.Tulpa:
                CambiarAEstadoAleatorio(EstadosTulpaEspejo, 0.05f);
                proximoCambioClip = Time.time + Random.Range(4.2f, 7.5f);
                break;
        }
    }

    private void CambiarAEstadoAleatorio(string[] estados, float fadeSeg)
    {
        if (animadorActivo == null || estados == null || estados.Length == 0) return;

        int elegido = Random.Range(0, estados.Length);
        if (estados.Length > 1 && elegido == indiceClipAnterior)
            elegido = (elegido + 1 + Random.Range(0, estados.Length - 1)) % estados.Length;

        indiceClipAnterior = elegido;
        animadorActivo.CrossFadeInFixedTime(estados[elegido], fadeSeg, 0);
    }

    private void Update()
    {
        float t = Time.time + semillaAleatoria;

        if (camTransform == null && Camera.main != null)
            camTransform = Camera.main.transform;

        if (tipoEntidad == TipoAnomalia.Corpse)
        {
            EvaluarEspasmoSimple();
            AnimarCorpse(t, duracionEspasmoActual > 0f);
            return;
        }

        // Actualizar objetivo de mirada de Animation Rigging hacia los ojos del jugador
        ActualizarPesoRigMirada();

        // Alternar clips esqueléticos de Mixamo + Custom de forma fluida
        if (!enSecuenciaSitToStand && !enReaccionReporte && animadorActivo != null && animadorActivo.runtimeAnimatorController != null && Time.time >= proximoCambioClip)
        {
            if (tipoEntidad == TipoAnomalia.Flawed)
            {
                if (modoFlawedSentado)
                {
                    CambiarAEstadoAleatorio(EstadosFlawedSentado, 0.42f);
                    proximoCambioClip = Time.time + Random.Range(5.0f, 8.8f);
                }
                else if (modoFlawedVentana)
                {
                    CambiarAEstadoAleatorio(EstadosFlawedVentana, 0.40f);
                    proximoCambioClip = Time.time + (indiceClipAnterior == 1 ? Random.Range(3.2f, 4.6f) : Random.Range(5.2f, 8.6f));
                }
                else
                {
                    CambiarAEstadoAleatorio(EstadosFlawedPie, 0.38f);
                    // Agony es más intenso y breve; los estados de respiración/acecho duran un poco más
                    bool esAgonia = indiceClipAnterior == 4;
                    proximoCambioClip = Time.time + (esAgonia ? Random.Range(3.2f, 4.4f) : Random.Range(4.6f, 8.2f));
                }
            }
            else if (tipoEntidad == TipoAnomalia.PureForm && !modoPureFormEscondido)
            {
                CambiarAEstadoAleatorio(EstadosPureFormDirecto, 0.40f);
                proximoCambioClip = Time.time + Random.Range(4.5f, 8.0f);
            }
            else if (tipoEntidad == TipoAnomalia.Tulpa)
            {
                CambiarAEstadoAleatorio(EstadosTulpaEspejo, 0.42f);
                proximoCambioClip = Time.time + Random.Range(4.2f, 7.6f);
            }
        }

        // Comportamiento espacial contextual según entidad
        if (tipoEntidad == TipoAnomalia.PureForm)
        {
            ActualizarComportamientoPureForm(t);
        }
        else if (tipoEntidad == TipoAnomalia.Flawed)
        {
            if (!modoFlawedSentado && !modoFlawedVentana && mirarAlJugador)
            {
                float velGiro = enReaccionReporte ? 5.2f : 1.65f;
                // De pie: gira lentamente hacia el jugador (o rápido si acaba de ser reportado)
                OrientarCuerpoLentoHaciaJugador(velGiro);
            }
        }
    }

    private void ActualizarPesoRigMirada()
    {
        if (rigMiradaActiva == null || camTransform == null) return;

        if (nodoObjetivoMirada != null)
            nodoObjetivoMirada.position = camTransform.position;

        float metaPeso = 0f;
        if (!enSecuenciaSitToStand)
        {
            Vector3 haciaJugador = camTransform.position - (transform.position + Vector3.up * 1.45f);
            float dist = haciaJugador.magnitude;
            if (dist > 0.35f && dist < 8.5f)
            {
                float dot = Vector3.Dot(transform.forward, haciaJugador / dist);
                if (dot > 0.05f || tipoEntidad == TipoAnomalia.Tulpa)
                {
                    // Reducir el aim durante Agony para no pelear con el retorcimiento completo de cabeza
                    bool enAgonia = (!enReaccionReporte && tipoEntidad == TipoAnomalia.Flawed && !modoFlawedSentado && !modoFlawedVentana && indiceClipAnterior == 4) ||
                                    (enReaccionReporte && tipoEntidad == TipoAnomalia.Flawed && !modoFlawedSentado && !modoFlawedVentana) ||
                                    (!enReaccionReporte && tipoEntidad == TipoAnomalia.Tulpa && indiceClipAnterior == 3);
                    metaPeso = enAgonia ? 0.15f : pesoMaximoMirada;
                }
            }
        }

        rigMiradaActiva.weight = Mathf.MoveTowards(rigMiradaActiva.weight, metaPeso, Time.deltaTime * 2.2f);
    }

    private void ActualizarComportamientoPureForm(float t)
    {
        if (modoPureFormEscondido)
        {
            // Variante escondida: asomado detrás de un mueble/marco; si el jugador cruza miradas con él, se retrae sutilmente
            bool jugadorLoMira = false;
            if (camTransform != null)
            {
                Vector3 posCabeza = transform.position + Vector3.up * 1.66f;
                Vector3 haciaEntidad = posCabeza - camTransform.position;
                float dist = haciaEntidad.magnitude;
                if (dist > 0.25f && dist < 8.5f)
                {
                    float dot = Vector3.Dot(camTransform.forward, haciaEntidad / dist);
                    if (dot > 0.87f)
                        jugadorLoMira = true;
                }
            }

            if (jugadorLoMira)
                tiempoMiradoAcecho = Mathf.Min(tiempoMiradoAcecho + Time.deltaTime, 2.0f);
            else
                tiempoMiradoAcecho = Mathf.Max(0f, tiempoMiradoAcecho - Time.deltaTime * 0.7f);

            float metaRetraccion = tiempoMiradoAcecho > 0.65f ? 1f : 0f;
            factorRetraccionAcecho = Mathf.MoveTowards(factorRetraccionAcecho, metaRetraccion, Time.deltaTime * 1.4f);

            transform.localPosition = posBaseLocal + Vector3.Lerp(Vector3.zero, offsetRetraccionLocal, factorRetraccionAcecho);
            OrientarCuerpoLentoHaciaJugador(2.1f);
        }
        else
        {
            // Al ser reportado, avanza un paso corto y amenazante hacia el frente antes del apagón
            Vector3 avanceReporte = enReaccionReporte ? new Vector3(0f, 0f, 0.28f) : Vector3.zero;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, posBaseLocal + avanceReporte, Time.deltaTime * 1.3f);
            OrientarCuerpoLentoHaciaJugador(enReaccionReporte ? 5.5f : 2.6f);
        }
    }

    private void OrientarCuerpoLentoHaciaJugador(float velocidadGiro)
    {
        if (!mirarAlJugador || camTransform == null) return;

        Vector3 dirMundial = camTransform.position - transform.position;
        dirMundial.y = 0f;
        if (dirMundial.sqrMagnitude > 0.04f)
        {
            Quaternion rotDeseada = Quaternion.LookRotation(dirMundial.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotDeseada, Time.deltaTime * velocidadGiro);
        }
    }

    private void EvaluarEspasmoSimple()
    {
        if (Time.time >= tiempoProximoEspasmo)
        {
            duracionEspasmoActual = Random.Range(0.07f, 0.16f);
            tiempoProximoEspasmo = Time.time + Random.Range(2.4f, 5.2f);
            rotEspasmo = Quaternion.Euler(Random.Range(-0.8f, 0.8f), Random.Range(-1.0f, 1.0f), Random.Range(-0.8f, 0.8f));
        }
        if (duracionEspasmoActual > 0f)
            duracionEspasmoActual -= Time.deltaTime;
    }

    private void AnimarCorpse(float t, bool enEspasmo)
    {
        transform.localScale = escalaBaseLocal;
        transform.localPosition = posBaseLocal;

        if (modoCorpse == ModoCorpseContextual.Colgado)
        {
            float penduloX = Mathf.Sin(t * 1.05f) * 2.1f;
            float penduloZ = Mathf.Cos(t * 0.88f) * 1.8f;
            float torsionCuerdaY = Mathf.Sin(t * 0.42f) * 7.5f;
            transform.localRotation = rotBaseLocal * Quaternion.Euler(penduloX, torsionCuerdaY, penduloZ);
        }
        else
        {
            transform.localRotation = rotBaseLocal * (enEspasmo ? rotEspasmo : Quaternion.identity);
        }
    }

    private void LateUpdate()
    {
        // Guardia anti-clipping en tiempo real tras evaluar Animator + Rigging:
        // Garantiza al 100% que NINGUNA entidad interior (ej. Flawed en Recámara 3 sobre la Cocina, PureForm, Preacher o Tulpa)
        // hunda los pies o piernas por debajo de la losa del piso hacia el techo del cuarto inferior.
        if (tipoEntidad == TipoAnomalia.Corpse || modoFlawedVentana || animadorActivo == null) return;
        if (transform.parent == null) return;

        if (renderersCuerpoCache == null || renderersCuerpoCache.Length == 0)
            renderersCuerpoCache = animadorActivo.GetComponentsInChildren<Renderer>(false);
        if (renderersCuerpoCache == null || renderersCuerpoCache.Length == 0) return;

        float minY = float.MaxValue;
        for (int i = 0; i < renderersCuerpoCache.Length; i++)
        {
            Renderer r = renderersCuerpoCache[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r.gameObject.name.Contains("Halo")) continue;
            float ry = r.bounds.min.y;
            if (ry < minY) minY = ry;
        }
        if (minY >= float.MaxValue * 0.5f) return;

        float pisoY = transform.parent.position.y;
        // En Piso 2 la losa del piso está en Y=3.732 (techo de Cocina en Y=3.408): jamás permitir bajar de 3.74f
        if (pisoY > 3.15f && pisoY < 3.74f)
            pisoY = 3.74f;

        float margenMinimo = pisoY + 0.006f;
        if (minY < margenMinimo)
        {
            float subir = margenMinimo - minY;
            animadorActivo.transform.position += new Vector3(0f, subir, 0f);
        }
        else if (!enSecuenciaSitToStand && minY > pisoY + 0.08f)
        {
            // Evitar también que quede flotando en el aire si algún clip sube el centro de masa
            float bajar = minY - (pisoY + 0.01f);
            animadorActivo.transform.position -= new Vector3(0f, bajar, 0f);
        }
    }
}

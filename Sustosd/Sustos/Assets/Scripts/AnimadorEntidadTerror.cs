using UnityEngine;

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

    [Header("Variantes Contextuales (PureForm y Corpse)")]
    public bool modoPureFormEscondido = false;
    public float inclinacionAsomoZ = 0f;
    public Vector3 offsetRetraccionLocal = new Vector3(-0.18f, -0.03f, -0.08f);
    public ModoCorpseContextual modoCorpse = ModoCorpseContextual.Acostado;

    private Vector3 posBaseLocal;
    private Quaternion rotBaseLocal;
    private Vector3 escalaBaseLocal;
    private Transform camTransform;

    private float tiempoProximoEspasmo;
    private float duracionEspasmoActual;
    private Quaternion rotEspasmo = Quaternion.identity;
    private Vector3 offsetEspasmo = Vector3.zero;
    private float semillaAleatoria;
    private float tiempoMiradoAcecho;
    private float factorRetraccionAcecho;

    private void Awake()
    {
        GuardarTransformBase();
        semillaAleatoria = Random.Range(0f, 100f);
    }

    private void OnEnable()
    {
        GuardarTransformBase();
        tiempoProximoEspasmo = Time.time + Random.Range(0.8f, 2.2f);
        duracionEspasmoActual = 0f;
        tiempoMiradoAcecho = 0f;
        factorRetraccionAcecho = 0f;
    }

    public void GuardarTransformBase()
    {
        posBaseLocal = transform.localPosition;
        rotBaseLocal = transform.localRotation;
        escalaBaseLocal = transform.localScale;
        if (escalaBaseLocal.sqrMagnitude < 0.00001f)
            escalaBaseLocal = Vector3.one;
    }

    private void Update()
    {
        float t = Time.time + semillaAleatoria;

        if (camTransform == null && Camera.main != null)
            camTransform = Camera.main.transform;

        // Evaluar espasmos de terror (twitching) sin generar basura en memoria
        if (Time.time >= tiempoProximoEspasmo)
        {
            duracionEspasmoActual = Random.Range(0.07f, 0.18f);
            tiempoProximoEspasmo = Time.time + Random.Range(1.1f, 3.4f);

            float intensidad = (tipoEntidad == TipoAnomalia.Flawed || tipoEntidad == TipoAnomalia.Tulpa) ? 18f : 10f;
            rotEspasmo = Quaternion.Euler(
                Random.Range(-intensidad * 0.6f, intensidad * 0.6f),
                Random.Range(-intensidad, intensidad),
                Random.Range(-intensidad, intensidad)
            );
            offsetEspasmo = new Vector3(
                Random.Range(-0.025f, 0.025f),
                Random.Range(-0.015f, 0.025f),
                Random.Range(-0.025f, 0.025f)
            );
        }

        bool enEspasmo = duracionEspasmoActual > 0f;
        if (enEspasmo)
            duracionEspasmoActual -= Time.deltaTime;

        switch (tipoEntidad)
        {
            case TipoAnomalia.PureForm:
                AnimarPureForm(t, enEspasmo);
                break;

            case TipoAnomalia.Preacher:
                AnimarPreacher(t, enEspasmo);
                break;

            case TipoAnomalia.Flawed:
                AnimarFlawed(t, enEspasmo);
                break;

            case TipoAnomalia.Tulpa:
                AnimarTulpa(t, enEspasmo);
                break;

            case TipoAnomalia.Corpse:
                AnimarCorpse(t, enEspasmo);
                break;
        }
    }

    private void AnimarPureForm(float t, bool enEspasmo)
    {
        float resp = 1f + Mathf.Sin(t * 2.1f) * 0.016f;
        transform.localScale = new Vector3(escalaBaseLocal.x * resp, escalaBaseLocal.y, escalaBaseLocal.z * resp);

        if (modoPureFormEscondido)
        {
            // Variante Acechador / Escondido: asomado detrás de muebles/marcos con inclinación lateral;
            // si el jugador cruza miradas con él, se retrae sutilmente hacia su escondite.
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
            factorRetraccionAcecho = Mathf.MoveTowards(factorRetraccionAcecho, metaRetraccion, Time.deltaTime * 1.35f);

            Vector3 microVaiven = new Vector3(Mathf.Sin(t * 1.1f) * 0.012f, Mathf.Sin(t * 1.7f) * 0.008f, 0f);
            transform.localPosition = posBaseLocal + Vector3.Lerp(Vector3.zero, offsetRetraccionLocal, factorRetraccionAcecho) + microVaiven;

            float anguloAsomoActual = Mathf.Lerp(inclinacionAsomoZ, inclinacionAsomoZ * 0.32f, factorRetraccionAcecho) + Mathf.Sin(t * 1.2f) * 1.5f;
            OrientarSutilmenteHaciaJugador(2.2f, false, anguloAsomoActual);
        }
        else
        {
            // Variante Directa / Confrontacional: erguido e imponente frente al jugador, mirándolo fijamente
            float derivaY = Mathf.Sin(t * 1.8f) * 0.012f;
            float vaivenX = Mathf.Cos(t * 1.1f) * 0.018f;
            transform.localPosition = posBaseLocal + new Vector3(vaivenX, derivaY, 0f) + (enEspasmo ? offsetEspasmo * 0.5f : Vector3.zero);

            OrientarSutilmenteHaciaJugador(2.6f, enEspasmo, Mathf.Sin(t * 1.3f) * 2.2f);
        }
    }

    private void AnimarPreacher(float t, bool enEspasmo)
    {
        // Monje inquietante y sigiloso: respiración muy lenta y leve balanceo fluido (sin sacudidas toscas)
        float resp = 1f + Mathf.Sin(t * 1.15f) * 0.014f;
        transform.localScale = new Vector3(escalaBaseLocal.x * resp, escalaBaseLocal.y, escalaBaseLocal.z * resp);

        float derivaY = Mathf.Sin(t * 1.15f) * 0.006f;
        transform.localPosition = posBaseLocal + new Vector3(0f, derivaY, 0f);

        float rezoPitch = Mathf.Sin(t * 0.75f) * 2.2f;
        float giroYaw   = Mathf.Sin(t * 0.45f) * 2.6f;
        float ladeadoZ  = Mathf.Cos(t * 0.60f) * 1.4f;
        Quaternion objetivo = rotBaseLocal * Quaternion.Euler(rezoPitch, giroYaw, ladeadoZ);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, objetivo, Time.deltaTime * 3.5f);
    }

    private void AnimarFlawed(float t, bool enEspasmo)
    {
        // Humanoide anómalo (Alternate Watch): quietud humana inquietante + respiración orgánica sutil + micro-espasmos bruscos ocasionales
        float resp = 1f + Mathf.Sin(t * 1.9f) * 0.011f;
        transform.localScale = new Vector3(escalaBaseLocal.x * resp, escalaBaseLocal.y, escalaBaseLocal.z * resp);

        float balanceoSutil = Mathf.Sin(t * 0.95f) * 0.014f;
        transform.localPosition = posBaseLocal + new Vector3(balanceoSutil, 0f, 0f) + (enEspasmo ? offsetEspasmo * 1.15f : Vector3.zero);

        OrientarSutilmenteHaciaJugador(2.4f, enEspasmo, Mathf.Sin(t * 0.85f) * 2.2f);
    }

    private void AnimarTulpa(float t, bool enEspasmo)
    {
        // Espíritu del espejo: oscilación etérea y cabeza ladeada observando desde el reflejo
        float flotar = Mathf.Sin(t * 2.4f) * 0.04f;
        float deriva = Mathf.Sin(t * 1.6f) * 0.035f;
        transform.localPosition = posBaseLocal + new Vector3(deriva, flotar, 0f) + (enEspasmo ? offsetEspasmo : Vector3.zero);

        float resp = 1f + Mathf.Sin(t * 3.0f) * 0.022f;
        transform.localScale = new Vector3(escalaBaseLocal.x * resp, escalaBaseLocal.y, escalaBaseLocal.z * resp);

        Quaternion ladeado = Quaternion.Euler(Mathf.Sin(t * 1.8f) * 4f, Mathf.Cos(t * 1.4f) * 7f, Mathf.Sin(t * 2.1f) * 8f);
        transform.localRotation = rotBaseLocal * ladeado * (enEspasmo ? rotEspasmo : Quaternion.identity);
    }

    private void AnimarCorpse(float t, bool enEspasmo)
    {
        transform.localScale = escalaBaseLocal;

        if (modoCorpse == ModoCorpseContextual.Colgado)
        {
            // Cadáver colgado del techo: balanceo pendular lento e inquietante desde el anclaje superior de la cuerda
            transform.localPosition = posBaseLocal;
            float penduloX = Mathf.Sin(t * 1.05f) * 2.1f;
            float penduloZ = Mathf.Cos(t * 0.88f) * 1.8f;
            float torsionCuerdaY = Mathf.Sin(t * 0.42f) * 7.5f;
            transform.localRotation = rotBaseLocal * Quaternion.Euler(penduloX, torsionCuerdaY, penduloZ);
        }
        else
        {
            // Cadáver acostado (en cama, suelo o bañera): inerte y realista, con rarísimo micro-espasmo post-mortem
            transform.localPosition = posBaseLocal;
            Quaternion microRigidez = enEspasmo
                ? Quaternion.Euler(Random.Range(-0.8f, 0.8f), Random.Range(-1.0f, 1.0f), Random.Range(-0.8f, 0.8f))
                : Quaternion.identity;
            transform.localRotation = rotBaseLocal * microRigidez;
        }
    }

    private void OrientarSutilmenteHaciaJugador(float velocidadGiro, bool enEspasmo, float ladeadoZ)
    {
        if (mirarAlJugador && camTransform != null && transform.parent != null)
        {
            Vector3 dirMundial = camTransform.position - transform.position;
            dirMundial.y = 0f;
            if (dirMundial.sqrMagnitude > 0.04f)
            {
                Quaternion rotDeseadaMundial = Quaternion.LookRotation(dirMundial.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, ladeadoZ);
                if (enEspasmo)
                    rotDeseadaMundial *= rotEspasmo;
                transform.rotation = Quaternion.Slerp(transform.rotation, rotDeseadaMundial, Time.deltaTime * velocidadGiro);
                return;
            }
        }

        Quaternion oscilacion = Quaternion.Euler(0f, Mathf.Sin(Time.time * 1.5f) * 8f, ladeadoZ);
        transform.localRotation = rotBaseLocal * oscilacion * (enEspasmo ? rotEspasmo : Quaternion.identity);
    }
}

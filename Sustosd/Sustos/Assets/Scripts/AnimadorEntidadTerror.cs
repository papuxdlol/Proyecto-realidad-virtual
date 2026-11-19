using UnityEngine;

public sealed class AnimadorEntidadTerror : MonoBehaviour
{
    public TipoAnomalia tipoEntidad = TipoAnomalia.PureForm;
    public bool mirarAlJugador = true;

    private Vector3 posBaseLocal;
    private Quaternion rotBaseLocal;
    private Vector3 escalaBaseLocal;
    private Transform camTransform;

    private float tiempoProximoEspasmo;
    private float duracionEspasmoActual;
    private Quaternion rotEspasmo = Quaternion.identity;
    private Vector3 offsetEspasmo = Vector3.zero;
    private float semillaAleatoria;

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
        // Levitación fantasmal suave + respiración profunda + seguimiento lento al jugador
        float flotarY = Mathf.Sin(t * 2.1f) * 0.055f;
        float vaivenX = Mathf.Cos(t * 1.3f) * 0.03f;
        transform.localPosition = posBaseLocal + new Vector3(vaivenX, flotarY, 0f) + (enEspasmo ? offsetEspasmo : Vector3.zero);

        float resp = 1f + Mathf.Sin(t * 2.6f) * 0.025f;
        transform.localScale = new Vector3(escalaBaseLocal.x * resp, escalaBaseLocal.y / Mathf.Sqrt(resp), escalaBaseLocal.z * resp);

        OrientarSutilmenteHaciaJugador(1.8f, enEspasmo, Mathf.Sin(t * 1.5f) * 4f);
    }

    private void AnimarPreacher(float t, bool enEspasmo)
    {
        // Rezo/balanceo rítmico hacia adelante y atrás en el rincón + temblor constante
        float rezoPitch = Mathf.Sin(t * 3.4f) * 11f;
        float temblorX = Mathf.Sin(t * 28f) * 0.008f;
        float temblorZ = Mathf.Cos(t * 31f) * 0.008f;

        transform.localPosition = posBaseLocal + new Vector3(temblorX, Mathf.Abs(Mathf.Sin(t * 3.4f)) * 0.02f, temblorZ);

        float resp = 1f + Mathf.Sin(t * 4.5f) * 0.03f;
        transform.localScale = new Vector3(escalaBaseLocal.x * resp, escalaBaseLocal.y, escalaBaseLocal.z * resp);

        Quaternion inclinacion = Quaternion.Euler(rezoPitch, Mathf.Sin(t * 1.9f) * 6f, Mathf.Cos(t * 2.3f) * 4f);
        transform.localRotation = rotBaseLocal * inclinacion * (enEspasmo ? rotEspasmo : Quaternion.identity);
    }

    private void AnimarFlawed(float t, bool enEspasmo)
    {
        // Monstruo deforme: respiración asimétrica + sacudidas bruscas y mirada fija al jugador
        float respX = 1f + Mathf.Sin(t * 5.2f) * 0.045f;
        float respY = 1f + Mathf.Cos(t * 4.1f) * 0.035f;
        transform.localScale = new Vector3(escalaBaseLocal.x * respX, escalaBaseLocal.y * respY, escalaBaseLocal.z * respX);

        float pasoLateral = Mathf.Sin(t * 2.2f) * 0.045f;
        transform.localPosition = posBaseLocal + new Vector3(pasoLateral, Mathf.Abs(Mathf.Cos(t * 4.4f)) * 0.025f, 0f) + (enEspasmo ? offsetEspasmo * 1.6f : Vector3.zero);

        OrientarSutilmenteHaciaJugador(3.2f, enEspasmo, Mathf.Sin(t * 3.7f) * 9f);
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
        // Bolsa de cadáver en el suelo: respiración agonizante y espasmos repentinos dentro de la bolsa
        float pulso = 1f + Mathf.Sin(t * 3.8f) * 0.03f + (enEspasmo ? 0.06f : 0f);
        transform.localScale = new Vector3(escalaBaseLocal.x * pulso, escalaBaseLocal.y * pulso, escalaBaseLocal.z);

        Vector3 sacudida = enEspasmo
            ? new Vector3(Random.Range(-0.02f, 0.02f), Random.Range(0f, 0.02f), Random.Range(-0.02f, 0.02f))
            : Vector3.zero;
        transform.localPosition = posBaseLocal + sacudida;

        Quaternion giroSuelo = enEspasmo
            ? Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-5f, 5f), Random.Range(-4f, 4f))
            : Quaternion.Euler(0f, Mathf.Sin(t * 2.0f) * 1.5f, 0f);
        transform.localRotation = rotBaseLocal * giroSuelo;
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

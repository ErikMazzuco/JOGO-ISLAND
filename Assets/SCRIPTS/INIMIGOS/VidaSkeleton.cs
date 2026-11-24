using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public class VidaSkeleton : MonoBehaviour
{
    [Header("Vida")]
    [Tooltip("Quantos golpes de espada ele aguenta antes de morrer.")]
    public int golpesParaMorrer = 2;
    [Tooltip("Golpes que chegam juntos (menos que isso de intervalo) contam como um só.")]
    public float intervaloEntreGolpes = 0.3f;

    // Campo antigo (a vida agora é contada em golpes). Mantido para não perder dados do prefab.
    [HideInInspector] public float vida = 100f;

    [Header("Efeito do golpe")]
    public Color corDoGolpe = new Color(1f, 0.35f, 0.3f);
    public float tempoPiscar = 0.12f;

    [Header("Morte")]
    [Tooltip("Tempo que ele leva para cair deitado (segundos).")]
    public float tempoParaCair = 0.7f;
    [Tooltip("Quanto tempo fica deitado no chão antes de começar a sumir.")]
    public float esperaDeitado = 1.5f;
    [Tooltip("Tempo que leva para desaparecer aos poucos.")]
    public float tempoParaSumir = 2f;
    [Tooltip("Quanto ele afunda no chão enquanto some (metros).")]
    public float afundar = 0.15f;

    [Header("Sons")]
    [Tooltip("Som de quando a espada acerta o esqueleto (toca em todo golpe, inclusive o que mata).")]
    public AudioClip somDano;
    [Range(0f, 1f)] public float volumeDano = 1f;
    [Tooltip("Som de quando o esqueleto morre (gemido, queda e ossos).")]
    public AudioClip somMorte;
    [Range(0f, 1f)] public float volumeMorte = 1f;

    [Header("Animação de morte do Animator (opcional)")]
    public bool tocarAnimacaoMorte = false;
    public string animacaoMorte = "DEATH";

    private Animator anim;
    private NavMeshAgent agent;
    private bool morto;
    private int golpesLevados;
    private float ultimoGolpe = -999f;

    public bool EstaMorto { get { return morto; } }
    public int GolpesRestantes { get { return Mathf.Max(0, golpesParaMorrer - golpesLevados); } }

    void Awake()
    {
        // Quem move o esqueleto é o NavMeshAgent. Um Rigidbody com física ligada briga com ele:
        // o esqueleto voa, gira, é arremessado ou afunda no chão. Deixamos o Rigidbody "cinemático"
        // (o Collider continua existindo, então o golpe da espada ainda acerta).
        var corpo = GetComponent<Rigidbody>();
        if (corpo != null)
        {
            corpo.isKinematic = true;
            corpo.useGravity = false;
        }

        anim = GetComponentInChildren<Animator>();

        // O movimento vem do agente, não da animação (root motion também faz o esqueleto se mexer sozinho)
        if (anim != null) anim.applyRootMotion = false;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // Cada golpe tira um "ponto de vida", não importa o dano da espada
    public void ReceberDano(float dano)
    {
        if (morto) return;
        if (Time.time - ultimoGolpe < intervaloEntreGolpes) return;   // mesmo golpe chegando duas vezes
        ultimoGolpe = Time.time;

        golpesLevados++;
        Debug.Log("Esqueleto levou golpe: faltam " + GolpesRestantes);

        TocarNoLocal(somDano, volumeDano);

        if (golpesLevados >= Mathf.Max(1, golpesParaMorrer))
        {
            Morrer();
            return;
        }

        StartCoroutine(Piscar());
        var ia = GetComponent<InimigoSkeleton>();
        if (ia != null && ia.enabled) ia.AoLevarGolpe();
    }

    // Toca um som 3D no lugar do esqueleto, numa fonte temporária (continua tocando mesmo se ele for
    // destruído) e com o tom variando um pouco para não soar sempre igual.
    void TocarNoLocal(AudioClip clip, float volume)
    {
        if (clip == null) return;

        float tom = Random.Range(0.94f, 1.06f);

        var go = new GameObject("SomEsqueleto");
        go.transform.position = transform.position + Vector3.up;

        var fonte = go.AddComponent<AudioSource>();
        fonte.clip = clip;
        fonte.volume = volume;
        fonte.pitch = tom;
        fonte.spatialBlend = 1f;
        fonte.rolloffMode = AudioRolloffMode.Linear;
        fonte.minDistance = 4f;
        fonte.maxDistance = 45f;
        fonte.Play();

        Destroy(go, clip.length / tom + 0.2f);
    }

    // Pisca avermelhado ao levar golpe
    IEnumerator Piscar()
    {
        var blocos = new List<(Renderer, MaterialPropertyBlock)>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            var antes = new MaterialPropertyBlock();
            r.GetPropertyBlock(antes);
            mpb.SetColor("_BaseColor", corDoGolpe);
            mpb.SetColor("_Color", corDoGolpe);
            r.SetPropertyBlock(mpb);
            blocos.Add((r, antes));
        }

        yield return new WaitForSeconds(tempoPiscar);

        foreach (var (r, antes) in blocos)
            if (r != null) r.SetPropertyBlock(antes);
    }

    /// <summary>Chamado quando o esqueleto morre (o chefão usa para soltar o último baú).</summary>
    public event System.Action Morreu;

    /// <summary>Mata na hora (usado em testes).</summary>
    public void MatarAgora() { if (!morto) Morrer(); }

    void Morrer()
    {
        morto = true;
        if (Morreu != null) Morreu();

        // Parar movimentação
        if (agent != null)
        {
            agent.enabled = false;
        }

        // Parar o comportamento do inimigo (senão ele continua atacando e tocando sons depois de morto)
        var ia = GetComponent<InimigoSkeleton>();
        if (ia != null) ia.enabled = false;

        foreach (var fonte in GetComponents<AudioSource>())
            fonte.Stop();

        // Som de morte (fonte separada, assim ele toca inteiro mesmo se o esqueleto for destruído)
        TocarNoLocal(somMorte, volumeMorte);

        // Animação de morte do próprio Animator (só se existir e estiver ligada)
        if (anim != null)
        {
            if (tocarAnimacaoMorte && anim.HasState(0, Animator.StringToHash(animacaoMorte)))
                anim.Play(animacaoMorte, 0, 0f);
            else
                anim.speed = 0f; // congela a pose (senão ele "anda" deitado no chão)
        }

        // Desativar colisores (o jogador pode passar pelo corpo)
        Collider[] colisores = GetComponentsInChildren<Collider>();

        foreach (Collider col in colisores)
        {
            col.enabled = false;
        }

        Debug.Log("Esqueleto morreu!");

        StartCoroutine(CairEDesaparecer());
    }

    IEnumerator CairEDesaparecer()
    {
        // ---------- 1) Cai deitado, de costas para longe do jogador ----------
        Vector3 empurrao = transform.forward * -1f; // se não achar o jogador, cai para trás
        var vidaJogador = FindFirstObjectByType<VidaJogador>();

        if (vidaJogador != null)
        {
            Vector3 longe = transform.position - vidaJogador.transform.position;
            longe.y = 0f;
            if (longe.sqrMagnitude > 0.001f) empurrao = longe.normalized;
        }

        Vector3 eixo = Vector3.Cross(Vector3.up, empurrao);
        Quaternion inicio = transform.rotation;
        Quaternion fim = Quaternion.AngleAxis(90f, eixo) * inicio;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, tempoParaCair);
            float suave = t * t; // começa devagar e acelera, como quem desaba
            transform.rotation = Quaternion.Slerp(inicio, fim, Mathf.Clamp01(suave));
            yield return null;
        }
        transform.rotation = fim;

        // ---------- 2) Fica deitado um tempinho ----------
        yield return new WaitForSeconds(esperaDeitado);

        // ---------- 3) Some aos poucos (e afunda um pouquinho) ----------
        var materiais = new List<Material>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;

            foreach (Material m in r.materials) // cópias, não mexe nos materiais originais
            {
                TornarTransparente(m);
                materiais.Add(m);
            }
        }

        Vector3 posInicial = transform.position;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, tempoParaSumir);
            float a = 1f - Mathf.Clamp01(t);

            foreach (Material m in materiais)
                DefinirAlfa(m, a);

            transform.position = posInicial + Vector3.down * (afundar * Mathf.Clamp01(t));
            yield return null;
        }

        Destroy(gameObject);
    }

    // ---------- Transparência em tempo de execução (URP Lit e Standard) ----------
    static void TornarTransparente(Material m)
    {
        if (m.HasProperty("_Surface")) // URP
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            m.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        else if (m.HasProperty("_Mode")) // Standard
        {
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
    }

    static void DefinirAlfa(Material m, float alfa)
    {
        if (m.HasProperty("_BaseColor"))
        {
            Color c = m.GetColor("_BaseColor");
            c.a = alfa;
            m.SetColor("_BaseColor", c);
        }
        else if (m.HasProperty("_Color"))
        {
            Color c = m.GetColor("_Color");
            c.a = alfa;
            m.SetColor("_Color", c);
        }
    }
}

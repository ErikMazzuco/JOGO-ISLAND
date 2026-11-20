using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public class VidaSkeleton : MonoBehaviour
{
    public float vida = 100f;

    [Header("Morte")]
    [Tooltip("Tempo que ele leva para cair deitado (segundos).")]
    public float tempoParaCair = 0.7f;
    [Tooltip("Quanto tempo fica deitado no chão antes de começar a sumir.")]
    public float esperaDeitado = 1.5f;
    [Tooltip("Tempo que leva para desaparecer aos poucos.")]
    public float tempoParaSumir = 2f;
    [Tooltip("Quanto ele afunda no chão enquanto some (metros).")]
    public float afundar = 0.15f;

    [Header("Som de morte")]
    [Tooltip("Som tocado quando o esqueleto morre (MORTE INIMIGO.mp3).")]
    public AudioClip somMorte;
    [Range(0f, 1f)] public float volumeMorte = 1f;

    [Header("Animação de morte do Animator (opcional)")]
    public bool tocarAnimacaoMorte = false;
    public string animacaoMorte = "DEATH";

    private Animator anim;
    private NavMeshAgent agent;
    private bool morto;

    public bool EstaMorto { get { return morto; } }

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

    public void ReceberDano(float dano)
    {
        if (morto) return;

        vida -= dano;

        Debug.Log("Vida do esqueleto: " + vida);

        if (vida <= 0)
        {
            Morrer();
        }
    }

    void Morrer()
    {
        morto = true;

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
        if (somMorte != null)
            AudioSource.PlayClipAtPoint(somMorte, transform.position + Vector3.up, volumeMorte);

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

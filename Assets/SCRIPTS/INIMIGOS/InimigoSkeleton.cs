using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// IA do esqueleto:
///  - PARADO/PASSEANDO: anda devagar em volta de onde nasceu.
///  - VER O JOGADOR: só quando o jogador está perto (distanciaVisao), NA FRENTE dele (anguloVisao)
///    e sem parede no meio. Muito perto (distanciaPercepcao) ele percebe mesmo de costas.
///  - PERSEGUIR: depois que viu, não desiste porque o jogador correu. Lembra onde o jogador estava
///    e só desiste se o jogador ficar muito longe (distanciaDesistir) ou sumir da vista por tempoMemoria.
///  - Levar um golpe também faz ele perseguir, e ele chama os esqueletos que estão por perto.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class InimigoSkeleton : MonoBehaviour
{
    [Header("Visão (quando ele percebe o jogador)")]
    [Tooltip("Distância máxima em que ele enxerga o jogador na frente dele.")]
    public float distanciaVisao = 12f;
    [Tooltip("Abertura da visão (graus). 120 = enxerga 60° para cada lado.")]
    [Range(10f, 360f)] public float anguloVisao = 120f;
    [Tooltip("Tão perto que ele percebe mesmo de costas (ouve o jogador).")]
    public float distanciaPercepcao = 3f;
    [Tooltip("Altura dos olhos do esqueleto.")]
    public float alturaOlhos = 1.4f;

    [Header("Perseguição")]
    [Tooltip("Se o jogador ficar mais longe que isso, ele desiste.")]
    public float distanciaDesistir = 35f;
    [Tooltip("Segundos que ele continua indo atrás depois de perder o jogador de vista.")]
    public float tempoMemoria = 12f;
    [Tooltip("Ao perceber o jogador (ou levar golpe), chama os esqueletos que estão a esta distância.")]
    public float raioChamarAmigos = 10f;
    public float velocidadePerseguir = 3.2f;
    public float velocidadePassear = 0.9f;

    [Header("Passear (quando não viu o jogador)")]
    public float raioPassear = 4f;
    public Vector2 pausaPassear = new Vector2(2f, 6f);

    [Header("Ataque")]
    public float rangeAtaque = 2f;
    public float dano = 10f;
    public float intervaloAtaque = 1.5f;

    [Header("Animações")]
    public string animParado = "IDLE";
    public string animAndar = "WALK";
    public string animAtaque = "ATTACK";

    [Header("Sons")]
    public AudioClip somAndar;
    public AudioClip somAtaque;
    public AudioClip somRugido;

    [Header("Configuração dos sons")]
    public float intervaloPassos = 0.6f;
    [Range(0f, 1f)] public float volumePassos = 0.8f;
    [Range(0f, 1f)] public float volumeAtaque = 1f;
    [Range(0f, 1f)] public float volumeRugido = 1f;

    // Campo antigo (substituído por distanciaVisao). Mantido para não perder dados do prefab.
    [HideInInspector] public float rangeVisao = 15f;

    NavMeshAgent agent;
    Animator anim;
    Transform cabecaJogador;     // câmera (para a linha de visão)
    Transform corpoJogador;      // objeto do jogador (para distância e ataque)
    VidaJogador vidaJogador;

    AudioSource audioPassos;
    AudioSource audioAtaque;
    AudioSource audioRugido;

    float proximoAtaque;
    float proximoPasso;
    float proximaChecagem;
    float proximoPasseio;
    float ultimaVezQueViu = -999f;
    float atordoadoAte;
    Vector3 ultimaPosicaoVista;
    Vector3 casa;

    string animAtual;
    bool perseguindo;

    public bool Perseguindo { get { return perseguindo; } }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        vidaJogador = FindFirstObjectByType<VidaJogador>();
        if (vidaJogador != null) corpoJogador = vidaJogador.transform;
        if (Camera.main != null) cabecaJogador = Camera.main.transform;
        if (corpoJogador == null) corpoJogador = cabecaJogador;
        if (cabecaJogador == null) cabecaJogador = corpoJogador;

        agent.stoppingDistance = rangeAtaque * 0.8f;
        agent.speed = velocidadePassear;
        casa = transform.position;
        proximoPasseio = Time.time + Random.Range(pausaPassear.x, pausaPassear.y);
        proximaChecagem = Time.time + Random.value * 0.2f;   // espalha as checagens entre os esqueletos

        audioPassos = CriarAudioSource();
        audioAtaque = CriarAudioSource();
        audioRugido = CriarAudioSource();
    }

    AudioSource CriarAudioSource()
    {
        AudioSource fonte = gameObject.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.loop = false;
        fonte.spatialBlend = 1f;
        fonte.rolloffMode = AudioRolloffMode.Linear;
        fonte.maxDistance = 20f;
        return fonte;
    }

    void Update()
    {
        if (corpoJogador == null || !agent.isOnNavMesh) return;

        bool jogadorMorto = vidaJogador != null && vidaJogador.Morto;
        if (jogadorMorto && perseguindo) Desistir();

        // Olhos: checa a visão algumas vezes por segundo (mais leve com muitos esqueletos)
        if (!jogadorMorto && Time.time >= proximaChecagem)
        {
            proximaChecagem = Time.time + 0.2f;
            if (PodeVerJogador())
            {
                ultimaVezQueViu = Time.time;
                ultimaPosicaoVista = corpoJogador.position;
                if (!perseguindo) ComecarPerseguir(true);
            }
        }

        if (Time.time < atordoadoAte)
        {
            // levou golpe: fica um instante parado
            agent.isStopped = true;
            Tocar(animParado);
            AtualizarSomPassos();
            return;
        }

        if (perseguindo) Perseguir();
        else Passear();

        AtualizarSomPassos();
    }

    // ---------- Visão ----------

    bool PodeVerJogador()
    {
        Vector3 olhos = transform.position + Vector3.up * alturaOlhos;
        Vector3 alvo = cabecaJogador.position;
        Vector3 dir = alvo - olhos;
        float dist = dir.magnitude;

        // Já perseguindo, ele enxerga mais longe (não perde o jogador por ele ter se afastado um pouco)
        float alcance = perseguindo ? distanciaDesistir : distanciaVisao;
        if (dist > alcance) return false;

        Vector3 plano = dir; plano.y = 0f;
        bool naFrente = Vector3.Angle(transform.forward, plano) <= anguloVisao * 0.5f;
        if (!perseguindo && !naFrente && dist > distanciaPercepcao) return false;

        return LinhaDeVisao(olhos, alvo, dist);
    }

    // Nada no meio (parede, pilar...) entre os olhos dele e o jogador?
    bool LinhaDeVisao(Vector3 de, Vector3 para, float dist)
    {
        var acertos = Physics.RaycastAll(de, (para - de) / Mathf.Max(dist, 0.001f), dist, ~0, QueryTriggerInteraction.Ignore);
        float maisPerto = float.MaxValue;
        bool jogadorPrimeiro = true;
        foreach (var h in acertos)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;                     // ele mesmo
            if (h.collider.GetComponentInParent<VidaSkeleton>() != null) continue;      // outro esqueleto não tapa
            if (h.distance >= maisPerto) continue;
            maisPerto = h.distance;
            jogadorPrimeiro = h.collider.GetComponentInParent<VidaJogador>() != null ||
                              (cabecaJogador != null && h.collider.transform.IsChildOf(cabecaJogador.root));
        }
        return jogadorPrimeiro;
    }

    // ---------- Estados ----------

    void ComecarPerseguir(bool chamarAmigos)
    {
        perseguindo = true;
        agent.speed = velocidadePerseguir;
        if (ultimaVezQueViu < 0f) { ultimaVezQueViu = Time.time; ultimaPosicaoVista = corpoJogador.position; }

        if (somRugido != null) audioRugido.PlayOneShot(somRugido, volumeRugido);

        if (chamarAmigos)
        {
            foreach (var outro in FindObjectsByType<InimigoSkeleton>(FindObjectsSortMode.None))
            {
                if (outro == this || outro.perseguindo || !outro.isActiveAndEnabled) continue;
                if ((outro.transform.position - transform.position).sqrMagnitude <= raioChamarAmigos * raioChamarAmigos)
                    outro.Alertar(corpoJogador.position, false);
            }
        }
    }

    /// <summary>Faz o esqueleto ir atrás do jogador (chamado por amigos ou ao levar golpe).</summary>
    public void Alertar(Vector3 posicaoJogador, bool chamarAmigos)
    {
        ultimaVezQueViu = Time.time;
        ultimaPosicaoVista = posicaoJogador;
        if (!perseguindo) ComecarPerseguir(chamarAmigos);
    }

    /// <summary>Chamado pela VidaSkeleton quando leva um golpe e não morre.</summary>
    public void AoLevarGolpe()
    {
        if (corpoJogador != null) Alertar(corpoJogador.position, true);
        atordoadoAte = Time.time + 0.35f;
        proximoAtaque = Mathf.Max(proximoAtaque, Time.time + 0.6f);

        // pequeno empurrão para trás
        if (corpoJogador != null && agent.isOnNavMesh)
        {
            Vector3 longe = transform.position - corpoJogador.position; longe.y = 0f;
            if (longe.sqrMagnitude > 0.001f) agent.Move(longe.normalized * 0.4f);
        }
    }

    void Desistir()
    {
        perseguindo = false;
        agent.speed = velocidadePassear;
        if (agent.isOnNavMesh) agent.SetDestination(casa);   // volta para o lugar dele
        proximoPasseio = Time.time + Random.Range(pausaPassear.x, pausaPassear.y);
    }

    void Perseguir()
    {
        float dist = DistanciaPlana(transform.position, corpoJogador.position);
        bool vendoAgora = Time.time - ultimaVezQueViu < 0.5f;

        if (dist > distanciaDesistir || Time.time - ultimaVezQueViu > tempoMemoria)
        {
            Desistir();
            return;
        }

        if (dist <= rangeAtaque && vendoAgora)
        {
            agent.isStopped = true;
            OlharPara(corpoJogador.position);
            Tocar(animAtaque);

            if (Time.time >= proximoAtaque)
            {
                proximoAtaque = Time.time + intervaloAtaque;

                if (somAtaque != null && !audioAtaque.isPlaying)
                {
                    audioAtaque.clip = somAtaque;
                    audioAtaque.volume = volumeAtaque;
                    audioAtaque.Play();
                }

                if (vidaJogador != null) vidaJogador.LevarDano(dano);
            }
            return;
        }

        // Vendo: vai direto no jogador. Sem ver: vai até o último lugar onde viu.
        agent.isStopped = false;
        agent.SetDestination(vendoAgora ? corpoJogador.position : ultimaPosicaoVista);
        if (vendoAgora && dist < distanciaVisao) OlharPara(corpoJogador.position);
        Tocar(agent.velocity.sqrMagnitude > 0.05f ? animAndar : animParado);
    }

    void Passear()
    {
        if (agent.pathPending) return;

        bool chegou = agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.3f);
        if (chegou)
        {
            agent.isStopped = true;
            Tocar(animParado);

            if (Time.time >= proximoPasseio)
            {
                proximoPasseio = Time.time + Random.Range(pausaPassear.x, pausaPassear.y);
                Vector2 r = Random.insideUnitCircle * raioPassear;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(casa + new Vector3(r.x, 0f, r.y), out hit, 1.5f, agent.areaMask))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                }
            }
        }
        else
        {
            agent.isStopped = false;
            Tocar(animAndar);
        }
    }

    // ---------- Utilidades ----------

    static float DistanciaPlana(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void AtualizarSomPassos()
    {
        bool caminhando = !agent.isStopped && agent.velocity.sqrMagnitude > 0.1f;

        // Correndo atrás do jogador a animação de andar acelera junto
        if (anim != null)
            anim.speed = animAtual == animAndar ? Mathf.Clamp(agent.velocity.magnitude / Mathf.Max(0.1f, velocidadePassear), 1f, 2.2f) : 1f;

        if (caminhando)
        {
            if (!audioPassos.isPlaying && Time.time >= proximoPasso && somAndar != null)
            {
                audioPassos.clip = somAndar;
                audioPassos.volume = volumePassos;
                audioPassos.Play();
                proximoPasso = Time.time + Mathf.Max(intervaloPassos, somAndar.length);
            }
        }
        else
        {
            audioPassos.Stop();
            proximoPasso = Time.time + 0.1f;
        }
    }

    void OlharPara(Vector3 pos)
    {
        Vector3 dir = pos - transform.position;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
    }

    void Tocar(string nome)
    {
        if (animAtual == nome || anim == null) return;
        animAtual = nome;
        anim.CrossFade(nome, 0.15f);
    }

    // Mostra a visão no editor (Scene) quando o esqueleto está selecionado
    void OnDrawGizmosSelected()
    {
        Vector3 olhos = transform.position + Vector3.up * alturaOlhos;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        Vector3 esq = Quaternion.Euler(0f, -anguloVisao * 0.5f, 0f) * transform.forward;
        Vector3 dir = Quaternion.Euler(0f, anguloVisao * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(olhos, olhos + esq * distanciaVisao);
        Gizmos.DrawLine(olhos, olhos + dir * distanciaVisao);
        Gizmos.DrawLine(olhos, olhos + transform.forward * distanciaVisao);
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, distanciaPercepcao);
    }
}

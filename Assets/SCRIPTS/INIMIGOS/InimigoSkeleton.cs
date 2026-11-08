
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class InimigoSkeleton : MonoBehaviour
{
    [Header("Alcance")]
    public float rangeVisao = 15f;
    public float rangeAtaque = 2f;

    [Header("Ataque")]
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
    [Range(0f, 1f)]
    public float volumePassos = 0.8f;
    [Range(0f, 1f)]
    public float volumeAtaque = 1f;
    [Range(0f, 1f)]
    public float volumeRugido = 1f;

    NavMeshAgent agent;
    Animator anim;
    Transform alvo;

    AudioSource audioPassos;
    AudioSource audioAtaque;
    AudioSource audioRugido;

    float proximoAtaque;
    float proximoPasso;

    string animAtual;
    bool alvoDetectado;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        if (Camera.main != null)
            alvo = Camera.main.transform;

        agent.stoppingDistance = rangeAtaque * 0.8f;

        // Criar fontes de áudio
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
        if (alvo == null) return;

        Vector3 a = transform.position;
        a.y = 0;

        Vector3 b = alvo.position;
        b.y = 0;

        float dist = Vector3.Distance(a, b);

        if (dist <= rangeAtaque)
        {
            agent.isStopped = true;

            OlharPara(alvo.position);
            Tocar(animAtaque);

            if (Time.time >= proximoAtaque)
            {
                proximoAtaque = Time.time + intervaloAtaque;

                // Toca o som somente se não estiver tocando
                if (somAtaque != null && !audioAtaque.isPlaying)
                {
                    audioAtaque.clip = somAtaque;
                    audioAtaque.volume = volumeAtaque;
                    audioAtaque.Play();
                }

                var vida = alvo.GetComponentInParent<VidaJogador>();

                if (vida != null)
                    vida.LevarDano(dano);
            }
        }
        else if (dist <= rangeVisao)
        {
            agent.isStopped = false;
            agent.SetDestination(alvo.position);

            // Rugido ao detectar o jogador
            if (!alvoDetectado)
            {
                alvoDetectado = true;

                if (somRugido != null)
                    audioRugido.PlayOneShot(somRugido, volumeRugido);
            }

            Tocar(animAndar);
        }
        else
        {
            agent.isStopped = true;
            alvoDetectado = false;

            Tocar(animParado);
        }

        AtualizarSomPassos();
    }

    void AtualizarSomPassos()
    {
        bool caminhando = !agent.isStopped &&
                          agent.velocity.sqrMagnitude > 0.1f;

        if (caminhando)
        {
            if (!audioPassos.isPlaying && Time.time >= proximoPasso)
            {
                if (somAndar != null)
                {
                    audioPassos.clip = somAndar;
                    audioPassos.volume = volumePassos;
                    audioPassos.Play();

                    proximoPasso = Time.time +
                        Mathf.Max(intervaloPassos, somAndar.length);
                }
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
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                10f * Time.deltaTime
            );
        }
    }

    void Tocar(string nome)
    {
        if (animAtual == nome) return;

        animAtual = nome;
        anim.CrossFade(nome, 0.15f);
    }
}

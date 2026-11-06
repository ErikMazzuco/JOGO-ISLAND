using UnityEngine;
using UnityEngine.AI;

public class InimigoSkeleton : MonoBehaviour
{
    [Header("Alcance")]
    public float rangeVisao = 15f;
    public float rangeAtaque = 2f;

    [Header("Ataque")]
    public float dano = 10f;
    public float intervaloAtaque = 1.5f;

    [Header("Nomes dos estados no Animator")]
    public string animParado = "IDLE";
    public string animAndar = "WALK";
    public string animAtaque = "ATTACK";

    NavMeshAgent agent;
    Animator anim;
    Transform alvo;
    float proximoAtaque;
    string animAtual;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        alvo = Camera.main.transform;   // seu jogador (Main Camera)
        agent.stoppingDistance = rangeAtaque * 0.8f;
    }

    void Update()
    {
        if (alvo == null) return;

        // distância ignorando altura (a câmera fica no alto)
        Vector3 a = transform.position; a.y = 0;
        Vector3 b = alvo.position; b.y = 0;
        float dist = Vector3.Distance(a, b);

        if (dist <= rangeAtaque)
        {
            agent.isStopped = true;
            OlharPara(alvo.position);
            Tocar(animAtaque);

            if (Time.time >= proximoAtaque)
            {
                proximoAtaque = Time.time + intervaloAtaque;
                var vida = alvo.GetComponentInParent<VidaJogador>();
                if (vida != null) vida.LevarDano(dano);
            }
        }
        else if (dist <= rangeVisao)
        {
            agent.isStopped = false;
            agent.SetDestination(alvo.position);
            Tocar(animAndar);
        }
        else
        {
            agent.isStopped = true;
            Tocar(animParado);
        }
    }

    void OlharPara(Vector3 pos)
    {
        Vector3 dir = pos - transform.position; dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), 10f * Time.deltaTime);
    }

    void Tocar(string nome)
    {
        if (animAtual == nome) return;
        animAtual = nome;
        anim.CrossFade(nome, 0.15f);
    }
}
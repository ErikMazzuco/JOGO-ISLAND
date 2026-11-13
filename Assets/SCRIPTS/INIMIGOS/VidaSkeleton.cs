
using UnityEngine;
using UnityEngine.AI;

public class VidaSkeleton : MonoBehaviour
{
    public float vida = 100f;

    [Header("Morte")]
    public string animacaoMorte = "DEATH";
    public float tempoParaDestruir = 3f;

    private Animator anim;
    private NavMeshAgent agent;
    private bool morto;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
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

        // Executar animação de morte
        if (anim != null)
        {
            anim.Play(animacaoMorte, 0, 0f);
        }
        else
        {
            Debug.LogWarning("Animator do esqueleto não encontrado!");
        }

        // Desativar colisores
        Collider[] colisores = GetComponentsInChildren<Collider>();

        foreach (Collider col in colisores)
        {
            col.enabled = false;
        }

        Debug.Log("Esqueleto morreu!");

        // Destruir após a animação
        Destroy(gameObject, tempoParaDestruir);
    }
}

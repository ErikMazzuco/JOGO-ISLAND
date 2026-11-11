
using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class GalinhaAI : MonoBehaviour
{
    [Header("Movimentação")]
    public float distanciaAndar = 8f;
    public float tempoParadaMin = 2f;
    public float tempoParadaMax = 5f;

    [Header("Sons")]
    public AudioClip somGalinha;
    public float intervaloMin = 8f;
    public float intervaloMax = 18f;

    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource audioSource;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();

        animator.SetFloat("State", 0);

        StartCoroutine(AndarAleatoriamente());
        StartCoroutine(SonsAleatorios());
    }

    void Update()
    {
        bool andando = agent.velocity.magnitude > 0.1f;
        animator.SetFloat("Vert", andando ? 1f : 0f);
    }

    IEnumerator AndarAleatoriamente()
    {
        while (true)
        {
            Vector3 destino = transform.position +
                Random.insideUnitSphere * distanciaAndar;

            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                destino, out hit, distanciaAndar, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);

                while (agent.pathPending ||
                    agent.remainingDistance > agent.stoppingDistance)
                {
                    yield return null;
                }
            }

            yield return new WaitForSeconds(
                Random.Range(tempoParadaMin, tempoParadaMax));
        }
    }

    IEnumerator SonsAleatorios()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                Random.Range(intervaloMin, intervaloMax));

            if (somGalinha != null && audioSource != null)
            {
                audioSource.PlayOneShot(somGalinha);
            }
        }
    }
}

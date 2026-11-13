
using UnityEngine;

public class AtaqueEspada : MonoBehaviour
{
    [Header("Configurações")]
    public float alcance = 2.5f;
    public float dano = 50f;
    public float intervaloAtaque = 0.8f;

    [Header("Animação")]
    public Animator animacaoArma;
    public string nomeAnimacao = "SWORD_ATTACK";

    [Header("Som")]
    public AudioSource audioSource;
    public AudioClip somEspada;

    private float proximoAtaque;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && Time.time >= proximoAtaque)
        {
            Atacar();
            proximoAtaque = Time.time + intervaloAtaque;
        }
    }

    void Atacar()
    {
        // Executar animação
        if (animacaoArma != null)
        {
            animacaoArma.Play(nomeAnimacao, 0, 0f);
        }

        // Reproduzir som
        if (audioSource != null && somEspada != null)
        {
            audioSource.PlayOneShot(somEspada);
        }

        // Verificar inimigo na frente
        RaycastHit hit;

        if (Physics.Raycast(transform.position, transform.forward,
            out hit, alcance))
        {
            VidaSkeleton inimigo =
                hit.collider.GetComponentInParent<VidaSkeleton>();

            if (inimigo != null)
            {
                inimigo.ReceberDano(dano);
                Debug.Log("Espada acertou o esqueleto!");
            }
        }
    }
}

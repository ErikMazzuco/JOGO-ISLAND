using UnityEngine;

/// <summary>
/// Faz a luz da tocha tremer como fogo e desliga luz + fogo quando o jogador está longe
/// (economiza desempenho quando há muitas tochas).
/// Colocado pelo menu  ISLAND > Decorar Templo_Yas.
/// </summary>
public class ChamaTocha : MonoBehaviour
{
    public Light luz;
    public ParticleSystem fogo;

    [Header("Tremido")]
    public float intensidadeBase = 2.2f;
    public float variacao = 0.6f;
    public float velocidade = 7f;

    [Header("Desempenho")]
    [Tooltip("Acima desta distância do jogador a luz e o fogo desligam.")]
    public float distanciaMaxima = 45f;

    Transform jogador;
    float semente;
    float proximaChecagem;
    bool ligada = true;

    void Start()
    {
        semente = Random.Range(0f, 100f);

        var vida = FindFirstObjectByType<VidaJogador>();
        if (vida != null) jogador = vida.transform;
        else if (Camera.main != null) jogador = Camera.main.transform;
    }

    void Update()
    {
        if (Time.time >= proximaChecagem)
        {
            proximaChecagem = Time.time + 0.3f + Random.value * 0.2f;
            bool perto = jogador == null ||
                         (jogador.position - transform.position).sqrMagnitude <= distanciaMaxima * distanciaMaxima;
            if (perto != ligada) Ligar(perto);
        }

        if (ligada && luz != null)
        {
            float ruido = Mathf.PerlinNoise(semente, Time.time * velocidade) - 0.5f;
            luz.intensity = Mathf.Max(0f, intensidadeBase + ruido * 2f * variacao);
        }
    }

    void Ligar(bool ligar)
    {
        ligada = ligar;
        if (luz != null) luz.enabled = ligar;
        if (fogo != null)
        {
            if (ligar) fogo.Play();
            else fogo.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}

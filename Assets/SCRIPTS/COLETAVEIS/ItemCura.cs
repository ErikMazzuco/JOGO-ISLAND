using UnityEngine;

/// <summary>
/// Coletável de cura: gira, flutua e, ao ser pego, recupera vida do jogador e toca um som.
/// A coleta é feita pela distância até o jogador, então não precisa de collider nem de tag.
/// Para preparar itens da cena: selecione na Hierarchy e use  ISLAND > Preparar Itens de Cura Selecionados.
/// </summary>
public class ItemCura : MonoBehaviour
{
    [Header("Cura")]
    [Tooltip("Quanto de vida o item recupera.")]
    public float quantidadeCura = 25f;
    [Tooltip("Se ligado, o item só é pego quando o jogador está machucado (senão ele fica esperando).")]
    public bool soPegaSeMachucado = true;

    [Header("Animação")]
    public bool girar = true;
    public float velocidadeGiro = 90f;       // graus por segundo
    public float alturaFlutuar = 0.12f;      // quanto sobe e desce (metros)
    public float velocidadeFlutuar = 2f;

    [Header("Coleta")]
    public float raioColeta = 1.6f;          // distância horizontal para pegar
    public float alturaMaximaColeta = 2.5f;  // diferença de altura aceita

    [Header("Som")]
    public AudioClip somColeta;
    [Range(0f, 1f)] public float volume = 1f;

    VidaJogador vidaJogador;
    Vector3 posicaoBase;
    float fase;
    bool coletado;

    void Awake()
    {
        // A coleta é por distância: colliders do item só atrapalhariam o jogador e a espada.
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;
    }

    void Start()
    {
        posicaoBase = transform.position;
        fase = Random.Range(0f, Mathf.PI * 2f);

        vidaJogador = FindFirstObjectByType<VidaJogador>();
    }

    void Update()
    {
        if (coletado) return;

        if (girar)
            transform.Rotate(0f, velocidadeGiro * Time.deltaTime, 0f, Space.World);

        float y = Mathf.Sin(Time.time * velocidadeFlutuar + fase) * alturaFlutuar;
        transform.position = posicaoBase + Vector3.up * y;

        if (vidaJogador == null || vidaJogador.Morto) return;

        Vector3 diferenca = vidaJogador.transform.position - transform.position;
        float altura = Mathf.Abs(diferenca.y);
        diferenca.y = 0f;

        if (diferenca.magnitude > raioColeta || altura > alturaMaximaColeta) return;

        if (soPegaSeMachucado && vidaJogador.vida >= vidaJogador.vidaMaxima) return;

        Coletar();
    }

    void Coletar()
    {
        coletado = true;

        vidaJogador.Curar(quantidadeCura);
        TocarSom();

        Destroy(gameObject);
    }

    void TocarSom()
    {
        if (somColeta == null) return;

        // Fonte de áudio própria (2D), porque o item é destruído logo em seguida
        var go = new GameObject("SomCura");
        go.transform.position = transform.position;

        var fonte = go.AddComponent<AudioSource>();
        fonte.clip = somColeta;
        fonte.volume = volume;
        fonte.spatialBlend = 0f;
        fonte.Play();

        Destroy(go, somColeta.length + 0.1f);
    }
}

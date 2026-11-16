using UnityEngine;

/// <summary>
/// Moeda coletável: gira, flutua, soma no contador ao ser pega e toca um som.
/// A coleta é feita pela distância até o jogador, então não precisa de collider nem de tag.
/// Para preparar moedas da cena: selecione na Hierarchy e use  ISLAND > Preparar Moedas Selecionadas.
/// </summary>
public class Moeda : MonoBehaviour
{
    [Header("Valor")]
    public int valor = 1;

    [Header("Animação")]
    [Tooltip("Endireita a moeda (de pé) se o modelo estiver deitado.")]
    public bool ficarDePe = true;
    public float velocidadeGiro = 120f;      // graus por segundo
    public float alturaFlutuar = 0.12f;      // quanto sobe e desce (metros)
    public float velocidadeFlutuar = 2f;

    [Header("Coleta")]
    public float raioColeta = 1.6f;          // distância horizontal para pegar
    public float alturaMaximaColeta = 2.5f;  // diferença de altura aceita

    [Header("Som")]
    public AudioClip somColeta;
    [Range(0f, 1f)] public float volume = 1f;

    Transform jogador;
    Vector3 posicaoBase;
    float fase;
    bool coletada;

    void Awake()
    {
        // A coleta é por distância: colliders da moeda só atrapalhariam o jogador e a espada.
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;
    }

    void Start()
    {
        if (ficarDePe) FicarDePe();

        posicaoBase = transform.position;
        fase = Random.Range(0f, Mathf.PI * 2f);

        var vida = FindFirstObjectByType<VidaJogador>();

        if (vida != null) jogador = vida.transform;
        else if (Camera.main != null) jogador = Camera.main.transform;
    }

    void Update()
    {
        if (coletada) return;

        // Gira em torno do eixo vertical do mundo
        transform.Rotate(0f, velocidadeGiro * Time.deltaTime, 0f, Space.World);

        // Flutua para cima e para baixo
        float y = Mathf.Sin(Time.time * velocidadeFlutuar + fase) * alturaFlutuar;
        transform.position = posicaoBase + Vector3.up * y;

        if (jogador == null) return;

        Vector3 diferenca = jogador.position - transform.position;
        float altura = Mathf.Abs(diferenca.y);
        diferenca.y = 0f;

        if (diferenca.magnitude <= raioColeta && altura <= alturaMaximaColeta)
            Coletar();
    }

    void Coletar()
    {
        coletada = true;

        ContadorMoedas.Adicionar(valor);
        TocarSom();

        Destroy(gameObject);
    }

    void TocarSom()
    {
        if (somColeta == null) return;

        // Fonte de áudio própria (2D), porque a moeda é destruída logo em seguida
        var go = new GameObject("SomMoeda");
        go.transform.position = transform.position;

        var fonte = go.AddComponent<AudioSource>();
        fonte.clip = somColeta;
        fonte.volume = volume;
        fonte.spatialBlend = 0f;
        fonte.Play();

        Destroy(go, somColeta.length + 0.1f);
    }

    // Se o disco da moeda estiver deitado, levanta para ficar de frente (como uma moeda girando)
    void FicarDePe()
    {
        var filtro = GetComponentInChildren<MeshFilter>();
        if (filtro == null || filtro.sharedMesh == null) return;

        Vector3 tamanho = filtro.sharedMesh.bounds.size;

        Vector3 eixoFino;
        if (tamanho.x <= tamanho.y && tamanho.x <= tamanho.z) eixoFino = Vector3.right;
        else if (tamanho.y <= tamanho.z) eixoFino = Vector3.up;
        else eixoFino = Vector3.forward;

        Vector3 normal = filtro.transform.TransformDirection(eixoFino).normalized;

        if (Mathf.Abs(normal.y) > 0.7f)
            transform.rotation = Quaternion.FromToRotation(normal, Vector3.forward) * transform.rotation;
    }
}

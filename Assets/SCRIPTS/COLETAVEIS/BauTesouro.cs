using UnityEngine;

/// <summary>
/// Baú de tesouro: quando o jogador chega perto aparece "[E] Abrir baú".
/// Ao abrir, toca a animação do baú (LootBox), dá moedas e/ou vida e toca um som.
/// O baú principal (no PONTO_TESOURO) mostra a mensagem de tesouro encontrado.
/// Normalmente é colocado pelo menu  ISLAND > Baús > Colocar Baús nos Templos.
/// </summary>
public class BauTesouro : MonoBehaviour
{
    [Header("Recompensa")]
    [Tooltip("Moedas que o baú dá ao ser aberto.")]
    public int moedas = 5;
    [Tooltip("Vida que o baú recupera ao ser aberto (0 = nenhuma).")]
    public float cura = 0f;
    [Tooltip("Marque no baú do tesouro final (PONTO_TESOURO).")]
    public bool tesouroPrincipal = false;

    [Header("Abrir")]
    public KeyCode tecla = KeyCode.E;
    public float raioInteracao = 2.2f;           // distância horizontal para abrir
    public float alturaMaximaInteracao = 2.5f;   // diferença de altura aceita

    [Header("Som")]
    public AudioClip somAbrir;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Mensagem")]
    public string textoAviso = "[E] Abrir baú";
    public string textoTesouro = "Você encontrou o tesouro da ilha!";
    public float tempoMensagem = 4f;

    LootBox lootBox;
    Animator animator;
    VidaJogador vidaJogador;
    Transform jogador;
    bool aberto;
    bool perto;
    float mensagemAte;
    string mensagem;

    static GUIStyle estiloAviso, estiloMensagem;

    void Awake()
    {
        lootBox = GetComponent<LootBox>();
        animator = GetComponent<Animator>();

        if (lootBox != null)
        {
            // Quem abre é este script: desliga a abertura própria do pacote
            // (que dependia de colisão com Rigidbody e da tecla Espaço, que é o pulo)
            lootBox.openingMethod = OpeningMethods.OpenOnKeyPress;
            lootBox.keyCode = KeyCode.None;
        }

        // O baú não deve ser empurrado nem cair
        var corpo = GetComponent<Rigidbody>();
        if (corpo != null)
        {
            corpo.isKinematic = true;
            corpo.useGravity = false;
        }
    }

    void Start()
    {
        vidaJogador = FindFirstObjectByType<VidaJogador>();
        if (vidaJogador != null) jogador = vidaJogador.transform;
        else
        {
            var mov = FindFirstObjectByType<PlayerMovement>();
            if (mov != null) jogador = mov.transform;
        }
    }

    void Update()
    {
        perto = false;
        if (aberto || jogador == null) return;
        if (vidaJogador != null && vidaJogador.Morto) return;

        Vector3 diferenca = jogador.position - transform.position;
        float altura = Mathf.Abs(diferenca.y);
        diferenca.y = 0f;

        perto = diferenca.magnitude <= raioInteracao && altura <= alturaMaximaInteracao;

        if (perto && Input.GetKeyDown(tecla))
            Abrir();
    }

    public void Abrir()
    {
        if (aberto) return;
        aberto = true;
        perto = false;

        if (lootBox != null)
        {
            lootBox.BounceBox(false);
            lootBox.Open();
        }
        else if (animator != null)
        {
            animator.Play("Open");
        }

        if (moedas > 0) ContadorMoedas.Adicionar(moedas);
        if (cura > 0f && vidaJogador != null) vidaJogador.Curar(cura);

        TocarSom();

        if (tesouroPrincipal) Mostrar(textoTesouro);
        else
        {
            string t = "";
            if (moedas > 0) t += "+" + moedas + " moedas";
            if (cura > 0f) t += (t.Length > 0 ? "   " : "") + "+" + Mathf.RoundToInt(cura) + " vida";
            if (t.Length > 0) Mostrar(t);
        }
    }

    void Mostrar(string texto)
    {
        mensagem = texto;
        mensagemAte = Time.time + tempoMensagem;
    }

    void TocarSom()
    {
        if (somAbrir == null) return;
        AudioSource.PlayClipAtPoint(somAbrir, transform.position, volume);
    }

    void OnGUI()
    {
        if (!perto && Time.time > mensagemAte) return;

        CriarEstilos();

        if (perto)
        {
            var r = new Rect(0, Screen.height * 0.62f, Screen.width, 40);
            Sombra(r, textoAviso, estiloAviso);
        }

        if (Time.time <= mensagemAte && !string.IsNullOrEmpty(mensagem))
        {
            // some aos poucos no final
            float alpha = Mathf.Clamp01((mensagemAte - Time.time) / 0.6f);
            var cor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            var r = new Rect(0, Screen.height * (tesouroPrincipal ? 0.3f : 0.55f), Screen.width, 60);
            Sombra(r, mensagem, tesouroPrincipal ? estiloMensagem : estiloAviso);
            GUI.color = cor;
        }
    }

    static void Sombra(Rect r, string texto, GUIStyle estilo)
    {
        var corOriginal = estilo.normal.textColor;
        estilo.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), texto, estilo);
        estilo.normal.textColor = corOriginal;
        GUI.Label(r, texto, estilo);
    }

    static void CriarEstilos()
    {
        if (estiloAviso != null) return;

        estiloAviso = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(18, Screen.height / 32),
            fontStyle = FontStyle.Bold
        };
        estiloAviso.normal.textColor = Color.white;

        estiloMensagem = new GUIStyle(estiloAviso)
        {
            fontSize = Mathf.Max(28, Screen.height / 16)
        };
        estiloMensagem.normal.textColor = new Color(1f, 0.85f, 0.25f);
    }
}

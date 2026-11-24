using UnityEngine;

/// <summary>
/// Carta enrolada (estilo mensagem na garrafa). Perto dela aparece "[E] Ler a carta".
/// Abre o texto num papel na tela; ao fechar pela primeira vez, a noite cai (NoiteMacabra).
/// Colocada pelo menu  ISLAND > Noite > Preparar Carta e Noite da Caveira.
/// </summary>
public class CartaMisteriosa : MonoBehaviour
{
    [Header("Texto")]
    public string titulo = "Para quem encontrar esta carta";
    [TextArea(6, 14)]
    public string texto =
        "Se você chegou até aqui, já é tarde demais.\n\n" +
        "Esta caveira era do capitão que trouxe a maldição para a ilha. " +
        "Enquanto ninguém lê estas palavras, o sol continua brilhando...\n\n" +
        "Mas agora você leu.\n\n" +
        "Quando a lua sangrar, os mortos vão acordar. Corra para os templos.";
    public string assinatura = "— O último sobrevivente";

    [Header("Ler")]
    public KeyCode tecla = KeyCode.E;
    public float raio = 2.6f;
    public float alturaMaxima = 4f;

    [Header("Som")]
    public AudioClip somPapel;
    [Tooltip("Toca assim que o jogador termina de ler (fecha a carta). A noite cai logo depois.")]
    public AudioClip somAoTerminar;
    [Range(0f, 1f)] public float volumeAoTerminar = 1f;
    [Tooltip("Segundos entre o som e a noite cair. Negativo = espera o som quase todo (até 2,5 s).")]
    public float esperaAntesDaNoite = -1f;

    Transform jogador;
    VidaJogador vida;
    bool perto, lendo, jaLeu;
    float podeFecharEm;

    static Texture2D papel, sombra;
    static GUIStyle estTitulo, estTexto, estAssinatura, estAviso, estDica;

    void Start()
    {
        vida = FindFirstObjectByType<VidaJogador>();
        if (vida != null) jogador = vida.transform;
        else if (Camera.main != null) jogador = Camera.main.transform;
    }

    void Update()
    {
        if (lendo)
        {
            if (Time.unscaledTime >= podeFecharEm && (Input.GetKeyDown(tecla) || Input.GetKeyDown(KeyCode.Escape)))
                Fechar();
            return;
        }

        perto = false;
        if (jogador == null || (vida != null && vida.Morto)) return;
        Vector3 d = jogador.position - transform.position;
        float altura = Mathf.Abs(d.y); d.y = 0f;
        perto = d.magnitude <= raio && altura <= alturaMaxima;

        if (perto && Input.GetKeyDown(tecla)) Abrir();
    }

    void Abrir()
    {
        lendo = true;
        podeFecharEm = Time.unscaledTime + 0.4f;   // não fecha no mesmo aperto de E
        if (somPapel != null) AudioSource.PlayClipAtPoint(somPapel, transform.position, 1f);
    }

    void Fechar()
    {
        lendo = false;
        if (!jaLeu)
        {
            jaLeu = true;
            StartCoroutine(SomEDepoisNoite());
        }
    }

    System.Collections.IEnumerator SomEDepoisNoite()
    {
        float espera = 0f;
        if (somAoTerminar != null)
        {
            // 2D (sem distância): parece que alguém sussurrou no ouvido
            var go = new GameObject("SomCarta");
            var fonte = go.AddComponent<AudioSource>();
            fonte.clip = somAoTerminar;
            fonte.volume = volumeAoTerminar;
            fonte.spatialBlend = 0f;
            fonte.Play();
            Destroy(go, somAoTerminar.length + 0.2f);
            espera = esperaAntesDaNoite >= 0f ? esperaAntesDaNoite : Mathf.Min(somAoTerminar.length * 0.85f, 2.5f);
        }
        if (espera > 0f) yield return new WaitForSeconds(espera);

        var noite = FindFirstObjectByType<NoiteMacabra>();
        if (noite != null) noite.Anoitecer();
    }

    void OnGUI()
    {
        CriarEstilos();

        if (!lendo)
        {
            if (perto)
            {
                var r = new Rect(0, Screen.height * 0.62f, Screen.width, 40);
                Sombra(r, "[" + tecla + "] Ler a carta", estAviso);
            }
            return;
        }

        float w = Mathf.Min(620f, Screen.width * 0.8f);
        float h = Mathf.Min(520f, Screen.height * 0.8f);
        var caixa = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), sombra);
        GUI.DrawTexture(caixa, papel);

        float m = 36f;
        var area = new Rect(caixa.x + m, caixa.y + m * 0.8f, caixa.width - m * 2, caixa.height - m * 1.6f);
        GUILayout.BeginArea(area);
        GUILayout.Label(titulo, estTitulo);
        GUILayout.Space(14);
        GUILayout.Label(texto, estTexto);
        GUILayout.Space(14);
        GUILayout.Label(assinatura, estAssinatura);
        GUILayout.FlexibleSpace();
        GUILayout.Label("[" + tecla + "] fechar", estDica);
        GUILayout.EndArea();
    }

    static void Sombra(Rect r, string t, GUIStyle e)
    {
        var c = e.normal.textColor;
        IMGUIFix.Cor(e, new Color(0, 0, 0, 0.75f));
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), t, e);
        IMGUIFix.Cor(e, c);
        GUI.Label(r, t, e);
    }

    static void CriarEstilos()
    {
        if (estTitulo != null) return;

        papel = Papel(64);
        sombra = new Texture2D(1, 1); sombra.SetPixel(0, 0, new Color(0, 0, 0, 0.55f)); sombra.Apply();

        var tinta = new Color(0.22f, 0.13f, 0.07f);
        estTitulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(22, Screen.height / 30), fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        IMGUIFix.Cor(estTitulo, tinta);
        estTexto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(17, Screen.height / 44), fontStyle = FontStyle.Italic, wordWrap = true };
        IMGUIFix.Cor(estTexto, tinta);
        estAssinatura = new GUIStyle(estTexto) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.BoldAndItalic };
        estDica = new GUIStyle(estTexto) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(13, Screen.height / 60), fontStyle = FontStyle.Normal };
        IMGUIFix.Cor(estDica, new Color(0.35f, 0.25f, 0.15f));
        estAviso = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(18, Screen.height / 32), fontStyle = FontStyle.Bold };
        IMGUIFix.Cor(estAviso, Color.white);
    }

    // Papel envelhecido: bege com bordas mais escuras e manchinhas
    static Texture2D Papel(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var baseCor = new Color(0.89f, 0.80f, 0.60f);
        var borda = new Color(0.62f, 0.45f, 0.25f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float dist = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                float b = Mathf.Clamp01(1f - dist / 0.14f);
                float ruido = Mathf.PerlinNoise(u * 6f, v * 6f) * 0.12f;
                Color c = Color.Lerp(baseCor, borda, b * 0.85f + ruido);
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return tex;
    }
}

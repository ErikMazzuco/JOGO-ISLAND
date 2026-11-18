using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Minimapa redondo: uma câmera olhando de cima segue o jogador e desenha o mapa dentro do círculo.
/// Mostra a seta do jogador, o norte (N) e marcadores de moedas, itens de cura e esqueletos.
/// Tecla M: alterna entre mapa perto e mapa longe.
/// Monte a interface com o menu  ISLAND > Criar Minimapa.
/// </summary>
public class Minimapa : MonoBehaviour
{
    [Header("Ligações (criadas pelo menu ISLAND > Criar Minimapa)")]
    public Image mascara;                 // círculo que recorta o mapa
    public RawImage imagemMapa;           // onde a câmera desenha
    public RectTransform marcadores;      // onde nascem os pontinhos
    public RectTransform seta;            // seta do jogador
    public RectTransform letraNorte;      // "N"

    [Header("Mapa")]
    [Tooltip("Quantos metros do centro até a borda do círculo (zoom perto).")]
    public float raioVisao = 35f;
    [Tooltip("Zoom longe (tecla do zoom).")]
    public float raioVisaoLonge = 90f;
    public KeyCode teclaZoom = KeyCode.M;
    [Tooltip("Ligado: o mapa gira e a seta aponta sempre para cima. Desligado: o norte fica sempre em cima.")]
    public bool girarComJogador = true;
    public int resolucao = 512;
    public float altura = 150f;
    public Color corFundo = new Color(0.08f, 0.22f, 0.32f);

    [Header("Marcadores")]
    public bool mostrarMoedas = true;
    public bool mostrarCura = true;
    public bool mostrarInimigos = true;
    public float tamanhoMarcador = 16f;
    public Color corMoeda = new Color(1f, 0.86f, 0.2f);
    public Color corCura = new Color(0.3f, 1f, 0.4f);
    public Color corInimigo = new Color(1f, 0.25f, 0.2f);

    enum Tipo { Moeda, Cura, Inimigo }

    class Marcador
    {
        public Transform alvo;
        public VidaSkeleton esqueleto;
        public Tipo tipo;
        public Image imagem;
    }

    Camera camMapa;
    RenderTexture textura;
    VidaJogador jogador;

    readonly Dictionary<Transform, Marcador> marcas = new Dictionary<Transform, Marcador>();
    float proximaAtualizacaoListas;
    bool longe;
    float raioAtual;

    static Sprite spriteCirculo, spriteMais, spriteSeta, spriteMascara;

    void Awake()
    {
        raioAtual = raioVisao;
        CriarSprites();

        if (mascara != null) mascara.sprite = spriteMascara;
        if (seta != null)
        {
            var img = seta.GetComponent<Image>();
            if (img != null) img.sprite = spriteSeta;
        }
        if (letraNorte != null)
        {
            var img = letraNorte.GetComponent<Image>();
            if (img != null) img.sprite = spriteCirculo;
        }

        CriarCamera();
    }

    void OnDestroy()
    {
        if (camMapa != null) Destroy(camMapa.gameObject);
        if (textura != null)
        {
            textura.Release();
            Destroy(textura);
        }
    }

    void CriarCamera()
    {
        textura = new RenderTexture(resolucao, resolucao, 16, RenderTextureFormat.ARGB32);
        textura.name = "TexturaMinimapa";
        textura.Create();

        var go = new GameObject("CameraMinimapa");
        camMapa = go.AddComponent<Camera>();
        camMapa.orthographic = true;
        camMapa.orthographicSize = raioVisao;
        camMapa.clearFlags = CameraClearFlags.SolidColor;
        camMapa.backgroundColor = corFundo;
        camMapa.nearClipPlane = 1f;
        camMapa.farClipPlane = altura + 300f;
        camMapa.cullingMask = ~(1 << 5); // tudo, menos a interface
        camMapa.allowHDR = false;
        camMapa.allowMSAA = false;
        camMapa.useOcclusionCulling = false;
        camMapa.depth = -50f;
        camMapa.targetTexture = textura;

        var dados = camMapa.GetUniversalAdditionalCameraData();
        if (dados != null)
        {
            dados.renderShadows = false;
            dados.renderPostProcessing = false;
            dados.antialiasing = AntialiasingMode.None;
        }

        if (imagemMapa != null) imagemMapa.texture = textura;
    }

    void LateUpdate()
    {
        if (camMapa == null) return;

        if (jogador == null)
        {
            jogador = FindFirstObjectByType<VidaJogador>();
            if (jogador == null) return;
        }

        // Zoom (perto / longe)
        if (Input.GetKeyDown(teclaZoom)) longe = !longe;
        float alvoRaio = longe ? raioVisaoLonge : raioVisao;
        raioAtual = Mathf.Lerp(raioAtual, alvoRaio, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));

        // Para onde o jogador está olhando
        Camera principal = Camera.main;
        float yaw = principal != null ? principal.transform.eulerAngles.y : jogador.transform.eulerAngles.y;
        float yawMapa = girarComJogador ? yaw : 0f;

        Vector3 centro = jogador.transform.position;
        camMapa.orthographicSize = raioAtual;
        camMapa.transform.SetPositionAndRotation(
            new Vector3(centro.x, centro.y + altura, centro.z),
            Quaternion.Euler(90f, yawMapa, 0f));

        Vector3 direita = camMapa.transform.right;
        Vector3 cima = camMapa.transform.up;

        float raioPx = marcadores != null ? marcadores.rect.width * 0.5f : 100f;
        float escala = raioPx / Mathf.Max(0.01f, raioAtual);

        // Seta do jogador
        if (seta != null)
            seta.localEulerAngles = new Vector3(0f, 0f, girarComJogador ? 0f : -yaw);

        // Letra N (onde fica o norte na borda do círculo)
        if (letraNorte != null)
        {
            Vector2 n = new Vector2(Vector3.Dot(Vector3.forward, direita), Vector3.Dot(Vector3.forward, cima));
            if (n.sqrMagnitude < 0.0001f) n = Vector2.up;
            letraNorte.anchoredPosition = n.normalized * (raioPx - 12f);
        }

        // Lista de marcadores (atualiza de vez em quando)
        if (Time.unscaledTime >= proximaAtualizacaoListas)
        {
            proximaAtualizacaoListas = Time.unscaledTime + 0.5f;
            AtualizarListas();
        }

        // Posição de cada marcador
        foreach (var par in marcas)
        {
            Marcador m = par.Value;

            if (m.alvo == null || (m.tipo == Tipo.Inimigo && m.esqueleto != null && m.esqueleto.EstaMorto))
            {
                m.imagem.enabled = false;
                continue;
            }

            Vector3 desvio = m.alvo.position - centro;
            float x = Vector3.Dot(desvio, direita) * escala;
            float y = Vector3.Dot(desvio, cima) * escala;

            float limite = raioPx - tamanhoMarcador * 0.6f;
            float dist = Mathf.Sqrt(x * x + y * y);
            bool fora = dist > limite;

            if (fora)
            {
                if (m.tipo == Tipo.Inimigo)
                {
                    m.imagem.enabled = false; // inimigos longe não aparecem
                    continue;
                }

                // Objetivos fora do mapa ficam na borda, apontando a direção
                x *= limite / dist;
                y *= limite / dist;
            }

            Color cor = CorDoTipo(m.tipo);
            if (fora) cor.a = 0.7f;

            m.imagem.enabled = true;
            m.imagem.color = cor;
            m.imagem.rectTransform.anchoredPosition = new Vector2(x, y);
        }
    }

    // ---------- Marcadores ----------
    void AtualizarListas()
    {
        if (marcadores == null) return;

        var vistos = new HashSet<Transform>();

        if (mostrarMoedas)
            foreach (var moeda in FindObjectsByType<Moeda>(FindObjectsSortMode.None))
                Garantir(moeda.transform, Tipo.Moeda, null, vistos);

        if (mostrarCura)
            foreach (var cura in FindObjectsByType<ItemCura>(FindObjectsSortMode.None))
                Garantir(cura.transform, Tipo.Cura, null, vistos);

        if (mostrarInimigos)
            foreach (var esq in FindObjectsByType<VidaSkeleton>(FindObjectsSortMode.None))
                if (!esq.EstaMorto)
                    Garantir(esq.transform, Tipo.Inimigo, esq, vistos);

        // Remove os que sumiram (moeda pega, esqueleto morto...)
        var chaves = new List<Transform>(marcas.Keys);
        foreach (var chave in chaves)
        {
            if (chave == null || !vistos.Contains(chave))
            {
                Marcador m = marcas[chave];
                if (m.imagem != null) Destroy(m.imagem.gameObject);
                marcas.Remove(chave);
            }
        }
    }

    void Garantir(Transform alvo, Tipo tipo, VidaSkeleton esq, HashSet<Transform> vistos)
    {
        vistos.Add(alvo);
        if (marcas.ContainsKey(alvo)) return;

        var go = new GameObject("Marcador_" + tipo, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(marcadores, false);

        var img = go.GetComponent<Image>();
        img.sprite = tipo == Tipo.Cura ? spriteMais : spriteCirculo;
        img.raycastTarget = false;

        float tam = tipo == Tipo.Inimigo ? tamanhoMarcador * 0.8f : tamanhoMarcador;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(tam, tam);

        marcas[alvo] = new Marcador { alvo = alvo, esqueleto = esq, tipo = tipo, imagem = img };
    }

    Color CorDoTipo(Tipo tipo)
    {
        switch (tipo)
        {
            case Tipo.Moeda: return corMoeda;
            case Tipo.Cura: return corCura;
            default: return corInimigo;
        }
    }

    // ---------- Imagens geradas pelo código ----------
    static void CriarSprites()
    {
        if (spriteCirculo != null) return;

        spriteCirculo = Gerar(32, (u, v) => (u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f) < 0.33f * 0.33f, 2);

        spriteMais = Gerar(32, (u, v) =>
            (Mathf.Abs(u - 0.5f) < 0.13f && Mathf.Abs(v - 0.5f) < 0.36f) ||
            (Mathf.Abs(v - 0.5f) < 0.13f && Mathf.Abs(u - 0.5f) < 0.36f), 2);

        var poligonoSeta = new[]
        {
            new Vector2(0.50f, 0.92f), new Vector2(0.88f, 0.12f),
            new Vector2(0.50f, 0.30f), new Vector2(0.12f, 0.12f)
        };
        spriteSeta = Gerar(64, (u, v) => DentroDoPoligono(poligonoSeta, u, v), 3);

        spriteMascara = GerarMascara(128);
    }

    static Sprite Gerar(int tam, System.Func<float, float, bool> dentro, int borda)
    {
        const int ss = 3; // suavização (3x3 amostras por pixel)
        var area = new float[tam * tam];

        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                int acertos = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                        if (dentro((x + (sx + 0.5f) / ss) / tam, (y + (sy + 0.5f) / ss) / tam)) acertos++;

                area[y * tam + x] = acertos / (float)(ss * ss);
            }
        }

        var cores = new Color[tam * tam];
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float miolo = area[y * tam + x];
                float contorno = 0f;

                for (int dy = -borda; dy <= borda; dy++)
                {
                    for (int dx = -borda; dx <= borda; dx++)
                    {
                        if (dx * dx + dy * dy > borda * borda) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= tam || ny >= tam) continue;
                        contorno = Mathf.Max(contorno, area[ny * tam + nx]);
                    }
                }

                float cinza = contorno > 0.001f ? Mathf.Clamp01(miolo / contorno) : 0f;
                cores[y * tam + x] = new Color(cinza, cinza, cinza, contorno);
            }
        }

        return CriarSprite(tam, cores);
    }

    static Sprite GerarMascara(int tam)
    {
        var cores = new Color[tam * tam];
        float c = (tam - 1) * 0.5f;
        float raio = tam * 0.5f;

        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                cores[y * tam + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(raio - d + 0.5f));
            }
        }

        return CriarSprite(tam, cores);
    }

    static Sprite CriarSprite(int tam, Color[] cores)
    {
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(cores);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool DentroDoPoligono(Vector2[] p, float u, float v)
    {
        bool dentro = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
        {
            if ((p[i].y > v) != (p[j].y > v) &&
                u < (p[j].x - p[i].x) * (v - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                dentro = !dentro;
        }
        return dentro;
    }
}

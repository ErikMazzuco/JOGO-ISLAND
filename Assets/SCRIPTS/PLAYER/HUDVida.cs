using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de vida do jogador: barra + número ("85 / 100") na tela.
/// Normalmente é criado pelo menu  ISLAND > Criar HUD de Vida.
/// </summary>
public class HUDVida : MonoBehaviour
{
    [Header("Jogador")]
    public VidaJogador jogador;          // se vazio, procura na cena

    [Header("Elementos da barra")]
    public RectTransform barraCheia;     // parte colorida
    public RectTransform barraAtraso;    // faixa clara que "escorre" depois do dano
    public Image imagemBarraCheia;
    public TMP_Text textoVida;           // "85 / 100"
    public TMP_Text textoRotulo;         // "VIDA"

    [Header("Cores da barra")]
    public Color corCheia = new Color(0.25f, 0.80f, 0.30f);
    public Color corMedia = new Color(0.95f, 0.78f, 0.20f);
    public Color corBaixa = new Color(0.85f, 0.18f, 0.15f);
    [Range(0f, 1f)] public float limiteMedia = 0.6f;
    [Range(0f, 1f)] public float limiteBaixa = 0.3f;

    [Header("Animação")]
    public float velocidadeBarra = 3f;    // barra principal (fração por segundo)
    public float atrasoPausa = 0.45f;     // espera antes da faixa clara descer
    public float velocidadeAtraso = 0.6f; // velocidade da faixa clara

    [Header("Flash de dano (tela vermelha)")]
    public Image flashDano;
    [Range(0f, 1f)] public float intensidadeFlash = 0.35f;
    public float velocidadeFlash = 2.5f;

    [Header("Vida baixa")]
    [Range(0f, 1f)] public float limitePulsar = 0.25f;
    public float velocidadePulso = 6f;
    public float forcaPulso = 0.04f;

    float ratioAlvo, ratioCheia, ratioAtraso;
    float tempoAtraso, alphaFlash;
    Vector3 escalaOriginal;

    void Awake()
    {
        escalaOriginal = transform.localScale;
        ConfigurarContorno(textoVida, 0.22f);
        ConfigurarContorno(textoRotulo, 0.2f);
    }

    static void ConfigurarContorno(TMP_Text texto, float largura)
    {
        if (texto == null) return;
        texto.outlineColor = new Color32(45, 22, 6, 255);
        texto.outlineWidth = largura;
    }

    void Start()
    {
        if (jogador == null) jogador = FindFirstObjectByType<VidaJogador>();

        if (jogador == null)
        {
            Debug.LogWarning("HUDVida: nenhum VidaJogador encontrado na cena.");
            enabled = false;
            return;
        }

        jogador.VidaMudou += AoMudarVida;
        jogador.DanoRecebido += AoReceberDano;

        ratioAlvo = ratioCheia = ratioAtraso = jogador.Percentual;
        AtualizarTexto(jogador.vida, jogador.vidaMaxima);
        AplicarBarra();
    }

    void OnDestroy()
    {
        if (jogador == null) return;
        jogador.VidaMudou -= AoMudarVida;
        jogador.DanoRecebido -= AoReceberDano;
    }

    void AoMudarVida(float atual, float maxima)
    {
        ratioAlvo = maxima > 0f ? Mathf.Clamp01(atual / maxima) : 0f;
        AtualizarTexto(atual, maxima);
    }

    void AoReceberDano(float dano)
    {
        tempoAtraso = Time.unscaledTime + atrasoPausa;
        alphaFlash = intensidadeFlash;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        ratioCheia = Mathf.MoveTowards(ratioCheia, ratioAlvo, velocidadeBarra * dt);

        if (ratioAtraso < ratioCheia)
            ratioAtraso = ratioCheia;                       // cura: faixa acompanha na hora
        else if (Time.unscaledTime >= tempoAtraso)
            ratioAtraso = Mathf.MoveTowards(ratioAtraso, ratioCheia, velocidadeAtraso * dt);

        AplicarBarra();
        AtualizarFlash(dt);
        AtualizarPulso(dt);
    }

    void AplicarBarra()
    {
        DefinirLargura(barraCheia, ratioCheia);
        DefinirLargura(barraAtraso, ratioAtraso);

        if (imagemBarraCheia != null)
            imagemBarraCheia.color = CorDaBarra(ratioCheia);
    }

    static void DefinirLargura(RectTransform barra, float ratio)
    {
        if (barra == null) return;
        Vector2 max = barra.anchorMax;
        max.x = Mathf.Clamp01(ratio);
        barra.anchorMax = max;
    }

    Color CorDaBarra(float r)
    {
        if (r >= limiteMedia)
            return Color.Lerp(corMedia, corCheia, Mathf.InverseLerp(limiteMedia, 1f, r));

        return Color.Lerp(corBaixa, corMedia, Mathf.InverseLerp(limiteBaixa, limiteMedia, r));
    }

    void AtualizarTexto(float atual, float maxima)
    {
        if (textoVida != null)
            textoVida.text = Mathf.CeilToInt(atual) + " / " + Mathf.CeilToInt(maxima);
    }

    void AtualizarFlash(float dt)
    {
        if (flashDano == null) return;

        alphaFlash = Mathf.MoveTowards(alphaFlash, 0f, velocidadeFlash * dt);
        Color c = flashDano.color;
        c.a = alphaFlash;
        flashDano.color = c;
    }

    void AtualizarPulso(float dt)
    {
        bool vidaBaixa = ratioAlvo > 0f && ratioAlvo <= limitePulsar;

        if (vidaBaixa)
        {
            float onda = Mathf.Sin(Time.unscaledTime * velocidadePulso) * 0.5f + 0.5f;
            transform.localScale = escalaOriginal * (1f + onda * forcaPulso);
        }
        else
        {
            transform.localScale = Vector3.Lerp(transform.localScale, escalaOriginal, 10f * dt);
        }
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tela de abertura do jogo: o ícone da ilha aparece com um "pulinho", o título ISLAND sobe suave
/// e uma barra de CARREGANDO acompanha o carregamento real do Menu (feito em segundo plano).
/// Quando termina, escurece e abre o Menu. Qualquer tecla ou clique acelera (se o Menu já estiver carregado).
/// Normalmente é criada pelo menu  ISLAND > Criar Tela de Abertura (Splash).
/// </summary>
public class SplashAbertura : MonoBehaviour
{
    [Header("Cena seguinte")]
    public string nomeCena = "Menu";

    [Header("Tempos (segundos)")]
    [Tooltip("Tempo mínimo na tela, mesmo que o Menu carregue antes.")]
    public float tempoMinimo = 3.6f;
    public float fadeSaida = 0.6f;

    [Header("Referências")]
    public CanvasGroup grupoGeral;
    public Image brilho;
    public RectTransform icone;
    public CanvasGroup grupoIcone;
    public RectTransform titulo;
    public CanvasGroup grupoTitulo;
    public CanvasGroup grupoCarregando;
    public RectTransform barraPreenchimento;   // a barra cresce pela âncora direita
    public TMP_Text textoCarregando;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (brilho != null) brilho.sprite = CriarSpriteBrilho(128);
        if (grupoGeral != null) grupoGeral.alpha = 1f;
        if (grupoIcone != null) grupoIcone.alpha = 0f;
        if (grupoTitulo != null) grupoTitulo.alpha = 0f;
        if (grupoCarregando != null) grupoCarregando.alpha = 0f;
        DefinirBarra(0f);
    }

    void Start()
    {
        StartCoroutine(Rotina());
    }

    IEnumerator Rotina()
    {
        // Começa a carregar o Menu já, em segundo plano (a troca de cena só acontece no final)
        AsyncOperation carga = SceneManager.LoadSceneAsync(nomeCena);
        carga.allowSceneActivation = false;

        Vector2 posIcone = icone != null ? icone.anchoredPosition : Vector2.zero;
        Vector2 posTitulo = titulo != null ? titulo.anchoredPosition : Vector2.zero;

        float t = 0f;
        float barra = 0f;
        bool pular = false;

        while (true)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;

            if (t > 1.2f && Input.anyKeyDown) pular = true;

            // ---- Ícone: aparece crescendo com um leve "estouro", depois flutua ----
            float ti = Mathf.Clamp01(t / 0.7f);
            if (grupoIcone != null) grupoIcone.alpha = Mathf.SmoothStep(0f, 1f, ti);
            if (icone != null)
            {
                float escala = Mathf.LerpUnclamped(0.55f, 1f, EaseOutBack(ti));
                icone.localScale = new Vector3(escala, escala, 1f);
                float flutua = Mathf.Sin(t * 2.2f) * 10f * Mathf.Clamp01(t - 0.5f);
                icone.anchoredPosition = posIcone + Vector2.up * flutua;
            }

            // ---- Brilho quente atrás do ícone, pulsando devagar ----
            if (brilho != null)
            {
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.2f) / 1f));
                var c = brilho.color;
                c.a = (0.22f + 0.07f * Mathf.Sin(t * 2f)) * a;
                brilho.color = c;
            }

            // ---- Título: sobe suave e aparece ----
            float tt = Mathf.Clamp01((t - 0.6f) / 0.8f);
            float suave = EaseOutCubic(tt);
            if (grupoTitulo != null) grupoTitulo.alpha = suave;
            if (titulo != null)
            {
                titulo.anchoredPosition = posTitulo + Vector2.down * (1f - suave) * 40f;
                float e = Mathf.Lerp(0.92f, 1f, suave);
                titulo.localScale = new Vector3(e, e, 1f);
            }

            // ---- Carregando ----
            if (grupoCarregando != null) grupoCarregando.alpha = Mathf.Clamp01((t - 1.3f) / 0.5f);

            // A barra segue o carregamento real, mas só enche por completo no fim do tempo mínimo
            float alvo = Mathf.Clamp01(carga.progress / 0.9f);
            float limiteTempo = pular ? 1f : Mathf.Clamp01(t / Mathf.Max(0.1f, tempoMinimo));
            alvo = Mathf.Min(alvo, limiteTempo);
            barra = Mathf.MoveTowards(barra, alvo, dt * 0.9f);
            DefinirBarra(barra);

            if (textoCarregando != null)
            {
                int pontos = (int)(t * 2.5f) % 4;
                textoCarregando.text = "CARREGANDO" + new string('.', pontos);
            }

            if (carga.progress >= 0.9f && barra >= 0.999f) break;

            yield return null;
        }

        if (textoCarregando != null) textoCarregando.text = "PRONTO!";

        // ---- Escurece e abre o Menu ----
        float f = 0f;
        float tempoFade = Mathf.Max(0.05f, fadeSaida);
        while (f < 1f)
        {
            f += Time.unscaledDeltaTime / tempoFade;
            if (grupoGeral != null) grupoGeral.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(f));
            yield return null;
        }

        carga.allowSceneActivation = true;
    }

    void DefinirBarra(float valor)
    {
        if (barraPreenchimento == null) return;
        barraPreenchimento.anchorMin = new Vector2(0f, 0f);
        barraPreenchimento.anchorMax = new Vector2(Mathf.Clamp01(valor), 1f);
        barraPreenchimento.offsetMin = Vector2.zero;
        barraPreenchimento.offsetMax = Vector2.zero;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    static float EaseOutCubic(float x)
    {
        float p = 1f - Mathf.Clamp01(x);
        return 1f - p * p * p;
    }

    // Círculo suave (branco, some nas bordas), pintado com a cor do Image "Brilho"
    static Sprite CriarSpriteBrilho(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}

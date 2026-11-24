using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tela de VITÓRIA (aparece quando o último baú é aberto). Os textos são objetos TextMeshPro normais:
/// dá para editar na Hierarchy (CanvasVitoria > PainelVitoria > ...). Os números (baús, moedas, tempo)
/// são preenchidos por esta classe; o formato deles está nos campos abaixo.
/// Criada por  ISLAND > Criar Tela de Vitória.
/// </summary>
public class TelaVitoria : MonoBehaviour
{
    [Header("Referências")]
    public GameObject painel;           // tudo (fundo escuro + painel de madeira)
    public CanvasGroup grupo;
    public RectTransform caixa;         // o painel de madeira (recebe a animação de entrada)
    public TMP_Text textoSubtitulo;
    public TMP_Text textoBaus;
    public TMP_Text textoMoedas;
    public TMP_Text textoTempo;
    public Button botaoJogarDeNovo;
    public Button botaoMenu;

    [Header("Textos (use {0} para o número)")]
    public string formatoBaus = "BAÚS ABERTOS:  <color=#FFD060>{0}</color>";
    public string formatoMoedas = "MOEDAS:  <color=#FFD060>{0}</color>";
    public string formatoTempo = "TEMPO:  <color=#FFD060>{0}</color>";

    [Header("Configuração")]
    public string nomeCenaMenu = "Menu";

    PlayerMovement movimento;

    void Start()
    {
        if (painel != null) painel.SetActive(false);
        if (botaoJogarDeNovo != null) botaoJogarDeNovo.onClick.AddListener(JogarDeNovo);
        if (botaoMenu != null) botaoMenu.onClick.AddListener(VoltarAoMenu);
    }

    public void Mostrar(int baus, int totalBaus, int moedas, float segundos)
    {
        // Para o controle do jogador e congela o jogo
        movimento = FindFirstObjectByType<PlayerMovement>();
        if (movimento != null) movimento.enabled = false;

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                           typeof(UnityEngine.EventSystems.StandaloneInputModule));

        int min = Mathf.FloorToInt(segundos / 60f);
        int seg = Mathf.FloorToInt(segundos % 60f);

        if (textoBaus != null) textoBaus.text = string.Format(formatoBaus, baus + " / " + totalBaus);
        if (textoMoedas != null) textoMoedas.text = string.Format(formatoMoedas, moedas);
        if (textoTempo != null) textoTempo.text = string.Format(formatoTempo, min + ":" + seg.ToString("00"));

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (painel != null) painel.SetActive(true);
        StartCoroutine(Entrada());
    }

    IEnumerator Entrada()
    {
        // Animação que funciona com o jogo parado (tempo "não escalado")
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.45f;
            float x = Mathf.Clamp01(t);
            if (grupo != null) grupo.alpha = Mathf.SmoothStep(0f, 1f, x);
            if (caixa != null)
            {
                const float c1 = 1.70158f, c3 = c1 + 1f;
                float p = x - 1f;
                float e = 1f + c3 * p * p * p + c1 * p * p;
                float escala = Mathf.LerpUnclamped(0.8f, 1f, e);
                caixa.localScale = new Vector3(escala, escala, 1f);
            }
            yield return null;
        }

        if (grupo != null) grupo.alpha = 1f;
        if (caixa != null) caixa.localScale = Vector3.one;
    }

    public void JogarDeNovo()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void VoltarAoMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(nomeCenaMenu);
    }

    void OnDestroy()
    {
        // Segurança: se a cena for trocada com a tela aberta, o jogo não fica congelado
        if (painel != null && painel.activeSelf) Time.timeScale = 1f;
    }
}

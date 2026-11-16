using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tela de "fim de jogo": quando o jogador morre, pausa o jogo, libera o mouse
/// e mostra os botões RENASCER e MENU.
/// Normalmente é criada pelo menu  ISLAND > Criar Tela de Morte.
/// </summary>
public class TelaMorte : MonoBehaviour
{
    [Header("Referências")]
    public VidaJogador jogador;          // se vazio, procura na cena
    public GameObject painel;            // tudo que aparece ao morrer
    public CanvasGroup grupo;            // usado no fade
    public Button botaoRenascer;
    public Button botaoMenu;

    [Header("Configuração")]
    public string nomeCenaMenu = "Menu";
    public float tempoFade = 0.8f;

    bool mostrando;

    void Start()
    {
        if (painel != null) painel.SetActive(false);

        if (botaoRenascer != null) botaoRenascer.onClick.AddListener(Renascer);
        if (botaoMenu != null) botaoMenu.onClick.AddListener(VoltarAoMenu);

        if (jogador == null) jogador = FindFirstObjectByType<VidaJogador>();

        if (jogador == null)
        {
            Debug.LogWarning("TelaMorte: nenhum VidaJogador encontrado na cena.");
            return;
        }

        jogador.Morreu += AoMorrer;
    }

    void OnDestroy()
    {
        if (jogador != null) jogador.Morreu -= AoMorrer;
    }

    void AoMorrer()
    {
        if (mostrando) return;
        mostrando = true;

        // Para o controle do jogador (movimento, câmera e ataque)
        var movimento = FindFirstObjectByType<PlayerMovement>();
        if (movimento != null) movimento.enabled = false;

        var espada = FindFirstObjectByType<AtaqueEspada>();
        if (espada != null) espada.enabled = false;

        // Garante que os botões consigam receber clique nesta cena
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Pausa o jogo e libera o mouse
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (grupo != null) grupo.alpha = 0f;
        if (painel != null) painel.SetActive(true);
    }

    void Update()
    {
        if (!mostrando || grupo == null || grupo.alpha >= 1f) return;

        float passo = tempoFade > 0f ? Time.unscaledDeltaTime / tempoFade : 1f;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, 1f, passo);
    }

    public void Renascer()
    {
        Retomar();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void VoltarAoMenu()
    {
        Retomar();
        SceneManager.LoadScene(nomeCenaMenu);
    }

    static void Retomar()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}

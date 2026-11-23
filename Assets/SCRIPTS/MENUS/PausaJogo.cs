using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu de pausa do jogo: ESC pausa e mostra CONTINUAR, OPÇÕES e MENU.
/// ESC de novo (ou CONTINUAR) volta ao jogo; dentro das opções, ESC volta para a pausa.
/// Normalmente é criado pelo menu  ISLAND > Criar Menu de Pausa.
/// </summary>
public class PausaJogo : MonoBehaviour
{
    [Header("Referências")]
    public GameObject painelPausa;       // overlay + título + botões
    public MenuOpcoes opcoes;            // painel de opções (abre por cima da pausa)
    public Button botaoContinuar;
    public Button botaoOpcoes;
    public Button botaoMenu;

    [Header("Configuração")]
    public string nomeCenaMenu = "Menu";

    bool pausado;
    PlayerMovement movimento;
    bool religarMovimento;

    public bool Pausado { get { return pausado; } }

    void Start()
    {
        if (painelPausa != null) painelPausa.SetActive(false);

        if (botaoContinuar != null) botaoContinuar.onClick.AddListener(Continuar);
        if (botaoOpcoes != null) botaoOpcoes.onClick.AddListener(AbrirOpcoes);
        if (botaoMenu != null) botaoMenu.onClick.AddListener(VoltarAoMenu);

        // Aqui quem trata o ESC é a pausa (senão as opções fechariam e a pausa junto)
        if (opcoes != null)
        {
            opcoes.fecharComEsc = false;
            opcoes.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (!pausado)
        {
            // Time.timeScale == 0 aqui significa tela de morte (ou outra tela): não pausa por cima
            if (Time.timeScale > 0f) Pausar();
        }
        else if (opcoes != null && opcoes.gameObject.activeSelf)
        {
            opcoes.Fechar();
        }
        else
        {
            Continuar();
        }
    }

    public void Pausar()
    {
        if (pausado) return;
        pausado = true;

        // Para o controle do jogador (assim um pulo apertado durante a pausa não "vaza" para depois)
        movimento = FindFirstObjectByType<PlayerMovement>();
        religarMovimento = movimento != null && movimento.enabled;
        if (religarMovimento) movimento.enabled = false;

        // Garante que os botões consigam receber clique nesta cena
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (opcoes != null) opcoes.gameObject.SetActive(false);
        if (painelPausa != null) painelPausa.SetActive(true);
    }

    public void Continuar()
    {
        if (!pausado) return;
        pausado = false;

        if (opcoes != null) opcoes.gameObject.SetActive(false);
        if (painelPausa != null) painelPausa.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (religarMovimento && movimento != null) movimento.enabled = true;
        religarMovimento = false;
    }

    public void AbrirOpcoes()
    {
        if (opcoes != null) opcoes.Abrir();
    }

    public void VoltarAoMenu()
    {
        pausado = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(nomeCenaMenu);
    }

    void OnDestroy()
    {
        // Segurança: se a cena for trocada durante a pausa, não deixa o jogo "congelado"
        if (pausado)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}

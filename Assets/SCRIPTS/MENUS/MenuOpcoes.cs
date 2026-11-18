using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Painel de opções da tela inicial (botão CONFIG).
/// Monte com o menu  ISLAND > Criar Menu de Opções  (com a cena Menu aberta).
/// </summary>
public class MenuOpcoes : MonoBehaviour
{
    [Header("Contador de FPS")]
    public Button botaoFps;
    public Image imagemFps;
    public TMP_Text textoFps;

    [Header("Sombras")]
    public Button botaoSombras;
    public Image imagemSombras;
    public TMP_Text textoSombras;

    [Header("Qualidade (Baixa, Média, Alta)")]
    public Button[] botoesQualidade;
    public Image[] imagensQualidade;

    [Header("Distância de renderização")]
    public Slider sliderDistancia;
    public TMP_Text textoDistancia;

    [Header("Sensibilidade do mouse")]
    public Slider sliderSensibilidade;
    public TMP_Text textoSensibilidade;

    [Header("Outros")]
    public Button botaoVoltar;
    public AudioMenu som;

    [Header("Cores")]
    public Color corLigado = new Color(0.45f, 0.85f, 0.35f);
    public Color corDesligado = new Color(0.62f, 0.32f, 0.22f);
    public Color corSelecionado = new Color(1f, 0.78f, 0.3f);
    public Color corNormal = new Color(0.5f, 0.3f, 0.2f);

    void Awake()
    {
        if (botaoFps != null)
            botaoFps.onClick.AddListener(() =>
            {
                Clique();
                Configuracoes.MostrarFps = !Configuracoes.MostrarFps;
                AtualizarTela();
            });

        if (botaoSombras != null)
            botaoSombras.onClick.AddListener(() =>
            {
                Clique();
                Configuracoes.Sombras = !Configuracoes.Sombras;
                AtualizarTela();
            });

        if (botoesQualidade != null)
        {
            for (int i = 0; i < botoesQualidade.Length; i++)
            {
                int indice = i;
                botoesQualidade[i].onClick.AddListener(() =>
                {
                    Clique();
                    Configuracoes.Qualidade = (NivelQualidade)indice;
                    AtualizarTela();
                });
            }
        }

        if (sliderDistancia != null)
        {
            sliderDistancia.minValue = Configuracoes.DistanciaMin;
            sliderDistancia.maxValue = Configuracoes.DistanciaMax;
            sliderDistancia.onValueChanged.AddListener(v =>
            {
                Configuracoes.Distancia = Mathf.Round(v / 5f) * 5f;
                AtualizarTela();
            });
        }

        if (sliderSensibilidade != null)
        {
            sliderSensibilidade.minValue = Configuracoes.SensibilidadeMin;
            sliderSensibilidade.maxValue = Configuracoes.SensibilidadeMax;
            sliderSensibilidade.onValueChanged.AddListener(v =>
            {
                Configuracoes.Sensibilidade = v;
                AtualizarTela();
            });
        }

        if (botaoVoltar != null)
            botaoVoltar.onClick.AddListener(() =>
            {
                Clique();
                Fechar();
            });
    }

    void OnEnable()
    {
        AtualizarTela();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Fechar();
    }

    public void Abrir()
    {
        gameObject.SetActive(true);
    }

    public void Fechar()
    {
        gameObject.SetActive(false);
    }

    void Clique()
    {
        if (som != null && som.audioSource != null && som.somClique != null)
            som.TocarClique();
    }

    // Mostra na tela os valores que estão salvos (sem disparar os eventos dos controles)
    void AtualizarTela()
    {
        MostrarLigado(imagemFps, textoFps, Configuracoes.MostrarFps);
        MostrarLigado(imagemSombras, textoSombras, Configuracoes.Sombras);

        if (imagensQualidade != null)
        {
            int atual = (int)Configuracoes.Qualidade;
            for (int i = 0; i < imagensQualidade.Length; i++)
                imagensQualidade[i].color = i == atual ? corSelecionado : corNormal;
        }

        if (sliderDistancia != null)
        {
            sliderDistancia.SetValueWithoutNotify(Configuracoes.Distancia);
            if (textoDistancia != null)
                textoDistancia.text = Mathf.RoundToInt(Configuracoes.Distancia) + " m";
        }

        if (sliderSensibilidade != null)
        {
            sliderSensibilidade.SetValueWithoutNotify(Configuracoes.Sensibilidade);
            if (textoSensibilidade != null)
                textoSensibilidade.text = Mathf.RoundToInt(Configuracoes.Sensibilidade * 100f) + "%";
        }
    }

    void MostrarLigado(Image imagem, TMP_Text texto, bool ligado)
    {
        if (imagem != null) imagem.color = ligado ? corLigado : corDesligado;
        if (texto != null) texto.text = ligado ? "LIGADO" : "DESLIGADO";
    }
}

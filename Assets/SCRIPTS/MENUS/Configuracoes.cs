using UnityEngine;

public enum NivelQualidade { Baixa = 0, Media = 1, Alta = 2 }

/// <summary>
/// Opções do jogo, salvas no computador (PlayerPrefs) e lidas em qualquer cena.
/// Quem aplica as opções no jogo é o AplicadorConfiguracoes (criado sozinho ao iniciar).
/// </summary>
public static class Configuracoes
{
    public const float DistanciaMin = 5f;
    public const float DistanciaMax = 1000f;
    public const float DistanciaPadrao = 300f;

    public const float SensibilidadeMin = 0.2f;
    public const float SensibilidadeMax = 3f;

    const string ChaveFps = "cfg_mostrar_fps";
    const string ChaveSombras = "cfg_sombras";
    const string ChaveQualidade = "cfg_qualidade";
    const string ChaveDistancia = "cfg_distancia";
    const string ChaveSensibilidade = "cfg_sensibilidade";

    /// <summary>Disparado sempre que alguma opção muda.</summary>
    public static event System.Action Mudou;

    static bool carregado;
    static bool mostrarFps = true;
    static bool sombras = true;
    static NivelQualidade qualidade = NivelQualidade.Alta;
    static float distancia = DistanciaPadrao;
    static float sensibilidade = 1f;

    static void Carregar()
    {
        if (carregado) return;
        carregado = true;

        mostrarFps = PlayerPrefs.GetInt(ChaveFps, 1) == 1;      // FPS vem LIGADO por padrão
        sombras = PlayerPrefs.GetInt(ChaveSombras, 1) == 1;
        qualidade = (NivelQualidade)Mathf.Clamp(PlayerPrefs.GetInt(ChaveQualidade, (int)NivelQualidade.Alta), 0, 2);
        distancia = Mathf.Clamp(PlayerPrefs.GetFloat(ChaveDistancia, DistanciaPadrao), DistanciaMin, DistanciaMax);
        sensibilidade = Mathf.Clamp(PlayerPrefs.GetFloat(ChaveSensibilidade, 1f), SensibilidadeMin, SensibilidadeMax);
    }

    static void Salvou()
    {
        PlayerPrefs.Save();
        if (Mudou != null) Mudou();
    }

    public static bool MostrarFps
    {
        get { Carregar(); return mostrarFps; }
        set { Carregar(); mostrarFps = value; PlayerPrefs.SetInt(ChaveFps, value ? 1 : 0); Salvou(); }
    }

    public static bool Sombras
    {
        get { Carregar(); return sombras; }
        set { Carregar(); sombras = value; PlayerPrefs.SetInt(ChaveSombras, value ? 1 : 0); Salvou(); }
    }

    public static NivelQualidade Qualidade
    {
        get { Carregar(); return qualidade; }
        set { Carregar(); qualidade = value; PlayerPrefs.SetInt(ChaveQualidade, (int)value); Salvou(); }
    }

    /// <summary>Distância de renderização em metros.</summary>
    public static float Distancia
    {
        get { Carregar(); return distancia; }
        set
        {
            Carregar();
            distancia = Mathf.Clamp(value, DistanciaMin, DistanciaMax);
            PlayerPrefs.SetFloat(ChaveDistancia, distancia);
            Salvou();
        }
    }

    /// <summary>Verdadeiro só depois que o jogador mexeu na distância (senão a cena fica como foi montada).</summary>
    public static bool DistanciaPersonalizada
    {
        get { return PlayerPrefs.HasKey(ChaveDistancia); }
    }

    /// <summary>Multiplicador da sensibilidade do mouse (1 = o valor configurado no jogador).</summary>
    public static float Sensibilidade
    {
        get { Carregar(); return sensibilidade; }
        set
        {
            Carregar();
            sensibilidade = Mathf.Clamp(value, SensibilidadeMin, SensibilidadeMax);
            PlayerPrefs.SetFloat(ChaveSensibilidade, sensibilidade);
            Salvou();
        }
    }
}

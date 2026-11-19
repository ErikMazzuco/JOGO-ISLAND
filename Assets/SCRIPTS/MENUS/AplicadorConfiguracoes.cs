using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Aplica as opções (FPS, qualidade, sombras, distância de renderização) em cada cena.
/// Nasce sozinho ao iniciar o jogo: não precisa colocar em nenhuma cena.
/// Os valores originais do projeto são guardados e devolvidos ao fechar o jogo,
/// então as opções nunca bagunçam as configurações do Editor.
///
/// Qualidade Alta  = configuração gráfica original do projeto.
/// Qualidade Média/Baixa = perfil leve (Assets/Resources/PerfilLeve, criado em ISLAND > Criar Perfil Gráfico Leve)
///                         + terreno mais simples (o maior ganho em PCs fracos).
/// </summary>
public class AplicadorConfiguracoes : MonoBehaviour
{
    const string CaminhoPerfilLeve = "PerfilLeve/RP_Leve";

    static AplicadorConfiguracoes instancia;

    class DadosCamera { public float farClip; public bool posProcessamento; }

    class DadosTerreno
    {
        public float erroPixel, densidadeDetalhe, distanciaArvores, distanciaDetalhe, distanciaBase;
    }

    // Pipelines gráficos
    RenderPipelineAsset pipelineOriginal;          // o que estava ligado ao abrir o jogo (pode ser nulo)
    UniversalRenderPipelineAsset assetAlta;        // configuração original
    UniversalRenderPipelineAsset assetLeve;        // cópia leve em memória (pode ser nula se não foi criada)

    // Valores originais do projeto
    float origEscala = 1f, origDistSombra = 50f, origLod = 1f;
    int origMsaa = 1, origMipLimite, origLuzesPixel = 2;

    readonly Dictionary<Light, LightShadows> luzes = new Dictionary<Light, LightShadows>();
    readonly Dictionary<Camera, DadosCamera> cameras = new Dictionary<Camera, DadosCamera>();
    readonly Dictionary<Terrain, DadosTerreno> terrenos = new Dictionary<Terrain, DadosTerreno>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        instancia = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (instancia != null) return;

        var go = new GameObject("AplicadorConfiguracoes");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<AplicadorConfiguracoes>();
    }

    void Awake()
    {
        pipelineOriginal = QualitySettings.renderPipeline;
        assetAlta = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (assetAlta != null)
        {
            origEscala = assetAlta.renderScale;
            origDistSombra = assetAlta.shadowDistance;
            origMsaa = assetAlta.msaaSampleCount;
        }

        // Perfil leve (cópia em memória, para nunca alterar o arquivo)
        var modelo = Resources.Load<UniversalRenderPipelineAsset>(CaminhoPerfilLeve);
        if (modelo != null) assetLeve = Instantiate(modelo);

        origLod = QualitySettings.lodBias;
        origMipLimite = QualitySettings.globalTextureMipmapLimit;
        origLuzesPixel = QualitySettings.pixelLightCount;

        SceneManager.sceneLoaded += AoCarregarCena;
        Configuracoes.Mudou += Aplicar;
        Application.quitting += Restaurar;

        Aplicar();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        Configuracoes.Mudou -= Aplicar;
        Application.quitting -= Restaurar;
    }

    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        Aplicar();
    }

    public void Aplicar()
    {
        NivelQualidade q = Configuracoes.Qualidade;

        AplicarQualidadeGlobal(q);
        AplicarTerrenos(q);
        AplicarCamera(q);
        AplicarSombras();
        AplicarFps();
    }

    // ---------- Qualidade (vale para o jogo todo) ----------
    void AplicarQualidadeGlobal(NivelQualidade q)
    {
        // Média e Baixa usam o perfil leve, se ele existir
        bool usarLeve = q != NivelQualidade.Alta && assetLeve != null;
        UniversalRenderPipelineAsset alvo = usarLeve ? assetLeve : assetAlta;

        if (alvo != null && GraphicsSettings.currentRenderPipeline != alvo)
            QualitySettings.renderPipeline = alvo;

        if (alvo != null)
        {
            float escala = q == NivelQualidade.Baixa ? 0.6f : q == NivelQualidade.Media ? 0.85f : origEscala;
            alvo.renderScale = Mathf.Min(origEscala, escala);

            int msaa = q == NivelQualidade.Alta ? origMsaa : 1;
            alvo.msaaSampleCount = Mathf.Min(origMsaa, msaa);

            float sombra = q == NivelQualidade.Baixa ? 15f : q == NivelQualidade.Media ? 30f : origDistSombra;
            alvo.shadowDistance = Mathf.Min(origDistSombra, sombra);
        }

        QualitySettings.lodBias = q == NivelQualidade.Baixa ? Mathf.Min(origLod, 0.8f)
                                : q == NivelQualidade.Media ? Mathf.Min(origLod, 1.2f)
                                : origLod;

        // Baixa: texturas em metade da resolução (economiza memória de vídeo)
        QualitySettings.globalTextureMipmapLimit = q == NivelQualidade.Baixa ? Mathf.Max(origMipLimite, 1) : origMipLimite;

        QualitySettings.pixelLightCount = q == NivelQualidade.Baixa ? Mathf.Min(origLuzesPixel, 1) : origLuzesPixel;
    }

    // ---------- Terreno ----------
    // O terreno tem 3 mapas de mistura (9 a 12 texturas): perto da câmera ele é desenhado em várias
    // passadas pesadas. Em PCs fracos isso é o que mais custa quando se olha para o chão.
    // "Distância do mapa base": além dela o terreno usa uma única textura pré-misturada (barato).
    void AplicarTerrenos(NivelQualidade q)
    {
        bool distancia = Configuracoes.DistanciaPersonalizada;
        float d = Configuracoes.Distancia;

        float limiteMapaBase = q == NivelQualidade.Baixa ? 0f : q == NivelQualidade.Media ? 20f : float.MaxValue;
        float limiteArvores = q == NivelQualidade.Baixa ? 120f : q == NivelQualidade.Media ? 300f : float.MaxValue;
        float limiteDetalhe = q == NivelQualidade.Baixa ? 20f : q == NivelQualidade.Media ? 45f : float.MaxValue;

        foreach (Terrain t in Terrain.activeTerrains)
        {
            DadosTerreno o;
            if (!terrenos.TryGetValue(t, out o))
            {
                o = new DadosTerreno
                {
                    erroPixel = t.heightmapPixelError,
                    densidadeDetalhe = t.detailObjectDensity,
                    distanciaArvores = t.treeDistance,
                    distanciaDetalhe = t.detailObjectDistance,
                    distanciaBase = t.basemapDistance
                };
                terrenos[t] = o;
            }

            t.heightmapPixelError = q == NivelQualidade.Baixa ? Mathf.Max(o.erroPixel, 20f)
                                  : q == NivelQualidade.Media ? Mathf.Max(o.erroPixel, 8f)
                                  : o.erroPixel;

            t.detailObjectDensity = q == NivelQualidade.Baixa ? Mathf.Min(o.densidadeDetalhe, 0.35f)
                                  : q == NivelQualidade.Media ? Mathf.Min(o.densidadeDetalhe, 0.7f)
                                  : o.densidadeDetalhe;

            t.basemapDistance = Mathf.Min(o.distanciaBase, limiteMapaBase);

            float arvores = Mathf.Min(o.distanciaArvores, limiteArvores);
            float detalhe = Mathf.Min(o.distanciaDetalhe, limiteDetalhe);

            if (distancia)
            {
                arvores = Mathf.Min(arvores, d);
                detalhe = Mathf.Min(detalhe, Mathf.Max(20f, d * 0.12f));
            }

            t.treeDistance = arvores;
            t.detailObjectDistance = detalhe;
        }
    }

    // ---------- Câmera principal: distância e pós-processamento ----------
    void AplicarCamera(NivelQualidade q)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        var dadosUrp = cam.GetUniversalAdditionalCameraData();

        DadosCamera o;
        if (!cameras.TryGetValue(cam, out o))
        {
            o = new DadosCamera
            {
                farClip = cam.farClipPlane,
                posProcessamento = dadosUrp != null && dadosUrp.renderPostProcessing
            };
            cameras[cam] = o;
        }

        cam.farClipPlane = Configuracoes.DistanciaPersonalizada
            ? Mathf.Max(cam.nearClipPlane + 1f, Configuracoes.Distancia)
            : o.farClip;

        // Qualidade baixa: sem efeitos de pós-processamento
        if (dadosUrp != null)
            dadosUrp.renderPostProcessing = o.posProcessamento && q != NivelQualidade.Baixa;
    }

    // ---------- Sombras ----------
    void AplicarSombras()
    {
        bool ligadas = Configuracoes.Sombras;

        foreach (Light luz in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!luzes.ContainsKey(luz)) luzes[luz] = luz.shadows;
            luz.shadows = ligadas ? luzes[luz] : LightShadows.None;
        }
    }

    // ---------- Contador de FPS ----------
    void AplicarFps()
    {
        bool mostrar = Configuracoes.MostrarFps;

        foreach (FPSCounter fps in FindObjectsByType<FPSCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            fps.enabled = mostrar;
    }

    // ---------- Devolve tudo como estava ao fechar o jogo ----------
    void Restaurar()
    {
        // Volta o pipeline gráfico que estava ligado antes
        QualitySettings.renderPipeline = pipelineOriginal;

        if (assetAlta != null)
        {
            assetAlta.renderScale = origEscala;
            assetAlta.shadowDistance = origDistSombra;
            assetAlta.msaaSampleCount = origMsaa;
        }

        QualitySettings.lodBias = origLod;
        QualitySettings.globalTextureMipmapLimit = origMipLimite;
        QualitySettings.pixelLightCount = origLuzesPixel;

        foreach (var par in terrenos)
        {
            Terrain t = par.Key;
            if (t == null) continue;

            t.heightmapPixelError = par.Value.erroPixel;
            t.detailObjectDensity = par.Value.densidadeDetalhe;
            t.treeDistance = par.Value.distanciaArvores;
            t.detailObjectDistance = par.Value.distanciaDetalhe;
            t.basemapDistance = par.Value.distanciaBase;
        }

        foreach (var par in luzes)
            if (par.Key != null) par.Key.shadows = par.Value;

        foreach (var par in cameras)
        {
            if (par.Key == null) continue;
            par.Key.farClipPlane = par.Value.farClip;

            var dados = par.Key.GetUniversalAdditionalCameraData();
            if (dados != null) dados.renderPostProcessing = par.Value.posProcessamento;
        }
    }
}

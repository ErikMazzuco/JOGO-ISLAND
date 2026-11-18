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
/// </summary>
public class AplicadorConfiguracoes : MonoBehaviour
{
    static AplicadorConfiguracoes instancia;

    class DadosCamera { public float farClip; public bool posProcessamento; }

    class DadosTerreno
    {
        public float erroPixel, densidadeDetalhe, distanciaArvores, distanciaDetalhe, distanciaBase;
    }

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

    UniversalRenderPipelineAsset Asset
    {
        get { return GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; }
    }

    void Awake()
    {
        var asset = Asset;
        if (asset != null)
        {
            origEscala = asset.renderScale;
            origDistSombra = asset.shadowDistance;
            origMsaa = asset.msaaSampleCount;
        }

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
        var asset = Asset;
        if (asset != null)
        {
            float escala = q == NivelQualidade.Baixa ? 0.7f : q == NivelQualidade.Media ? 0.85f : origEscala;
            asset.renderScale = Mathf.Min(origEscala, escala);

            int msaa = q == NivelQualidade.Alta ? origMsaa : (q == NivelQualidade.Media ? 2 : 1);
            asset.msaaSampleCount = Mathf.Min(origMsaa, msaa);

            float sombra = q == NivelQualidade.Baixa ? 15f : q == NivelQualidade.Media ? 30f : origDistSombra;
            asset.shadowDistance = Mathf.Min(origDistSombra, sombra);
        }

        QualitySettings.lodBias = q == NivelQualidade.Baixa ? Mathf.Min(origLod, 0.8f)
                                : q == NivelQualidade.Media ? Mathf.Min(origLod, 1.2f)
                                : origLod;

        // Baixa: texturas em metade da resolução (economiza memória de vídeo)
        QualitySettings.globalTextureMipmapLimit = q == NivelQualidade.Baixa ? Mathf.Max(origMipLimite, 1) : origMipLimite;

        QualitySettings.pixelLightCount = q == NivelQualidade.Baixa ? Mathf.Min(origLuzesPixel, 1) : origLuzesPixel;
    }

    // ---------- Terreno: detalhe, árvores e distância ----------
    void AplicarTerrenos(NivelQualidade q)
    {
        bool distancia = Configuracoes.DistanciaPersonalizada;
        float d = Configuracoes.Distancia;

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

            t.heightmapPixelError = q == NivelQualidade.Baixa ? Mathf.Max(o.erroPixel, 12f)
                                  : q == NivelQualidade.Media ? Mathf.Max(o.erroPixel, 6f)
                                  : o.erroPixel;

            t.detailObjectDensity = q == NivelQualidade.Baixa ? Mathf.Min(o.densidadeDetalhe, 0.4f)
                                  : q == NivelQualidade.Media ? Mathf.Min(o.densidadeDetalhe, 0.7f)
                                  : o.densidadeDetalhe;

            if (distancia)
            {
                t.treeDistance = Mathf.Min(o.distanciaArvores, d);
                t.basemapDistance = Mathf.Min(o.distanciaBase, d);
                t.detailObjectDistance = Mathf.Min(o.distanciaDetalhe, Mathf.Max(20f, d * 0.12f));
            }
            else
            {
                t.treeDistance = o.distanciaArvores;
                t.basemapDistance = o.distanciaBase;
                t.detailObjectDistance = o.distanciaDetalhe;
            }
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
        var asset = Asset;
        if (asset != null)
        {
            asset.renderScale = origEscala;
            asset.shadowDistance = origDistSombra;
            asset.msaaSampleCount = origMsaa;
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

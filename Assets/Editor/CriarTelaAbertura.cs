#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu  ISLAND > Criar Tela de Abertura (Splash)
/// Cria a cena Assets/Scenes/Abertura.unity (ícone da ilha animado + título ISLAND + barra de carregando)
/// e coloca ela como PRIMEIRA cena do Build Settings. Depois é só abrir a cena e apertar Play para ver.
/// </summary>
public static class CriarTelaAbertura
{
    const string CaminhoCena = "Assets/Scenes/Abertura.unity";
    const string CaminhoIcone = "Assets/ICON/ICONE447X447.png";
    const string CaminhoTitulo = "Assets/TELA HOME/TITLE.png";
    const string CaminhoPilula = "Assets/TELA HOME/UI PILULA.png";

    static readonly Color Fundo = new Color(0.05f, 0.08f, 0.10f);
    static readonly Color Creme = new Color(1f, 0.92f, 0.72f);

    [MenuItem("ISLAND/Criar Tela de Abertura (Splash)")]
    static void Criar()
    {
        AssetDatabase.Refresh();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (File.Exists(CaminhoCena) &&
            !EditorUtility.DisplayDialog("Tela de Abertura",
                "Já existe a cena " + CaminhoCena + ". Substituir pela nova?", "Substituir", "Cancelar"))
            return;

        Sprite iconeSpr = CriarHUDVida.CarregarSprite(CaminhoIcone);
        Sprite tituloSpr = CriarHUDVida.CarregarSprite(CaminhoTitulo);
        Sprite pilulaSpr = CriarHUDVida.CarregarSprite(CaminhoPilula);

        var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------- Câmera (só para mostrar preto atrás da interface) ----------
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 0;
        camGO.AddComponent<AudioListener>();

        // ---------- Canvas ----------
        var canvasGO = new GameObject("CanvasAbertura", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.layer = 5;
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var splash = canvasGO.AddComponent<SplashAbertura>();
        splash.nomeCena = "Menu";

        // ---------- Grupo geral (o fade final escurece tudo) ----------
        var geralRT = CriarHUDVida.NovoRect("Geral", canvasGO.transform);
        CriarHUDVida.Esticar(geralRT);
        splash.grupoGeral = geralRT.gameObject.AddComponent<CanvasGroup>();

        var fundo = CriarHUDVida.NovaImagem("Fundo", geralRT, Fundo);
        CriarHUDVida.Esticar(fundo.rectTransform);

        // Brilho quente atrás do ícone (o sprite é criado pelo script ao iniciar)
        var brilho = CriarHUDVida.NovaImagem("Brilho", geralRT, new Color(1f, 0.78f, 0.4f, 0.25f));
        Posicionar(brilho.rectTransform, new Vector2(0f, 190f), new Vector2(1000f, 1000f));
        splash.brilho = brilho;

        // ---------- Ícone da ilha ----------
        var icone = CriarHUDVida.NovaImagem("Icone", geralRT, iconeSpr != null ? Color.white : new Color(0.3f, 0.6f, 0.9f));
        icone.sprite = iconeSpr;
        icone.preserveAspect = true;
        Posicionar(icone.rectTransform, new Vector2(0f, 190f), new Vector2(400f, 400f));
        splash.icone = icone.rectTransform;
        splash.grupoIcone = icone.gameObject.AddComponent<CanvasGroup>();

        // ---------- Título ISLAND ----------
        var titulo = CriarHUDVida.NovaImagem("Titulo", geralRT, tituloSpr != null ? Color.white : new Color(1f, 0.8f, 0.4f));
        titulo.sprite = tituloSpr;
        titulo.preserveAspect = true;
        Posicionar(titulo.rectTransform, new Vector2(0f, -130f), new Vector2(660f, 261f));
        splash.titulo = titulo.rectTransform;
        splash.grupoTitulo = titulo.gameObject.AddComponent<CanvasGroup>();

        if (tituloSpr == null)
        {
            titulo.color = Color.clear;
            var tRT = CriarHUDVida.NovoRect("TituloTexto", titulo.rectTransform);
            CriarHUDVida.Esticar(tRT);
            CriarHUDVida.NovoTexto(tRT, "ISLAND", TextAlignmentOptions.Center, new Color(1f, 0.8f, 0.4f), 40f, 160f);
        }

        // ---------- Carregando (texto + barra) ----------
        var carregandoRT = CriarHUDVida.NovoRect("Carregando", geralRT);
        CriarHUDVida.Esticar(carregandoRT);
        splash.grupoCarregando = carregandoRT.gameObject.AddComponent<CanvasGroup>();

        var textoRT = CriarHUDVida.NovoRect("Texto", carregandoRT);
        Posicionar(textoRT, new Vector2(0f, -360f), new Vector2(600f, 48f));
        splash.textoCarregando = CriarHUDVida.NovoTexto(textoRT, "CARREGANDO", TextAlignmentOptions.Center, Creme, 20f, 34f);

        var barraImg = CriarHUDVida.NovaImagem("Barra", carregandoRT, new Color(0.15f, 0.1f, 0.06f, 0.9f));
        barraImg.sprite = pilulaSpr;
        if (pilulaSpr != null) barraImg.type = Image.Type.Sliced;
        Posicionar(barraImg.rectTransform, new Vector2(0f, -415f), new Vector2(560f, 24f));

        var areaRT = CriarHUDVida.NovoRect("Area", barraImg.rectTransform);
        CriarHUDVida.Esticar(areaRT);
        areaRT.offsetMin = new Vector2(4f, 4f);
        areaRT.offsetMax = new Vector2(-4f, -4f);

        var fill = CriarHUDVida.NovaImagem("Preenchimento", areaRT, new Color(1f, 0.7f, 0.22f));
        fill.sprite = pilulaSpr;
        if (pilulaSpr != null) fill.type = Image.Type.Sliced;
        CriarHUDVida.Esticar(fill.rectTransform);
        splash.barraPreenchimento = fill.rectTransform;

        // ---------- Salva e coloca como primeira cena do Build ----------
        Directory.CreateDirectory(Path.GetDirectoryName(CaminhoCena));
        EditorSceneManager.SaveScene(cena, CaminhoCena);
        AssetDatabase.Refresh();

        var lista = EditorBuildSettings.scenes.Where(s => s.path != CaminhoCena).ToList();
        lista.Insert(0, new EditorBuildSettingsScene(CaminhoCena, true));
        EditorBuildSettings.scenes = lista.ToArray();

        Selection.activeGameObject = canvasGO;

        EditorUtility.DisplayDialog("Tela de Abertura",
            "Cena criada: " + CaminhoCena + "\n\n" +
            "Ela já é a PRIMEIRA cena do Build (as cenas Menu e Island continuam na lista).\n" +
            "Aperte Play com ela aberta para ver. No jogo gerado (Build), é por ela que o jogo começa.", "OK");
    }

    static void Posicionar(RectTransform rt, Vector2 posicao, Vector2 tamanho)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamanho;
        rt.anchoredPosition = posicao;
    }
}
#endif

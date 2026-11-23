#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu  ISLAND > Criar Menu de Opções
/// Com a cena Menu aberta: cria o painel de opções e liga o botão CONFIG para abri-lo.
/// Salve a cena depois (Ctrl+S).
/// </summary>
public static class CriarMenuOpcoes
{
    const string Pasta = "Assets/TELA HOME/";
    const string CaminhoPainel = Pasta + "OPCOES PAINEL.png";
    const string CaminhoTitulo = Pasta + "OPCOES TITULO.png";
    const string CaminhoVoltar = Pasta + "BOTAO VOLTAR.png";
    const string CaminhoPilula = Pasta + "UI PILULA.png";
    const string CaminhoBola = Pasta + "UI BOLA.png";

    static readonly Color Creme = new Color(1f, 0.92f, 0.72f);

    [MenuItem("ISLAND/Criar Menu de Opções")]
    static void Criar()
    {
        AssetDatabase.Refresh();

        // ---------- Acha o botão CONFIG ----------
        Button botaoConfig = null;
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.name == "CONFIG") { botaoConfig = b; break; }
        }

        if (botaoConfig == null)
        {
            EditorUtility.DisplayDialog("Menu de Opções",
                "Não achei o botão CONFIG. Abra a cena Menu e tente de novo.", "OK");
            return;
        }

        Canvas canvas = botaoConfig.GetComponentInParent<Canvas>().rootCanvas;

        // ---------- Remove um painel antigo, se houver ----------
        var antigo = Object.FindFirstObjectByType<MenuOpcoes>(FindObjectsInactive.Include);
        if (antigo != null)
        {
            if (!EditorUtility.DisplayDialog("Menu de Opções",
                    "Já existe um menu de opções nesta cena. Substituir pelo novo?", "Substituir", "Cancelar"))
            {
                Selection.activeGameObject = antigo.gameObject;
                return;
            }

            Undo.DestroyObjectImmediate(antigo.gameObject);
        }

        // Limpa chamadas antigas "Abrir" do botão
        for (int i = botaoConfig.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (botaoConfig.onClick.GetPersistentMethodName(i) == "Abrir")
                UnityEventTools.RemovePersistentListener(botaoConfig.onClick, i);
        }

        var opcoes = ConstruirPainel(canvas, Object.FindFirstObjectByType<AudioMenu>());

        // ---------- Liga o botão CONFIG ----------
        UnityEventTools.AddVoidPersistentListener(botaoConfig.onClick, opcoes.Abrir);
        EditorUtility.SetDirty(botaoConfig);

        EditorSceneManager.MarkSceneDirty(opcoes.gameObject.scene);
        Selection.activeGameObject = opcoes.gameObject;

        Debug.Log("Menu de opções criado e ligado ao botão CONFIG. Salve a cena (Ctrl+S) e aperte Play.");
    }

    /// <summary>
    /// Constrói o painel de opções dentro do canvas dado (começa fechado). Usado pelo botão CONFIG
    /// do Menu e pelo menu de pausa do jogo.
    /// </summary>
    public static MenuOpcoes ConstruirPainel(Canvas canvas, AudioMenu som)
    {
        // ---------- Imagens ----------
        Sprite painelSpr = CriarHUDVida.CarregarSprite(CaminhoPainel);
        Sprite tituloSpr = CriarHUDVida.CarregarSprite(CaminhoTitulo);
        Sprite voltarSpr = CriarHUDVida.CarregarSprite(CaminhoVoltar);
        Sprite bolaSpr = CriarHUDVida.CarregarSprite(CaminhoBola);
        Sprite pilulaSpr = CarregarSpriteComBorda(CaminhoPilula, new Vector4(24, 24, 24, 24));

        // ---------- Fundo escuro que cobre a tela (bloqueia os botões de trás) ----------
        var raiz = CriarHUDVida.NovoRect("MenuOpcoes", canvas.transform);
        Undo.RegisterCreatedObjectUndo(raiz.gameObject, "Criar Menu de Opções");
        CriarHUDVida.Esticar(raiz);
        raiz.localScale = Vector3.one;

        var escuro = raiz.gameObject.AddComponent<Image>();
        escuro.color = new Color(0f, 0f, 0f, 0.65f);
        escuro.raycastTarget = true;

        var opcoes = raiz.gameObject.AddComponent<MenuOpcoes>();
        opcoes.som = som;

        // ---------- Painel de madeira ----------
        var painelRT = CriarHUDVida.NovoRect("Painel", raiz);
        painelRT.anchorMin = painelRT.anchorMax = painelRT.pivot = new Vector2(0.5f, 0.5f);
        painelRT.sizeDelta = new Vector2(1000f, 860f);
        painelRT.anchoredPosition = new Vector2(0f, -30f);

        var painelImg = CriarHUDVida.NovaImagem("Fundo", painelRT, painelSpr != null ? Color.white : new Color(0.5f, 0.26f, 0.1f));
        painelImg.sprite = painelSpr;
        painelImg.raycastTarget = true;
        CriarHUDVida.Esticar(painelImg.rectTransform);

        // Título (pedra)
        var tituloImg = CriarHUDVida.NovaImagem("Titulo", painelRT, Color.white);
        tituloImg.sprite = tituloSpr;
        tituloImg.preserveAspect = true;
        Posicionar(tituloImg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(430f, 172f));

        // ---------- Linhas ----------
        float y0 = -200f;
        const float passo = 100f;

        // 1) FPS
        CriarLinha(painelRT, "Mostrar FPS", y0 + 0 * passo, pilulaSpr);
        var fps = CriarBotaoPilula("BotaoFps", Linha(painelRT, 0), pilulaSpr, new Vector2(300f, 64f), "LIGADO", 26f);
        opcoes.botaoFps = fps.botao; opcoes.imagemFps = fps.imagem; opcoes.textoFps = fps.texto;

        // 2) Qualidade
        CriarLinha(painelRT, "Qualidade", y0 + 1 * passo, pilulaSpr);
        string[] nomesQ = { "BAIXA", "MÉDIA", "ALTA" };
        opcoes.botoesQualidade = new Button[3];
        opcoes.imagensQualidade = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var q = CriarBotaoPilula("Qualidade" + nomesQ[i], Linha(painelRT, 1), pilulaSpr, new Vector2(124f, 64f), nomesQ[i], 24f);
            q.botao.GetComponent<RectTransform>().anchoredPosition = new Vector2(-(2 - i) * 134f - 8f, 0f);
            opcoes.botoesQualidade[i] = q.botao;
            opcoes.imagensQualidade[i] = q.imagem;
        }

        // 3) Sombras
        CriarLinha(painelRT, "Sombras", y0 + 2 * passo, pilulaSpr);
        var somb = CriarBotaoPilula("BotaoSombras", Linha(painelRT, 2), pilulaSpr, new Vector2(300f, 64f), "LIGADO", 26f);
        opcoes.botaoSombras = somb.botao; opcoes.imagemSombras = somb.imagem; opcoes.textoSombras = somb.texto;

        // 4) Distância de renderização
        CriarLinha(painelRT, "Distância de visão", y0 + 3 * passo, pilulaSpr);
        CriarSlider("SliderDistancia", Linha(painelRT, 3), pilulaSpr, bolaSpr, out opcoes.sliderDistancia, out opcoes.textoDistancia);

        // 5) Sensibilidade
        CriarLinha(painelRT, "Sensibilidade do mouse", y0 + 4 * passo, pilulaSpr);
        CriarSlider("SliderSensibilidade", Linha(painelRT, 4), pilulaSpr, bolaSpr, out opcoes.sliderSensibilidade, out opcoes.textoSensibilidade);

        // Valores iniciais (só para o Editor; o jogo lê os valores salvos)
        opcoes.sliderDistancia.minValue = Configuracoes.DistanciaMin;
        opcoes.sliderDistancia.maxValue = Configuracoes.DistanciaMax;
        opcoes.sliderDistancia.value = Configuracoes.DistanciaPadrao;
        opcoes.sliderSensibilidade.minValue = Configuracoes.SensibilidadeMin;
        opcoes.sliderSensibilidade.maxValue = Configuracoes.SensibilidadeMax;
        opcoes.sliderSensibilidade.value = 1f;
        opcoes.textoDistancia.text = "1000 m";
        opcoes.textoSensibilidade.text = "100%";

        // Botão VOLTAR
        var voltarImg = CriarHUDVida.NovaImagem("BotaoVoltar", painelRT, Color.white);
        voltarImg.sprite = voltarSpr;
        voltarImg.preserveAspect = true;
        voltarImg.raycastTarget = true;
        Posicionar(voltarImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(374f, 117f));
        var voltarBtn = voltarImg.gameObject.AddComponent<Button>();
        voltarBtn.targetGraphic = voltarImg;
        voltarImg.gameObject.AddComponent<AnimacaoBotao>();
        opcoes.botaoVoltar = voltarBtn;

        // Começa fechado
        raiz.gameObject.SetActive(false);
        return opcoes;
    }

    // ---------- Linhas e controles ----------

    // Cada linha: faixa escura de fundo + nome à esquerda + "área de controle" à direita
    static void CriarLinha(RectTransform painel, string nome, float y, Sprite pilula)
    {
        var linha = CriarHUDVida.NovoRect("Linha_" + nome, painel);
        linha.anchorMin = linha.anchorMax = linha.pivot = new Vector2(0.5f, 1f);
        linha.sizeDelta = new Vector2(880f, 84f);
        linha.anchoredPosition = new Vector2(0f, y);

        var faixa = CriarHUDVida.NovaImagem("Faixa", linha, new Color(0.1f, 0.04f, 0.01f, 0.35f));
        faixa.sprite = pilula;
        faixa.type = Image.Type.Sliced;
        CriarHUDVida.Esticar(faixa.rectTransform);

        var rotuloRT = CriarHUDVida.NovoRect("Nome", linha);
        rotuloRT.anchorMin = new Vector2(0f, 0f);
        rotuloRT.anchorMax = new Vector2(0f, 1f);
        rotuloRT.pivot = new Vector2(0f, 0.5f);
        rotuloRT.sizeDelta = new Vector2(430f, 0f);
        rotuloRT.anchoredPosition = new Vector2(36f, 0f);
        CriarHUDVida.NovoTexto(rotuloRT, nome, TextAlignmentOptions.Left, Creme, 20f, 36f);

        // Área onde vai o controle (lado direito)
        var area = CriarHUDVida.NovoRect("Controle", linha);
        area.anchorMin = new Vector2(1f, 0f);
        area.anchorMax = new Vector2(1f, 1f);
        area.pivot = new Vector2(1f, 0.5f);
        area.sizeDelta = new Vector2(420f, 0f);
        area.anchoredPosition = new Vector2(-24f, 0f);
    }

    static RectTransform Linha(RectTransform painel, int indice)
    {
        // As linhas são criadas em ordem, uma "Controle" dentro de cada "Linha_*"
        int n = 0;
        foreach (Transform filho in painel)
        {
            if (!filho.name.StartsWith("Linha_")) continue;
            if (n == indice) return (RectTransform)filho.Find("Controle");
            n++;
        }
        return null;
    }

    struct BotaoPilula { public Button botao; public Image imagem; public TMP_Text texto; }

    static BotaoPilula CriarBotaoPilula(string nome, RectTransform pai, Sprite pilula, Vector2 tamanho, string texto, float tamFonte)
    {
        var img = CriarHUDVida.NovaImagem(nome, pai, Color.white);
        img.sprite = pilula;
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;

        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = tamanho;
        rt.anchoredPosition = Vector2.zero;

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        var textoRT = CriarHUDVida.NovoRect("Texto", rt);
        CriarHUDVida.Esticar(textoRT);
        var t = CriarHUDVida.NovoTexto(textoRT, texto, TextAlignmentOptions.Center, new Color(0.2f, 0.09f, 0.02f), 12f, tamFonte);

        return new BotaoPilula { botao = btn, imagem = img, texto = t };
    }

    static void CriarSlider(string nome, RectTransform pai, Sprite pilula, Sprite bola, out Slider slider, out TMP_Text valor)
    {
        // Texto do valor (extrema direita)
        var valorRT = CriarHUDVida.NovoRect("Valor", pai);
        valorRT.anchorMin = valorRT.anchorMax = valorRT.pivot = new Vector2(1f, 0.5f);
        valorRT.sizeDelta = new Vector2(110f, 60f);
        valorRT.anchoredPosition = Vector2.zero;
        valor = CriarHUDVida.NovoTexto(valorRT, "0", TextAlignmentOptions.Right, Creme, 16f, 30f);

        // Slider
        var sliderRT = CriarHUDVida.NovoRect(nome, pai);
        sliderRT.anchorMin = sliderRT.anchorMax = sliderRT.pivot = new Vector2(1f, 0.5f);
        sliderRT.sizeDelta = new Vector2(280f, 44f);
        sliderRT.anchoredPosition = new Vector2(-124f, 0f);

        // Trilho
        var fundo = CriarHUDVida.NovaImagem("Fundo", sliderRT, new Color(0.22f, 0.1f, 0.04f));
        fundo.sprite = pilula; fundo.type = Image.Type.Sliced; fundo.raycastTarget = true;
        fundo.rectTransform.anchorMin = new Vector2(0f, 0.3f);
        fundo.rectTransform.anchorMax = new Vector2(1f, 0.7f);
        fundo.rectTransform.offsetMin = Vector2.zero;
        fundo.rectTransform.offsetMax = Vector2.zero;

        // Área do preenchimento
        var areaFill = CriarHUDVida.NovoRect("AreaPreenchimento", sliderRT);
        areaFill.anchorMin = new Vector2(0f, 0.3f);
        areaFill.anchorMax = new Vector2(1f, 0.7f);
        areaFill.offsetMin = new Vector2(8f, 0f);
        areaFill.offsetMax = new Vector2(-8f, 0f);

        var fill = CriarHUDVida.NovaImagem("Preenchimento", areaFill, new Color(1f, 0.7f, 0.22f));
        fill.sprite = pilula; fill.type = Image.Type.Sliced; fill.raycastTarget = false;
        fill.rectTransform.anchorMin = new Vector2(0f, 0f);
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

        // Área da bolinha
        var areaHandle = CriarHUDVida.NovoRect("AreaBolinha", sliderRT);
        CriarHUDVida.Esticar(areaHandle);
        areaHandle.offsetMin = new Vector2(22f, 0f);
        areaHandle.offsetMax = new Vector2(-22f, 0f);

        var handle = CriarHUDVida.NovaImagem("Bolinha", areaHandle, Color.white);
        handle.sprite = bola; handle.raycastTarget = true;
        handle.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(44f, 0f);

        slider = sliderRT.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.wholeNumbers = false;
    }

    // ---------- Ajudantes ----------

    static void Posicionar(RectTransform rt, Vector2 ancora, Vector2 posicao, Vector2 tamanho)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = ancora;
        rt.sizeDelta = tamanho;
        rt.anchoredPosition = posicao;
    }

    static Sprite CarregarSpriteComBorda(string caminho, Vector4 borda)
    {
        var importer = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("Imagem não encontrada em " + caminho);
            return null;
        }

        if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != borda)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = borda;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
    }
}
#endif

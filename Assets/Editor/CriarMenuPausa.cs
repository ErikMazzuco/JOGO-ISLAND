#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu  ISLAND > Criar Menu de Pausa
/// Com a cena Island aberta: monta o menu que abre com ESC (CONTINUAR, OPÇÕES e MENU),
/// com o painel de opções completo dentro. Salve a cena depois (Ctrl+S).
/// </summary>
public static class CriarMenuPausa
{
    const string Pasta = "Assets/TELA HOME/";
    const string CaminhoTitulo = Pasta + "PAUSADO TITULO.png";
    const string CaminhoContinuar = Pasta + "BOTAO CONTINUAR.png";
    const string CaminhoOpcoes = Pasta + "BOTAO OPCOES.png";
    const string CaminhoMenu = Pasta + "BOTAO MENU.png";

    [MenuItem("ISLAND/Criar Menu de Pausa")]
    static void Criar()
    {
        AssetDatabase.Refresh();

        var existente = Object.FindFirstObjectByType<PausaJogo>(FindObjectsInactive.Include);
        if (existente != null)
        {
            if (!EditorUtility.DisplayDialog("Menu de Pausa",
                    "Já existe um menu de pausa nesta cena. Substituir pelo novo?", "Substituir", "Cancelar"))
            {
                Selection.activeGameObject = existente.gameObject;
                return;
            }

            Undo.DestroyObjectImmediate(existente.gameObject);
        }

        Sprite spriteTitulo = CriarHUDVida.CarregarSprite(CaminhoTitulo);
        Sprite spriteContinuar = CriarHUDVida.CarregarSprite(CaminhoContinuar);
        Sprite spriteOpcoes = CriarHUDVida.CarregarSprite(CaminhoOpcoes);
        Sprite spriteMenu = CriarHUDVida.CarregarSprite(CaminhoMenu);

        // ---------- Canvas (por cima do HUD e da tela de morte) ----------
        var canvasGO = new GameObject("CanvasPausa", typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Criar Menu de Pausa");
        canvasGO.layer = 5;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        var pausa = canvasGO.AddComponent<PausaJogo>();

        // ---------- Painel da pausa ----------
        var painelRT = CriarHUDVida.NovoRect("PainelPausa", canvasGO.transform);
        CriarHUDVida.Esticar(painelRT);

        var escuro = CriarHUDVida.NovaImagem("Escurecer", painelRT, new Color(0f, 0f, 0f, 0.7f));
        escuro.raycastTarget = true; // impede cliques "atrás" do painel
        CriarHUDVida.Esticar(escuro.rectTransform);

        var titulo = CriarHUDVida.NovaImagem("Titulo", painelRT, Color.white);
        titulo.sprite = spriteTitulo;
        titulo.preserveAspect = true;
        Centralizar(titulo.rectTransform, new Vector2(900f, 220f), new Vector2(0f, 300f));

        if (spriteTitulo == null)
        {
            titulo.color = Color.clear;
            var tRT = CriarHUDVida.NovoRect("TituloTexto", titulo.rectTransform);
            CriarHUDVida.Esticar(tRT);
            CriarHUDVida.NovoTexto(tRT, "PAUSADO", TextAlignmentOptions.Center,
                                   new Color(1f, 0.8f, 0.4f), 40f, 140f);
        }

        var botaoContinuar = NovoBotao(painelRT, "BotaoContinuar", "CONTINUAR", spriteContinuar, new Vector2(0f, 40f));
        var botaoOpcoes = NovoBotao(painelRT, "BotaoOpcoes", "OPÇÕES", spriteOpcoes, new Vector2(0f, -150f));
        var botaoMenu = NovoBotao(painelRT, "BotaoMenu", "MENU", spriteMenu, new Vector2(0f, -340f));

        // ---------- Painel de opções (por cima da pausa, começa fechado) ----------
        var opcoes = CriarMenuOpcoes.ConstruirPainel(canvas, null);
        opcoes.fecharComEsc = false;

        // ---------- Liga tudo ----------
        pausa.painelPausa = painelRT.gameObject;
        pausa.opcoes = opcoes;
        pausa.botaoContinuar = botaoContinuar;
        pausa.botaoOpcoes = botaoOpcoes;
        pausa.botaoMenu = botaoMenu;
        pausa.nomeCenaMenu = "Menu";

        // O painel começa escondido (o ESC mostra)
        painelRT.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Selection.activeGameObject = canvasGO;

        Debug.Log("Menu de pausa criado. Salve a cena (Ctrl+S), aperte Play e use o ESC.");
    }

    static Button NovoBotao(Transform pai, string nome, string rotulo, Sprite sprite, Vector2 posicao)
    {
        var img = CriarHUDVida.NovaImagem(nome, pai, sprite != null ? Color.white : new Color(0.6f, 0.3f, 0.1f));
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = true;
        Centralizar(img.rectTransform, new Vector2(520f, 163f), posicao);

        var botao = img.gameObject.AddComponent<Button>();
        botao.targetGraphic = img;
        botao.navigation = new Navigation { mode = Navigation.Mode.None };

        var cores = botao.colors;
        cores.normalColor = Color.white;
        cores.highlightedColor = Color.white;
        cores.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cores.selectedColor = Color.white;
        botao.colors = cores;

        // Mesmo efeito de aumentar ao passar o mouse dos botões do Home
        img.gameObject.AddComponent<AnimacaoBotao>();

        // Só usa texto do TextMesh Pro se a imagem do botão não for encontrada
        if (sprite == null)
        {
            var textoRT = CriarHUDVida.NovoRect("Texto", img.rectTransform);
            CriarHUDVida.Esticar(textoRT);
            CriarHUDVida.NovoTexto(textoRT, rotulo, TextAlignmentOptions.Center,
                                   new Color(1f, 0.92f, 0.72f), 18f, 60f);
        }

        return botao;
    }

    static void Centralizar(RectTransform rt, Vector2 tamanho, Vector2 posicao)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamanho;
        rt.anchoredPosition = posicao;
    }
}
#endif

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu  ISLAND > Criar Tela de Morte
/// Monta o Canvas da tela de fim de jogo com as imagens da pasta TELA HOME.
/// Rode com a cena Island aberta e salve a cena depois (Ctrl+S).
/// </summary>
public static class CriarTelaMorte
{
    const string CaminhoTitulo = "Assets/TELA HOME/FIM DE JOGO.png";
    const string CaminhoBotaoRenascer = "Assets/TELA HOME/BOTAO RENASCER.png";
    const string CaminhoBotaoMenu = "Assets/TELA HOME/BOTAO MENU.png";

    [MenuItem("ISLAND/Criar Tela de Morte")]
    static void Criar()
    {
        AssetDatabase.Refresh();

        var existente = Object.FindFirstObjectByType<TelaMorte>();
        if (existente != null)
        {
            bool substituir = EditorUtility.DisplayDialog(
                "Tela de Morte",
                "Já existe uma tela de morte nesta cena. Substituir pela nova?",
                "Substituir", "Cancelar");

            if (!substituir)
            {
                Selection.activeGameObject = existente.gameObject;
                return;
            }

            Undo.DestroyObjectImmediate(existente.gameObject);
        }

        Sprite spriteTitulo = CriarHUDVida.CarregarSprite(CaminhoTitulo);
        Sprite spriteRenascer = CriarHUDVida.CarregarSprite(CaminhoBotaoRenascer);
        Sprite spriteMenu = CriarHUDVida.CarregarSprite(CaminhoBotaoMenu);

        // ---------- Canvas ----------
        var canvasGO = new GameObject("CanvasMorte", typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Criar Tela de Morte");
        canvasGO.layer = 5;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        var tela = canvasGO.AddComponent<TelaMorte>();

        // ---------- Painel (aparece ao morrer) ----------
        var painelRT = CriarHUDVida.NovoRect("Painel", canvasGO.transform);
        CriarHUDVida.Esticar(painelRT);
        var grupo = painelRT.gameObject.AddComponent<CanvasGroup>();

        // Escurecimento da tela
        var escuro = CriarHUDVida.NovaImagem("Escurecer", painelRT, new Color(0.06f, 0f, 0f, 0.8f));
        escuro.raycastTarget = true; // impede cliques "atrás" do painel
        CriarHUDVida.Esticar(escuro.rectTransform);

        // Título "FIM DE JOGO" (imagem no estilo do logo ISLAND)
        var titulo = CriarHUDVida.NovaImagem("Titulo", painelRT, Color.white);
        titulo.sprite = spriteTitulo;
        titulo.preserveAspect = true;
        Centralizar(titulo.rectTransform, new Vector2(1100f, 209f), new Vector2(0f, 190f));

        if (spriteTitulo == null)
        {
            titulo.color = Color.clear;
            var tRT = CriarHUDVida.NovoRect("TituloTexto", titulo.rectTransform);
            CriarHUDVida.Esticar(tRT);
            CriarHUDVida.NovoTexto(tRT, "FIM DE JOGO", TextAlignmentOptions.Center,
                                   new Color(1f, 0.8f, 0.4f), 40f, 140f);
        }

        // Botões (mesmo estilo dos botões do Home)
        var botaoRenascer = NovoBotao(painelRT, "BotaoRenascer", "RENASCER", spriteRenascer, new Vector2(0f, -60f));
        var botaoMenu = NovoBotao(painelRT, "BotaoMenu", "MENU", spriteMenu, new Vector2(0f, -250f));

        // ---------- Liga tudo no componente ----------
        tela.jogador = Object.FindFirstObjectByType<VidaJogador>();
        tela.painel = painelRT.gameObject;
        tela.grupo = grupo;
        tela.botaoRenascer = botaoRenascer;
        tela.botaoMenu = botaoMenu;
        tela.nomeCenaMenu = "Menu";

        // O painel começa escondido (a TelaMorte mostra quando o jogador morre)
        painelRT.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Selection.activeGameObject = canvasGO;

        Debug.Log("Tela de morte criada. Salve a cena (Ctrl+S) e teste morrendo para um esqueleto.");
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

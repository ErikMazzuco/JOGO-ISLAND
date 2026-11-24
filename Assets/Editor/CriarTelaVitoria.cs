#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu  ISLAND > Criar Tela de Vitória
/// Com a cena Island aberta: cria a tela de vitória com visual de madeira (painel, título de pedra e botões)
/// e liga ela no ObjetivoBaus. Os textos ficam como objetos TextMeshPro editáveis na Hierarchy.
/// </summary>
public static class CriarTelaVitoria
{
    const string Pasta = "Assets/TELA HOME/";
    const string CaminhoPainel = Pasta + "OPCOES PAINEL.png";
    const string CaminhoTitulo = Pasta + "VITORIA TITULO.png";
    const string CaminhoJogarDeNovo = Pasta + "BOTAO JOGAR DE NOVO.png";
    const string CaminhoMenu = Pasta + "BOTAO MENU.png";

    static readonly Color Creme = new Color(1f, 0.92f, 0.72f);

    [MenuItem("ISLAND/Criar Tela de Vitória")]
    static void Criar()
    {
        AssetDatabase.Refresh();

        var existente = Object.FindFirstObjectByType<TelaVitoria>(FindObjectsInactive.Include);
        if (existente != null)
        {
            if (!EditorUtility.DisplayDialog("Tela de Vitória",
                    "Já existe uma tela de vitória nesta cena. Substituir pela nova?", "Substituir", "Cancelar"))
            {
                Selection.activeGameObject = existente.gameObject;
                return;
            }
            Undo.DestroyObjectImmediate(existente.gameObject);
        }

        Sprite painelSpr = CriarHUDVida.CarregarSprite(CaminhoPainel);
        Sprite tituloSpr = CriarHUDVida.CarregarSprite(CaminhoTitulo);
        Sprite jogarSpr = CriarHUDVida.CarregarSprite(CaminhoJogarDeNovo);
        Sprite menuSpr = CriarHUDVida.CarregarSprite(CaminhoMenu);

        // ---------- Canvas (acima de tudo, inclusive da pausa) ----------
        var canvasGO = new GameObject("CanvasVitoria", typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Criar Tela de Vitória");
        canvasGO.layer = 5;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        var tela = canvasGO.AddComponent<TelaVitoria>();

        // ---------- Painel (fundo escuro + caixa de madeira) ----------
        var painelRT = CriarHUDVida.NovoRect("PainelVitoria", canvasGO.transform);
        CriarHUDVida.Esticar(painelRT);
        var grupo = painelRT.gameObject.AddComponent<CanvasGroup>();

        var escuro = CriarHUDVida.NovaImagem("Escurecer", painelRT, new Color(0f, 0f, 0f, 0.72f));
        escuro.raycastTarget = true;
        CriarHUDVida.Esticar(escuro.rectTransform);

        var caixa = CriarHUDVida.NovaImagem("Caixa", painelRT, painelSpr != null ? Color.white : new Color(0.45f, 0.24f, 0.09f));
        caixa.sprite = painelSpr;
        caixa.raycastTarget = true;
        Posicionar(caixa.rectTransform, new Vector2(0f, 0f), new Vector2(1000f, 860f));

        // ---------- Título ----------
        var titulo = CriarHUDVida.NovaImagem("Titulo", caixa.rectTransform, tituloSpr != null ? Color.white : Color.clear);
        titulo.sprite = tituloSpr;
        titulo.preserveAspect = true;
        Posicionar(titulo.rectTransform, new Vector2(0f, 300f), new Vector2(560f, 180f));

        if (tituloSpr == null)
        {
            var tRT = CriarHUDVida.NovoRect("TituloTexto", titulo.rectTransform);
            CriarHUDVida.Esticar(tRT);
            CriarHUDVida.NovoTexto(tRT, "VITÓRIA!", TextAlignmentOptions.Center, new Color(1f, 0.8f, 0.25f), 40f, 120f);
        }

        // ---------- Textos (todos editáveis na Hierarchy) ----------
        var sub = NovoTexto(caixa.rectTransform, "TextoSubtitulo", "Você encontrou todos os tesouros da ilha.",
                            new Vector2(0f, 160f), new Vector2(860f, 110f), 26f, 42f, Creme, true);
        var baus = NovoTexto(caixa.rectTransform, "TextoBaus", "BAÚS ABERTOS:  <color=#FFD060>0 / 0</color>",
                             new Vector2(0f, 30f), new Vector2(760f, 70f), 24f, 44f, Color.white, false);
        var moedas = NovoTexto(caixa.rectTransform, "TextoMoedas", "MOEDAS:  <color=#FFD060>0</color>",
                               new Vector2(0f, -50f), new Vector2(760f, 70f), 24f, 44f, Color.white, false);
        var tempo = NovoTexto(caixa.rectTransform, "TextoTempo", "TEMPO:  <color=#FFD060>0:00</color>",
                              new Vector2(0f, -130f), new Vector2(760f, 70f), 24f, 44f, Color.white, false);

        // ---------- Botões ----------
        var botaoJogar = NovoBotao(caixa.rectTransform, "BotaoJogarDeNovo", "JOGAR DE NOVO", jogarSpr, new Vector2(-215f, -310f));
        var botaoMenu = NovoBotao(caixa.rectTransform, "BotaoMenu", "MENU", menuSpr, new Vector2(215f, -310f));

        // ---------- Liga tudo ----------
        tela.painel = painelRT.gameObject;
        tela.grupo = grupo;
        tela.caixa = caixa.rectTransform;
        tela.textoSubtitulo = sub;
        tela.textoBaus = baus;
        tela.textoMoedas = moedas;
        tela.textoTempo = tempo;
        tela.botaoJogarDeNovo = botaoJogar;
        tela.botaoMenu = botaoMenu;
        tela.nomeCenaMenu = "Menu";

        painelRT.gameObject.SetActive(false);   // o ObjetivoBaus mostra quando o último baú for aberto

        // ---------- Liga no ObjetivoBaus ----------
        int ligados = 0;
        foreach (var objetivo in Object.FindObjectsByType<ObjetivoBaus>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(objetivo);
            var prop = so.FindProperty("telaVitoria");
            if (prop == null) continue;
            prop.objectReferenceValue = tela;
            so.ApplyModifiedPropertiesWithoutUndo();
            ligados++;
        }

        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Selection.activeGameObject = canvasGO;

        Debug.Log("Tela de vitória criada" + (ligados > 0 ? " e ligada no ObjetivoBaus." : ", mas não achei ObjetivoBaus na cena (rode ISLAND > Objetivo > Preparar Objetivo e Rei Esqueleto).") +
                  " Salve a cena (Ctrl+S).");
    }

    static TMP_Text NovoTexto(RectTransform pai, string nome, string texto, Vector2 posicao, Vector2 tamanho,
                              float min, float max, Color cor, bool quebraLinha)
    {
        var rt = CriarHUDVida.NovoRect(nome, pai);
        Posicionar(rt, posicao, tamanho);
        var t = CriarHUDVida.NovoTexto(rt, texto, TextAlignmentOptions.Center, cor, min, max);
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    static Button NovoBotao(RectTransform pai, string nome, string rotulo, Sprite sprite, Vector2 posicao)
    {
        var img = CriarHUDVida.NovaImagem(nome, pai, sprite != null ? Color.white : new Color(0.6f, 0.3f, 0.1f));
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = true;
        Posicionar(img.rectTransform, posicao, new Vector2(400f, 125f));

        var botao = img.gameObject.AddComponent<Button>();
        botao.targetGraphic = img;
        botao.navigation = new Navigation { mode = Navigation.Mode.None };

        var cores = botao.colors;
        cores.normalColor = Color.white;
        cores.highlightedColor = Color.white;
        cores.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cores.selectedColor = Color.white;
        botao.colors = cores;

        img.gameObject.AddComponent<AnimacaoBotao>();

        if (sprite == null)
        {
            var textoRT = CriarHUDVida.NovoRect("Texto", img.rectTransform);
            CriarHUDVida.Esticar(textoRT);
            CriarHUDVida.NovoTexto(textoRT, rotulo, TextAlignmentOptions.Center, Creme, 18f, 48f);
        }

        return botao;
    }

    static void Posicionar(RectTransform rt, Vector2 posicao, Vector2 tamanho)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamanho;
        rt.anchoredPosition = posicao;
    }
}
#endif

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu  ISLAND > Criar HUD de Vida
/// Monta o Canvas com a placa de madeira (Assets/TELA HOME/PLACA.png), a barra e o número de vida.
/// Rode com a cena Island aberta e salve a cena depois (Ctrl+S).
/// </summary>
public static class CriarHUDVida
{
    internal const string CaminhoPlaca = "Assets/TELA HOME/PLACA.png";

    [MenuItem("ISLAND/Criar HUD de Vida")]
    static void Criar()
    {
        var existente = Object.FindFirstObjectByType<HUDVida>();
        if (existente != null)
        {
            bool outro = EditorUtility.DisplayDialog(
                "HUD de Vida",
                "Já existe um HUD de vida nesta cena. Criar outro mesmo assim?",
                "Criar outro", "Cancelar");

            if (!outro)
            {
                Selection.activeGameObject = existente.gameObject;
                return;
            }
        }

        Sprite placa = CarregarSprite(CaminhoPlaca);

        // ---------- Canvas ----------
        var canvasGO = new GameObject("CanvasHUD", typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Criar HUD de Vida");
        canvasGO.layer = 5; // UI

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // ---------- Flash vermelho de dano (tela cheia) ----------
        var flash = NovaImagem("FlashDano", canvasGO.transform, new Color(0.8f, 0f, 0f, 0f));
        Esticar(flash.rectTransform);

        // ---------- Painel (canto superior esquerdo) ----------
        var painelRT = NovoRect("HUDVida", canvasGO.transform);
        painelRT.anchorMin = painelRT.anchorMax = new Vector2(0f, 1f);
        painelRT.pivot = new Vector2(0f, 1f);
        painelRT.sizeDelta = new Vector2(480f, 240f);
        painelRT.anchoredPosition = new Vector2(10f, -10f);
        var hud = painelRT.gameObject.AddComponent<HUDVida>();

        // Placa de madeira
        var fundo = NovaImagem("Placa", painelRT, placa != null ? Color.white : new Color(0.6f, 0.3f, 0.1f));
        fundo.sprite = placa;
        fundo.preserveAspect = true;
        Esticar(fundo.rectTransform);

        // Rótulo "VIDA"
        var rotuloRT = NovoRect("Rotulo", painelRT);
        Ancorar(rotuloRT, 0.16f, 0.55f, 0.50f, 0.69f);
        var rotulo = NovoTexto(rotuloRT, "VIDA", TextAlignmentOptions.Left, new Color(1f, 0.92f, 0.72f), 14f, 34f);

        // Fundo escuro da barra
        var barraFundo = NovaImagem("BarraFundo", painelRT, new Color(0.16f, 0.08f, 0.04f, 0.95f));
        Ancorar(barraFundo.rectTransform, 0.15f, 0.28f, 0.76f, 0.52f);

        // Área interna (margem de 5px)
        var areaRT = NovoRect("Area", barraFundo.rectTransform);
        Esticar(areaRT);
        areaRT.offsetMin = new Vector2(5f, 5f);
        areaRT.offsetMax = new Vector2(-5f, -5f);

        var atraso = NovaImagem("BarraAtraso", areaRT, new Color(1f, 0.95f, 0.70f, 0.9f));
        Esticar(atraso.rectTransform);

        var cheia = NovaImagem("BarraCheia", areaRT, new Color(0.25f, 0.8f, 0.3f));
        Esticar(cheia.rectTransform);

        // Número "100 / 100" por cima da barra
        var textoRT = NovoRect("TextoVida", barraFundo.rectTransform);
        Esticar(textoRT);
        var texto = NovoTexto(textoRT, "100 / 100", TextAlignmentOptions.Center, Color.white, 12f, 34f);

        // ---------- Liga tudo no componente ----------
        hud.jogador = Object.FindFirstObjectByType<VidaJogador>();
        hud.barraCheia = cheia.rectTransform;
        hud.barraAtraso = atraso.rectTransform;
        hud.imagemBarraCheia = cheia;
        hud.textoVida = texto;
        hud.textoRotulo = rotulo;
        hud.flashDano = flash;

        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Selection.activeGameObject = painelRT.gameObject;

        Debug.Log("HUD de vida criado. Salve a cena (Ctrl+S) e aperte Play.");
    }

    // ---------- Ajudantes ----------

    internal static Sprite CarregarSprite(string caminho)
    {
        var importer = AssetImporter.GetAtPath(caminho) as TextureImporter;

        if (importer == null)
        {
            Debug.LogWarning("Imagem não encontrada em " + caminho + ". Será usada uma cor lisa no lugar.");
            return null;
        }

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
    }

    internal static RectTransform NovoRect(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(pai, false);
        return go.GetComponent<RectTransform>();
    }

    internal static Image NovaImagem(string nome, Transform pai, Color cor)
    {
        var rt = NovoRect(nome, pai);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = cor;
        img.raycastTarget = false;
        return img;
    }

    internal static TMP_Text NovoTexto(RectTransform rt, string conteudo, TextAlignmentOptions alinhamento,
                              Color cor, float tamMin, float tamMax)
    {
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = conteudo;
        t.alignment = alinhamento;
        t.color = cor;
        t.fontStyle = FontStyles.Bold;
        t.enableAutoSizing = true;
        t.fontSizeMin = tamMin;
        t.fontSizeMax = tamMax;
        t.raycastTarget = false;
        return t;
    }

    internal static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    internal static void Ancorar(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif

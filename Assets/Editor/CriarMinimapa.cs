#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu  ISLAND > Criar Minimapa
/// Monta a interface do minimapa (moldura de madeira, círculo do mapa, seta e N) no canto superior direito,
/// abaixo do contador de moedas. A câmera e os marcadores são criados pelo script Minimapa ao dar Play.
/// Rode com a cena Island aberta e salve a cena depois (Ctrl+S).
/// </summary>
public static class CriarMinimapa
{
    const string CaminhoMoldura = "Assets/TELA HOME/MINIMAPA MOLDURA.png";

    [MenuItem("ISLAND/Criar Minimapa")]
    static void Criar()
    {
        AssetDatabase.Refresh();

        var existente = Object.FindFirstObjectByType<Minimapa>();
        if (existente != null)
        {
            bool substituir = EditorUtility.DisplayDialog(
                "Minimapa",
                "Já existe um minimapa nesta cena. Substituir pelo novo?",
                "Substituir", "Cancelar");

            if (!substituir)
            {
                Selection.activeGameObject = existente.gameObject;
                return;
            }

            Undo.DestroyObjectImmediate(existente.gameObject);
        }

        Sprite moldura = CriarHUDVida.CarregarSprite(CaminhoMoldura);

        // ---------- Usa o mesmo Canvas do HUD de vida (ou cria um) ----------
        Transform pai;
        var hudVida = Object.FindFirstObjectByType<HUDVida>();
        Canvas canvasExistente = hudVida != null ? hudVida.GetComponentInParent<Canvas>() : null;

        if (canvasExistente != null)
        {
            pai = canvasExistente.transform;
        }
        else
        {
            var canvasGO = new GameObject("CanvasHUD", typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Criar Minimapa");
            canvasGO.layer = 5;

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            pai = canvasGO.transform;
        }

        // ---------- Painel (canto superior direito, abaixo das moedas) ----------
        const float tamanho = 300f;

        var raiz = CriarHUDVida.NovoRect("Minimapa", pai);
        Undo.RegisterCreatedObjectUndo(raiz.gameObject, "Criar Minimapa");
        raiz.anchorMin = raiz.anchorMax = new Vector2(1f, 1f);
        raiz.pivot = new Vector2(1f, 1f);
        raiz.sizeDelta = new Vector2(tamanho, tamanho);
        raiz.anchoredPosition = new Vector2(-14f, -182f);

        var minimapa = raiz.gameObject.AddComponent<Minimapa>();

        // Círculo do mapa (o furo da moldura tem ~80% do tamanho; o mapa fica um pouco por baixo da madeira)
        var mascaraRT = CriarHUDVida.NovoRect("Mascara", raiz);
        mascaraRT.anchorMin = mascaraRT.anchorMax = mascaraRT.pivot = new Vector2(0.5f, 0.5f);
        mascaraRT.sizeDelta = new Vector2(tamanho * 0.82f, tamanho * 0.82f);
        mascaraRT.anchoredPosition = Vector2.zero;

        var mascaraImg = mascaraRT.gameObject.AddComponent<Image>();
        mascaraImg.color = Color.white;
        mascaraImg.raycastTarget = false;
        var mascara = mascaraRT.gameObject.AddComponent<Mask>();
        mascara.showMaskGraphic = false;

        var mapaGO = CriarHUDVida.NovoRect("Mapa", mascaraRT);
        CriarHUDVida.Esticar(mapaGO);
        var mapaImg = mapaGO.gameObject.AddComponent<RawImage>();
        mapaImg.color = Color.white;
        mapaImg.raycastTarget = false;

        var marcadoresRT = CriarHUDVida.NovoRect("Marcadores", mascaraRT);
        CriarHUDVida.Esticar(marcadoresRT);

        var topoRT = CriarHUDVida.NovoRect("Topo", mascaraRT);
        CriarHUDVida.Esticar(topoRT);

        // Seta do jogador (no centro)
        var setaImg = CriarHUDVida.NovaImagem("SetaJogador", topoRT, new Color(1f, 0.95f, 0.75f));
        setaImg.rectTransform.anchorMin = setaImg.rectTransform.anchorMax = setaImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        setaImg.rectTransform.sizeDelta = new Vector2(30f, 30f);
        setaImg.rectTransform.anchoredPosition = Vector2.zero;

        // Letra N (norte)
        var norteImg = CriarHUDVida.NovaImagem("Norte", topoRT, new Color(0.22f, 0.11f, 0.04f, 0.85f));
        norteImg.rectTransform.anchorMin = norteImg.rectTransform.anchorMax = norteImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        norteImg.rectTransform.sizeDelta = new Vector2(26f, 26f);

        var letraRT = CriarHUDVida.NovoRect("Letra", norteImg.rectTransform);
        CriarHUDVida.Esticar(letraRT);
        CriarHUDVida.NovoTexto(letraRT, "N", TextAlignmentOptions.Center, new Color(1f, 0.92f, 0.72f), 10f, 20f);

        // Moldura de madeira por cima
        var molduraImg = CriarHUDVida.NovaImagem("Moldura", raiz, moldura != null ? Color.white : new Color(0.55f, 0.28f, 0.1f));
        molduraImg.sprite = moldura;
        molduraImg.preserveAspect = true;
        CriarHUDVida.Esticar(molduraImg.rectTransform);

        if (moldura == null)
            Debug.LogWarning("Moldura não encontrada em " + CaminhoMoldura + ". Verifique se o arquivo está na pasta TELA HOME.");

        // ---------- Liga tudo no componente ----------
        minimapa.mascara = mascaraImg;
        minimapa.imagemMapa = mapaImg;
        minimapa.marcadores = marcadoresRT;
        minimapa.seta = setaImg.rectTransform;
        minimapa.letraNorte = norteImg.rectTransform;

        EditorSceneManager.MarkSceneDirty(raiz.gameObject.scene);
        Selection.activeGameObject = raiz.gameObject;

        Debug.Log("Minimapa criado. Salve a cena (Ctrl+S) e aperte Play. Tecla M alterna o zoom.");
    }
}
#endif

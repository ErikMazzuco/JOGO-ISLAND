#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menus:
///   ISLAND > Criar Contador de Moedas       (monta o contador no HUD, canto superior direito)
///   ISLAND > Preparar Moedas Selecionadas   (adiciona o script Moeda e o som nas moedas escolhidas)
/// </summary>
public static class CriarContadorMoedas
{
    const string CaminhoFundo = "Assets/TELA HOME/CONTADORCOINVAZIO.png";
    const string PastaDigitos = "Assets/TELA HOME/DIGITOS/";
    const string CaminhoSom = "Assets/SONGS/POINTSOUND.mp3";

    [MenuItem("ISLAND/Criar Contador de Moedas")]
    static void CriarContador()
    {
        AssetDatabase.Refresh();

        var existente = Object.FindFirstObjectByType<ContadorMoedas>();
        if (existente != null)
        {
            bool substituir = EditorUtility.DisplayDialog(
                "Contador de Moedas",
                "Já existe um contador de moedas nesta cena. Substituir pelo novo?",
                "Substituir", "Cancelar");

            if (!substituir)
            {
                Selection.activeGameObject = existente.gameObject;
                return;
            }

            Undo.DestroyObjectImmediate(existente.gameObject);
        }

        Sprite fundo = CriarHUDVida.CarregarSprite(CaminhoFundo);

        var digitosSprites = new Sprite[10];
        bool digitosOk = true;
        for (int i = 0; i < 10; i++)
        {
            digitosSprites[i] = CriarHUDVida.CarregarSprite(PastaDigitos + i + ".png");
            if (digitosSprites[i] == null) digitosOk = false;
        }

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
            Undo.RegisterCreatedObjectUndo(canvasGO, "Criar Contador de Moedas");
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

        // ---------- Painel (canto superior direito) ----------
        var painelRT = CriarHUDVida.NovoRect("ContadorMoedas", pai);
        Undo.RegisterCreatedObjectUndo(painelRT.gameObject, "Criar Contador de Moedas");
        painelRT.anchorMin = painelRT.anchorMax = new Vector2(1f, 1f);
        painelRT.pivot = new Vector2(1f, 1f);
        painelRT.sizeDelta = new Vector2(460f, 167f);
        painelRT.anchoredPosition = new Vector2(-10f, -10f);

        var contador = painelRT.gameObject.AddComponent<ContadorMoedas>();

        var imgFundo = CriarHUDVida.NovaImagem("Fundo", painelRT, fundo != null ? Color.white : new Color(0.6f, 0.3f, 0.1f));
        imgFundo.sprite = fundo;
        imgFundo.preserveAspect = true;
        CriarHUDVida.Esticar(imgFundo.rectTransform);

        // Área dos números (parte vazia da tábua, à direita da moeda)
        var numerosRT = CriarHUDVida.NovoRect("Numeros", painelRT);
        CriarHUDVida.Ancorar(numerosRT, 0.36f, 0.29f, 0.86f, 0.60f);

        var layout = numerosRT.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = -3f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var digitos = new Image[5];
        for (int i = 0; i < digitos.Length; i++)
        {
            digitos[i] = CriarHUDVida.NovaImagem("Digito" + i, numerosRT, Color.white);
            digitos[i].preserveAspect = true;
            digitos[i].rectTransform.sizeDelta = new Vector2(38f, 50f);
        }

        // ---------- Liga tudo no componente ----------
        contador.digitos = digitos;
        contador.alturaDigito = 50f;

        if (digitosOk)
        {
            contador.spritesDigitos = digitosSprites;
        }
        else
        {
            // Sem as imagens dos números: usa texto do TextMesh Pro
            var textoRT = CriarHUDVida.NovoRect("TextoAlternativo", numerosRT);
            var le = textoRT.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            CriarHUDVida.Esticar(textoRT);
            contador.textoAlternativo = CriarHUDVida.NovoTexto(textoRT, "0", TextAlignmentOptions.Center,
                                                               new Color(1f, 0.92f, 0.72f), 12f, 50f);
            Debug.LogWarning("Imagens dos números não encontradas em " + PastaDigitos + ". Usando texto no lugar.");
        }

        EditorSceneManager.MarkSceneDirty(painelRT.gameObject.scene);
        Selection.activeGameObject = painelRT.gameObject;

        Debug.Log("Contador de moedas criado. Salve a cena (Ctrl+S).");
    }

    [MenuItem("ISLAND/Preparar Moedas Selecionadas")]
    static void PrepararMoedas()
    {
        var selecionados = Selection.gameObjects;
        var som = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoSom);

        int preparadas = 0;

        foreach (var go in selecionados)
        {
            if (EditorUtility.IsPersistent(go)) continue; // só objetos da cena

            var moeda = go.GetComponent<Moeda>();
            if (moeda == null) moeda = Undo.AddComponent<Moeda>(go);

            Undo.RecordObject(moeda, "Preparar Moeda");
            if (moeda.somColeta == null) moeda.somColeta = som;
            EditorUtility.SetDirty(moeda);

            // Objetos "Static" não devem se mover
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0)
                {
                    Undo.RecordObject(t.gameObject, "Preparar Moeda");
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                }
            }

            EditorSceneManager.MarkSceneDirty(go.scene);
            preparadas++;
        }

        if (preparadas == 0)
        {
            EditorUtility.DisplayDialog(
                "Preparar Moedas",
                "Selecione a(s) moeda(s) na janela Hierarchy (dentro da cena) e clique de novo.",
                "OK");
            return;
        }

        if (som == null)
            Debug.LogWarning("Som não encontrado em " + CaminhoSom + ". Arraste um som no campo Som Coleta da moeda.");

        Debug.Log(preparadas + " moeda(s) preparada(s). Salve a cena (Ctrl+S) e aperte Play.");
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu:  ISLAND > Preparar Itens de Cura Selecionados
/// Adiciona o script ItemCura e o som nos objetos selecionados na Hierarchy.
/// </summary>
public static class PrepararItensCura
{
    const string CaminhoSom = "Assets/SONGS/POINTSOUND.mp3";

    [MenuItem("ISLAND/Preparar Itens de Cura Selecionados")]
    static void Preparar()
    {
        var som = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoSom);
        int preparados = 0;

        foreach (var go in Selection.gameObjects)
        {
            if (EditorUtility.IsPersistent(go)) continue; // só objetos da cena

            var item = go.GetComponent<ItemCura>();
            if (item == null) item = Undo.AddComponent<ItemCura>(go);

            Undo.RecordObject(item, "Preparar Item de Cura");
            if (item.somColeta == null) item.somColeta = som;
            EditorUtility.SetDirty(item);

            // Objetos "Static" não devem se mover
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0)
                {
                    Undo.RecordObject(t.gameObject, "Preparar Item de Cura");
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                }
            }

            EditorSceneManager.MarkSceneDirty(go.scene);
            preparados++;
        }

        if (preparados == 0)
        {
            EditorUtility.DisplayDialog(
                "Preparar Itens de Cura",
                "Selecione o(s) objeto(s) na janela Hierarchy (dentro da cena) e clique de novo.",
                "OK");
            return;
        }

        if (som == null)
            Debug.LogWarning("Som não encontrado em " + CaminhoSom + ". Arraste um som no campo Som Coleta do item.");

        Debug.Log(preparados + " item(ns) de cura preparado(s). Salve a cena (Ctrl+S) e aperte Play.");
    }
}
#endif

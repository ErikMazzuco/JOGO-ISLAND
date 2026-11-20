using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ISLAND > Ligar Som de Morte do Esqueleto
// Coloca o "MORTE INIMIGO.mp3" no campo "Som Morte" de todos os esqueletos (prefabs e cena aberta).
public static class LigarSomMorteInimigo
{
    const string CaminhoSom = "Assets/SONGS/INIMIGOS/MORTE INIMIGO.mp3";

    [MenuItem("ISLAND/Ligar Som de Morte do Esqueleto")]
    static void Ligar()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoSom);
        if (clip == null)
        {
            EditorUtility.DisplayDialog("Som não encontrado",
                "Não achei o arquivo:\n" + CaminhoSom + "\n\nConfira o nome e a pasta do som.", "OK");
            return;
        }

        int prefabs = 0, cena = 0;

        // 1) Prefabs (é de onde o spawner cria os esqueletos)
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            if (!caminho.StartsWith("Assets/")) continue;

            var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (raiz == null) continue;

            foreach (var vida in raiz.GetComponentsInChildren<VidaSkeleton>(true))
            {
                if (Definir(vida, clip)) prefabs++;
            }
        }

        // 2) Esqueletos que já estão na cena aberta
        foreach (var vida in Object.FindObjectsByType<VidaSkeleton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (Definir(vida, clip)) cena++;
        }

        if (cena > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Som de morte ligado",
            "Prefabs atualizados: " + prefabs + "\nEsqueletos na cena: " + cena +
            "\n\nSalve a cena (Ctrl+S).", "OK");
    }

    static bool Definir(VidaSkeleton vida, AudioClip clip)
    {
        var so = new SerializedObject(vida);
        var prop = so.FindProperty("somMorte");
        if (prop == null) return false;

        prop.objectReferenceValue = clip;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(vida);
        return true;
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ISLAND > Ligar Sons de Combate
// Coloca os sons de combate nos scripts (prefabs e cena aberta):
//   Esqueleto: Som Dano (ACERTO OSSO) e Som Morte (MORTE ESQUELETO)
//   Espada:    Som Espada (GOLPE ESPADA)
public static class LigarSonsCombate
{
    const string SomGolpe = "Assets/SONGS/PLAYER/GOLPE ESPADA.wav";
    const string SomAcerto = "Assets/SONGS/INIMIGOS/ACERTO OSSO.wav";
    const string SomMorte = "Assets/SONGS/INIMIGOS/MORTE ESQUELETO.wav";

    [MenuItem("ISLAND/Ligar Sons de Combate")]
    static void Ligar()
    {
        AssetDatabase.Refresh();

        var golpe = AssetDatabase.LoadAssetAtPath<AudioClip>(SomGolpe);
        var acerto = AssetDatabase.LoadAssetAtPath<AudioClip>(SomAcerto);
        var morte = AssetDatabase.LoadAssetAtPath<AudioClip>(SomMorte);

        if (golpe == null || acerto == null || morte == null)
        {
            EditorUtility.DisplayDialog("Sons não encontrados",
                "Não achei algum destes arquivos:\n" + SomGolpe + "\n" + SomAcerto + "\n" + SomMorte, "OK");
            return;
        }

        int esqueletos = 0, espadas = 0;

        // 1) Prefabs (é de onde o spawner cria os esqueletos)
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            if (!caminho.StartsWith("Assets/")) continue;

            var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (raiz == null) continue;

            foreach (var vida in raiz.GetComponentsInChildren<VidaSkeleton>(true))
                if (DefinirEsqueleto(vida, acerto, morte)) esqueletos++;

            foreach (var espada in raiz.GetComponentsInChildren<AtaqueEspada>(true))
                if (DefinirEspada(espada, golpe)) espadas++;
        }

        // 2) O que já está na cena aberta
        bool cena = false;
        foreach (var vida in Object.FindObjectsByType<VidaSkeleton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (DefinirEsqueleto(vida, acerto, morte)) { esqueletos++; cena = true; }

        foreach (var espada in Object.FindObjectsByType<AtaqueEspada>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (DefinirEspada(espada, golpe)) { espadas++; cena = true; }

        if (cena) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Sons de combate ligados",
            "Esqueletos atualizados: " + esqueletos + "\nEspadas atualizadas: " + espadas +
            "\n\nSalve a cena (Ctrl+S).", "OK");
    }

    static bool DefinirEsqueleto(VidaSkeleton vida, AudioClip acerto, AudioClip morte)
    {
        var so = new SerializedObject(vida);
        var dano = so.FindProperty("somDano");
        var mor = so.FindProperty("somMorte");
        if (dano == null || mor == null) return false;

        dano.objectReferenceValue = acerto;
        mor.objectReferenceValue = morte;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(vida);
        return true;
    }

    static bool DefinirEspada(AtaqueEspada espada, AudioClip golpe)
    {
        var so = new SerializedObject(espada);
        var prop = so.FindProperty("somEspada");
        if (prop == null) return false;

        prop.objectReferenceValue = golpe;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(espada);
        return true;
    }
}

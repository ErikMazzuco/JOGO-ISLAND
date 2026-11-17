#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu  ISLAND > Ligar Sons do Jogador
/// Coloca os sons de dano e de morte no VidaJogador do jogador na cena.
/// </summary>
public static class LigarSonsJogador
{
    const string CaminhoDano = "Assets/SONGS/PLAYER/DANO PLAYER.mp3";
    const string CaminhoMorte = "Assets/SONGS/PLAYER/MORTE.mp3";

    [MenuItem("ISLAND/Ligar Sons do Jogador")]
    static void Ligar()
    {
        var vida = Object.FindFirstObjectByType<VidaJogador>();

        if (vida == null)
        {
            EditorUtility.DisplayDialog(
                "Sons do Jogador",
                "Não achei o jogador (VidaJogador) na cena. Abra a cena Island e tente de novo.",
                "OK");
            return;
        }

        var dano = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoDano);
        var morte = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoMorte);

        if (dano == null) Debug.LogWarning("Som não encontrado: " + CaminhoDano);
        if (morte == null) Debug.LogWarning("Som não encontrado: " + CaminhoMorte);

        Undo.RecordObject(vida, "Ligar Sons do Jogador");
        vida.somDano = dano;
        vida.somMorte = morte;
        EditorUtility.SetDirty(vida);

        EditorSceneManager.MarkSceneDirty(vida.gameObject.scene);
        Selection.activeGameObject = vida.gameObject;

        Debug.Log("Sons de dano e morte ligados no jogador. Salve a cena (Ctrl+S).");
    }
}
#endif

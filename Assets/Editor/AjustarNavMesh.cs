#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu:  ISLAND > Inimigos > Ajustar NavMesh (incluir pisos dos templos)
///
/// O NavMesh Surface do TerrainGroup só pegava os filhos dele (o terreno). Dentro do Templo_Yas o único
/// chão "andável" era o terreno embaixo da base do templo, então os esqueletos andavam afundados.
/// Esta ferramenta muda o Surface para:
///   - Collect Objects = All Game Objects (entram os pisos dos templos, pontes, escadas...);
///   - Use Geometry   = Physics Colliders (só o que tem colisão: água e plantas ficam de fora).
/// Depois é só clicar em "Bake" no Inspector (o TerrainGroup fica selecionado).
/// </summary>
public static class AjustarNavMesh
{
    [MenuItem("ISLAND/Inimigos/Ajustar NavMesh (incluir pisos dos templos)")]
    static void Ajustar()
    {
        MonoBehaviour surface = null;
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (mb != null && mb.GetType().Name == "NavMeshSurface") { surface = mb; break; }

        if (surface == null)
        {
            EditorUtility.DisplayDialog("NavMesh", "Não achei nenhum NavMesh Surface na cena.", "OK");
            return;
        }

        var so = new SerializedObject(surface);
        var coletar = so.FindProperty("m_CollectObjects");
        var geometria = so.FindProperty("m_UseGeometry");
        if (coletar != null) coletar.intValue = 0;      // All Game Objects
        if (geometria != null) geometria.intValue = 1;  // Physics Colliders
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);

        Selection.activeGameObject = surface.gameObject;
        EditorGUIUtility.PingObject(surface.gameObject);
        EditorUtility.DisplayDialog("NavMesh",
            "NavMesh Surface de \"" + surface.name + "\" ajustado:\n" +
            "• Collect Objects: All Game Objects\n• Use Geometry: Physics Colliders\n\n" +
            "Agora clique em  Bake  no Inspector (pode levar alguns minutos).\n" +
            "Depois rode  ISLAND > Inimigos > Colocar Esqueletos nos Templos  e salve a cena.", "OK");
    }
}
#endif

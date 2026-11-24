#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Menu:  ISLAND > Objetivo > Preparar Objetivo e Rei Esqueleto
///        ISLAND > Objetivo > Derrotar Rei Agora (no Play)
///        ISLAND > Objetivo > Abrir Todos os Baús (no Play)
///
/// Monta o objetivo do jogo:
///   - OBJETIVO_BAUS: contador "Baús X/Y" e tela de vitória ao abrir todos;
///   - CHEFAO_REI_ESQUELETO: ponto no 2º andar do Templo_Yas onde o Rei Esqueleto surge quando a noite cai.
///     Ao morrer ele solta o último baú.
///   - O baú "Tesouro Principal" vira um baú comum (o último tesouro agora é o do chefão).
/// Ctrl+Z desfaz. Rodar de novo refaz.
/// </summary>
public static class PrepararChefao
{
    const string CaminhoEsqueleto = "Assets/INIMIGO.prefab";
    const string CaminhoBau = "Assets/IMPORTS/FS Loot Boxes/Assets/Prefabs/TreasureChestPrefab.prefab";
    const string CaminhoRugido = "Assets/SONGS/INIMIGOS/MONSTER SCREAM.mp3";
    const string CaminhoSomBau = "Assets/SONGS/COLETAVEIS/POINTSOUND.mp3";

    [MenuItem("ISLAND/Objetivo/Preparar Objetivo e Rei Esqueleto")]
    static void Preparar()
    {
        var templo = AcharNaCena.Objeto("templo");
        if (templo == null) { Aviso("Não achei o Templo_Yas (\"templo\") na cena aberta."); return; }

        Undo.SetCurrentGroupName("Preparar Objetivo");
        int undo = Undo.GetCurrentGroup();
        var cena = templo.gameObject.scene;

        foreach (var nome in new[] { "OBJETIVO_BAUS", "CHEFAO_REI_ESQUELETO" })
        {
            var velho = GameObject.Find(nome);
            if (velho != null) Undo.DestroyObjectImmediate(velho);
        }

        // ---------- Objetivo ----------
        var obj = new GameObject("OBJETIVO_BAUS");
        if (obj.scene != cena) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, cena);
        Undo.RegisterCreatedObjectUndo(obj, "Preparar Objetivo");
        obj.AddComponent<ObjetivoBaus>();

        // ---------- Ponto do chefão ----------
        Vector3 ponto; string onde;
        if (!AcharPontoNo2Andar(templo, out ponto)) { ponto = templo.position + Vector3.up * 9f; onde = "(não achei um lugar livre no 2º andar; ajuste a posição à mão)"; }
        else onde = "no 2º andar do Templo_Yas";

        var chefe = new GameObject("CHEFAO_REI_ESQUELETO");
        if (chefe.scene != cena) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chefe, cena);
        Undo.RegisterCreatedObjectUndo(chefe, "Preparar Objetivo");
        chefe.transform.position = ponto;
        var escada = GameObject.Find("ESCADA_2_ANDAR");
        if (escada != null)
        {
            Vector3 olhar = escada.transform.position - ponto; olhar.y = 0f;   // olhando para quem sobe a escada
            if (olhar.sqrMagnitude > 0.01f) chefe.transform.rotation = Quaternion.LookRotation(olhar);
        }
        var c = chefe.AddComponent<ChefaoReiEsqueleto>();
        c.prefabEsqueleto = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoEsqueleto);
        c.prefabBau = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoBau);
        c.somRugido = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoRugido);
        c.somBau = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoSomBau);

        // ---------- Baús que já existem: nenhum é mais "o tesouro final" ----------
        int baus = 0;
        foreach (var b in Object.FindObjectsByType<BauTesouro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            baus++;
            if (!b.tesouroPrincipal) continue;
            Undo.RecordObject(b, "Preparar Objetivo");
            b.tesouroPrincipal = false;
            EditorUtility.SetDirty(b);
        }

        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(cena);
        Selection.activeGameObject = chefe;

        string faltando = (c.prefabEsqueleto == null ? "\n⚠ Não achei " + CaminhoEsqueleto : "") +
                          (c.prefabBau == null ? "\n⚠ Não achei " + CaminhoBau : "");
        Aviso("Pronto!\n\n• Objetivo: abrir " + (baus + 1) + " baús (" + baus + " na ilha + o do Rei Esqueleto).\n" +
              "• Rei Esqueleto " + onde + ": surge quando a noite cai (ao ler a carta do laguinho).\n" +
              "• Ao morrer ele solta o último baú. Abrindo todos: tela de VITÓRIA." + faltando +
              "\n\nPara testar no Play:  ISLAND > Noite > Anoitecer Agora  e depois  ISLAND > Objetivo > Derrotar Rei Agora.\n\nSalve a cena (Ctrl+S).");
    }

    [MenuItem("ISLAND/Objetivo/Derrotar Rei Agora (no Play)")]
    static void DerrotarAgora()
    {
        if (!EditorApplication.isPlaying) { Aviso("Aperte Play primeiro."); return; }
        var c = Object.FindFirstObjectByType<ChefaoReiEsqueleto>();
        if (c == null) { Aviso("Não há CHEFAO_REI_ESQUELETO na cena."); return; }
        c.DerrotarAgora();
    }

    [MenuItem("ISLAND/Objetivo/Abrir Todos os Baús (no Play)")]
    static void AbrirTodos()
    {
        if (!EditorApplication.isPlaying) { Aviso("Aperte Play primeiro."); return; }
        foreach (var b in Object.FindObjectsByType<BauTesouro>(FindObjectsSortMode.None)) b.Abrir();
    }

    // Lugar livre no piso do 2º andar (plataformas dentro de "andar_2"), o mais longe possível da escada
    static bool AcharPontoNo2Andar(Transform templo, out Vector3 ponto)
    {
        ponto = Vector3.zero;
        Transform andar2 = null;
        foreach (var t in templo.GetComponentsInChildren<Transform>(true)) if (t.name == "andar_2") { andar2 = t; break; }
        if (andar2 == null) return false;

        var escada = GameObject.Find("ESCADA_2_ANDAR");
        Vector3 refEscada = escada != null ? escada.transform.position : templo.position;

        Physics.SyncTransforms();
        float melhor = -1f;
        foreach (var r in andar2.GetComponentsInChildren<MeshRenderer>())
        {
            Bounds b = r.bounds;
            for (int i = -1; i <= 1; i++)
            for (int k = -1; k <= 1; k++)
            {
                Vector3 topo = new Vector3(b.center.x + i * b.extents.x * 0.5f, b.max.y + 1.5f, b.center.z + k * b.extents.z * 0.5f);
                RaycastHit h;
                if (!Physics.Raycast(topo, Vector3.down, out h, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!h.collider.transform.IsChildOf(andar2) || h.normal.y < 0.8f) continue;

                NavMeshHit nh;
                if (!NavMesh.SamplePosition(h.point, out nh, 0.6f, NavMesh.AllAreas) || Mathf.Abs(nh.position.y - h.point.y) > 0.4f) continue;
                // espaço para um esqueleto 2x maior
                if (Physics.CheckCapsule(h.point + Vector3.up * 1.3f, h.point + Vector3.up * 3.6f, 1.0f, ~0, QueryTriggerInteraction.Ignore)) continue;

                float d = Vector3.Distance(new Vector3(h.point.x, 0, h.point.z), new Vector3(refEscada.x, 0, refEscada.z));
                if (d > melhor) { melhor = d; ponto = nh.position; }
            }
        }
        return melhor >= 0f;
    }

    static void Aviso(string msg) { EditorUtility.DisplayDialog("Objetivo e Rei Esqueleto", msg, "OK"); }
}
#endif

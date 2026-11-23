#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Menu:  ISLAND > Inimigos > Colocar Esqueletos nos Templos
///        ISLAND > Inimigos > Remover Esqueletos dos Templos
///
/// Espalha esqueletos (INIMIGO.prefab) DENTRO dos templos, nos dois andares, em lugares com teto,
/// chão do templo e NavMesh (para eles conseguirem andar). Também aumenta a quantidade de esqueletos
/// que nascem pelo mapa (SpawnInimigos).
/// Tudo vai para "ESQUELETOS_TEMPLOS". Rodar de novo refaz do zero. Ctrl+Z desfaz.
/// </summary>
public static class ColocarEsqueletos
{
    const string CaminhoPrefab = "Assets/INIMIGO.prefab";
    const string NomeGrupo = "ESQUELETOS_TEMPLOS";
    const int EsqueletosNoMapa = 25;          // SpawnInimigos (pelo mapa todo)
    const float DistanciaEntreEles = 4f;
    const float DistanciaDoJogador = 15f;

    struct Templo
    {
        public string objeto; public int quantidade;
        public Templo(string objeto, int quantidade) { this.objeto = objeto; this.quantidade = quantidade; }
    }

    static readonly Templo[] Templos =
    {
        new Templo("templo",      12),   // Templo_Yas (2 andares)
        new Templo("TEMPLO 3",     7),
        new Templo("TEMPLO_SALA",  6),
        new Templo("tower_short",  3),
    };

    [MenuItem("ISLAND/Inimigos/Colocar Esqueletos nos Templos")]
    static void Colocar()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoPrefab);
        if (prefab == null) { EditorUtility.DisplayDialog("Esqueletos", "Não achei " + CaminhoPrefab, "OK"); return; }

        Undo.SetCurrentGroupName("Colocar Esqueletos");
        int undo = Undo.GetCurrentGroup();

        var antigo = AcharGrupo();
        if (antigo != null) Undo.DestroyObjectImmediate(antigo);

        var cena = EditorSceneManager.GetActiveScene();
        var grupo = new GameObject(NomeGrupo);
        Undo.RegisterCreatedObjectUndo(grupo, "Colocar Esqueletos");

        Vector3? jogador = null;
        var mov = Object.FindFirstObjectByType<PlayerMovement>();
        if (mov != null) jogador = mov.transform.position;

        var rnd = new System.Random(4321);
        var log = new StringBuilder();
        int total = 0;

        Physics.SyncTransforms();
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        try
        {
            foreach (var t in Templos)
            {
                var alvo = Achar(t.objeto);
                if (alvo == null) { log.AppendLine("✗ " + t.objeto + ": não encontrado"); continue; }

                var pontos = PontosDentro(alvo);
                Embaralhar(pontos, rnd);

                var sub = new GameObject("Esqueletos - " + t.objeto);
                Undo.RegisterCreatedObjectUndo(sub, "Colocar Esqueletos");
                sub.transform.SetParent(grupo.transform, false);

                var usados = new List<Vector3>();
                foreach (var p in pontos)
                {
                    if (usados.Count >= t.quantidade) break;
                    if (jogador.HasValue && Vector3.Distance(p, jogador.Value) < DistanciaDoJogador) continue;
                    bool perto = false;
                    foreach (var u in usados) if (Vector3.Distance(u, p) < DistanciaEntreEles) { perto = true; break; }
                    if (perto) continue;

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cena);
                    Undo.RegisterCreatedObjectUndo(go, "Colocar Esqueletos");
                    go.transform.SetParent(sub.transform, true);
                    go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f));
                    go.name = "Esqueleto " + (usados.Count + 1);
                    usados.Add(p);
                }

                total += usados.Count;
                log.AppendLine((usados.Count > 0 ? "✓ " : "✗ ") + t.objeto + ": " + usados.Count + " de " + t.quantidade +
                               (usados.Count < t.quantidade ? "  (pouco chão com NavMesh lá dentro)" : ""));
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = backfaces;
        }

        // Mais esqueletos pelo mapa
        foreach (var s in Object.FindObjectsByType<SpawnInimigos>(FindObjectsSortMode.None))
        {
            if (s.quantidade >= EsqueletosNoMapa) continue;
            Undo.RecordObject(s, "Colocar Esqueletos");
            log.AppendLine("✓ Pelo mapa (" + s.name + "): " + s.quantidade + " → " + EsqueletosNoMapa);
            s.quantidade = EsqueletosNoMapa;
            EditorUtility.SetDirty(s);
        }

        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(cena);
        Selection.activeGameObject = grupo;

        Debug.Log("Esqueletos nos templos: " + total + "\n" + log);
        EditorUtility.DisplayDialog("Colocar Esqueletos",
            total + " esqueleto(s) colocados dentro dos templos.\n\n" + log +
            "\nSe algum templo ficou com poucos, o NavMesh não cobre o chão de lá: selecione o objeto com NavMeshSurface (TerrainGroup) e clique em Bake, depois rode de novo.\n\n" +
            "Salve a cena (Ctrl+S). Ctrl+Z desfaz.", "OK");
    }

    [MenuItem("ISLAND/Inimigos/Remover Esqueletos dos Templos")]
    static void Remover()
    {
        var antigo = AcharGrupo();
        if (antigo == null) { EditorUtility.DisplayDialog("Esqueletos", "Não há esqueletos colocados por esta ferramenta.", "OK"); return; }
        var cena = antigo.scene;
        Undo.DestroyObjectImmediate(antigo);
        EditorSceneManager.MarkSceneDirty(cena);
    }

    // Chão do templo, com teto em cima, espaço para ficar em pé e NavMesh no mesmo nível
    static List<Vector3> PontosDentro(Transform alvo)
    {
        var lista = new List<Vector3>();
        Bounds b = Limites(alvo);
        const float passo = 1.5f;

        for (float x = b.min.x; x <= b.max.x; x += passo)
        for (float z = b.min.z; z <= b.max.z; z += passo)
        {
            var hits = Physics.RaycastAll(new Vector3(x, b.max.y + 2f, z), Vector3.down, b.size.y + 10f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.normal.y < 0.8f || !h.collider.transform.IsChildOf(alvo)) continue;

                RaycastHit cima;
                if (!Physics.Raycast(h.point + Vector3.up * 0.05f, Vector3.up, out cima, 30f, ~0, QueryTriggerInteraction.Ignore)) continue; // sem teto
                if (cima.distance < 2.2f) continue;

                // longe de parede (não nascer grudado / dentro de parede)
                if (Physics.CheckCapsule(h.point + Vector3.up * 0.6f, h.point + Vector3.up * 1.7f, 0.5f, ~0, QueryTriggerInteraction.Ignore)) continue;

                NavMeshHit nh;
                if (!NavMesh.SamplePosition(h.point, out nh, 0.6f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(nh.position.y - h.point.y) > 0.4f) continue;

                lista.Add(nh.position);
            }
        }
        return lista;
    }

    static Transform Achar(string nome)
    {
        return AcharNaCena.Objeto(nome);
    }

    static GameObject AcharGrupo()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!EditorUtility.IsPersistent(t) && t.name == NomeGrupo && t.parent == null) return t.gameObject;
        return null;
    }

    static Bounds Limites(Transform alvo)
    {
        var b = new Bounds(alvo.position, Vector3.one * 10f);
        bool tem = false;
        foreach (var r in alvo.GetComponentsInChildren<Renderer>())
        {
            if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static void Embaralhar<T>(List<T> l, System.Random rnd)
    {
        for (int i = l.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; }
    }
}
#endif

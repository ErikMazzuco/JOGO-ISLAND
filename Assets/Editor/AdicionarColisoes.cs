#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu:  ISLAND > Colisões > Mostrar Objetos Sem Colisão
///        ISLAND > Colisões > Adicionar Colisão nos Objetos da Cena
///
/// Procura na cena aberta tudo que é desenhado (MeshRenderer) e não tem colisão,
/// e coloca um MeshCollider (do formato exato do modelo) nesses objetos.
/// Os prefabs originais NÃO são alterados: a colisão fica só na cena.
///
/// Fica de fora (continua sem colisão):
///  - coletáveis (moedas, itens de cura), jogador, inimigos e animais;
///  - água / mar;
///  - grama, flores, plantas, cogumelos e arbustos (dá para atravessar);
///  - objetos muito pequenos (garrafas, tigelas...);
///  - versões de longe das árvores (LOD1, LOD2...);
///  - interface (HUD, minimapa).
/// Dá para desfazer com Ctrl+Z.
/// </summary>
public static class AdicionarColisoes
{
    // Tamanho mínimo (maior lado, em metros) para ganhar colisão
    const float TamanhoMinimo = 0.4f;

    // Partes do nome (dele ou de algum pai) que indicam algo que NÃO deve ter colisão
    static readonly string[] NomesIgnorados =
    {
        // água
        "agua", "água", "water", "ocean", "mar", "sea",
        // vegetação baixa
        "grass", "grama", "flower", "flor", "plant", "planta", "mushroom", "cogumelo",
        "bush", "arbusto", "leaf", "leaves", "folha",
        // coletáveis / efeitos
        "coin", "moeda", "cura", "fx", "particle",
    };

    [MenuItem("ISLAND/Colisões/Mostrar Objetos Sem Colisão")]
    static void Mostrar()
    {
        var lista = Procurar();
        var sb = new StringBuilder();
        sb.AppendLine(lista.Count + " objeto(s) sem colisão que vão ganhar colisão:");
        foreach (var r in lista) sb.AppendLine("  " + Caminho(r.transform));
        Debug.Log(sb.ToString());

        Selection.objects = lista.ConvertAll(r => (Object)r.gameObject).ToArray();
        EditorUtility.DisplayDialog("Objetos sem colisão",
            lista.Count + " objeto(s) encontrados.\nEles foram selecionados na Hierarchy e a lista está no Console.",
            "OK");
    }

    [MenuItem("ISLAND/Colisões/Adicionar Colisão nos Objetos da Cena")]
    static void Adicionar()
    {
        var lista = Procurar();
        if (lista.Count == 0)
        {
            EditorUtility.DisplayDialog("Adicionar Colisão", "Nenhum objeto sem colisão encontrado. Está tudo certo!", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Adicionar Colisão nos Objetos");
        int grupo = Undo.GetCurrentGroup();

        var sb = new StringBuilder();
        foreach (var r in lista)
        {
            var mf = r.GetComponent<MeshFilter>();
            // Pode já existir um MeshCollider sem modelo: reaproveita
            var mc = r.GetComponent<MeshCollider>();
            if (mc == null) mc = Undo.AddComponent<MeshCollider>(r.gameObject);
            else Undo.RecordObject(mc, "Adicionar Colisão nos Objetos");
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
            mc.isTrigger = false;
            EditorUtility.SetDirty(mc);
            EditorSceneManager.MarkSceneDirty(r.gameObject.scene);
            sb.AppendLine("  " + Caminho(r.transform));
        }

        Undo.CollapseUndoOperations(grupo);

        Debug.Log("Colisão adicionada em " + lista.Count + " objeto(s):\n" + sb);
        EditorUtility.DisplayDialog("Adicionar Colisão",
            "Colisão adicionada em " + lista.Count + " objeto(s).\n\nSalve a cena (Ctrl+S) e aperte Play.\n(Ctrl+Z desfaz.)",
            "OK");
    }

    // ------------------------------------------------------------------------------------------

    static List<MeshRenderer> Procurar()
    {
        var resultado = new List<MeshRenderer>();
        var lodsDeLonge = RenderersDeLodDeLonge();

        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (EditorUtility.IsPersistent(r)) continue;          // só objetos da cena
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            // Já tem colisão do formato do modelo: ok
            var proprio = r.GetComponent<MeshCollider>();
            if (proprio != null && proprio.sharedMesh != null) continue;
            // Objeto desligado com alguma colisão: não dá para medir, deixa como está
            if (!r.gameObject.activeInHierarchy && r.GetComponent<Collider>() != null) continue;

            if (lodsDeLonge.Contains(r)) continue;
            if (r.GetComponent<RectTransform>() != null) continue; // interface
            if (EhIgnorado(r.transform)) continue;
            if (AguaPeloMaterial(r)) continue;

            var tamanho = r.bounds.size;
            if (r.gameObject.activeInHierarchy &&
                Mathf.Max(tamanho.x, Mathf.Max(tamanho.y, tamanho.z)) < TamanhoMinimo) continue;

            if (CobertoPorOutraColisao(r)) continue;

            resultado.Add(r);
        }

        return resultado;
    }

    static bool EhIgnorado(Transform t)
    {
        for (var atual = t; atual != null; atual = atual.parent)
        {
            if (atual.GetComponent<Moeda>() != null) return true;
            if (atual.GetComponent<ItemCura>() != null) return true;
            if (atual.GetComponent<PlayerMovement>() != null) return true;
            if (atual.GetComponent<CharacterController>() != null) return true;
            if (atual.GetComponent<InimigoSkeleton>() != null) return true;
            if (atual.GetComponent<VidaSkeleton>() != null) return true;
            if (atual.GetComponent<GalinhaAI>() != null) return true;
            if (atual.GetComponent<Canvas>() != null) return true;
            if (atual.GetComponent<ParticleSystem>() != null) return true;
            if (atual.GetComponent<Camera>() != null) return true;

            if (NomeIgnorado(atual.name)) return true;
        }
        return false;
    }

    static bool NomeIgnorado(string nome)
    {
        string n = nome.ToLowerInvariant();
        foreach (var palavra in NomesIgnorados)
        {
            // "mar" e "sea" só como palavra inteira (para não pegar "Marcador", "Season"...)
            if (palavra == "mar" || palavra == "sea" || palavra == "fx")
            {
                if (TemPalavraInteira(n, palavra)) return true;
            }
            else if (n.Contains(palavra)) return true;
        }
        return false;
    }

    static bool TemPalavraInteira(string texto, string palavra)
    {
        int i = 0;
        while ((i = texto.IndexOf(palavra, i, System.StringComparison.Ordinal)) >= 0)
        {
            bool inicioOk = i == 0 || !char.IsLetter(texto[i - 1]);
            int fim = i + palavra.Length;
            bool fimOk = fim >= texto.Length || !char.IsLetter(texto[fim]);
            if (inicioOk && fimOk) return true;
            i = fim;
        }
        return false;
    }

    static bool AguaPeloMaterial(MeshRenderer r)
    {
        foreach (var m in r.sharedMaterials)
        {
            if (m == null) continue;
            string nome = m.name.ToLowerInvariant();
            string shader = m.shader != null ? m.shader.name.ToLowerInvariant() : "";
            if (nome.Contains("water") || nome.Contains("agua") || nome.Contains("ocean") ||
                shader.Contains("water") || shader.Contains("ocean"))
                return true;
        }
        return false;
    }

    // Renderers que só aparecem de longe (LOD1, LOD2...): a colisão fica só no LOD0
    static HashSet<Renderer> RenderersDeLodDeLonge()
    {
        var set = new HashSet<Renderer>();
        foreach (var g in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var lods = g.GetLODs();
            var lod0 = new HashSet<Renderer>();
            if (lods.Length > 0)
                foreach (var r in lods[0].renderers) if (r != null) lod0.Add(r);

            for (int i = 1; i < lods.Length; i++)
                foreach (var r in lods[i].renderers)
                    if (r != null && !lod0.Contains(r)) set.Add(r);
        }
        return set;
    }

    // A colisão pode estar no próprio objeto (ex.: uma caixa) ou num pai/filho/irmão.
    // Se já existe uma colisão sólida cobrindo quase todo o objeto, não precisa de outra.
    // Se ela cobre só um pedaço (ex.: a torre com uma caixa só na plataforma), ganha a colisão completa.
    static bool CobertoPorOutraColisao(MeshRenderer r)
    {
        if (!r.gameObject.activeInHierarchy) return false;

        var raiz = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject);
        Transform busca = raiz != null ? raiz.transform : (r.transform.parent != null ? r.transform.parent : r.transform);

        Bounds b = r.bounds;
        float volume = Volume(b.size);
        if (volume <= 0f) return false;

        foreach (var c in busca.GetComponentsInChildren<Collider>(false))
        {
            if (c.isTrigger || !c.enabled) continue;
            if (c is MeshCollider mc && mc.sharedMesh == null) continue;

            Bounds cb = c.bounds;
            if (!cb.Intersects(b)) continue;

            Vector3 min = Vector3.Max(b.min, cb.min);
            Vector3 max = Vector3.Min(b.max, cb.max);
            float sobreposto = Volume(max - min);
            if (sobreposto / volume >= 0.8f) return true;
        }
        return false;
    }

    static float Volume(Vector3 s)
    {
        // objetos chatos (paredes finas, pisos) não podem dar volume zero
        return Mathf.Max(s.x, 0.01f) * Mathf.Max(s.y, 0.01f) * Mathf.Max(s.z, 0.01f);
    }

    static string Caminho(Transform t)
    {
        string c = t.name;
        while (t.parent != null) { t = t.parent; c = t.name + "/" + c; }
        return c;
    }
}
#endif

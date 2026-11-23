#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu:  ISLAND > Baús > Colocar Baús nos Templos
///        ISLAND > Baús > Colocar Baú Onde a Scene View Está Olhando
///        ISLAND > Baús > Converter Materiais do Baú para URP
///
/// Coloca o baú do pacote "FS Loot Boxes" em lugares do mapa:
///   - PONTO_TESOURO  → baú principal (tesouro final)
///   - templo e TEMPLO 3 → dentro de cada templo
///   - TOWER          → lá em cima da torre
///   - PRAINHA        → na praia
/// O baú é "jogado" de cima até encostar no chão, procurando um espaço livre.
/// Todos ficam dentro do objeto "BAUS" na Hierarchy, então dá para arrastar depois.
/// Rodar de novo não duplica: lugares que já têm baú são pulados.
/// Dá para desfazer com Ctrl+Z.
/// </summary>
public static class ColocarBaus
{
    const string CaminhoPrefab = "Assets/FS Loot Boxes/Assets/Prefabs/TreasureChestPrefab.prefab";
    const string CaminhoSom = "Assets/SONGS/COLETAVEIS/POINTSOUND.mp3";
    const string NomePai = "BAUS";

    // Tamanho do baú (metade), um pouquinho folgado
    static readonly Vector3 MeioBau = new Vector3(0.65f, 0.4f, 0.55f);
    const float AlturaLivre = 1.9f;   // espaço acima do baú para o jogador chegar

    enum Modo { Exato, Dentro, Topo, Chao }

    struct Lugar
    {
        public string objeto;      // nome do objeto na cena
        public string nome;        // nome do baú
        public Modo modo;
        public int moedas;
        public float cura;
        public bool principal;
        public Lugar(string objeto, string nome, Modo modo, int moedas, float cura, bool principal)
        { this.objeto = objeto; this.nome = nome; this.modo = modo; this.moedas = moedas; this.cura = cura; this.principal = principal; }
    }

    static readonly Lugar[] Lugares =
    {
        new Lugar("PONTO_TESOURO", "Bau - Tesouro Principal", Modo.Exato,  50, 100f, true),
        new Lugar("templo",        "Bau - Templo",            Modo.Dentro, 10,   0f, false),
        new Lugar("TEMPLO 3",      "Bau - Templo 3",          Modo.Dentro, 10,   0f, false),
        new Lugar("tower_short",   "Bau - Torre",             Modo.Topo,   15,  50f, false),
        new Lugar("PRAINHA",       "Bau - Prainha",           Modo.Chao,    5,  25f, false),
    };

    // ------------------------------------------------------------------------------------------

    [MenuItem("ISLAND/Baús/Colocar Baús nos Templos")]
    static void ColocarNosTemplos()
    {
        var prefab = CarregarPrefab();
        if (prefab == null) return;
        ConverterMateriais(prefab, false);

        var som = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoSom);
        Physics.SyncTransforms();

        Undo.SetCurrentGroupName("Colocar Baús");
        int grupo = Undo.GetCurrentGroup();

        Transform pai = null;
        var log = new StringBuilder();
        int colocados = 0;

        foreach (var lugar in Lugares)
        {
            var alvo = Achar(lugar.objeto);
            if (alvo == null) { log.AppendLine("✗ " + lugar.objeto + ": objeto não encontrado na cena"); continue; }

            if (pai == null) pai = PegarPai(alvo.gameObject.scene);
            if (pai.Find(lugar.nome) != null) { log.AppendLine("• " + lugar.nome + ": já existe, pulado"); continue; }

            Vector3 pos; Quaternion rot;
            if (!AcharPosicao(alvo, lugar.modo, out pos, out rot))
            {
                log.AppendLine("✗ " + lugar.nome + ": não achei um chão livre em " + lugar.objeto + " (use a opção da Scene View)");
                continue;
            }

            Criar(prefab, pai, lugar.nome, pos, rot, lugar.moedas, lugar.cura, lugar.principal, som);
            log.AppendLine("✓ " + lugar.nome + " em " + pos.ToString("F1"));
            colocados++;
        }

        Undo.CollapseUndoOperations(grupo);
        Debug.Log("Baús:\n" + log);

        EditorUtility.DisplayDialog("Colocar Baús",
            colocados + " baú(s) colocado(s).\n\n" + log +
            "\nConfira cada um (eles estão em BAUS na Hierarchy), salve a cena (Ctrl+S) e aperte Play.\n(Ctrl+Z desfaz.)",
            "OK");
    }

    [MenuItem("ISLAND/Baús/Colocar Baú Onde a Scene View Está Olhando")]
    static void ColocarNaMira()
    {
        var prefab = CarregarPrefab();
        if (prefab == null) return;
        ConverterMateriais(prefab, false);

        var sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.camera == null)
        {
            EditorUtility.DisplayDialog("Colocar Baú", "Abra a janela Scene e olhe para onde quer o baú.", "OK");
            return;
        }

        Physics.SyncTransforms();
        var cam = sv.camera.transform;
        RaycastHit hit;
        if (!Physics.Raycast(cam.position, cam.forward, out hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
        {
            EditorUtility.DisplayDialog("Colocar Baú", "Não achei chão no centro da Scene View. Olhe para um chão com colisão.", "OK");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        var pai = PegarPai(scene);

        // Vira o baú de frente para quem está olhando
        Vector3 frente = -cam.forward; frente.y = 0f;
        var rot = frente.sqrMagnitude > 0.001f ? Quaternion.LookRotation(frente.normalized) : Quaternion.identity;

        int n = 1;
        while (pai.Find("Bau - Extra " + n) != null) n++;

        var go = Criar(prefab, pai, "Bau - Extra " + n, hit.point, rot, 5, 0f, false,
                       AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoSom));
        Selection.activeGameObject = go;
        Debug.Log("Baú colocado em " + hit.point.ToString("F1") + ". Salve a cena (Ctrl+S).");
    }

    [MenuItem("ISLAND/Baús/Converter Materiais do Baú para URP")]
    static void ConverterMenu()
    {
        var prefab = CarregarPrefab();
        if (prefab == null) return;
        int n = ConverterMateriais(prefab, true);
        EditorUtility.DisplayDialog("Materiais do Baú",
            n > 0 ? n + " material(is) convertido(s) para URP." : "Os materiais já estavam em URP.", "OK");
    }

    // ------------------------------------------------------------------------------------------

    static GameObject CarregarPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoPrefab);
        if (prefab == null)
            EditorUtility.DisplayDialog("Baú não encontrado",
                "Não achei o prefab em:\n" + CaminhoPrefab + "\n\nImporte o pacote \"Animated Cartoon Treasure Chest\".", "OK");
        return prefab;
    }

    static Transform PegarPai(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var raiz in scene.GetRootGameObjects())
            if (raiz.name == NomePai) return raiz.transform;

        var go = new GameObject(NomePai);
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Colocar Baús");
        return go.transform;
    }

    // Se houver mais de um objeto com o mesmo nome, fica com o mais "de cima" na Hierarchy
    static Transform Achar(string nome)
    {
        return AcharNaCena.Objeto(nome);
    }

    static GameObject Criar(GameObject prefab, Transform pai, string nome, Vector3 pos, Quaternion rot,
                            int moedas, float cura, bool principal, AudioClip som)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(go, "Colocar Baús");
        go.name = nome;
        go.transform.SetParent(pai, true);
        go.transform.SetPositionAndRotation(pos, rot);

        // Um baú parado (sem pular) combina mais com templo; o principal pula para chamar atenção
        var loot = go.GetComponent<LootBox>();
        if (loot != null)
        {
            loot.bouncingBox = principal;
            loot.openingMethod = OpeningMethods.OpenOnKeyPress;
            loot.keyCode = KeyCode.None;
            EditorUtility.SetDirty(loot);
        }

        var bau = go.GetComponent<BauTesouro>();
        if (bau == null) bau = go.AddComponent<BauTesouro>();
        bau.moedas = moedas;
        bau.cura = cura;
        bau.tesouroPrincipal = principal;
        if (bau.somAbrir == null) bau.somAbrir = som;
        EditorUtility.SetDirty(bau);

        // Sem "Static": o baú tem animação
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        EditorSceneManager.MarkSceneDirty(go.scene);
        return go;
    }

    // ---------- Procurar um chão livre ----------

    static bool AcharPosicao(Transform alvo, Modo modo, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero; rot = Quaternion.identity;

        bool backfacesAntes = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;   // para enxergar tetos feitos de uma face só
        try
        {
            if (modo == Modo.Exato)
            {
                // Desce do ponto até o chão
                RaycastHit hit;
                Vector3 origem = alvo.position + Vector3.up * 1.0f;
                if (Physics.Raycast(origem, Vector3.down, out hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                    pos = hit.point;
                else
                    pos = alvo.position;

                rot = Quaternion.Euler(0f, alvo.eulerAngles.y, 0f);
                if (Mathf.Approximately(alvo.eulerAngles.y, 0f)) rot = VirarParaOAberto(pos);
                return true;
            }

            Bounds b;
            if (!Limites(alvo, out b)) return false;

            var candidatos = new List<Vector3>();
            const int N = 13;
            for (int ix = 0; ix < N; ix++)
            for (int iz = 0; iz < N; iz++)
            {
                // só a parte de dentro (80%) da área
                float fx = Mathf.Lerp(0.1f, 0.9f, ix / (float)(N - 1));
                float fz = Mathf.Lerp(0.1f, 0.9f, iz / (float)(N - 1));
                Vector3 topo = new Vector3(Mathf.Lerp(b.min.x, b.max.x, fx), b.max.y + 2f, Mathf.Lerp(b.min.z, b.max.z, fz));

                var hits = Physics.RaycastAll(topo, Vector3.down, b.size.y + 30f, ~0, QueryTriggerInteraction.Ignore);

                // No chão aberto vale só a superfície mais baixa (senão pegaria topo de pedra / copa de árvore)
                if (modo == Modo.Chao && hits.Length > 1)
                {
                    var maisBaixo = hits[0];
                    foreach (var h in hits) if (h.point.y < maisBaixo.point.y) maisBaixo = h;
                    hits = new[] { maisBaixo };
                }

                foreach (var h in hits)
                {
                    if (h.normal.y < 0.75f) continue;                 // não é chão (parede, rampa íngreme)
                    if (!EspacoLivre(h.point)) continue;               // não cabe o baú / o jogador
                    bool coberto = TemTeto(h.point);
                    if (modo == Modo.Dentro && !coberto) continue;     // dentro = tem teto em cima
                    if (modo == Modo.Dentro && h.collider is TerrainCollider) continue;
                    if (modo == Modo.Topo && !Pertence(h.collider, alvo)) continue;
                    candidatos.Add(h.point);
                }
            }

            if (candidatos.Count == 0) return false;

            Vector3 centro = b.center;
            Vector3 melhor = candidatos[0];
            float melhorNota = float.MinValue;
            foreach (var c in candidatos)
            {
                float distCentro = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(centro.x, centro.z));
                float nota;
                switch (modo)
                {
                    case Modo.Topo: nota = c.y * 10f - distCentro; break;     // o mais alto
                    case Modo.Dentro: nota = -distCentro - c.y * 0.5f; break; // perto do meio, no andar de baixo
                    default: nota = -distCentro; break;                       // perto do meio
                }
                if (nota > melhorNota) { melhorNota = nota; melhor = c; }
            }

            pos = melhor;
            rot = VirarParaOAberto(pos);
            return true;
        }
        finally
        {
            Physics.queriesHitBackfaces = backfacesAntes;
        }
    }

    static bool Limites(Transform alvo, out Bounds b)
    {
        b = new Bounds();
        bool tem = false;
        foreach (var r in alvo.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
        }
        if (!tem)
        {
            b = new Bounds(alvo.position, new Vector3(10f, 4f, 10f));
            tem = true;
        }
        return tem;
    }

    static bool EspacoLivre(Vector3 chao)
    {
        // o baú cabe?
        Vector3 centroBau = chao + Vector3.up * (MeioBau.y + 0.08f);
        if (Physics.CheckBox(centroBau, MeioBau, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return false;

        // dá para o jogador ficar em pé na frente (pelo menos em algum lado)?
        if (Physics.Raycast(chao + Vector3.up * 0.1f, Vector3.up, AlturaLivre, ~0, QueryTriggerInteraction.Ignore)) return false;
        return true;
    }

    static bool TemTeto(Vector3 chao)
    {
        return Physics.Raycast(chao + Vector3.up * 0.1f, Vector3.up, 25f, ~0, QueryTriggerInteraction.Ignore);
    }

    static bool Pertence(Collider c, Transform alvo)
    {
        return c != null && c.transform.IsChildOf(alvo);
    }

    // Vira a frente do baú para o lado com mais espaço livre (normalmente a sala / a porta)
    static Quaternion VirarParaOAberto(Vector3 chao)
    {
        Vector3 origem = chao + Vector3.up * 0.6f;
        Vector3 melhorDir = Vector3.forward;
        float melhorDist = -1f;
        for (int i = 0; i < 16; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
            RaycastHit hit;
            float d = Physics.Raycast(origem, dir, out hit, 30f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 30f;
            if (d > melhorDist) { melhorDist = d; melhorDir = dir; }
        }
        return Quaternion.LookRotation(melhorDir);
    }

    // ---------- Materiais (o pacote é de 2019 e usa o shader Standard, que fica rosa no URP) ----------

    internal static int ConverterMateriais(GameObject prefab, bool avisar)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            if (avisar) Debug.LogWarning("Shader URP/Lit não encontrado.");
            return 0;
        }

        int convertidos = 0;
        var vistos = new HashSet<Material>();
        foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || !vistos.Add(m)) continue;
                if (m.shader == lit) continue;
                string nomeShader = m.shader != null ? m.shader.name : "";
                if (!nomeShader.StartsWith("Standard") && !nomeShader.StartsWith("Legacy") &&
                    !nomeShader.Contains("InternalErrorShader")) continue;

                Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                Color cor = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                float suave = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.3f;
                float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;

                Undo.RecordObject(m, "Converter Materiais do Baú");
                m.shader = lit;
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", cor);
                m.SetFloat("_Smoothness", suave);
                m.SetFloat("_Metallic", metal);
                EditorUtility.SetDirty(m);
                convertidos++;
            }
        }

        if (convertidos > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log(convertidos + " material(is) do baú convertido(s) para URP.");
        }
        return convertidos;
    }
}
#endif

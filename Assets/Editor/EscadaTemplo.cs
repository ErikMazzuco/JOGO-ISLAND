#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu:  ISLAND > Decorar Templo_Yas > Colocar Escada para o 2º Andar
///        ISLAND > Decorar Templo_Yas > Remover Escada Nova
///
/// O Templo_Yas tem um 2º andar (~8 m acima) com um vão no piso, mas a escada "Stairs" (ProBuilder)
/// não tem colisão e não serve para subir. Esta ferramenta:
///   1) desliga a escada antiga (não apaga);
///   2) acha o vão do 2º andar e uma borda onde dá para chegar;
///   3) monta uma escada com peças de escada que já existem nos assets (pacote TEMPLO 2, mesmo estilo
///      das paredes; se não der, usa a do pacote egípcio);
///   4) coloca uma RAMPA INVISÍVEL por baixo dos degraus: é nela que o jogador pisa, então sobe liso,
///      sem travar em degrau alto.
/// Enfeites que estiverem no caminho da escada são removidos. Ctrl+Z desfaz.
/// Depois, faça o Bake do NavMesh se quiser que os esqueletos também usem a escada.
/// </summary>
public static class EscadaTemplo
{
    const string NomeTemplo = "templo";
    const string NomeEscadaVelha = "Stairs";
    const string NomeEscada = "ESCADA_2_ANDAR";

    static readonly string[] PecasDeEscada =
    {
        "Assets/IMPORTS/TEMPLO 2/Prefabs/Basic/Platforms/Basic_Stairs_1A.prefab",
        "Assets/IMPORTS/TEMPLO 2/Prefabs/Basic/Platforms/Basic_Stairs_1B.prefab",
        "Assets/CGM_EgyptPack/Prefabs/Buildings/LVL_3/CGM_ENV_BLD_LVL3_stairs2.prefab",
    };

    const float Largura = 2.6f;
    static readonly float[] Angulos = { 33f, 36f, 40f };   // o jogador sobe até 45°

    static Transform templo;
    static float espacoNaFrente = 3f;   // piso livre na frente do 1º degrau

    [MenuItem("ISLAND/Decorar Templo_Yas/Colocar Escada para o 2º Andar")]
    static void Colocar()
    {
        templo = AcharTemplo();
        if (templo == null) { Aviso("Não achei o objeto \"" + NomeTemplo + "\" na cena aberta."); return; }

        Undo.SetCurrentGroupName("Escada do Templo");
        int undo = Undo.GetCurrentGroup();

        // Refaz do zero
        var antiga = AcharEscadaNova();
        if (antiga != null) Undo.DestroyObjectImmediate(antiga);

        // Desliga a escada inútil (o ProBuilder) para ela não atrapalhar a busca nem aparecer
        var velha = templo.Find(NomeEscadaVelha);
        if (velha != null && velha.gameObject.activeSelf)
        {
            Undo.RecordObject(velha.gameObject, "Escada do Templo");
            velha.gameObject.SetActive(false);
        }

        Physics.SyncTransforms();
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        try
        {
            Bounds b = Limites(templo);
            float y0, y1;
            if (!AcharAndares(b, out y0, out y1))
            {
                Aviso("Não consegui achar o térreo e o 2º andar do templo (ele precisa ter colisão).\n" +
                      "Rode antes  ISLAND > Colisões > Adicionar Colisão nos Objetos da Cena.");
                return;
            }
            float subida = y1 - y0;

            Vector3[] dirs = Direcoes();
            var vao = PontosDoVao(b, y0, y1);
            if (vao.Count == 0) { Aviso("Não achei vão no piso do 2º andar (lugar com o térreo embaixo e sem laje em cima)."); return; }

            Vector3 preferido = Vector3.zero;
            foreach (var v in vao) preferido += v;
            preferido /= vao.Count;   // meio do vão

            // Procura: ponto do vão + direção → borda do 2º andar → a escada cabe?
            bool achou = false;
            Vector3 melhorBorda = Vector3.zero, melhorDir = Vector3.zero;
            float melhorAngulo = 0f, melhorNota = float.MaxValue;
            foreach (float frente in new[] { 3.0f, 1.2f })   // primeiro exige 3 m na frente; se não der, 1,2 m
            foreach (float ang in Angulos)
            {
                if (achou) break;
                espacoNaFrente = frente;
                float comp = subida / Mathf.Tan(ang * Mathf.Deg2Rad);
                for (int i = 0; i < vao.Count; i += 2)
                {
                    foreach (var d in dirs)
                    {
                        Vector3 borda;
                        if (!AcharBorda(vao[i], d, y1, out borda)) continue;
                        if (!Cabe(borda, d, comp, y0, y1)) continue;
                        float nota = Vector3.Distance(new Vector3(borda.x, 0, borda.z) - d * comp * 0.5f,
                                                      new Vector3(preferido.x, 0, preferido.z));
                        if (nota < melhorNota) { melhorNota = nota; melhorBorda = borda; melhorDir = d; melhorAngulo = ang; achou = true; }
                    }
                }
                if (achou) break;   // fica com a escada menos íngreme que couber
            }

            if (!achou)
            {
                Aviso("Achei o vão, mas nenhuma borda com espaço para uma escada de " + Largura + " m de largura.\n" +
                      "Tente apagar o que estiver no vão (paredinhas, enfeites) e rodar de novo.");
                return;
            }

            float comprimento = subida / Mathf.Tan(melhorAngulo * Mathf.Deg2Rad);
            var info = Montar(melhorBorda, melhorDir, comprimento, y0, y1);

            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(templo.gameObject.scene);
            Debug.Log("Escada do templo: " + info);
            EditorUtility.DisplayDialog("Escada para o 2º Andar",
                "Escada colocada! " + info + "\n\nA escada antiga (Stairs) foi desligada, não apagada.\n" +
                "Para os esqueletos também subirem: selecione o TerrainGroup (NavMeshSurface) e clique em Bake.\n\n" +
                "Salve a cena (Ctrl+S). Ctrl+Z desfaz.", "OK");
        }
        finally
        {
            Physics.queriesHitBackfaces = backfaces;
        }
    }

    [MenuItem("ISLAND/Decorar Templo_Yas/Remover Escada Nova")]
    static void Remover()
    {
        var e = AcharEscadaNova();
        if (e == null) { Aviso("Não há escada nova para remover."); return; }
        var cena = e.scene;
        Undo.DestroyObjectImmediate(e);
        var t = AcharTemplo();
        var velha = t != null ? t.Find(NomeEscadaVelha) : null;
        if (velha != null) { Undo.RecordObject(velha.gameObject, "Escada do Templo"); velha.gameObject.SetActive(true); }
        EditorSceneManager.MarkSceneDirty(cena);
    }

    // ---------- Montagem ----------

    static string Montar(Vector3 borda, Vector3 d, float comp, float y0, float y1)
    {
        float subida = y1 - y0;
        Vector3 lado = Vector3.Cross(Vector3.up, d).normalized;
        Vector3 inicio = new Vector3(borda.x, y0, borda.z) - d * comp;   // pé da escada (no térreo)

        var raiz = new GameObject(NomeEscada);
        Undo.RegisterCreatedObjectUndo(raiz, "Escada do Templo");
        raiz.transform.SetParent(templo.parent, true);
        raiz.transform.SetPositionAndRotation(inicio, Quaternion.LookRotation(d));

        // 1) Rampa invisível (é nela que o jogador pisa)
        float hip = Mathf.Sqrt(comp * comp + subida * subida);
        float ang = Mathf.Atan2(subida, comp) * Mathf.Rad2Deg;
        var rampa = new GameObject("Rampa (invisível)");
        Undo.RegisterCreatedObjectUndo(rampa, "Escada do Templo");
        rampa.transform.SetParent(raiz.transform, false);
        const float espessura = 0.4f;
        Vector3 meio = inicio + d * (comp * 0.5f) + Vector3.up * (subida * 0.5f);
        Quaternion inclinacao = Quaternion.LookRotation(d) * Quaternion.Euler(-ang, 0f, 0f);
        rampa.transform.SetPositionAndRotation(meio - (inclinacao * Vector3.up) * (espessura * 0.5f), inclinacao);
        var box = rampa.AddComponent<BoxCollider>();
        box.size = new Vector3(Largura, espessura, hip + 0.6f);   // passa um pouquinho no chão e na laje

        // 2) Corrimão invisível dos lados (para não cair do vão)
        for (int s = -1; s <= 1; s += 2)
        {
            var c = new GameObject(s < 0 ? "Proteção esquerda (invisível)" : "Proteção direita (invisível)");
            Undo.RegisterCreatedObjectUndo(c, "Escada do Templo");
            c.transform.SetParent(raiz.transform, false);
            c.transform.SetPositionAndRotation(meio + lado * (s * (Largura * 0.5f + 0.1f)) + (inclinacao * Vector3.up) * 0.6f, inclinacao);
            var cb = c.AddComponent<BoxCollider>();
            cb.size = new Vector3(0.15f, 1.4f, hip * 0.85f);
            cb.center = new Vector3(0f, 0f, hip * 0.075f);   // começa um pouco depois do pé, para dar para entrar
        }

        // 3) Degraus de verdade (visual), usando uma peça de escada dos assets
        string peca = "nenhuma (só a rampa)";
        foreach (var caminho in PecasDeEscada)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (prefab == null) continue;
            ColocarBaus.ConverterMateriais(prefab, false);
            if (DegrausVisuais(prefab, raiz.transform, inicio, d, comp, subida))
            {
                peca = System.IO.Path.GetFileNameWithoutExtension(caminho);
                break;
            }
        }

        // 4) Tira enfeites que ficaram no caminho
        int removidos = LimparCaminho(inicio, d, comp, subida);

        Selection.activeGameObject = raiz;
        return "Subida de " + subida.ToString("F1") + " m em " + comp.ToString("F1") + " m (" + ang.ToString("F0") + "°), peça: " + peca +
               (removidos > 0 ? ", " + removidos + " enfeite(s) removidos do caminho" : "") + ".";
    }

    // Encaixa N cópias da peça de escada uma depois da outra, esticadas para cobrir a rampa
    static bool DegrausVisuais(GameObject prefab, Transform pai, Vector3 inicio, Vector3 d, float comp, float subida)
    {
        // mede a peça "deitada" na orientação original
        var teste = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai.gameObject.scene);
        teste.transform.SetPositionAndRotation(new Vector3(0f, -5000f, 0f), Quaternion.identity);
        Bounds b = LimitesDe(teste);
        if (b.size.y < 0.1f) { Object.DestroyImmediate(teste); return false; }

        // eixo de subida = o maior lado horizontal; descobre para que lado ela sobe
        bool subidaEmZ = b.size.z >= b.size.x;
        Vector3 eixo = subidaEmZ ? Vector3.forward : Vector3.right;
        Vector3 sobeLocal = LadoQueSobe(teste, eixo);
        float altPeca = b.size.y;
        float compPeca = subidaEmZ ? b.size.z : b.size.x;
        float largPeca = subidaEmZ ? b.size.x : b.size.z;
        Object.DestroyImmediate(teste);

        // quantas peças: mantém a altura de cada uma parecida com a original
        int n = Mathf.Clamp(Mathf.RoundToInt(subida / altPeca), 1, 12);
        float alturaCada = subida / n, compCada = comp / n;

        // gira a peça para "sobeLocal" apontar para d
        Quaternion rot = Quaternion.LookRotation(d) * Quaternion.Inverse(Quaternion.LookRotation(sobeLocal));
        Vector3 escalaLocal = subidaEmZ
            ? new Vector3(Largura / largPeca, alturaCada / altPeca, compCada / compPeca)
            : new Vector3(compCada / compPeca, alturaCada / altPeca, Largura / largPeca);

        for (int i = 0; i < n; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go, "Escada do Templo");
            go.name = "Degraus " + (i + 1);
            go.transform.SetParent(pai, true);
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.Scale(prefab.transform.localScale, escalaLocal);

            // encaixa pela caixa: começo da peça no começo do trecho, base na altura do trecho
            Vector3 alvo = inicio + d * (compCada * (i + 0.5f));
            Bounds gb = LimitesDe(go);
            go.transform.position += new Vector3(alvo.x - gb.center.x, (inicio.y + alturaCada * i) - gb.min.y, alvo.z - gb.center.z);

            // quem segura o jogador é a rampa; os degraus são só visuais
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) { Undo.RecordObject(c, "Escada do Templo"); c.enabled = false; }
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }
        return true;
    }

    // Para que lado (ao longo do eixo) os degraus sobem? Usa a malha dos degraus (a com mais vértices):
    // na ponta de baixo os vértices mais altos são baixinhos (1º degrau); na ponta de cima, altura cheia.
    static Vector3 LadoQueSobe(GameObject peca, Vector3 eixo)
    {
        MeshFilter degraus = null;
        foreach (var mf in peca.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null && (degraus == null || mf.sharedMesh.vertexCount > degraus.sharedMesh.vertexCount)) degraus = mf;
        if (degraus == null) return eixo;

        var vs = degraus.sharedMesh.vertices;
        float min = float.MaxValue, max = float.MinValue;
        var mundo = new Vector3[vs.Length];
        for (int i = 0; i < vs.Length; i++)
        {
            mundo[i] = degraus.transform.TransformPoint(vs[i]);
            float a = Vector3.Dot(mundo[i], eixo);
            if (a < min) min = a; if (a > max) max = a;
        }
        float fatia = (max - min) * 0.15f;
        float altoA = float.MinValue, altoB = float.MinValue;
        foreach (var p in mundo)
        {
            float a = Vector3.Dot(p, eixo);
            if (a <= min + fatia && p.y > altoA) altoA = p.y;   // ponta do lado -eixo
            if (a >= max - fatia && p.y > altoB) altoB = p.y;   // ponta do lado +eixo
        }
        return altoB >= altoA ? eixo : -eixo;
    }

    // Remove enfeites (das ferramentas de decoração) que ficaram em cima/embaixo/no caminho da escada.
    // Usa a caixa do desenho, porque velas e tapetes não têm colisão.
    static int LimparCaminho(Vector3 inicio, Vector3 d, float comp, float subida)
    {
        var deco = GameObject.Find("DECORACAO_TEMPLO_YAS");
        if (deco == null) return 0;

        Quaternion paraLocal = Quaternion.Inverse(Quaternion.LookRotation(d));
        var apagar = new List<GameObject>();
        foreach (Transform sub in deco.transform)
            foreach (Transform enfeite in sub)
            {
                foreach (var r in enfeite.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer) continue;
                    Bounds b = r.bounds;
                    Vector3 l = paraLocal * (b.center - inicio);
                    float folgaLado = Largura * 0.5f + 0.3f + Mathf.Max(b.extents.x, b.extents.z);
                    if (Mathf.Abs(l.x) > folgaLado) continue;
                    if (l.z < -1.0f || l.z > comp + 0.8f) continue;
                    float superficie = Mathf.Clamp01(l.z / comp) * subida;
                    if (b.max.y - inicio.y < -0.2f || b.min.y - inicio.y > superficie + 2.3f) continue;
                    apagar.Add(enfeite.gameObject);
                    break;
                }
            }
        foreach (var go in apagar) Undo.DestroyObjectImmediate(go);
        return apagar.Count;
    }

    // Só mexe em coisas colocadas pelas ferramentas de decoração (nunca no templo)
    static GameObject RaizDeEnfeite(Transform t)
    {
        for (var a = t; a != null; a = a.parent)
        {
            if (a.parent != null && a.parent.parent != null && a.parent.parent.name == "DECORACAO_TEMPLO_YAS") return a.gameObject;
        }
        return null;
    }

    // ---------- Andares e vão (pela estrutura do Templo_Yas) ----------
    // Térreo = plataformas filhas diretas do "templo"; 2º andar = plataformas dentro de "andar_2".

    static List<Collider> pisoTerreo = new List<Collider>();
    static List<Collider> piso2 = new List<Collider>();
    static List<Renderer> rendTerreo = new List<Renderer>();
    static List<Renderer> rend2 = new List<Renderer>();

    static bool AcharAndares(Bounds b, out float y0, out float y1)
    {
        pisoTerreo.Clear(); piso2.Clear(); rendTerreo.Clear(); rend2.Clear();
        var alt0 = new List<float>();
        var alt1 = new List<float>();

        foreach (var r in templo.GetComponentsInChildren<MeshRenderer>())
        {
            if (!r.name.StartsWith("HD_Platform")) continue;
            Transform t = r.transform;
            bool terreo = t.parent == templo;
            bool segundo = !terreo && AlgumPaiChamado(t, "andar_2");
            if (!terreo && !segundo) continue;

            (terreo ? rendTerreo : rend2).Add(r);
            (terreo ? alt0 : alt1).Add(r.bounds.max.y);
            foreach (var c in r.GetComponentsInChildren<Collider>())
                if (c.enabled) (terreo ? pisoTerreo : piso2).Add(c);
        }

        y0 = y1 = 0f;
        if (alt0.Count == 0 || alt1.Count == 0) return false;
        y0 = Mediana(alt0);
        y1 = Mediana(alt1);

        // altura exata da superfície (a caixa do desenho pode ter bordinha)
        float a;
        if (AlturaDoPiso(pisoTerreo, rendTerreo, rendTerreo[rendTerreo.Count / 2].bounds.center, y0, out a)) y0 = a;
        if (AlturaDoPiso(piso2, rend2, rend2[rend2.Count / 2].bounds.center, y1, out a)) y1 = a;
        return y1 - y0 > 2.5f;
    }

    static bool AlgumPaiChamado(Transform t, string nome)
    {
        for (var a = t.parent; a != null && a != templo; a = a.parent)
            if (a.name == nome) return true;
        return false;
    }

    // Há piso deste conjunto neste ponto (x,z)? Usa a colisão; sem colisão, usa a caixa do desenho.
    static bool AlturaDoPiso(List<Collider> cols, List<Renderer> rends, Vector3 xz, float yRef, out float y)
    {
        y = yRef;
        var ray = new Ray(new Vector3(xz.x, yRef + 3f, xz.z), Vector3.down);
        float melhor = float.MinValue;
        foreach (var c in cols)
        {
            RaycastHit h;
            if (c.Raycast(ray, out h, 6f) && h.normal.y > 0.7f && h.point.y > melhor) melhor = h.point.y;
        }
        if (melhor > float.MinValue) { y = melhor; return true; }

        if (cols.Count == 0)
            foreach (var r in rends)
            {
                Bounds rb = r.bounds;
                if (xz.x >= rb.min.x && xz.x <= rb.max.x && xz.z >= rb.min.z && xz.z <= rb.max.z) { y = rb.max.y; return true; }
            }
        return false;
    }

    static bool TemTerreo(Vector3 xz, float y0) { float y; return AlturaDoPiso(pisoTerreo, rendTerreo, xz, y0, out y) && Mathf.Abs(y - y0) < 0.4f; }
    static bool Tem2Andar(Vector3 xz, float y1) { float y; return AlturaDoPiso(piso2, rend2, xz, y1, out y) && Mathf.Abs(y - y1) < 0.4f; }

    // Lugares com o térreo embaixo e sem piso do 2º andar em cima
    static List<Vector3> PontosDoVao(Bounds b, float y0, float y1)
    {
        var lista = new List<Vector3>();
        for (float x = b.min.x; x <= b.max.x; x += 1f)
        for (float z = b.min.z; z <= b.max.z; z += 1f)
        {
            var p = new Vector3(x, y0, z);
            if (TemTerreo(p, y0) && !Tem2Andar(p, y1)) lista.Add(p);
        }
        return lista;
    }

    // Anda do ponto do vão na direção d até achar o piso do 2º andar
    static bool AcharBorda(Vector3 p, Vector3 d, float y1, out Vector3 borda)
    {
        borda = Vector3.zero;
        for (float t = 0.25f; t < 25f; t += 0.25f)
        {
            Vector3 q = p + d * t;
            if (Tem2Andar(q, y1))
            {
                // volta até o último ponto sem piso: a borda exata
                float t0 = t - 0.25f, t1 = t;
                for (int i = 0; i < 6; i++) { float m = (t0 + t1) * 0.5f; if (Tem2Andar(p + d * m, y1)) t1 = m; else t0 = m; }
                Vector3 e = p + d * t1;
                borda = new Vector3(e.x, y1, e.z);
                return true;
            }
        }
        return false;
    }

    // A escada inteira (pé no térreo, topo na borda) cabe sem bater em nada?
    static bool Cabe(Vector3 borda, Vector3 d, float comp, float y0, float y1)
    {
        float subida = y1 - y0;
        Vector3 lado = Vector3.Cross(Vector3.up, d).normalized;
        Vector3 pe = new Vector3(borda.x, y0, borda.z) - d * comp;

        // chão do térreo embaixo de toda a escada e ANTES dela (espaço para chegar de frente, por dentro do templo)
        for (float t = -espacoNaFrente; t <= comp; t += 0.75f)
            for (int s = -1; s <= 1; s++)
            {
                if (!TemTerreo(pe + d * t + lado * (s * Largura * 0.45f), y0)) return false;
            }
        // e esse espaço da frente precisa estar livre (sem parede, pilar...) e coberto (dentro do templo)
        for (float t = -espacoNaFrente + 0.4f; t < -0.2f; t += 0.6f)
        {
            Vector3 c = pe + d * t + Vector3.up * 1.15f;
            foreach (var col in Physics.OverlapBox(c, new Vector3(Largura * 0.45f, 0.9f, 0.25f), Quaternion.LookRotation(d), ~0, QueryTriggerInteraction.Ignore))
                if (RaizDeEnfeite(col.transform) == null && !col.isTrigger && col.GetComponentInParent<VidaSkeleton>() == null &&
                    col.GetComponentInParent<CharacterController>() == null)
                    return false;
            if (!Physics.Raycast(pe + d * t + Vector3.up * 0.3f, Vector3.up, 40f, ~0, QueryTriggerInteraction.Ignore)) return false;
        }

        // espaço livre em cima da rampa (cabeça) — ignora os enfeites, que serão removidos
        for (float t = 0.3f; t <= comp - 0.3f; t += 0.5f)
        {
            float y = y0 + subida * (t / comp);
            Vector3 c = pe + d * t + Vector3.up * (y - y0 + 1.15f);
            foreach (var col in Physics.OverlapBox(c, new Vector3(Largura * 0.5f, 0.95f, 0.2f), Quaternion.LookRotation(d), ~0, QueryTriggerInteraction.Ignore))
                if (RaizDeEnfeite(col.transform) == null && !col.isTrigger && col.GetComponentInParent<VidaSkeleton>() == null &&
                    col.GetComponentInParent<CharacterController>() == null)
                    return false;
        }

        // o 2º andar continua na frente da chegada (não termina em parede nem em buraco)
        for (float t = 0.4f; t <= 2.0f; t += 0.4f)
            for (int s = -1; s <= 1; s++)
                if (!Tem2Andar(new Vector3(borda.x, y1, borda.z) + d * t + lado * (s * Largura * 0.4f), y1)) return false;

        // dá para ficar em pé (e andar) quando chega lá em cima: nada de parede na frente
        Vector3 topo = new Vector3(borda.x, y1, borda.z) + d * 1.2f;
        if (Physics.CheckBox(topo + Vector3.up * 1.2f, new Vector3(Largura * 0.45f, 0.9f, 1.0f), Quaternion.LookRotation(d), ~0, QueryTriggerInteraction.Ignore))
            return false;
        return true;
    }

    // ---------- Utilidades ----------

    static Vector3[] Direcoes()
    {
        Vector3 x = templo.right; x.y = 0; x.Normalize();
        Vector3 z = templo.forward; z.y = 0; z.Normalize();
        return new[] { x, -x, z, -z };
    }

    static float Mediana(List<float> l) { var c = new List<float>(l); c.Sort(); return c[c.Count / 2]; }

    static void Aviso(string msg) { EditorUtility.DisplayDialog("Escada para o 2º Andar", msg, "OK"); }

    static Transform AcharTemplo()
    {
        return AcharNaCena.Objeto(NomeTemplo);
    }

    static GameObject AcharEscadaNova()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!EditorUtility.IsPersistent(t) && t.name == NomeEscada) return t.gameObject;
        return null;
    }

    static Bounds Limites(Transform alvo)
    {
        var b = new Bounds(alvo.position, Vector3.zero);
        bool tem = false;
        foreach (var r in alvo.GetComponentsInChildren<Renderer>())
        {
            if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static Bounds LimitesDe(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
#endif

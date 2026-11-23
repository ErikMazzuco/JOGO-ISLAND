#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu:  ISLAND > Decorar Templo_Yas > Decorar
///        ISLAND > Decorar Templo_Yas > Decorar de Novo (Outra Variação)
///        ISLAND > Decorar Templo_Yas > Remover Decoração
///
/// Decora o templo "templo" (Templo_Yas) nos dois andares, com cara de templo egípcio:
///   - ALTAR em cada andar, na parede do fim do salão mais comprido: tablado, mesa com velas,
///     moedas e jarros, dois braseiros do lado, tapete até o meio do salão e colunas acompanhando o tapete;
///   - tochas com fogo e luz tremendo ao longo das paredes;
///   - painéis de relevo (ornamentos egípcios) e escudos pendurados nas paredes;
///   - colunas quebradas, braseiros e grupos de vasos/velas nos cantos e entre as tochas.
/// Sem plantas. Tudo vai para o objeto "DECORACAO_TEMPLO_YAS" (dá para mover/apagar à vontade).
/// Portas, escadas e corredores estreitos ficam livres. Ctrl+Z desfaz.
/// Os materiais dos pacotes (que ficariam rosa no URP) são convertidos automaticamente.
/// </summary>
public static class DecorarTemplo
{
    const string NomeTemplo = "templo";
    const string NomeGrupo = "DECORACAO_TEMPLO_YAS";
    const string PastaMateriais = "Assets/IMPORTS/Templo_Yas/Decoracao";
    const string CaminhoFogoMat = PastaMateriais + "/FogoTocha.mat";
    const string ChaveSemente = "ISLAND_DecorarTemplo_Semente";

    // ---------- Assets ----------
    const string T2 = "Assets/IMPORTS/TEMPLO 2/Prefabs/High_Detailed/Small_Objects/";
    const string EG = "Assets/CGM_EgyptPack/Prefabs/";
    const string MD = "Assets/LowPolyMedievalPropsLite/Prefabs/";
    const string C = "Assets/IMPORTS/COSMETICOS/Prefabs/";

    static readonly string[] Tochas = { T2 + "Torch_Post_1A.prefab", T2 + "Torch_Post_1B.prefab", T2 + "Torch_Post_1C.prefab" };
    static readonly string[] Braseiros = { EG + "Props/CGM_ENV_PRP_fire_bowl1.prefab" };
    static readonly string[] Colunas = { EG + "Buildings/LVL_3/Present_House_LVL_3/CGM_ENV_BLD_LVL3_fortress_pillar3.prefab" };
    static readonly string[] ColunasQuebradas = { EG + "Ground/CGM_ENV_GRD_broken_pillar2.prefab", EG + "Ground/CGM_ENV_GRD_broken_pillar3.prefab" };
    static readonly string[] Paineis = { EG + "Buildings/LVL_3/CGM_ENV_BLD_LVL3_ornament8.prefab" };
    static readonly string[] Escudos = { EG + "Weapons/CGM_ENV_WEP_shield1.prefab", EG + "Weapons/CGM_ENV_WEP_shield3.prefab" };
    static readonly string[] Tablado = { EG + "Buildings/LVL_3/CGM_ENV_BLD_LVL3_roof_ground3.prefab" };
    static readonly string[] Altar = { MD + "Furniture_01.prefab" };
    static readonly string[] Tapete = { MD + "Carpet_01.prefab" };
    static readonly string[] Velas = { MD + "Candle_01.prefab", MD + "Candle_02.prefab", MD + "Candle_03.prefab" };
    static readonly string[] Moedas = { MD + "Coin_03.prefab", MD + "Coin_02.prefab" };
    static readonly string[] Jarros = { MD + "Jug_01.prefab", MD + "Jug_02.prefab", MD + "Jug_03.prefab", MD + "Jug_04.prefab" };
    static readonly string[] Vasos =
    {
        C + "Props/Containers/rpgpp_lt_vase_01.prefab", C + "Props/Containers/rpgpp_lt_vase_02.prefab",
        C + "Props/Containers/rpgpp_lt_vase_03.prefab", MD + "Jug_01.prefab", MD + "Jug_02.prefab",
    };

    // ---------- Quantidades / espaçamentos ----------
    const float Passo = 0.9f;                 // espaçamento da grade que procura chão
    const float EspacoTochas = 5.5f;
    const float EspacoBraseiros = 8f;
    const float EspacoParede = 2.4f;
    const float EspacoPaineis = 7f;
    const float EspacoEscudos = 5f;
    const float DistanciaMeioSalao = 4f;      // braseiro do meio só onde as paredes estão longe
    const int MaxLuzes = 24;                  // luzes de verdade (o resto só tem fogo)
    const int MaxPecasTapete = 10;
    const int MaxParesColunas = 3;

    enum Tipo { Canto, Parede, Aberto, Estreito }

    class Ponto
    {
        public Vector3 pos;
        public Tipo tipo;
        public Vector3 paraDentro;   // direção que se afasta da parede mais próxima
        public Vector3 normalParede; // normal da parede mais próxima
        public Vector3 pontoParede;  // onde a parede foi encontrada (altura do chão)
        public float distParede;
    }

    struct Ocupado { public Vector3 pos; public float raio; }

    static System.Random rnd;
    static List<Ocupado> ocupados;        // chão (objetos)
    static List<Ocupado> tochasColocadas; // só entre tochas
    static List<Ocupado> braseirosColocados;
    static List<Ocupado> paineisColocados;
    static List<Ocupado> escudosColocados;
    static Dictionary<string, GameObject> cache;
    static Transform grupo;
    static Transform templo;
    static Vector3[] dirsTemplo;
    static string[] tochaDoTemplo;        // um modelo só de tocha, para ficar uniforme
    static int contagem;
    static int luzes;

    // ------------------------------------------------------------------------------------------

    [MenuItem("ISLAND/Decorar Templo_Yas/Decorar")]
    static void Decorar() { Executar(EditorPrefs.GetInt(ChaveSemente, 1234)); }

    [MenuItem("ISLAND/Decorar Templo_Yas/Decorar de Novo (Outra Variação)")]
    static void DecorarDeNovo()
    {
        int semente = System.Environment.TickCount & 0x7fffffff;
        EditorPrefs.SetInt(ChaveSemente, semente);
        Executar(semente);
    }

    [MenuItem("ISLAND/Decorar Templo_Yas/Remover Decoração")]
    static void Remover()
    {
        var antigo = AcharGrupo();
        if (antigo == null) { EditorUtility.DisplayDialog("Remover Decoração", "Não há decoração para remover.", "OK"); return; }
        var cena = antigo.scene;
        Undo.DestroyObjectImmediate(antigo);
        EditorSceneManager.MarkSceneDirty(cena);
        Debug.Log("Decoração do Templo_Yas removida.");
    }

    // ------------------------------------------------------------------------------------------

    static void Executar(int semente)
    {
        templo = AcharTemplo();
        if (templo == null)
        {
            EditorUtility.DisplayDialog("Decorar Templo_Yas", "Não achei o objeto \"" + NomeTemplo + "\" na cena aberta.\nAbra a cena Island.", "OK");
            return;
        }

        bool temColisao = false;
        foreach (var c in templo.GetComponentsInChildren<Collider>()) if (c.enabled) { temColisao = true; break; }
        if (!temColisao)
        {
            EditorUtility.DisplayDialog("Decorar Templo_Yas",
                "O templo não tem colisão, então não dá para achar o chão.\nRode antes  ISLAND > Colisões > Adicionar Colisão nos Objetos da Cena.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Decorar Templo_Yas");
        int undo = Undo.GetCurrentGroup();

        // Refaz do zero
        var antigo = AcharGrupo();
        if (antigo != null) Undo.DestroyObjectImmediate(antigo);

        rnd = new System.Random(semente);
        ocupados = new List<Ocupado>();
        tochasColocadas = new List<Ocupado>();
        braseirosColocados = new List<Ocupado>();
        paineisColocados = new List<Ocupado>();
        escudosColocados = new List<Ocupado>();
        cache = new Dictionary<string, GameObject>();
        contagem = 0; luzes = 0;
        tochaDoTemplo = new[] { Tochas[rnd.Next(Tochas.Length)] };

        var go = new GameObject(NomeGrupo);
        if (go.scene != templo.gameObject.scene)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, templo.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(go, "Decorar Templo_Yas");
        grupo = go.transform;
        grupo.SetParent(templo.parent, false);
        grupo.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var gAltar = Sub("Altares"); var gTochas = Sub("Tochas"); var gBraseiros = Sub("Braseiros");
        var gColunas = Sub("Colunas"); var gParede = Sub("Paredes"); var gObjetos = Sub("Vasos e Velas");

        // Não decorar em cima de baús, do jogador e de pontos importantes
        foreach (var b in Object.FindObjectsByType<BauTesouro>(FindObjectsSortMode.None)) Reservar(b.transform.position, 2.2f);
        foreach (var p in Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)) Reservar(p.transform.position, 3f);

        Physics.SyncTransforms();
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        int altares = 0;
        try
        {
            EditorUtility.DisplayProgressBar("Decorar Templo_Yas", "Procurando o chão dos andares...", 0.1f);
            Bounds b = Limites(templo);
            var pontos = ProcurarPontos(b);
            Embaralhar(pontos);

            // 1) Altar em cada andar
            EditorUtility.DisplayProgressBar("Decorar Templo_Yas", "Altares...", 0.25f);
            foreach (var andar in SepararAndares(pontos))
            {
                var lugar = MelhorLugarDeAltar(andar);
                if (lugar != null && MontarAltar(lugar, gAltar, gColunas)) altares++;
            }

            // 2) Tochas ao longo das paredes
            EditorUtility.DisplayProgressBar("Decorar Templo_Yas", "Tochas...", 0.4f);
            foreach (var p in pontos)
                if (p.tipo == Tipo.Parede && Livre(tochasColocadas, p.pos, EspacoTochas * 0.5f))
                    ColocarTocha(p, gTochas);

            // 3) Painéis de relevo e escudos nas paredes
            EditorUtility.DisplayProgressBar("Decorar Templo_Yas", "Paredes...", 0.55f);
            foreach (var p in pontos)
            {
                if (p.tipo != Tipo.Parede) continue;
                if (!Livre(tochasColocadas, p.pos, 1.3f)) continue;
                if (Livre(paineisColocados, p.pos, EspacoPaineis * 0.5f) && Chance(0.6f))
                {
                    if (NaParede(Paineis, p, 0.15f, gParede, true) != null)
                        paineisColocados.Add(new Ocupado { pos = p.pos, raio = EspacoPaineis * 0.5f });
                }
                else if (Livre(escudosColocados, p.pos, EspacoEscudos * 0.5f) && Livre(paineisColocados, p.pos, 1.5f) && Chance(0.5f))
                {
                    if (NaParede(Escudos, p, 2.3f, gParede, false) != null)
                        escudosColocados.Add(new Ocupado { pos = p.pos, raio = EspacoEscudos * 0.5f });
                }
            }

            // 4) Braseiros no meio dos salões grandes
            EditorUtility.DisplayProgressBar("Decorar Templo_Yas", "Braseiros...", 0.65f);
            foreach (var p in pontos)
                if (p.tipo == Tipo.Aberto && p.distParede >= DistanciaMeioSalao &&
                    Livre(braseirosColocados, p.pos, EspacoBraseiros))
                    ColocarBraseiro(p.pos, gBraseiros);

            // 5) Cantos: coluna quebrada, braseiro ou vasos com velas
            EditorUtility.DisplayProgressBar("Decorar Templo_Yas", "Cantos...", 0.75f);
            foreach (var p in pontos)
            {
                if (p.tipo != Tipo.Canto || !Livre(p.pos, 1.3f)) continue;
                float r = (float)rnd.NextDouble();
                if (r < 0.4f)
                    Colocar(ColunasQuebradas, p.pos + p.paraDentro * 0.9f, gColunas, 0.9f, 1.05f, 1.3f, null, true);
                else if (r < 0.7f && Livre(braseirosColocados, p.pos, EspacoBraseiros * 0.5f))
                    ColocarBraseiro(p.pos + p.paraDentro * 0.6f, gBraseiros);
                else
                {
                    GrupoDeVasos(p.pos + p.paraDentro * 0.55f, p.paraDentro, gObjetos);
                    Velas_(p.pos + p.paraDentro * 1.0f, gObjetos, rnd.Next(2, 4));
                }
            }

            // 6) Entre as tochas: braseiros, velas e vasos
            EditorUtility.DisplayProgressBar("Decorar Templo_Yas", "Velas e vasos...", 0.85f);
            foreach (var p in pontos)
            {
                if (p.tipo != Tipo.Parede || !Livre(p.pos, EspacoParede * 0.5f)) continue;
                if (!Livre(tochasColocadas, p.pos, 1.5f)) continue;
                Vector3 pos = p.pos + p.paraDentro * 0.5f;
                float r = (float)rnd.NextDouble();
                if (r < 0.25f && Livre(braseirosColocados, pos, EspacoBraseiros * 0.5f)) ColocarBraseiro(pos, gBraseiros);
                else if (r < 0.5f) { Reservar(pos, 0.7f); Velas_(pos, gObjetos, rnd.Next(3, 6)); }
                else if (r < 0.75f) GrupoDeVasos(pos, p.paraDentro, gObjetos);
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = backfaces;
            EditorUtility.ClearProgressBar();
        }

        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(templo.gameObject.scene);
        Selection.activeGameObject = grupo.gameObject;

        string msg = contagem + " enfeites colocados: " + altares + " altar(es), " + gTochas.childCount + " tochas, " +
                     gBraseiros.childCount + " braseiros, " + luzes + " luzes.";
        Debug.Log("Decorar Templo_Yas: " + msg);
        EditorUtility.DisplayDialog("Decorar Templo_Yas",
            msg + "\n\nTudo está em " + NomeGrupo + " na Hierarchy, separado por tipo.\n" +
            "Não gostou? Use \"Decorar de Novo\" para outra variação, ou mova/apague à vontade.\n\n" +
            "Salve a cena (Ctrl+S). Ctrl+Z desfaz.", "OK");
    }

    // ---------- Altar ----------

    // Agrupa os pontos por altura (cada grupo = um andar), do mais cheio para o mais vazio
    static List<List<Ponto>> SepararAndares(List<Ponto> pontos)
    {
        var ordenados = new List<Ponto>(pontos);
        ordenados.Sort((a, b) => a.pos.y.CompareTo(b.pos.y));
        var andares = new List<List<Ponto>>();
        List<Ponto> atual = null;
        float ultimoY = float.MinValue;
        foreach (var p in ordenados)
        {
            if (atual == null || p.pos.y - ultimoY > 2f) { atual = new List<Ponto>(); andares.Add(atual); }
            atual.Add(p);
            ultimoY = p.pos.y;
        }
        andares.RemoveAll(a => a.Count < 40);       // degraus, plataformas pequenas
        andares.Sort((a, b) => b.Count.CompareTo(a.Count));
        if (andares.Count > 2) andares.RemoveRange(2, andares.Count - 2);
        return andares;
    }

    // Parede no fim do salão mais comprido: de lá se vê o salão inteiro
    static Ponto MelhorLugarDeAltar(List<Ponto> andar)
    {
        Ponto melhor = null;
        float melhorVista = 9f;   // salão precisa de pelo menos ~9 m na frente do altar
        foreach (var p in andar)
        {
            if (p.tipo != Tipo.Parede || p.distParede > 0.7f) continue;
            if (!Livre(p.pos, 3.5f)) continue;

            RaycastHit h;
            Vector3 o = p.pos + Vector3.up * 1.0f;
            float vista = Physics.Raycast(o, p.paraDentro, out h, 40f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 40f;
            if (vista <= melhorVista) continue;

            // espaço livre para o tablado (3,4 m) e os braseiros dos lados
            Vector3 lado = Vector3.Cross(Vector3.up, p.paraDentro).normalized;
            Vector3 centro = p.pos + p.paraDentro * 2.0f + Vector3.up * 1.2f;
            Vector3 meio = new Vector3(2.6f, 1.0f, 1.8f);
            if (Physics.CheckBox(centro, meio, Quaternion.LookRotation(p.paraDentro), ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!ChaoContinuo(p.pos + p.paraDentro * 2f, lado, p.paraDentro, 2.4f, 1.8f)) continue;

            melhorVista = vista;
            melhor = p;
        }
        return melhor;
    }

    static bool ChaoContinuo(Vector3 centro, Vector3 lado, Vector3 frente, float meioLado, float meioFrente)
    {
        for (int i = -1; i <= 1; i++)
        for (int j = -1; j <= 1; j++)
        {
            Vector3 p = centro + lado * (i * meioLado) + frente * (j * meioFrente);
            RaycastHit h;
            if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out h, 1.0f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(h.point.y - centro.y) > 0.2f) return false;
        }
        return true;
    }

    static bool MontarAltar(Ponto w, Transform paiAltar, Transform paiColunas)
    {
        Vector3 d = w.paraDentro; d.y = 0; d.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, d).normalized;
        Quaternion rot = Quaternion.LookRotation(d);
        float chao = w.pos.y;
        Vector3 baseParede = new Vector3(w.pontoParede.x, chao, w.pontoParede.z);

        var altar = new GameObject("Altar");
        Undo.RegisterCreatedObjectUndo(altar, "Decorar Templo_Yas");
        altar.transform.SetParent(paiAltar, false);
        altar.transform.SetPositionAndRotation(baseParede, rot);

        // Tablado encostado na parede
        float topoTablado = chao;
        float fundoTablado = 0.3f;
        var tablado = Sortear(Tablado);
        if (tablado != null)
        {
            var t = Instanciar(tablado, altar.transform);
            t.transform.rotation = rot;
            Bounds tb = LimitesDe(t);
            float prof = Extensao(tb, d) * 2f;
            Assentar(t, baseParede + d * (0.15f + prof * 0.5f), chao);
            GarantirColisao(t);
            tb = LimitesDe(t);
            topoTablado = tb.max.y;
            fundoTablado = 0.15f + prof;
        }

        // Mesa do altar em cima do tablado, comprida ao longo da parede
        var mesa = Sortear(Altar);
        Vector3 topoMesa = baseParede + d * 0.9f + Vector3.up * topoTablado;
        float larguraMesa = 1.6f;
        if (mesa != null)
        {
            var m = Instanciar(mesa, altar.transform);
            m.transform.rotation = rot;
            Assentar(m, baseParede + d * 0.75f, topoTablado);
            Bounds mb = LimitesDe(m);
            // se o lado comprido ficou para a frente, gira 90°
            if (Extensao(mb, d) > Extensao(mb, lado)) { m.transform.rotation = rot * Quaternion.Euler(0f, 90f, 0f); Assentar(m, baseParede + d * 0.75f, topoTablado); mb = LimitesDe(m); }
            topoMesa = new Vector3(mb.center.x, mb.max.y, mb.center.z);
            larguraMesa = Extensao(mb, lado) * 2f;
        }

        // Em cima da mesa: velas nas pontas, moedas e jarro no meio
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 ponta = topoMesa + lado * (s * (larguraMesa * 0.5f - 0.2f));
            Vela(ponta, altar.transform, 1.2f);
            Vela(ponta - lado * s * 0.18f + d * 0.1f, altar.transform, 0.9f);
        }
        for (int i = 0; i < 3; i++)
        {
            var moeda = Sortear(Moedas);
            if (moeda == null) break;
            var mo = Instanciar(moeda, altar.transform);
            mo.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            Assentar(mo, topoMesa + lado * ((i - 1) * 0.22f) + d * 0.05f, topoMesa.y);
            SemColisao(mo);
        }
        var jarro = Sortear(Jarros);
        if (jarro != null)
        {
            var j = Instanciar(jarro, altar.transform);
            Assentar(j, topoMesa + lado * 0.45f - d * 0.1f, topoMesa.y);
            SemColisao(j);
        }

        // Velas na beirada da frente do tablado
        Vector3 frenteTablado = baseParede + d * (fundoTablado - 0.25f);
        for (int s = -1; s <= 1; s += 2)
            for (int k = 0; k < 3; k++)
                Vela(frenteTablado + lado * (s * (1.2f + k * 0.15f)) - d * (k * 0.12f) + Vector3.up * (topoTablado - chao), altar.transform, 1f);

        // Dois braseiros grandes, um de cada lado do tablado
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 pos = baseParede + d * (fundoTablado * 0.5f) + lado * (s * 2.5f);
            ColocarBraseiro(pos, altar.transform, 1.3f, false);
        }

        // Tapete do altar até o salão
        Vector3 inicio = baseParede + d * (fundoTablado + 0.05f);
        float comprimento = ColocarTapete(inicio, d, altar.transform);

        // Colunas acompanhando o tapete
        int pares = 0;
        for (float dist = 2.5f; dist < comprimento - 0.5f && pares < MaxParesColunas; dist += 4.5f)
        {
            bool algum = false;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 pos = inicio + d * dist + lado * (s * 2.8f);
                if (ColunaComFolga(pos, paiColunas, rot)) algum = true;
            }
            if (algum) pares++;
        }

        // Reserva a área do altar e do tapete
        Reservar(baseParede + d * 2f, 3.2f);
        for (float t = 0; t < comprimento; t += 1.2f) Reservar(inicio + d * t, 1.3f);
        braseirosColocados.Add(new Ocupado { pos = baseParede + d * 2f, raio = 4f });
        tochasColocadas.Add(new Ocupado { pos = baseParede + d * 1f, raio = 2.5f });
        paineisColocados.Add(new Ocupado { pos = baseParede, raio = 3f });
        return true;
    }

    // Peças de tapete uma atrás da outra, enquanto houver chão livre
    static float ColocarTapete(Vector3 inicio, Vector3 d, Transform pai)
    {
        var prefab = Sortear(Tapete);
        if (prefab == null) return 0f;

        float andado = 0f;
        for (int i = 0; i < MaxPecasTapete; i++)
        {
            var t = Instanciar(prefab, pai);
            t.transform.rotation = Quaternion.LookRotation(d);
            Bounds b = LimitesDe(t);
            // o lado comprido do tapete vai ao longo do caminho
            if (Extensao(b, d) < Extensao(b, Vector3.Cross(Vector3.up, d)))
            { t.transform.rotation = Quaternion.LookRotation(d) * Quaternion.Euler(0f, 90f, 0f); b = LimitesDe(t); }
            float comp = Extensao(b, d) * 2f;
            Vector3 centro = inicio + d * (andado + comp * 0.5f);

            // chão no mesmo nível e nada no caminho?
            RaycastHit h;
            bool chaoOk = Physics.Raycast(centro + Vector3.up * 0.5f, Vector3.down, out h, 1.0f, ~0, QueryTriggerInteraction.Ignore)
                          && Mathf.Abs(h.point.y - inicio.y) < 0.12f;
            bool livre = !Physics.CheckBox(centro + Vector3.up * 0.6f, new Vector3(0.6f, 0.45f, comp * 0.5f), Quaternion.LookRotation(d), ~0, QueryTriggerInteraction.Ignore);
            if (!chaoOk || !livre)
            {
                Undo.DestroyObjectImmediate(t); contagem--;
                break;
            }
            Assentar(t, centro, inicio.y + 0.005f);
            SemColisao(t);
            andado += comp;
        }
        return andado;
    }

    static bool ColunaComFolga(Vector3 pos, Transform pai, Quaternion rot)
    {
        RaycastHit h;
        if (!Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out h, 1.0f, ~0, QueryTriggerInteraction.Ignore)) return false;
        // coluna (1,7 m) + 1,2 m de passagem em volta + altura até o teto
        if (Physics.CheckBox(h.point + Vector3.up * 2.6f, new Vector3(2.0f, 2.4f, 2.0f), rot, ~0, QueryTriggerInteraction.Ignore)) return false;
        return Colocar(Colunas, h.point, pai, 1f, 1f, 1.8f, rot, true) != null;
    }

    // ---------- Procurar lugares ----------

    static List<Ponto> ProcurarPontos(Bounds b)
    {
        var lista = new List<Ponto>();
        Vector3 dirX = templo.right; dirX.y = 0; dirX.Normalize();
        Vector3 dirZ = templo.forward; dirZ.y = 0; dirZ.Normalize();
        if (dirX.sqrMagnitude < 0.5f) { dirX = Vector3.right; dirZ = Vector3.forward; }
        Vector3[] dirs = { dirX, -dirX, dirZ, -dirZ };
        dirsTemplo = dirs;

        float topo = b.max.y + 2f;

        for (float x = b.min.x; x <= b.max.x; x += Passo)
        for (float z = b.min.z; z <= b.max.z; z += Passo)
        {
            float jx = ((float)rnd.NextDouble() - 0.5f) * Passo * 0.6f;
            float jz = ((float)rnd.NextDouble() - 0.5f) * Passo * 0.6f;
            Vector3 origem = new Vector3(x + jx, topo, z + jz);

            var hits = Physics.RaycastAll(origem, Vector3.down, b.size.y + 10f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.normal.y < 0.8f || !EhDoTemplo(h.collider)) continue;

                Vector3 p = h.point;
                RaycastHit cima;
                bool temTeto = Physics.Raycast(p + Vector3.up * 0.05f, Vector3.up, out cima, 30f, ~0, QueryTriggerInteraction.Ignore);
                if (!temTeto) continue;                           // telhado / fora: não decora
                if (cima.distance < 2.1f) continue;               // não cabe gente em pé
                if (!Continuo(p, dirs)) continue;                 // topo de parede / beirada fina

                float[] d = new float[4];
                RaycastHit[] hp = new RaycastHit[4];
                for (int i = 0; i < 4; i++)
                {
                    float d1 = 99f, d2 = 99f;
                    RaycastHit h1, h2;
                    if (Physics.Raycast(p + Vector3.up * 0.5f, dirs[i], out h1, 6f, ~0, QueryTriggerInteraction.Ignore)) d1 = h1.distance; else h1 = default;
                    if (Physics.Raycast(p + Vector3.up * 1.6f, dirs[i], out h2, 6f, ~0, QueryTriggerInteraction.Ignore)) d2 = h2.distance; else h2 = default;
                    d[i] = Mathf.Min(d1, d2);
                    hp[i] = d1 <= d2 ? h1 : h2;
                }

                int perto = 0, maisPerto = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (d[i] < 1.0f) perto++;
                    if (d[i] < d[maisPerto]) maisPerto = i;
                }
                bool cantoX = d[0] < 1.0f || d[1] < 1.0f;
                bool cantoZ = d[2] < 1.0f || d[3] < 1.0f;
                bool estreito = (d[0] + d[1] < 3.2f) || (d[2] + d[3] < 3.2f);

                var pt = new Ponto { pos = p, distParede = d[maisPerto] };
                pt.paraDentro = -dirs[maisPerto];
                pt.normalParede = hp[maisPerto].collider != null ? hp[maisPerto].normal : -dirs[maisPerto];
                pt.pontoParede = hp[maisPerto].collider != null ? hp[maisPerto].point : p + dirs[maisPerto] * d[maisPerto];
                pt.pontoParede.y = p.y;

                if (estreito) pt.tipo = Tipo.Estreito;          // corredor, porta, escada: deixa livre
                else if (cantoX && cantoZ)
                {
                    pt.tipo = Tipo.Canto;
                    Vector3 v = (d[0] < d[1] ? -dirs[0] : -dirs[1]) + (d[2] < d[3] ? -dirs[2] : -dirs[3]);
                    pt.paraDentro = v.normalized;
                }
                else if (perto > 0) pt.tipo = Tipo.Parede;
                else pt.tipo = Tipo.Aberto;

                lista.Add(pt);
            }
        }
        return lista;
    }

    // O chão continua para pelo menos um dos lados em cada eixo? (topo de parede é fino nos dois lados)
    static bool Continuo(Vector3 p, Vector3[] dirs)
    {
        return (ChaoNaMesmaAltura(p, dirs[0]) || ChaoNaMesmaAltura(p, dirs[1]))
            && (ChaoNaMesmaAltura(p, dirs[2]) || ChaoNaMesmaAltura(p, dirs[3]));
    }

    static bool ChaoNaMesmaAltura(Vector3 p, Vector3 dir)
    {
        RaycastHit h;
        Vector3 origem = p + dir * 0.7f + Vector3.up * 0.3f;
        return Physics.Raycast(origem, Vector3.down, out h, 0.6f, ~0, QueryTriggerInteraction.Ignore)
               && Mathf.Abs(h.point.y - p.y) < 0.3f;
    }

    static bool EhDoTemplo(Collider c) { return c != null && c.transform.IsChildOf(templo); }

    // ---------- Colocar coisas ----------

    static void ColocarTocha(Ponto p, Transform pai)
    {
        Vector3 pos = p.pos + p.paraDentro * 0.55f;
        var go = Colocar(tochaDoTemplo, pos, pai, 1f, 1f, 0.6f, Quaternion.LookRotation(p.paraDentro), true);
        if (go == null) return;
        tochasColocadas.Add(new Ocupado { pos = pos, raio = EspacoTochas * 0.5f });

        Bounds b = LimitesDe(go);
        AdicionarFogo(go, new Vector3(b.center.x, b.max.y - 0.05f, b.center.z), p.paraDentro * 0.2f, 1f, true);
    }

    static void ColocarBraseiro(Vector3 pos, Transform pai, float escala = 1.15f, bool checarEspaco = true)
    {
        var go = Colocar(Braseiros, pos, pai, escala, escala, checarEspaco ? 0.9f : 0f, null, true);
        if (go == null) return;
        go.name = "Braseiro";
        braseirosColocados.Add(new Ocupado { pos = pos, raio = EspacoBraseiros * 0.5f });
        Bounds b = LimitesDe(go);
        AdicionarFogo(go, new Vector3(b.center.x, b.max.y - 0.08f, b.center.z), Vector3.zero, 1.4f * escala, true);
    }

    static void GrupoDeVasos(Vector3 centro, Vector3 paraDentro, Transform pai)
    {
        if (!Livre(centro, 0.9f)) return;
        Reservar(centro, 0.9f);
        int n = rnd.Next(2, 4);
        Vector3 lado = Vector3.Cross(Vector3.up, paraDentro).normalized;
        for (int i = 0; i < n; i++)
        {
            Vector3 pos = centro + lado * ((i - (n - 1) * 0.5f) * 0.55f) + paraDentro * ((float)rnd.NextDouble() * 0.3f);
            Colocar(Vasos, pos, pai, 0.9f, 1.2f, 0f, null, true);
        }
    }

    // Grupinho de velas acesas no chão
    static void Velas_(Vector3 centro, Transform pai, int quantas)
    {
        for (int i = 0; i < quantas; i++)
        {
            double a = rnd.NextDouble() * System.Math.PI * 2, r = 0.1 + rnd.NextDouble() * 0.3;
            Vector3 pos = centro + new Vector3((float)(System.Math.Cos(a) * r), 0f, (float)(System.Math.Sin(a) * r));
            RaycastHit h;
            if (!Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out h, 1.0f, ~0, QueryTriggerInteraction.Ignore)) continue;
            Vela(h.point, pai, 0.9f + (float)rnd.NextDouble() * 0.6f);
        }
    }

    static void Vela(Vector3 chao, Transform pai, float escala)
    {
        var prefab = Sortear(Velas);
        if (prefab == null) return;
        var v = Instanciar(prefab, pai);
        v.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
        v.transform.localScale = Vector3.Scale(v.transform.localScale, Vector3.one * escala);
        Assentar(v, chao, chao.y);
        SemColisao(v);
        Bounds b = LimitesDe(v);
        AdicionarFogo(v, new Vector3(b.center.x, b.max.y, b.center.z), Vector3.zero, 0.22f, false);
    }

    // Coloca algo encostado na parede, virado para dentro, com a base "altura" acima do chão
    static GameObject NaParede(string[] opcoes, Ponto p, float altura, Transform pai, bool solido)
    {
        Vector3 n = p.normalParede; n.y = 0;
        if (n.sqrMagnitude < 0.5f) return null;
        n.Normalize();

        var prefab = Sortear(opcoes);
        if (prefab == null) return null;

        // a parede precisa existir em toda a altura do objeto
        Vector3 naParede = new Vector3(p.pontoParede.x, p.pos.y, p.pontoParede.z);
        foreach (float y in new[] { altura + 0.3f, altura + 1.5f, altura + 3.0f })
        {
            if (y > altura + 1.5f && !solido) break;
            if (!Physics.Raycast(naParede + n * 0.6f + Vector3.up * y, -n, 1.2f, ~0, QueryTriggerInteraction.Ignore)) return null;
        }

        var go = Instanciar(prefab, pai);
        go.transform.rotation = Quaternion.identity;
        go.transform.position = Vector3.zero;
        Bounds b0 = LimitesDe(go);
        // o lado mais fino do objeto fica de costas para a parede
        Quaternion virar = b0.size.x < b0.size.z ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity;
        go.transform.rotation = Quaternion.LookRotation(n) * virar;
        go.transform.position = naParede;

        Physics.SyncTransforms();
        Bounds b = LimitesDe(go);
        float tras = Vector3.Dot(b.center - naParede, n) - Extensao(b, n);
        go.transform.position += -n * tras + n * 0.02f;
        b = LimitesDe(go);
        go.transform.position += Vector3.up * (p.pos.y + altura - b.min.y);

        // não pode atravessar parede do lado nem teto
        b = LimitesDe(go);
        if (b.max.y > p.pos.y + 0.3f)
        {
            RaycastHit teto;
            if (Physics.Raycast(p.pos + Vector3.up * 0.1f, Vector3.up, out teto, 30f, ~0, QueryTriggerInteraction.Ignore) &&
                teto.point.y < b.max.y + 0.1f)
            {
                Undo.DestroyObjectImmediate(go); contagem--;
                return null;
            }
        }

        if (solido) GarantirColisao(go); else SemColisao(go);
        return go;
    }

    static GameObject Colocar(string[] opcoes, Vector3 pos, Transform pai, float escMin, float escMax,
                              float raio, Quaternion? rotacao, bool solido)
    {
        if (raio > 0f && !Livre(pos, raio)) return null;

        var prefab = Sortear(opcoes);
        if (prefab == null) return null;

        RaycastHit h;
        if (!Physics.Raycast(pos + Vector3.up * 0.6f, Vector3.down, out h, 1.5f, ~0, QueryTriggerInteraction.Ignore) || h.normal.y < 0.7f)
            return null;
        pos = h.point;

        var go = Instanciar(prefab, pai);
        go.transform.rotation = rotacao ?? Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
        float esc = Mathf.Lerp(escMin, escMax, (float)rnd.NextDouble());
        go.transform.localScale = Vector3.Scale(go.transform.localScale, Vector3.one * esc);
        Assentar(go, pos, pos.y);

        // Objeto sólido não pode entrar em parede / escada / outro objeto
        Physics.SyncTransforms();
        if (BateEmAlgo(go))
        {
            Undo.DestroyObjectImmediate(go);
            contagem--;
            return null;
        }
        if (solido) GarantirColisao(go);

        if (raio > 0f) Reservar(pos, raio);
        return go;
    }

    // Move o objeto para que o meio dele fique em "centro" (no plano) e a base encoste em "chaoY"
    static void Assentar(GameObject go, Vector3 centro, float chaoY)
    {
        Bounds b = LimitesDe(go);
        Vector3 desloc = new Vector3(centro.x - b.center.x, chaoY - b.min.y, centro.z - b.center.z);
        go.transform.position += desloc;
    }

    static GameObject Instanciar(GameObject prefab, Transform pai)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(go, "Decorar Templo_Yas");
        go.transform.SetParent(pai, false);
        // Sem "Static": alguns têm fogo / podem ser movidos depois
        contagem++;
        return go;
    }

    static bool BateEmAlgo(GameObject go)
    {
        Bounds b = LimitesDe(go);
        Vector3 meio = b.extents * 0.85f;
        Vector3 centro = b.center + Vector3.up * 0.08f;   // um pouquinho acima do chão
        foreach (var c in Physics.OverlapBox(centro, meio, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.transform.IsChildOf(go.transform)) continue;
            return true;
        }
        return false;
    }

    static void SemColisao(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            Undo.RecordObject(c, "Decorar Templo_Yas");
            c.enabled = false;
        }
    }

    // Prefabs sem colisão (coluna, braseiro, painel, tablado) ganham MeshCollider
    static void GarantirColisao(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
            mc.sharedMesh = mf.sharedMesh;
        }
        Physics.SyncTransforms();
    }

    // ---------- Fogo ----------

    // Fogo de partículas + (se pedido e ainda houver "vaga") uma luz que treme
    static void AdicionarFogo(GameObject dono, Vector3 ponto, Vector3 deslocLuz, float tamanho, bool comLuz)
    {
        var fogoGo = new GameObject("Fogo");
        Undo.RegisterCreatedObjectUndo(fogoGo, "Decorar Templo_Yas");
        fogoGo.transform.SetParent(dono.transform, false);
        fogoGo.transform.position = ponto;
        fogoGo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        Vector3 s = dono.transform.lossyScale;
        fogoGo.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));
        var ps = fogoGo.AddComponent<ParticleSystem>();
        ConfigurarFogo(ps, tamanho);

        Light luz = null;
        if (comLuz && luzes < MaxLuzes)
        {
            var luzGo = new GameObject("Luz");
            Undo.RegisterCreatedObjectUndo(luzGo, "Decorar Templo_Yas");
            luzGo.transform.SetParent(dono.transform, false);
            luzGo.transform.position = ponto + Vector3.up * 0.3f + deslocLuz;
            luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.62f, 0.25f);
            luz.intensity = 2.2f;
            luz.range = 9f;
            luz.shadows = LightShadows.None;
            luzes++;
        }

        var chama = dono.AddComponent<ChamaTocha>();
        chama.luz = luz;
        chama.fogo = ps;
        if (!comLuz) chama.distanciaMaxima = 25f;   // velinhas: só de perto
    }

    static void ConfigurarFogo(ParticleSystem ps, float tamanho)
    {
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f * Mathf.Sqrt(tamanho), 0.9f * Mathf.Sqrt(tamanho));
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f * tamanho, 0.38f * tamanho);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.45f, 0.1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = tamanho < 0.5f ? 12 : 60;
        main.gravityModifier = -0.05f;

        var em = ps.emission;
        em.rateOverTime = tamanho < 0.5f ? 10f : 28f * Mathf.Min(tamanho, 1.6f);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.07f * tamanho;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.6f), new GradientColorKey(new Color(0.4f, 0.1f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = MaterialFogo();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    static Material MaterialFogo()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(CaminhoFogoMat);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        mat = new Material(shader) { name = "FogoTocha" };

        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex; }
        mat.SetColor("_BaseColor", Color.white);

        // Transparente aditivo (brilha)
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (!AssetDatabase.IsValidFolder(PastaMateriais))
            AssetDatabase.CreateFolder("Assets/IMPORTS/Templo_Yas", "Decoracao");
        AssetDatabase.CreateAsset(mat, CaminhoFogoMat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // ---------- Utilidades ----------

    static Transform AcharTemplo()
    {
        return AcharNaCena.Objeto(NomeTemplo);
    }

    static GameObject AcharGrupo()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!EditorUtility.IsPersistent(t) && t.name == NomeGrupo) return t.gameObject;
        return null;
    }

    static Transform Sub(string nome)
    {
        var go = new GameObject(nome);
        Undo.RegisterCreatedObjectUndo(go, "Decorar Templo_Yas");
        go.transform.SetParent(grupo, false);
        return go.transform;
    }

    static GameObject Sortear(string[] caminhos)
    {
        for (int tentativa = 0; tentativa < 4; tentativa++)
        {
            string c = caminhos[rnd.Next(caminhos.Length)];
            GameObject p;
            if (!cache.TryGetValue(c, out p))
            {
                p = AssetDatabase.LoadAssetAtPath<GameObject>(c);
                cache[c] = p;
                if (p == null) Debug.LogWarning("Decorar Templo_Yas: não achei " + c);
                else ColocarBaus.ConverterMateriais(p, false);   // material antigo → URP (senão fica rosa)
            }
            if (p != null) return p;
        }
        return null;
    }

    static bool Livre(Vector3 pos, float raio) { return Livre(ocupados, pos, raio); }

    static bool Livre(List<Ocupado> lista, Vector3 pos, float raio)
    {
        foreach (var o in lista)
        {
            if (Mathf.Abs(o.pos.y - pos.y) > 2.5f) continue;   // outro andar
            float d = Vector2.Distance(new Vector2(o.pos.x, o.pos.z), new Vector2(pos.x, pos.z));
            if (d < raio + o.raio) return false;
        }
        return true;
    }

    static void Reservar(Vector3 pos, float raio) { ocupados.Add(new Ocupado { pos = pos, raio = raio }); }

    static bool Chance(float p) { return rnd.NextDouble() < p; }

    static void Embaralhar<T>(List<T> l)
    {
        for (int i = l.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; }
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
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool tem = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
        }
        if (!tem) b = new Bounds(go.transform.position, Vector3.one * 0.3f);
        return b;
    }

    // meia-largura de uma caixa (alinhada aos eixos) numa direção
    static float Extensao(Bounds b, Vector3 dir)
    {
        return Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.y) * b.extents.y + Mathf.Abs(dir.z) * b.extents.z;
    }
}
#endif

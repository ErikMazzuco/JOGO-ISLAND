#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Menu:  ISLAND > Noite > Preparar Carta e Noite da Caveira
///        ISLAND > Noite > Anoitecer Agora (no Play)
///        ISLAND > Noite > Remover
///
/// Monta o "easter egg" do laguinho:
///   - uma carta enrolada (papel envelhecido + fita vermelha) ao lado da caveira (Skull_01);
///   - o objeto NOITE_MACABRA, que faz a noite cair quando a carta é lida;
///   - a névoa volumétrica (Pure Volumetric Fog), desligada de dia.
/// O jogo continua começando de dia. Ctrl+Z desfaz. Rodar de novo refaz.
/// </summary>
public static class PrepararNoite
{
    const string NomeGrupo = "NOITE_MACABRA";
    const string NomeCaveira = "Skull_01";
    const string Pasta = "Assets/IMPORTS/NOITE";

    const string CaminhoCeu = "Assets/Fantasy Skybox FREE/Panoramics/FS013/FS013_Night.mat";
    const string CaminhoEsqueleto = "Assets/INIMIGO.prefab";
    const string CaminhoGrito = "Assets/SONGS/INIMIGOS/MONSTER SCREAM.mp3";
    const string CaminhoMusica = "Assets/SONGS/MENU/SOM FUNDO MISTERIO.mp3";
    const string CaminhoPapelSom = "Assets/SONGS/MENU/CLICK.mp3";
    const string CaminhoSomCarta = "Assets/SONGS/NOITE/I SEE YOU.mp3";

    [MenuItem("ISLAND/Noite/Preparar Carta e Noite da Caveira")]
    static void Preparar()
    {
        var caveira = AcharNaCena.Objeto(NomeCaveira);
        if (caveira == null) { Aviso("Não achei a caveira (" + NomeCaveira + ") na cena aberta."); return; }

        Undo.SetCurrentGroupName("Preparar Noite");
        int undo = Undo.GetCurrentGroup();

        var antigo = GameObject.Find(NomeGrupo);
        if (antigo != null) Undo.DestroyObjectImmediate(antigo);

        var cena = caveira.gameObject.scene;
        var raiz = new GameObject(NomeGrupo);
        if (raiz.scene != cena) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, cena);
        Undo.RegisterCreatedObjectUndo(raiz, "Preparar Noite");

        // ---------- Controlador da noite ----------
        var noite = raiz.AddComponent<NoiteMacabra>();
        noite.ceuNoite = AssetDatabase.LoadAssetAtPath<Material>(CaminhoCeu);
        noite.prefabEsqueleto = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoEsqueleto);
        noite.somGrito = null;   // sem grito: só o "I see you" da carta e depois a música de terror
        noite.musicaNoite = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoMusica);
        noite.caveira = caveira;

        // ---------- Névoa volumétrica (desligada de dia) ----------
        string avisoNevoa = "";
        var tipoNevoa = AcharTipo("BKPureNature.PureVolumetricFog");
        if (tipoNevoa != null)
        {
            var nevoa = new GameObject("Nevoa Volumetrica (noite)");
            Undo.RegisterCreatedObjectUndo(nevoa, "Preparar Noite");
            nevoa.transform.SetParent(raiz.transform, false);
            nevoa.SetActive(false);
            var comp = nevoa.AddComponent(tipoNevoa);
            var so = new SerializedObject(comp);
            Definir(so, "density", 0.05f);           // bem densa perto do chão
            Definir(so, "groundFogHeight", 7f);      // até ~7 m acima do terreno
            Definir(so, "topSoftness", 0.6f);
            Definir(so, "terrainFollow", 0.95f);
            Definir(so, "noiseAmount", 0.55f);       // em "manchas", não um muro liso
            Definir(so, "noiseCoverage", 0.55f);
            Definir(so, "largeNoiseScale", 120f);
            Definir(so, "detailNoiseScale", 25f);
            Definir(so, "windSpeed", 2.5f);
            Definir(so, "targetStepLength", 6f);
            Definir(so, "maximumFogDistance", 450f);
            Definir(so, "nearFadeDistance", 4f);
            Definir(so, "sunStrength", 0.35f);
            Definir(so, "sunScattering", 0.25f);
            so.ApplyModifiedPropertiesWithoutUndo();
            noite.nevoaVolumetrica = nevoa;
        }
        else avisoNevoa = "\n(Pure Volumetric Fog não encontrada: fica só a névoa normal da Unity.)";

        // ---------- Carta enrolada ao lado da caveira ----------
        var carta = CriarCarta(raiz.transform, caveira);

        // ---------- Build: não remover os shaders de névoa ----------
        // No modo "Automatic" a Unity tira do jogo exportado a névoa que nenhuma cena usa desde o início.
        // Aqui ela só liga quando a carta é lida, então fica em "Custom" mantendo os três tipos.
        ManterNevoaNoBuild();

        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(cena);
        Selection.activeGameObject = carta != null ? carta : raiz;

        Aviso("Pronto!\n\n• Carta enrolada ao lado da caveira (\"[E] Ler a carta\").\n" +
              "• Ao fechar a carta: \"I see you\", céu de lua vermelha, névoa, música de terror, olhos da caveira acesos e esqueletos saindo do chão." +
              avisoNevoa + "\n\nPara ver sem ir até o lago: aperte Play e use  ISLAND > Noite > Anoitecer Agora.\n\nSalve a cena (Ctrl+S).");
    }

    [MenuItem("ISLAND/Noite/Anoitecer Agora (no Play)")]
    static void AnoitecerAgora()
    {
        if (!EditorApplication.isPlaying) { Aviso("Aperte Play primeiro."); return; }
        var n = Object.FindFirstObjectByType<NoiteMacabra>();
        if (n == null) { Aviso("Não há NOITE_MACABRA na cena. Rode \"Preparar Carta e Noite da Caveira\" antes."); return; }
        n.Anoitecer();
    }

    [MenuItem("ISLAND/Noite/Levar Jogador até a Carta (no Play)")]
    static void LevarJogador()
    {
        if (!EditorApplication.isPlaying) { Aviso("Aperte Play primeiro."); return; }
        var carta = Object.FindFirstObjectByType<CartaMisteriosa>();
        var jogador = Object.FindFirstObjectByType<PlayerMovement>();
        if (carta == null || jogador == null) { Aviso("Não achei a carta ou o PLAYER."); return; }

        var cc = jogador.GetComponent<CharacterController>();
        float baseCC = cc != null ? (cc.center.y - Mathf.Max(cc.height * 0.5f, cc.radius)) * jogador.transform.lossyScale.y : 0f;

        // procura chão a ~1,3 m da carta, tentando 8 direções
        Vector3 c = carta.transform.position;
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
            RaycastHit h;
            if (!Physics.Raycast(c + dir * 1.3f + Vector3.up * 3f, Vector3.down, out h, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (h.collider.transform.IsChildOf(jogador.transform)) continue;
            if (cc != null) cc.enabled = false;
            jogador.transform.position = h.point + Vector3.up * (0.05f - baseCC);
            Vector3 olhar = c - h.point; olhar.y = 0f;
            if (olhar.sqrMagnitude > 0.01f) jogador.transform.rotation = Quaternion.LookRotation(olhar);
            if (cc != null) cc.enabled = true;
            Selection.activeGameObject = jogador.gameObject;
            Debug.Log("Jogador ao lado da carta. Clique no Game e aperte E.");
            return;
        }
        Aviso("Não achei chão perto da carta.");
    }

    [MenuItem("ISLAND/Noite/Remover")]
    static void Remover()
    {
        var g = GameObject.Find(NomeGrupo);
        if (g == null) { Aviso("Não há nada para remover."); return; }
        var cena = g.scene;
        Undo.DestroyObjectImmediate(g);
        EditorSceneManager.MarkSceneDirty(cena);
    }

    // ---------- Carta ----------

    static GameObject CriarCarta(Transform pai, Transform caveira)
    {
        Bounds b = Limites(caveira);
        Vector3[] lados = { caveira.right, -caveira.right, caveira.forward, -caveira.forward };

        Vector3 chao = Vector3.zero, ladoEscolhido = caveira.right;
        bool achou = false;
        foreach (var l in lados)
        {
            Vector3 lado = l; lado.y = 0; if (lado.sqrMagnitude < 0.01f) continue; lado.Normalize();
            Vector3 p = b.center + lado * (Mathf.Max(b.extents.x, b.extents.z) + 0.45f);
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(caveira.parent != null ? caveira.parent : caveira)) continue;   // caveira/espada
                if (h.normal.y < 0.6f) continue;
                if (!achou || h.point.y > chao.y) { chao = h.point; ladoEscolhido = lado; achou = true; }
            }
            if (achou) break;
        }
        if (!achou) { chao = new Vector3(b.center.x, b.min.y, b.center.z) + caveira.right * (b.extents.x + 0.45f); }

        var carta = new GameObject("Carta Enrolada");
        Undo.RegisterCreatedObjectUndo(carta, "Preparar Noite");
        carta.transform.SetParent(pai, false);
        carta.transform.position = chao;
        // deitada, meio torta, como se tivesse sido largada ali
        carta.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, ladoEscolhido)) * Quaternion.Euler(0f, 23f, 0f);

        var leitura = carta.AddComponent<CartaMisteriosa>();
        leitura.somPapel = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoPapelSom);
        leitura.somAoTerminar = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoSomCarta);

        const float raio = 0.06f, comprimento = 0.42f;
        var matPapel = Material("Papel Envelhecido", new Color(0.86f, 0.77f, 0.58f), 0.15f);
        var matBorda = Material("Papel Borda", new Color(0.70f, 0.56f, 0.36f), 0.1f);
        var matFita = Material("Fita Vermelha", new Color(0.55f, 0.06f, 0.05f), 0.35f);

        // rolo de papel (deitado ao longo do eixo X local)
        Peca(carta.transform, "Papel", PrimitiveType.Cylinder, matPapel,
             new Vector3(0f, raio, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(raio * 2f, comprimento * 0.5f, raio * 2f));
        // pontas um pouco mais escuras (papel enrolado)
        for (int s = -1; s <= 1; s += 2)
            Peca(carta.transform, "Ponta", PrimitiveType.Cylinder, matBorda,
                 new Vector3(s * comprimento * 0.5f, raio, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(raio * 2.06f, 0.006f, raio * 2.06f));
        // fita vermelha amarrada no meio
        Peca(carta.transform, "Fita", PrimitiveType.Cylinder, matFita,
             new Vector3(0.03f, raio, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(raio * 2.25f, 0.02f, raio * 2.25f));
        // laço caído da fita
        Peca(carta.transform, "Laço", PrimitiveType.Cube, matFita,
             new Vector3(0.03f, 0.012f, raio * 1.6f), Quaternion.Euler(0f, 25f, 0f), new Vector3(0.025f, 0.01f, 0.09f));
        // aba do papel aparecendo
        Peca(carta.transform, "Aba", PrimitiveType.Cube, matPapel,
             new Vector3(-0.02f, 0.006f, -raio * 1.3f), Quaternion.Euler(0f, -8f, 0f), new Vector3(comprimento * 0.85f, 0.004f, 0.07f));

        return carta;
    }

    static void Peca(Transform pai, string nome, PrimitiveType tipo, Material mat, Vector3 pos, Quaternion rot, Vector3 esc)
    {
        var go = GameObject.CreatePrimitive(tipo);
        Undo.RegisterCreatedObjectUndo(go, "Preparar Noite");
        go.name = nome;
        Object.DestroyImmediate(go.GetComponent<Collider>());   // a carta é lida por distância
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = esc;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material Material(string nome, Color cor, float brilho)
    {
        if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets/IMPORTS", "NOITE");
        string caminho = Pasta + "/" + nome + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(shader);
        m.SetColor("_BaseColor", cor);
        m.color = cor;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", brilho);
        AssetDatabase.CreateAsset(m, caminho);
        return m;
    }

    // ---------- Utilidades ----------

    static void ManterNevoaNoBuild()
    {
        var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (gs == null || gs.Length == 0) return;
        var so = new SerializedObject(gs[0]);
        var modo = so.FindProperty("m_FogStripping");
        if (modo == null) return;
        modo.intValue = 1;   // Custom
        foreach (var nome in new[] { "m_FogKeepLinear", "m_FogKeepExp", "m_FogKeepExp2" })
        {
            var p = so.FindProperty(nome);
            if (p != null) p.boolValue = true;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    static void Definir(SerializedObject so, string campo, float valor)
    {
        var p = so.FindProperty(campo);
        if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = valor;
        else Debug.LogWarning("Névoa: campo não encontrado: " + campo);
    }

    static Type AcharTipo(string nome)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType(nome);
            if (t != null) return t;
        }
        return null;
    }

    static Bounds Limites(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.3f);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void Aviso(string msg) { EditorUtility.DisplayDialog("Noite da Caveira", msg, "OK"); }
}
#endif

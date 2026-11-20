using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ISLAND > Gerar LOD das Árvores (Terreno + Cena)
// ISLAND > Gerar LOD dos Prefabs Selecionados
//
// Para cada árvore:
//  1) tira uma "foto" dela de frente e de lado (fundo preto e branco para descobrir a transparência);
//  2) monta um modelo barato: dois planos cruzados com essa foto (alpha clip);
//  3) cria uma VARIANTE do prefab (o original NÃO é alterado) com um LOD Group:
//       LOD0 = árvore completa (perto) | LOD1 = planos cruzados (médio/longe) | depois some;
//  4) troca os prefabs na lista de árvores dos terrenos (as árvores pintadas continuam onde estão)
//     e as instâncias soltas na cena aberta.
// Tudo é salvo em Assets/ARVORES_LOD.
public static class GerarLODArvores
{
    const string Pasta = "Assets/ARVORES_LOD";
    const int Celula = 256;                 // resolução de cada foto (frente e lado)
    const int CamadaTemp = 31;              // camada usada só durante a foto
    const float LimiteLOD0 = 0.12f;         // abaixo disso (altura na tela) troca para os planos
    const float LimiteSumir = 0.01f;        // abaixo disso a árvore some
    const float Brilho = 0.95f;             // ajuste de cor dos planos (1 = igual à foto)
    static readonly Vector3 PosCaptura = new Vector3(0f, 10000f, 0f);

    [MenuItem("ISLAND/Gerar LOD das Árvores (Terreno + Cena)")]
    static void GerarDoTerreno()
    {
        var fontes = new List<GameObject>();

        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.terrainData == null) continue;
            foreach (var p in t.terrainData.treePrototypes)
            {
                if (p.prefab != null && !fontes.Contains(p.prefab))
                    fontes.Add(p.prefab);
            }
        }

        if (fontes.Count == 0)
        {
            EditorUtility.DisplayDialog("Nenhuma árvore", "Não achei árvores nos terrenos da cena aberta.\nAbra a cena Island e tente de novo.", "OK");
            return;
        }

        Executar(fontes);
    }

    [MenuItem("ISLAND/Gerar LOD dos Prefabs Selecionados")]
    static void GerarDosSelecionados()
    {
        var fontes = new List<GameObject>();
        foreach (var go in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
        {
            if (go != null && !fontes.Contains(go)) fontes.Add(go);
        }

        if (fontes.Count == 0)
        {
            EditorUtility.DisplayDialog("Nada selecionado", "Selecione prefabs de árvore na janela Project e tente de novo.", "OK");
            return;
        }

        Executar(fontes);
    }

    // ------------------------------------------------------------------------------------------

    static void Executar(List<GameObject> fontes)
    {
        var mapa = new Dictionary<GameObject, GameObject>();
        var pulados = new List<string>();
        int criados = 0, reaproveitados = 0;

        try
        {
            for (int i = 0; i < fontes.Count; i++)
            {
                var fonte = fontes[i];
                EditorUtility.DisplayProgressBar("Gerando LOD das árvores", fonte.name, (float)i / fontes.Count);

                string motivo;
                bool novo;
                var variante = ProcessarFonte(fonte, out motivo, out novo);

                if (variante == null)
                {
                    pulados.Add(fonte.name + ": " + motivo);
                    continue;
                }

                mapa[fonte] = variante;
                if (novo) criados++; else reaproveitados++;
            }

            EditorUtility.DisplayProgressBar("Gerando LOD das árvores", "Trocando nos terrenos e na cena...", 0.95f);

            int trocasTerreno = TrocarNosTerrenos(mapa);
            int trocasCena = TrocarInstanciasNaCena(mapa);

            if (trocasTerreno > 0 || trocasCena > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var msg = new StringBuilder();
            msg.AppendLine("Árvores com LOD novo: " + criados);
            if (reaproveitados > 0) msg.AppendLine("Já existiam (reaproveitadas): " + reaproveitados);
            msg.AppendLine("Trocadas na lista dos terrenos: " + trocasTerreno);
            msg.AppendLine("Prefabs soltos trocados na cena: " + trocasCena);

            if (pulados.Count > 0)
            {
                msg.AppendLine();
                msg.AppendLine("Puladas (" + pulados.Count + "):");
                foreach (var p in pulados) msg.AppendLine("- " + p);
            }

            msg.AppendLine();
            msg.AppendLine("Salve a cena (Ctrl+S).");

            Debug.Log("[LOD Árvores]\n" + msg);
            EditorUtility.DisplayDialog("LOD das árvores", msg.ToString(), "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ------------------------------------------------------------------------------------------

    static GameObject ProcessarFonte(GameObject fonte, out string motivo, out bool novo)
    {
        motivo = "";
        novo = false;

        string caminhoFonte = AssetDatabase.GetAssetPath(fonte);
        if (string.IsNullOrEmpty(caminhoFonte))
        {
            motivo = "não é um arquivo do projeto";
            return null;
        }

        if (caminhoFonte.StartsWith(Pasta))
        {
            motivo = "já é uma versão com LOD";
            return null;
        }

        if (fonte.GetComponentInChildren<LODGroup>(true) != null)
        {
            motivo = "já tem LOD Group";
            return null;
        }

        string guid = AssetDatabase.AssetPathToGUID(caminhoFonte);
        string nome = Limpar(fonte.name) + "_" + guid.Substring(0, 4);
        string caminhoPrefab = Pasta + "/" + nome + "_LOD.prefab";

        var existente = AssetDatabase.LoadAssetAtPath<GameObject>(caminhoPrefab);
        if (existente != null) return existente;

        if (!AssetDatabase.IsValidFolder(Pasta))
            AssetDatabase.CreateFolder("Assets", "ARVORES_LOD");

        // 1) Fotos
        Bounds limites;
        float lado;
        Texture2D atlas;
        if (!Capturar(fonte, out limites, out lado, out atlas, out motivo))
            return null;

        string caminhoTex = Pasta + "/" + nome + "_atlas.png";
        File.WriteAllBytes(caminhoTex, atlas.EncodeToPNG());
        Object.DestroyImmediate(atlas);
        AssetDatabase.ImportAsset(caminhoTex);

        var importador = AssetImporter.GetAtPath(caminhoTex) as TextureImporter;
        if (importador != null)
        {
            importador.textureType = TextureImporterType.Default;
            importador.sRGBTexture = true;
            importador.alphaSource = TextureImporterAlphaSource.FromInput;
            importador.alphaIsTransparency = true;
            importador.mipmapEnabled = true;
            importador.mipMapsPreserveCoverage = true;
            importador.alphaTestReferenceValue = 0.5f;
            importador.wrapMode = TextureWrapMode.Clamp;
            importador.filterMode = FilterMode.Bilinear;
            importador.maxTextureSize = 512;
            importador.SaveAndReimport();
        }

        var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(caminhoTex);

        // 2) Material (Unlit com alpha clip: a foto já tem a luz "assada")
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            motivo = "shader Universal Render Pipeline/Unlit não encontrado";
            return null;
        }

        var material = new Material(shader);
        material.SetTexture("_BaseMap", textura);
        material.SetColor("_BaseColor", new Color(Brilho, Brilho, Brilho, 1f));
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.5f);
        material.SetFloat("_Cull", 0f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;
        AssetDatabase.CreateAsset(material, Pasta + "/" + nome + "_billboard.mat");

        // 3) Variante do prefab com LOD Group
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fonte);
        inst.transform.SetPositionAndRotation(PosCaptura, Quaternion.identity);

        var renderersLOD0 = new List<Renderer>();
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer) renderersLOD0.Add(r);
        }

        var malha = CriarMalha(inst.transform, limites, lado);
        malha.name = nome + "_billboard";
        AssetDatabase.CreateAsset(malha, Pasta + "/" + nome + "_billboard.asset");

        var filho = new GameObject("LOD1_Planos");
        filho.transform.SetParent(inst.transform, false);
        var mf = filho.AddComponent<MeshFilter>();
        mf.sharedMesh = malha;
        var mr = filho.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        var lod = inst.AddComponent<LODGroup>();
        lod.fadeMode = LODFadeMode.None;
        lod.SetLODs(new[]
        {
            new LOD(LimiteLOD0, renderersLOD0.ToArray()),
            new LOD(LimiteSumir, new Renderer[] { mr })
        });
        lod.RecalculateBounds();

        // Volta o objeto para a posição/rotação do original (assim não vira "override" no prefab)
        inst.transform.SetPositionAndRotation(fonte.transform.position, fonte.transform.rotation);

        var prefab = PrefabUtility.SaveAsPrefabAsset(inst, caminhoPrefab);
        Object.DestroyImmediate(inst);

        if (prefab == null)
        {
            motivo = "não consegui salvar o prefab";
            return null;
        }

        novo = true;
        return prefab;
    }

    // ------------------------------------------------------------------------------------------
    // Fotos: 2 vistas (frente e lado) x 2 fundos (preto e branco). Pela diferença entre os fundos
    // descobrimos a transparência, sem depender do que o URP faz com o canal alfa.
    static bool Capturar(GameObject fonte, out Bounds limites, out float lado, out Texture2D atlas, out string motivo)
    {
        motivo = "";
        limites = new Bounds();
        lado = 1f;
        atlas = null;

        var temp = (GameObject)PrefabUtility.InstantiatePrefab(fonte);
        temp.transform.SetPositionAndRotation(PosCaptura, Quaternion.identity);

        foreach (var t in temp.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = CamadaTemp;

        bool primeiro = true;
        foreach (var r in temp.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (primeiro) { limites = r.bounds; primeiro = false; }
            else limites.Encapsulate(r.bounds);
        }

        if (primeiro || limites.size.sqrMagnitude < 0.0001f)
        {
            Object.DestroyImmediate(temp);
            motivo = "sem malhas (Mesh Renderer) para fotografar";
            return false;
        }

        lado = Mathf.Max(limites.size.x, Mathf.Max(limites.size.y, limites.size.z)) * 1.05f;

        var camGO = new GameObject("CapturaArvore");
        var cam = camGO.AddComponent<Camera>();
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = lado * 0.5f;
        cam.aspect = 1f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.cullingMask = 1 << CamadaTemp;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = lado * 3f + 50f;
        cam.allowMSAA = false;
        cam.allowHDR = false;

        var dados = cam.GetUniversalAdditionalCameraData();
        dados.renderPostProcessing = false;
        dados.antialiasing = AntialiasingMode.None;

        var rt = new RenderTexture(Celula, Celula, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.antiAliasing = 1;

        var preto = new Color32[2][];
        var branco = new Color32[2][];
        var leitura = new Texture2D(Celula, Celula, TextureFormat.RGBA32, false, false);

        try
        {
            for (int vista = 0; vista < 2; vista++)
            {
                Vector3 dir = vista == 0 ? Vector3.forward : Vector3.right;
                camGO.transform.position = limites.center - dir * (lado * 1.5f + 10f);
                camGO.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

                for (int fundo = 0; fundo < 2; fundo++)
                {
                    cam.backgroundColor = fundo == 0 ? Color.black : Color.white;
                    cam.targetTexture = rt;
                    cam.Render();

                    RenderTexture.active = rt;
                    leitura.ReadPixels(new Rect(0, 0, Celula, Celula), 0, 0, false);
                    RenderTexture.active = null;

                    var pixels = leitura.GetPixels32();
                    if (fundo == 0) preto[vista] = pixels; else branco[vista] = pixels;
                }
            }
        }
        finally
        {
            cam.targetTexture = null;
            RenderTexture.active = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(leitura);
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(temp);
        }

        // Monta o atlas: [frente | lado]
        var final = new Color32[Celula * 2 * Celula];
        var opacos = 0;
        long somaR = 0, somaG = 0, somaB = 0;

        for (int vista = 0; vista < 2; vista++)
        {
            for (int y = 0; y < Celula; y++)
            {
                for (int x = 0; x < Celula; x++)
                {
                    int i = y * Celula + x;
                    Color32 b = preto[vista][i];
                    Color32 w = branco[vista][i];

                    float diferenca = ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / (3f * 255f);
                    float a = Mathf.Clamp01(1f - diferenca);

                    Color32 c;
                    if (a < 0.02f)
                    {
                        c = new Color32(0, 0, 0, 0);
                    }
                    else
                    {
                        byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(b.r / a), 0, 255);
                        byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(b.g / a), 0, 255);
                        byte bl = (byte)Mathf.Clamp(Mathf.RoundToInt(b.b / a), 0, 255);
                        c = new Color32(r, g, bl, (byte)Mathf.RoundToInt(a * 255f));

                        if (a > 0.5f) { opacos++; somaR += r; somaG += g; somaB += bl; }
                    }

                    final[y * Celula * 2 + vista * Celula + x] = c;
                }
            }
        }

        if (opacos < 20)
        {
            motivo = "a foto saiu vazia (materiais incompatíveis com o URP?)";
            return false;
        }

        // Pixels transparentes recebem a cor média da árvore (evita borda escura nos mipmaps)
        var media = new Color32((byte)(somaR / opacos), (byte)(somaG / opacos), (byte)(somaB / opacos), 0);
        for (int i = 0; i < final.Length; i++)
        {
            if (final[i].a == 0) final[i] = media;
        }

        atlas = new Texture2D(Celula * 2, Celula, TextureFormat.RGBA32, false, false);
        atlas.SetPixels32(final);
        atlas.Apply();
        return true;
    }

    // Dois planos cruzados (um no plano XY, outro no ZY), com UV apontando para cada metade do atlas.
    static Mesh CriarMalha(Transform raiz, Bounds b, float lado)
    {
        Vector3 c = b.center;
        float h = lado * 0.5f;

        var mundo = new[]
        {
            // vista da frente (olhando para +Z; direita da tela = +X)
            new Vector3(c.x - h, c.y - h, c.z),
            new Vector3(c.x + h, c.y - h, c.z),
            new Vector3(c.x + h, c.y + h, c.z),
            new Vector3(c.x - h, c.y + h, c.z),
            // vista do lado (olhando para +X; direita da tela = -Z)
            new Vector3(c.x, c.y - h, c.z + h),
            new Vector3(c.x, c.y - h, c.z - h),
            new Vector3(c.x, c.y + h, c.z - h),
            new Vector3(c.x, c.y + h, c.z + h)
        };

        var vertices = new Vector3[8];
        var normais = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            vertices[i] = raiz.InverseTransformPoint(mundo[i]);
            normais[i] = Vector3.up;
        }

        var uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 1f),
            new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f)
        };

        var malha = new Mesh();
        malha.vertices = vertices;
        malha.normals = normais;
        malha.uv = uv;
        malha.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
        malha.RecalculateBounds();
        return malha;
    }

    // ------------------------------------------------------------------------------------------

    static int TrocarNosTerrenos(Dictionary<GameObject, GameObject> mapa)
    {
        int trocas = 0;
        var feitos = new HashSet<TerrainData>();

        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var td = t.terrainData;
            if (td == null || !feitos.Add(td)) continue;

            var protos = td.treePrototypes;
            bool mudou = false;

            for (int i = 0; i < protos.Length; i++)
            {
                GameObject variante;
                if (protos[i].prefab != null && mapa.TryGetValue(protos[i].prefab, out variante))
                {
                    protos[i].prefab = variante;
                    mudou = true;
                    trocas++;
                }
            }

            if (mudou)
            {
                td.treePrototypes = protos;
                EditorUtility.SetDirty(td);
                t.Flush();
            }
        }

        return trocas;
    }

    static int TrocarInstanciasNaCena(Dictionary<GameObject, GameObject> mapa)
    {
        var alvos = new List<GameObject>();
        var variantes = new List<GameObject>();

        foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = tr.gameObject;
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;

            var origem = PrefabUtility.GetCorrespondingObjectFromSource(go);
            GameObject variante;
            if (origem != null && mapa.TryGetValue(origem, out variante))
            {
                alvos.Add(go);
                variantes.Add(variante);
            }
        }

        for (int i = 0; i < alvos.Count; i++)
        {
            var antigo = alvos[i];
            var pai = antigo.transform.parent;
            int indice = antigo.transform.GetSiblingIndex();

            var novo = (GameObject)PrefabUtility.InstantiatePrefab(variantes[i], antigo.scene);
            novo.transform.SetParent(pai, false);
            novo.transform.localPosition = antigo.transform.localPosition;
            novo.transform.localRotation = antigo.transform.localRotation;
            novo.transform.localScale = antigo.transform.localScale;
            novo.transform.SetSiblingIndex(indice);
            novo.name = antigo.name;
            novo.tag = antigo.tag;
            novo.layer = antigo.layer;
            GameObjectUtility.SetStaticEditorFlags(novo, GameObjectUtility.GetStaticEditorFlags(antigo));
            novo.SetActive(antigo.activeSelf);

            Undo.RegisterCreatedObjectUndo(novo, "Trocar árvore por versão com LOD");
            Undo.DestroyObjectImmediate(antigo);
        }

        return alvos.Count;
    }

    static string Limpar(string nome)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            nome = nome.Replace(c, '_');
        return nome.Replace(' ', '_');
    }
}

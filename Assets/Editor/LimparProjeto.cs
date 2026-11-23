using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Limpeza do projeto: acha arquivos que o jogo não usa.
//
//  ISLAND > Limpeza > 1 - Analisar Arquivos Não Usados      (só lê; gera RELATORIO_LIMPEZA.txt)
//  ISLAND > Limpeza > 2 - Mover Não Usados para Quarentena   (move para Assets/_QUARENTENA; dá para desfazer)
//  ISLAND > Limpeza > 3 - Restaurar Quarentena               (devolve tudo para o lugar de origem)
//  ISLAND > Limpeza > 4 - Apagar Quarentena (definitivo)     (só depois de testar o jogo!)
//  ISLAND > Limpeza > 5 - Proteger Selecionados              (nunca mais entram na limpeza; se estiverem na
//                                                              quarentena, voltam para o lugar de origem)
//
// A lista de itens protegidos fica em LIMPEZA_MANTER.txt (na pasta do projeto, ao lado de Assets).
//
// "Usado" = alcançável a partir das cenas ligadas no Build Settings (File > Build Profiles / Scene List),
// do pipeline gráfico (URP), das pastas especiais (Resources, Editor, Plugins, Settings, TextMesh Pro...)
// e de qualquer arquivo cujo NOME apareça escrito em algum script (.cs).
// Scripts, shaders e arquivos de configuração nunca são movidos.
public static class LimparProjeto
{
    const string Quarentena = "Assets/_QUARENTENA";

    static readonly string[] PastasProtegidas =
    {
        "Assets/Editor", "Assets/Plugins", "Assets/Resources", "Assets/StreamingAssets", "Assets/Settings",
        "Assets/TextMesh Pro", "Assets/Gizmos", "Assets/Editor Default Resources", "Assets/AddressableAssetsData",
        "Assets/XR", "Assets/XRI", Quarentena
    };

    static readonly string[] ExtensoesProtegidas =
    {
        ".cs", ".asmdef", ".asmref", ".dll", ".so", ".shader", ".hlsl", ".cginc", ".compute", ".glslinc",
        ".shadergraph", ".shadersubgraph", ".json", ".inputactions", ".md", ".txt", ".rsp", ".uss", ".uxml", ".tss",
        ".preset"
    };

    // Arquivos usados fora das cenas (ícone do jogo, logo, cursor...): nunca entram na limpeza
    static readonly string[] PalavrasProtegidas = { "icon", "logo", "splash", "cursor", "thumb" };

    class Resultado
    {
        public List<string> inuteis = new List<string>();
        public Dictionary<string, long> tamanhos = new Dictionary<string, long>();
        public List<string> cenasForaDoBuild = new List<string>();
        public int totalAssets;
        public int usados;
        public int protegidosPorCodigo;
    }

    // ------------------------------------------------------------------------------------------

    [MenuItem("ISLAND/Limpeza/1 - Analisar Arquivos Não Usados")]
    static void Analisar()
    {
        var r = Calcular();
        if (r == null) return;

        string caminhoRelatorio = EscreverRelatorio(r);
        long total = r.inuteis.Sum(p => r.tamanhos[p]);

        var msg = new StringBuilder();
        msg.AppendLine("Arquivos no projeto: " + r.totalAssets);
        msg.AppendLine("Usados pelo jogo: " + r.usados);
        msg.AppendLine("Não usados: " + r.inuteis.Count + "  (" + MB(total) + ")");
        if (r.protegidosPorCodigo > 0)
            msg.AppendLine("Mantidos porque o nome aparece em algum script: " + r.protegidosPorCodigo);
        msg.AppendLine();
        msg.AppendLine("Relatório completo (com tamanhos):");
        msg.AppendLine(caminhoRelatorio);
        msg.AppendLine();
        msg.AppendLine("Para tirar do projeto: ISLAND > Limpeza > 2 - Mover Não Usados para Quarentena.");

        Debug.Log("[Limpeza]\n" + msg);
        EditorUtility.DisplayDialog("Análise concluída", msg.ToString(), "OK");
    }

    [MenuItem("ISLAND/Limpeza/2 - Mover Não Usados para Quarentena")]
    static void Mover()
    {
        var r = Calcular();
        if (r == null) return;

        if (r.inuteis.Count == 0)
        {
            EditorUtility.DisplayDialog("Limpeza", "Não achei nada para limpar.", "OK");
            return;
        }

        long total = r.inuteis.Sum(p => r.tamanhos[p]);
        int cenas = r.inuteis.Count(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));

        var pergunta = new StringBuilder();
        pergunta.AppendLine("Vou mover " + r.inuteis.Count + " arquivos (" + MB(total) + ") para " + Quarentena + ".");
        pergunta.AppendLine("Nada é apagado: dá para devolver com 'Restaurar Quarentena'.");
        if (cenas > 0)
        {
            pergunta.AppendLine();
            pergunta.AppendLine("ATENÇÃO: " + cenas + " cena(s) que não estão no Build também serão movidas:");
            foreach (var c in r.inuteis.Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)).Take(8))
                pergunta.AppendLine("- " + c);
        }
        pergunta.AppendLine();
        pergunta.AppendLine("Feche as cenas abertas com mudanças não salvas antes (salve com Ctrl+S). Continuar?");

        if (!EditorUtility.DisplayDialog("Mover para quarentena", pergunta.ToString(), "Mover", "Cancelar"))
            return;

        EscreverRelatorio(r);

        // Cria as pastas de destino antes (fora do "lote" de edição)
        var pastasOrigem = new HashSet<string>();
        foreach (var p in r.inuteis)
        {
            string destino = Quarentena + "/" + p.Substring("Assets/".Length);
            GarantirPasta(Normalizar(Path.GetDirectoryName(destino)));
            pastasOrigem.Add(Normalizar(Path.GetDirectoryName(p)));
        }

        int movidos = 0, falhas = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < r.inuteis.Count; i++)
            {
                string p = r.inuteis[i];
                string destino = Quarentena + "/" + p.Substring("Assets/".Length);
                string erro = AssetDatabase.MoveAsset(p, destino);

                if (string.IsNullOrEmpty(erro)) movidos++;
                else { falhas++; Debug.LogWarning("[Limpeza] Não consegui mover " + p + ": " + erro); }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        int pastasApagadas = RemoverPastasVazias(pastasOrigem);

        string resumo = "Movidos: " + movidos + (falhas > 0 ? "\nFalhas: " + falhas + " (veja o Console)" : "") +
                        "\nPastas vazias removidas: " + pastasApagadas +
                        "\n\nAgora abra as cenas Menu e Island, aperte Play e teste o jogo.\n" +
                        "Se algo sumiu: ISLAND > Limpeza > 3 - Restaurar Quarentena.\n" +
                        "Se estiver tudo certo: 4 - Apagar Quarentena.";
        Debug.Log("[Limpeza]\n" + resumo);
        EditorUtility.DisplayDialog("Quarentena", resumo, "OK");
    }

    [MenuItem("ISLAND/Limpeza/3 - Restaurar Quarentena")]
    static void Restaurar()
    {
        if (!AssetDatabase.IsValidFolder(Quarentena))
        {
            EditorUtility.DisplayDialog("Restaurar", "Não existe a pasta " + Quarentena + ".", "OK");
            return;
        }

        var arquivos = ArquivosDaQuarentena();
        if (arquivos.Count == 0)
        {
            EditorUtility.DisplayDialog("Restaurar", "A quarentena está vazia.", "OK");
            return;
        }

        foreach (var q in arquivos)
        {
            string original = "Assets/" + q.Substring((Quarentena + "/").Length);
            GarantirPasta(Normalizar(Path.GetDirectoryName(original)));
        }

        int ok = 0, falhas = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var q in arquivos)
            {
                string original = "Assets/" + q.Substring((Quarentena + "/").Length);
                string erro = AssetDatabase.MoveAsset(q, original);

                if (string.IsNullOrEmpty(erro)) ok++;
                else { falhas++; Debug.LogWarning("[Limpeza] Não consegui restaurar " + q + ": " + erro); }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();

        if (falhas == 0) AssetDatabase.DeleteAsset(Quarentena);

        EditorUtility.DisplayDialog("Restaurar",
            "Restaurados: " + ok + (falhas > 0 ? "\nFalhas: " + falhas + " (veja o Console)" : "\nA pasta de quarentena foi removida."), "OK");
    }

    [MenuItem("ISLAND/Limpeza/4 - Apagar Quarentena (definitivo)")]
    static void ApagarQuarentena()
    {
        if (!AssetDatabase.IsValidFolder(Quarentena))
        {
            EditorUtility.DisplayDialog("Apagar", "Não existe a pasta " + Quarentena + ".", "OK");
            return;
        }

        var arquivos = ArquivosDaQuarentena();
        long total = arquivos.Sum(p => TamanhoDoArquivo(p));

        if (!EditorUtility.DisplayDialog("Apagar quarentena",
                "Vou apagar DEFINITIVAMENTE " + arquivos.Count + " arquivos (" + MB(total) + ").\n\n" +
                "Só faça isso depois de testar o jogo (Play e, de preferência, um Build).\n" +
                "Se você usa git, faça um commit antes.", "Apagar", "Cancelar"))
            return;

        AssetDatabase.DeleteAsset(Quarentena);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Apagar", "Quarentena apagada.", "OK");
    }

    // ------------------------------------------------------------------------------------------

    static Resultado Calcular()
    {
        var cenasBuild = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
        if (cenasBuild.Count == 0)
        {
            EditorUtility.DisplayDialog("Limpeza",
                "Nenhuma cena está ligada no Build Settings.\n\n" +
                "Abra File > Build Profiles (ou Build Settings), adicione as cenas Menu e Island na lista e tente de novo.\n" +
                "Sem isso eu não sei o que o jogo usa.", "OK");
            return null;
        }

        var r = new Resultado();

        try
        {
            EditorUtility.DisplayProgressBar("Limpeza", "Lendo arquivos do projeto...", 0.05f);

            var todos = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Where(p => File.Exists(p))
                .ToList();
            r.totalAssets = todos.Count;

            // ---- Raízes: o que com certeza é usado ----
            var raizes = new List<string>(cenasBuild);

            if (GraphicsSettings.defaultRenderPipeline != null)
                raizes.Add(AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                var rp = QualitySettings.GetRenderPipelineAssetAt(i);
                if (rp != null) raizes.Add(AssetDatabase.GetAssetPath(rp));
            }

            foreach (var o in PlayerSettings.GetPreloadedAssets())
            {
                if (o != null) raizes.Add(AssetDatabase.GetAssetPath(o));
            }

            // Tudo que as Project Settings apontam (ícone do jogo, tela de splash, cursor padrão...)
            raizes.AddRange(ReferenciasDasConfiguracoes());

            // Itens que você pediu para nunca limpar
            var manter = LerManter();

            // Pastas especiais e configurações do URP: tudo dentro conta como usado
            foreach (var p in todos)
            {
                if (EmPastaProtegida(p) || p.IndexOf("GlobalSettings", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    EstaNaListaManter(p, manter) || TemPalavraProtegida(p))
                    raizes.Add(p);
            }

            raizes.RemoveAll(string.IsNullOrEmpty);

            EditorUtility.DisplayProgressBar("Limpeza", "Seguindo as dependências das cenas...", 0.25f);
            var usados = new HashSet<string>(AssetDatabase.GetDependencies(raizes.Distinct().ToArray(), true));
            r.usados = usados.Count(p => p.StartsWith("Assets/", StringComparison.Ordinal));

            // Pastas de dados de cena (iluminação, NavMesh...) ficam ao lado da cena, com o mesmo nome dela
            var pastasDeCena = new List<string>();
            foreach (var cena in todos.Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)))
                pastasDeCena.Add(cena.Substring(0, cena.Length - ".unity".Length) + "/");

            // ---- Texto de todos os scripts: nomes de arquivos citados no código ficam protegidos ----
            EditorUtility.DisplayProgressBar("Limpeza", "Lendo os scripts...", 0.4f);
            var codigo = new StringBuilder();
            foreach (var arquivo in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
            {
                try { codigo.Append(File.ReadAllText(arquivo)).Append('\n'); }
                catch { /* arquivo bloqueado: ignora */ }
            }
            string textoCodigo = codigo.ToString().ToLowerInvariant();

            // ---- Candidatos ----
            var candidatos = new List<string>();
            foreach (var p in todos)
            {
                if (usados.Contains(p)) continue;
                if (EmPastaProtegida(p)) continue;
                if (ExtensoesProtegidas.Contains(Path.GetExtension(p).ToLowerInvariant())) continue;
                if (pastasDeCena.Any(pc => p.StartsWith(pc, StringComparison.Ordinal))) continue;
                if (EstaNaListaManter(p, manter) || TemPalavraProtegida(p)) continue;
                candidatos.Add(p);
            }

            for (int i = 0; i < candidatos.Count; i++)
            {
                if (i % 50 == 0)
                    EditorUtility.DisplayProgressBar("Limpeza", "Conferindo " + candidatos[i], 0.45f + 0.5f * i / Mathf.Max(1, candidatos.Count));

                string p = candidatos[i];
                string nome = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();

                if (nome.Length >= 4 && textoCodigo.Contains(nome, StringComparison.Ordinal))
                {
                    r.protegidosPorCodigo++;
                    continue;
                }

                r.inuteis.Add(p);
                r.tamanhos[p] = TamanhoDoArquivo(p);
            }

            r.inuteis = r.inuteis.OrderByDescending(p => r.tamanhos[p]).ToList();

            foreach (var cena in todos.Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)))
                if (!cenasBuild.Contains(cena)) r.cenasForaDoBuild.Add(cena);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        return r;
    }

    static string EscreverRelatorio(Resultado r)
    {
        string raizProjeto = Directory.GetParent(Application.dataPath).FullName;
        string caminho = Path.Combine(raizProjeto, "RELATORIO_LIMPEZA.txt");

        long total = r.inuteis.Sum(p => r.tamanhos[p]);
        var sb = new StringBuilder();
        sb.AppendLine("RELATÓRIO DE LIMPEZA - " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        sb.AppendLine("Cenas do Build: " + string.Join(", ", EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)));
        sb.AppendLine("Arquivos no projeto: " + r.totalAssets + " | usados: " + r.usados + " | não usados: " + r.inuteis.Count + " (" + MB(total) + ")");
        sb.AppendLine("Mantidos por aparecerem escritos em scripts: " + r.protegidosPorCodigo);
        sb.AppendLine();

        if (r.cenasForaDoBuild.Count > 0)
        {
            sb.AppendLine("CENAS FORA DO BUILD (o que só elas usam aparece como não usado):");
            foreach (var c in r.cenasForaDoBuild) sb.AppendLine("  " + c);
            sb.AppendLine();
        }

        sb.AppendLine("POR PASTA (maiores primeiro):");
        var porPasta = r.inuteis
            .GroupBy(p => PastaNivel(p, 3))
            .Select(g => new { pasta = g.Key, qtd = g.Count(), bytes = g.Sum(p => r.tamanhos[p]) })
            .OrderByDescending(x => x.bytes);
        foreach (var x in porPasta)
            sb.AppendLine(string.Format("  {0,10}  {1,5} arquivos  {2}", MB(x.bytes), x.qtd, x.pasta));
        sb.AppendLine();

        sb.AppendLine("TODOS OS ARQUIVOS NÃO USADOS (maiores primeiro):");
        foreach (var p in r.inuteis)
            sb.AppendLine(string.Format("  {0,10}  {1}", MB(r.tamanhos[p]), p));

        File.WriteAllText(caminho, sb.ToString(), new UTF8Encoding(true));
        return caminho;
    }

    // ------------------------------------------------------------------------------------------
    // Itens protegidos

    [MenuItem("ISLAND/Limpeza/5 - Proteger Selecionados (e restaurar da quarentena)")]
    static void Proteger()
    {
        var selecionados = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets)
            .Select(o => AssetDatabase.GetAssetPath(o))
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToList();

        if (selecionados.Count == 0)
        {
            EditorUtility.DisplayDialog("Proteger",
                "Selecione na janela Project os arquivos ou pastas que você quer manter (pode ser dentro de " +
                Quarentena + ") e tente de novo.", "OK");
            return;
        }

        var lista = LerManter();
        int protegidos = 0, restaurados = 0, falhas = 0;
        string prefixoQuarentena = Quarentena + "/";

        foreach (var sel in selecionados)
        {
            if (sel == Quarentena) continue;

            string original = sel;

            if (sel.StartsWith(prefixoQuarentena, StringComparison.Ordinal))
            {
                original = "Assets/" + sel.Substring(prefixoQuarentena.Length);

                List<string> arquivos;
                if (AssetDatabase.IsValidFolder(sel))
                {
                    arquivos = AssetDatabase.FindAssets("", new[] { sel })
                        .Select(g => AssetDatabase.GUIDToAssetPath(g))
                        .Where(a => !AssetDatabase.IsValidFolder(a))
                        .Distinct()
                        .ToList();
                }
                else
                {
                    arquivos = new List<string> { sel };
                }

                foreach (var arquivo in arquivos)
                {
                    string destino = "Assets/" + arquivo.Substring(prefixoQuarentena.Length);
                    GarantirPasta(Normalizar(Path.GetDirectoryName(destino)));

                    string erro = AssetDatabase.MoveAsset(arquivo, destino);
                    if (string.IsNullOrEmpty(erro)) restaurados++;
                    else { falhas++; Debug.LogWarning("[Limpeza] Não consegui restaurar " + arquivo + ": " + erro); }
                }
            }

            if (!lista.Contains(original))
            {
                lista.Add(original);
                protegidos++;
            }
        }

        GravarManter(lista);
        AssetDatabase.Refresh();

        if (AssetDatabase.IsValidFolder(Quarentena) && ArquivosDaQuarentena().Count == 0)
            AssetDatabase.DeleteAsset(Quarentena);

        EditorUtility.DisplayDialog("Proteger",
            "Protegidos (nunca mais entram na limpeza): " + protegidos +
            "\nRestaurados da quarentena: " + restaurados +
            (falhas > 0 ? "\nFalhas: " + falhas + " (veja o Console)" : "") +
            "\n\nLista salva em LIMPEZA_MANTER.txt", "OK");
    }

    static string CaminhoManter()
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "LIMPEZA_MANTER.txt");
    }

    static List<string> LerManter()
    {
        var lista = new List<string>();
        string arquivo = CaminhoManter();
        if (!File.Exists(arquivo)) return lista;

        foreach (var linha in File.ReadAllLines(arquivo))
        {
            string l = Normalizar(linha.Trim()).TrimEnd('/');
            if (l.Length == 0 || l.StartsWith("#", StringComparison.Ordinal)) continue;
            if (!lista.Contains(l)) lista.Add(l);
        }
        return lista;
    }

    static void GravarManter(List<string> lista)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Arquivos e pastas que a limpeza do projeto NUNCA move (um caminho por linha).");
        sb.AppendLine("# Para adicionar: selecione no Project e use ISLAND > Limpeza > 5 - Proteger Selecionados.");
        foreach (var l in lista) sb.AppendLine(l);
        File.WriteAllText(CaminhoManter(), sb.ToString(), new UTF8Encoding(false));
    }

    static bool EstaNaListaManter(string caminho, List<string> manter)
    {
        foreach (var m in manter)
        {
            if (caminho.Equals(m, StringComparison.Ordinal) ||
                caminho.StartsWith(m + "/", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    static bool TemPalavraProtegida(string caminho)
    {
        string nome = Path.GetFileNameWithoutExtension(caminho);
        foreach (var palavra in PalavrasProtegidas)
        {
            if (nome.IndexOf(palavra, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    // Lê os arquivos de ProjectSettings/*.asset (Player, Graphics, Quality...) e devolve os arquivos de
    // Assets/ que eles apontam. É assim que achamos o ícone do jogo, que nenhuma cena usa.
    static List<string> ReferenciasDasConfiguracoes()
    {
        var achados = new HashSet<string>();

        string pasta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings");
        if (!Directory.Exists(pasta)) return achados.ToList();

        foreach (var arquivo in Directory.GetFiles(pasta, "*.asset"))
        {
            string relativo = "ProjectSettings/" + Path.GetFileName(arquivo);

            UnityEngine.Object[] objetos;
            try { objetos = AssetDatabase.LoadAllAssetsAtPath(relativo); }
            catch { continue; }

            foreach (var obj in objetos)
            {
                if (obj == null) continue;

                try
                {
                    var so = new SerializedObject(obj);
                    var it = so.GetIterator();

                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;

                        var alvo = it.objectReferenceValue;
                        if (alvo == null) continue;

                        string caminho = AssetDatabase.GetAssetPath(alvo);
                        if (caminho.StartsWith("Assets/", StringComparison.Ordinal)) achados.Add(caminho);
                    }
                }
                catch { /* alguns objetos de configuração não podem ser lidos: ignora */ }
            }
        }

        return achados.ToList();
    }

    // ------------------------------------------------------------------------------------------

    static bool EmPastaProtegida(string caminho)
    {
        foreach (var pasta in PastasProtegidas)
        {
            if (caminho.Equals(pasta, StringComparison.Ordinal) ||
                caminho.StartsWith(pasta + "/", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    static List<string> ArquivosDaQuarentena()
    {
        return AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith(Quarentena + "/", StringComparison.Ordinal))
            .Where(p => !AssetDatabase.IsValidFolder(p))
            .Where(p => File.Exists(p))
            .ToList();
    }

    static void GarantirPasta(string caminho)
    {
        if (string.IsNullOrEmpty(caminho) || AssetDatabase.IsValidFolder(caminho)) return;

        string pai = Normalizar(Path.GetDirectoryName(caminho));
        GarantirPasta(pai);

        string nome = Path.GetFileName(caminho);
        AssetDatabase.CreateFolder(pai, nome);
    }

    // Apaga as pastas que ficaram vazias (só com .meta) depois da limpeza, subindo até "Assets"
    static int RemoverPastasVazias(HashSet<string> pastasOrigem)
    {
        int apagadas = 0;

        foreach (var inicio in pastasOrigem.OrderByDescending(p => p.Length))
        {
            string pasta = inicio;

            while (!string.IsNullOrEmpty(pasta) && pasta != "Assets" && AssetDatabase.IsValidFolder(pasta))
            {
                if (EmPastaProtegida(pasta)) break;
                if (!PastaSoComMeta(pasta)) break;

                if (!AssetDatabase.DeleteAsset(pasta)) break;
                apagadas++;
                pasta = Normalizar(Path.GetDirectoryName(pasta));
            }
        }

        return apagadas;
    }

    static bool PastaSoComMeta(string pasta)
    {
        try
        {
            return Directory.GetFiles(pasta, "*", SearchOption.AllDirectories)
                .All(f => f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    static string PastaNivel(string caminho, int niveis)
    {
        var partes = caminho.Split('/');
        int n = Mathf.Min(niveis, partes.Length - 1);
        return string.Join("/", partes.Take(n));
    }

    static string Normalizar(string caminho)
    {
        return caminho == null ? null : caminho.Replace('\\', '/');
    }

    static long TamanhoDoArquivo(string caminho)
    {
        try { return new FileInfo(caminho).Length; }
        catch { return 0; }
    }

    static string MB(long bytes)
    {
        if (bytes >= 1024L * 1024L) return (bytes / 1048576.0).ToString("0.0") + " MB";
        return (bytes / 1024.0).ToString("0") + " KB";
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Objetivo do jogo: abrir TODOS os baús da ilha.
///  - Mostra "Baús: X / Y" no topo da tela.
///  - Conta os baús da cena + o baú que o Rei Esqueleto solta ao morrer.
///  - Quando o último é aberto: tela de VITÓRIA (moedas e tempo), com "Jogar de novo" e "Menu".
/// Colocado pelo menu  ISLAND > Objetivo > Preparar Objetivo e Rei Esqueleto.
/// </summary>
public class ObjetivoBaus : MonoBehaviour
{
    public string nomeCenaMenu = "Menu";
    [Tooltip("Segundos entre abrir o último baú e a tela de vitória (para ver o baú abrindo).")]
    public float esperaVitoria = 2.5f;
    [Tooltip("Tela de vitória (uGUI, com os textos editáveis). Criada por ISLAND > Criar Tela de Vitória.")]
    public TelaVitoria telaVitoria;

    int total;
    readonly HashSet<BauTesouro> abertos = new HashSet<BauTesouro>();
    bool venceu;
    bool telaNova;   // true quando a vitória é mostrada pela TelaVitoria (uGUI) em vez do desenho antigo
    float tempoFinal;
    float pulso;

    static GUIStyle estHud, estTitulo, estSub, estInfo, estBotao;
    static Texture2D fundo, caixa;

    void Start()
    {
        total = FindObjectsByType<BauTesouro>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        if (FindFirstObjectByType<ChefaoReiEsqueleto>() != null) total += 1;   // o baú do chefão
        BauTesouro.AlgumAberto += AoAbrir;
    }

    void OnDestroy() { BauTesouro.AlgumAberto -= AoAbrir; }

    void AoAbrir(BauTesouro b)
    {
        if (b == null || venceu || !abertos.Add(b)) return;
        pulso = 1f;
        if (abertos.Count >= total) StartCoroutine(Vencer());
    }

    IEnumerator Vencer()
    {
        venceu = true;
        tempoFinal = Time.timeSinceLevelLoad;
        yield return new WaitForSeconds(esperaVitoria);

        // Tela de vitória nova (uGUI com visual de madeira)
        var tela = telaVitoria != null ? telaVitoria : FindFirstObjectByType<TelaVitoria>(FindObjectsInactive.Include);
        if (tela != null)
        {
            telaNova = true;
            tela.Mostrar(abertos.Count, total, ContadorMoedas.Total, tempoFinal);
            yield break;
        }

        // Plano B (se a tela nova não existir na cena): o desenho antigo
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (pulso > 0f) pulso = Mathf.MoveTowards(pulso, 0f, Time.unscaledDeltaTime * 1.5f);
    }

    void OnGUI()
    {
        Estilos();

        if (!venceu || Time.timeScale > 0f)
        {
            // contador no topo
            string t = "BAÚS  " + abertos.Count + " / " + total;
            float w = 220f + pulso * 30f, h = 40f + pulso * 6f;
            var r = new Rect((Screen.width - w) * 0.5f, 14f, w, h);
            GUI.DrawTexture(r, caixa);
            var cor = estHud.normal.textColor;
            IMGUIFix.Cor(estHud, Color.Lerp(new Color(1f, 0.88f, 0.45f), Color.white, pulso));
            GUI.Label(r, t, estHud);
            IMGUIFix.Cor(estHud, cor);
            if (!venceu || Time.timeScale > 0f) return;
        }

        if (telaNova) return;   // a TelaVitoria (uGUI) cuida da vitória

        // ---------- tela de vitória (plano B) ----------
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), fundo);
        float pw = Mathf.Min(640f, Screen.width * 0.85f), ph = Mathf.Min(440f, Screen.height * 0.8f);
        var p = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);

        GUILayout.BeginArea(p);
        GUILayout.FlexibleSpace();
        GUILayout.Label("VITÓRIA!", estTitulo);
        GUILayout.Label("Você encontrou todos os tesouros da ilha.", estSub);
        GUILayout.Space(18);
        int min = Mathf.FloorToInt(tempoFinal / 60f), seg = Mathf.FloorToInt(tempoFinal % 60f);
        GUILayout.Label("Baús abertos: " + abertos.Count + " / " + total, estInfo);
        GUILayout.Label("Moedas: " + ContadorMoedas.Total, estInfo);
        GUILayout.Label("Tempo: " + min + ":" + seg.ToString("00"), estInfo);
        GUILayout.Space(26);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Jogar de novo", estBotao, GUILayout.Width(220), GUILayout.Height(54)))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        GUILayout.Space(20);
        if (GUILayout.Button("Menu", estBotao, GUILayout.Width(220), GUILayout.Height(54)))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(nomeCenaMenu);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
    }

    static void Estilos()
    {
        if (estHud != null) return;
        fundo = Cor(new Color(0.03f, 0.02f, 0.01f, 0.82f));
        caixa = Cor(new Color(0.18f, 0.10f, 0.04f, 0.78f));

        estHud = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(20, Screen.height / 34), fontStyle = FontStyle.Bold };
        IMGUIFix.Cor(estHud, new Color(1f, 0.88f, 0.45f));
        estTitulo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(48, Screen.height / 9), fontStyle = FontStyle.Bold };
        IMGUIFix.Cor(estTitulo, new Color(1f, 0.80f, 0.25f));
        estSub = new GUIStyle(estHud) { fontSize = Mathf.Max(20, Screen.height / 30), fontStyle = FontStyle.Italic };
        IMGUIFix.Cor(estSub, new Color(1f, 0.95f, 0.85f));
        estInfo = new GUIStyle(estHud) { fontSize = Mathf.Max(18, Screen.height / 36), fontStyle = FontStyle.Normal };
        IMGUIFix.Cor(estInfo, Color.white);
        estBotao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Max(18, Screen.height / 36), fontStyle = FontStyle.Bold };
        estBotao.normal.background = Cor(new Color(0.55f, 0.30f, 0.10f));
        estBotao.hover.background = Cor(new Color(0.72f, 0.42f, 0.14f));
        estBotao.active.background = Cor(new Color(0.40f, 0.20f, 0.06f));
        estBotao.normal.textColor = estBotao.hover.textColor = estBotao.active.textColor = new Color(1f, 0.95f, 0.85f);
    }

    static Texture2D Cor(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
    }
}

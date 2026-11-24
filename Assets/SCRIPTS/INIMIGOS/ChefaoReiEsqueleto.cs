using System.Collections;
using UnityEngine;

/// <summary>
/// O REI ESQUELETO (chefão). Fica neste ponto (2º andar do Templo_Yas) e só surge quando a noite cai
/// (NoiteMacabra.Anoiteceu). É o esqueleto do jogo, maior, com aura vermelha, mais vida e mais força.
/// Ao morrer, o último baú sai do chão no lugar onde ele caiu.
/// Colocado pelo menu  ISLAND > Objetivo > Preparar Objetivo e Rei Esqueleto.
/// </summary>
public class ChefaoReiEsqueleto : MonoBehaviour
{
    [Header("Modelos")]
    public GameObject prefabEsqueleto;
    public GameObject prefabBau;

    [Header("Força do chefão")]
    public float escala = 2f;
    public int golpesParaMorrer = 10;
    public float dano = 25f;
    public float intervaloAtaque = 1.8f;
    public float velocidade = 2.6f;
    public float distanciaVisao = 18f;
    public Color corAura = new Color(1f, 0.1f, 0.05f);

    [Header("Último baú")]
    public int moedasDoBau = 100;
    public AudioClip somBau;

    [Header("Sons")]
    public AudioClip somRugido;

    [Header("Mensagens")]
    public string avisoSurgiu = "O Rei Esqueleto despertou no 2º andar do templo de pedra...";
    public string avisoBau = "O Rei caiu. O último baú surgiu!";

    VidaSkeleton vidaChefe;
    Transform chefe;
    Transform jogador;
    bool surgiu, morreu;
    string aviso;
    float avisoAte;
    static GUIStyle estAviso, estNome;
    static Texture2D barraFundo, barraCheia;

    void OnEnable() { NoiteMacabra.Anoiteceu += Surgir; }
    void OnDisable() { NoiteMacabra.Anoiteceu -= Surgir; }

    void Start()
    {
        var v = FindFirstObjectByType<VidaJogador>();
        jogador = v != null ? v.transform : (Camera.main != null ? Camera.main.transform : null);
        if (NoiteMacabra.EstaDeNoite) Surgir();
    }

    public void Surgir()
    {
        if (surgiu || prefabEsqueleto == null) return;
        surgiu = true;

        var go = Instantiate(prefabEsqueleto, transform.position, transform.rotation);
        go.name = "Rei Esqueleto";
        go.transform.localScale = prefabEsqueleto.transform.localScale * escala;
        chefe = go.transform;

        var ia = go.GetComponent<InimigoSkeleton>();
        if (ia != null)
        {
            ia.dano = dano;
            ia.intervaloAtaque = intervaloAtaque;
            ia.velocidadePerseguir = velocidade;
            ia.distanciaVisao = distanciaVisao;
            ia.rangeAtaque *= escala * 0.9f;
            ia.alturaOlhos *= escala;
            ia.distanciaDesistir = 45f;
            ia.tempoMemoria = 30f;
            ia.raioPassear = 2f;
            if (somRugido != null) ia.somRugido = somRugido;
        }

        vidaChefe = go.GetComponent<VidaSkeleton>();
        if (vidaChefe != null)
        {
            vidaChefe.golpesParaMorrer = golpesParaMorrer;
            vidaChefe.Morreu += AoMorrer;
        }

        // aura vermelha
        var luz = new GameObject("Aura do Rei").AddComponent<Light>();
        luz.transform.SetParent(go.transform, false);
        luz.transform.localPosition = Vector3.up * 1.6f;
        luz.type = LightType.Point;
        luz.color = corAura;
        luz.range = 6f;
        luz.intensity = 2.5f;
        luz.shadows = LightShadows.None;

        Avisar(avisoSurgiu, 7f);
    }

    void AoMorrer()
    {
        if (morreu) return;
        morreu = true;
        StartCoroutine(SoltarBau(chefe != null ? chefe.position : transform.position));
    }

    IEnumerator SoltarBau(Vector3 onde)
    {
        yield return new WaitForSeconds(2f);   // deixa o corpo cair primeiro
        if (prefabBau == null) yield break;

        // chão embaixo de onde ele morreu
        RaycastHit h;
        if (Physics.Raycast(onde + Vector3.up * 2f, Vector3.down, out h, 6f, ~0, QueryTriggerInteraction.Ignore)) onde = h.point;

        Vector3 olhar = jogador != null ? jogador.position - onde : Vector3.forward; olhar.y = 0f;
        var bau = Instantiate(prefabBau, onde + Vector3.down * 1.2f,
                              olhar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(olhar) : Quaternion.identity);
        bau.name = "Bau - Rei Esqueleto";

        var b = bau.GetComponent<BauTesouro>();
        if (b == null) b = bau.AddComponent<BauTesouro>();
        b.moedas = moedasDoBau;
        b.cura = 0f;
        b.tesouroPrincipal = true;
        b.textoTesouro = "Você derrotou o Rei Esqueleto e achou o último tesouro!";
        if (somBau != null) b.somAbrir = somBau;

        // brilho dourado
        var luz = new GameObject("Brilho").AddComponent<Light>();
        luz.transform.SetParent(bau.transform, false);
        luz.transform.localPosition = Vector3.up * 1.2f;
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.8f, 0.3f);
        luz.range = 5f;
        luz.intensity = 2f;
        luz.shadows = LightShadows.None;

        // sobe do chão
        foreach (var c in bau.GetComponentsInChildren<Collider>()) c.enabled = false;
        Vector3 de = bau.transform.position, para = onde;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.5f)
        {
            bau.transform.position = Vector3.Lerp(de, para, 1f - (1f - t) * (1f - t));
            yield return null;
        }
        bau.transform.position = para;
        foreach (var c in bau.GetComponentsInChildren<Collider>()) c.enabled = true;

        Avisar(avisoBau, 6f);
    }

    void Avisar(string texto, float segundos)
    {
        aviso = texto;
        avisoAte = Time.time + segundos;
    }

    // Teste: mata o chefão na hora (menu ISLAND > Objetivo > Derrotar Rei Agora)
    public void DerrotarAgora()
    {
        if (!surgiu) Surgir();
        if (vidaChefe != null) vidaChefe.MatarAgora();
    }

    void OnGUI()
    {
        if (estAviso == null)
        {
            estAviso = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(20, Screen.height / 30), fontStyle = FontStyle.BoldAndItalic, wordWrap = true };
            IMGUIFix.Cor(estAviso, new Color(1f, 0.35f, 0.3f));
            estNome = new GUIStyle(estAviso) { fontSize = Mathf.Max(16, Screen.height / 42), fontStyle = FontStyle.Bold };
            IMGUIFix.Cor(estNome, new Color(1f, 0.9f, 0.8f));
            barraFundo = new Texture2D(1, 1); barraFundo.SetPixel(0, 0, new Color(0, 0, 0, 0.7f)); barraFundo.Apply();
            barraCheia = new Texture2D(1, 1); barraCheia.SetPixel(0, 0, new Color(0.8f, 0.08f, 0.05f)); barraCheia.Apply();
        }

        if (Time.time < avisoAte && !string.IsNullOrEmpty(aviso))
        {
            var r = new Rect(Screen.width * 0.1f, Screen.height * 0.2f, Screen.width * 0.8f, Screen.height * 0.16f);
            var c = estAviso.normal.textColor;
            IMGUIFix.Cor(estAviso, new Color(0, 0, 0, 0.8f));
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), aviso, estAviso);
            IMGUIFix.Cor(estAviso, c);
            GUI.Label(r, aviso, estAviso);
        }

        // barra de vida do chefão quando o jogador está por perto
        if (vidaChefe != null && !vidaChefe.EstaMorto && chefe != null && jogador != null &&
            Vector3.Distance(jogador.position, chefe.position) < 25f)
        {
            float w = Mathf.Min(520f, Screen.width * 0.5f);
            var fundoR = new Rect((Screen.width - w) * 0.5f, 70f, w, 18f);
            GUI.Label(new Rect(fundoR.x, fundoR.y - 26f, w, 24f), "REI ESQUELETO", estNome);
            GUI.DrawTexture(fundoR, barraFundo);
            float frac = vidaChefe.golpesParaMorrer > 0 ? (float)vidaChefe.GolpesRestantes / vidaChefe.golpesParaMorrer : 0f;
            GUI.DrawTexture(new Rect(fundoR.x + 2, fundoR.y + 2, (w - 4) * frac, 14f), barraCheia);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.1f, 0.05f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 1.5f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3.5f);
    }
}

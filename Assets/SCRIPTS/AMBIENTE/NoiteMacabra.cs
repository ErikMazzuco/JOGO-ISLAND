using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// A noite macabra da ilha. O jogo começa de dia; quando o jogador lê a carta do laguinho
/// (CartaMisteriosa), chama Anoitecer():
///   - grito + clarão, céu troca para a noite de lua vermelha;
///   - o sol vira uma luz fria e fraca, a luz ambiente escurece, a névoa fecha;
///   - liga a névoa volumétrica rente ao chão;
///   - os olhos da caveira acendem em vermelho;
///   - esqueletos saem do chão em volta do lago;
///   - a música troca para a de mistério.
/// Acontece uma vez só por partida. Colocado pelo menu  ISLAND > Noite > Preparar Carta e Noite da Caveira.
/// </summary>
public class NoiteMacabra : MonoBehaviour
{
    [Header("Céu e névoa")]
    public Material ceuNoite;
    [Tooltip("Objeto com a Pure Volumetric Fog (fica desligado de dia).")]
    public GameObject nevoaVolumetrica;
    public Color corNevoa = new Color(0.10f, 0.06f, 0.09f);
    [Range(0f, 0.08f)] public float densidadeNevoa = 0.022f;

    [Header("Luz")]
    public Color corLua = new Color(0.72f, 0.55f, 0.62f);
    public float intensidadeLua = 0.18f;
    public Color ambienteCeu = new Color(0.13f, 0.09f, 0.14f);
    public Color ambienteHorizonte = new Color(0.08f, 0.05f, 0.07f);
    public Color ambienteChao = new Color(0.03f, 0.02f, 0.03f);
    public float duracao = 4f;

    [Header("Caveira")]
    public Transform caveira;
    public Color corOlhos = new Color(1f, 0.08f, 0.05f);

    [Header("Esqueletos que levantam")]
    public GameObject prefabEsqueleto;
    public int quantidadeEsqueletos = 5;
    public float raioMinimo = 14f, raioMaximo = 26f;
    public float distanciaMinimaDoJogador = 12f;
    [Tooltip("Segundos depois do anoitecer até o primeiro esqueleto levantar.")]
    public float atrasoEsqueletos = 2f;

    [Header("Sons")]
    public AudioClip somGrito;
    [Range(0f, 1f)] public float volumeGrito = 0.9f;
    public AudioClip musicaNoite;

    public static bool EstaDeNoite { get; private set; }

    /// <summary>Avisado quando a noite termina de cair (o chefão surge aqui).</summary>
    public static event System.Action Anoiteceu;

    float clarao;            // 0..1, desenhado na tela
    Light luzOlhos;
    static Texture2D branco;

    void Awake() { EstaDeNoite = false; }

    public void Anoitecer()
    {
        if (EstaDeNoite) return;
        EstaDeNoite = true;
        StartCoroutine(Transicao());
    }

    IEnumerator Transicao()
    {
        Light sol = AcharSol();
        float solInt0 = sol != null ? sol.intensity : 1f;
        Color solCor0 = sol != null ? sol.color : Color.white;

        // a luz ambiente atual (vinda do céu) vira o ponto de partida, para não "piscar"
        Color amb0Ceu, amb0Hor, amb0Chao;
        AmbienteAtual(out amb0Ceu, out amb0Hor, out amb0Chao);
        RenderSettings.ambientMode = AmbientMode.Trilight;

        Color nevoaCor0 = RenderSettings.fog ? RenderSettings.fogColor : amb0Hor;
        float nevoaDens0 = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        // grito + clarão; no pico do clarão o céu troca
        if (somGrito != null) AudioSource.PlayClipAtPoint(somGrito, Camera.main != null ? Camera.main.transform.position : transform.position, volumeGrito);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f) { clarao = t; yield return null; }
        clarao = 1f;

        Material ceu = null;
        if (ceuNoite != null)
        {
            ceu = new Material(ceuNoite);
            if (ceu.HasProperty("_Exposure")) ceu.SetFloat("_Exposure", 0.2f);
            RenderSettings.skybox = ceu;
        }
        if (nevoaVolumetrica != null) nevoaVolumetrica.SetActive(true);
        AcenderOlhos();
        AcharAudioManager()?.TrocarMusica(musicaNoite, 3f);
        StartCoroutine(LevantarEsqueletos());

        float tc = 0f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, duracao))
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            tc += Time.deltaTime;
            clarao = Mathf.Clamp01(1f - tc / 0.8f);

            if (sol != null)
            {
                sol.intensity = Mathf.Lerp(solInt0, intensidadeLua, s);
                sol.color = Color.Lerp(solCor0, corLua, s);
            }
            RenderSettings.ambientSkyColor = Color.Lerp(amb0Ceu, ambienteCeu, s);
            RenderSettings.ambientEquatorColor = Color.Lerp(amb0Hor, ambienteHorizonte, s);
            RenderSettings.ambientGroundColor = Color.Lerp(amb0Chao, ambienteChao, s);
            RenderSettings.ambientLight = RenderSettings.ambientSkyColor;
            RenderSettings.fogColor = Color.Lerp(nevoaCor0, corNevoa, s);
            RenderSettings.fogDensity = Mathf.Lerp(nevoaDens0, densidadeNevoa, s);
            if (ceu != null && ceu.HasProperty("_Exposure")) ceu.SetFloat("_Exposure", Mathf.Lerp(0.2f, 1f, s));
            yield return null;
        }
        clarao = 0f;
        DynamicGI.UpdateEnvironment();
        if (Anoiteceu != null) Anoiteceu();
    }

    // ---------- Caveira ----------

    void AcenderOlhos()
    {
        if (caveira == null) return;
        var go = new GameObject("OlhosDaCaveira");
        go.transform.SetParent(caveira, false);
        Bounds b = Limites(caveira);
        go.transform.position = b.center + Vector3.up * b.extents.y * 0.4f;
        luzOlhos = go.AddComponent<Light>();
        luzOlhos.type = LightType.Point;
        luzOlhos.color = corOlhos;
        luzOlhos.range = 5f;
        luzOlhos.intensity = 0f;
        luzOlhos.shadows = LightShadows.None;
    }

    void Update()
    {
        if (luzOlhos == null) return;
        // pulsa como se respirasse
        float alvo = 1.7f + Mathf.Sin(Time.time * 2.3f) * 0.7f;
        luzOlhos.intensity = Mathf.MoveTowards(luzOlhos.intensity, alvo, Time.deltaTime * 3f);
    }

    // ---------- Esqueletos ----------

    IEnumerator LevantarEsqueletos()
    {
        if (prefabEsqueleto == null || quantidadeEsqueletos <= 0) yield break;
        Vector3 centro = caveira != null ? caveira.position : transform.position;
        Transform jogador = AcharJogador();
        yield return new WaitForSeconds(atrasoEsqueletos);

        int criados = 0;
        for (int tentativa = 0; tentativa < quantidadeEsqueletos * 20 && criados < quantidadeEsqueletos; tentativa++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(raioMinimo, raioMaximo);
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(centro + new Vector3(r.x, 0f, r.y), out hit, 4f, NavMesh.AllAreas)) continue;
            if (jogador != null && Vector3.Distance(hit.position, jogador.position) < distanciaMinimaDoJogador) continue;

            // olhando para um lado qualquer: ele só vem se "vir" o jogador (InimigoSkeleton)
            var esq = Instantiate(prefabEsqueleto, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            StartCoroutine(SairDoChao(esq));
            criados++;
            yield return new WaitForSeconds(Random.Range(0.8f, 1.8f));
        }
    }

    // O corpo começa enterrado e sobe; a IA só liga quando ele está de pé
    IEnumerator SairDoChao(GameObject esq)
    {
        var ia = esq.GetComponent<InimigoSkeleton>();
        if (ia != null) ia.enabled = false;

        var filhos = new Transform[esq.transform.childCount];
        var orig = new Vector3[filhos.Length];
        for (int i = 0; i < filhos.Length; i++) { filhos[i] = esq.transform.GetChild(i); orig[i] = filhos[i].localPosition; }

        const float profundidade = 1.8f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.6f)
        {
            float y = Mathf.Lerp(-profundidade, 0f, 1f - (1f - t) * (1f - t));
            for (int i = 0; i < filhos.Length; i++)
                if (filhos[i] != null) filhos[i].localPosition = orig[i] + Vector3.up * y;
            yield return null;
        }
        for (int i = 0; i < filhos.Length; i++) if (filhos[i] != null) filhos[i].localPosition = orig[i];
        if (ia != null) ia.enabled = true;
    }

    // ---------- Clarão na tela ----------

    void OnGUI()
    {
        if (clarao <= 0.001f) return;
        if (branco == null) { branco = new Texture2D(1, 1); branco.SetPixel(0, 0, Color.white); branco.Apply(); }
        var c = GUI.color;
        GUI.color = new Color(1f, 0.75f, 0.75f, clarao * 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), branco);
        GUI.color = c;
    }

    // ---------- Utilidades ----------

    static Light AcharSol()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        Light melhor = null;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && (melhor == null || l.intensity > melhor.intensity)) melhor = l;
        return melhor;
    }

    static void AmbienteAtual(out Color ceu, out Color horizonte, out Color chao)
    {
        if (RenderSettings.ambientMode == AmbientMode.Trilight)
        {
            ceu = RenderSettings.ambientSkyColor; horizonte = RenderSettings.ambientEquatorColor; chao = RenderSettings.ambientGroundColor;
            return;
        }
        if (RenderSettings.ambientMode == AmbientMode.Flat)
        {
            ceu = horizonte = chao = RenderSettings.ambientLight;
            return;
        }
        var dirs = new[] { Vector3.up, Vector3.forward, Vector3.down };
        var res = new Color[3];
        RenderSettings.ambientProbe.Evaluate(dirs, res);
        ceu = res[0]; horizonte = res[1]; chao = res[2];
    }

    static AudioManager AcharAudioManager() { return FindFirstObjectByType<AudioManager>(); }

    static Transform AcharJogador()
    {
        var v = FindFirstObjectByType<VidaJogador>();
        return v != null ? v.transform : (Camera.main != null ? Camera.main.transform : null);
    }

    static Bounds Limites(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.3f);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}

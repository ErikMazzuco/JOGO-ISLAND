using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ataque da espada. O golpe sai do CENTRO DA TELA (onde fica a miguinha) e acerta o primeiro
/// esqueleto na frente, dentro do alcance. Paredes e o relevo bloqueiam o golpe.
/// A mira (bolinha) é criada sozinha ao iniciar a cena e fica vermelha quando há um esqueleto ao alcance.
/// </summary>
public class AtaqueEspada : MonoBehaviour
{
    [Header("Configurações")]
    public float alcance = 2.5f;
    public float dano = 50f;
    public float intervaloAtaque = 0.8f;
    [Tooltip("Tempo entre o clique e o golpe acertar (segundos), para casar com a animação da espada.")]
    public float atrasoDoGolpe = 0.25f;

    [Header("Mira (bolinha no centro)")]
    public bool mostrarMira = true;
    public float tamanhoMira = 10f;                          // pixels
    public Color corMira = Color.white;
    public Color corMiraNoAlvo = new Color(1f, 0.25f, 0.2f); // quando tem esqueleto ao alcance
    [Tooltip("Espessura do golpe (metros). Maior = mais fácil de acertar.")]
    public float raioGolpe = 0.5f;

    [Header("Animação")]
    public Animator animacaoArma;
    public string nomeAnimacao = "SWORD_ATTACK";

    [Header("Som")]
    [Tooltip("Não é mais usado: o golpe toca numa fonte própria (a do jogador era cortada pelo som dos passos).")]
    public AudioSource audioSource;
    [Tooltip("Som do golpe da espada.")]
    public AudioClip somEspada;
    [Range(0f, 1f)] public float volumeGolpe = 1f;

    float proximoAtaque;
    float momentoDoGolpe;
    bool golpePendente;
    Camera cam;

    GameObject miraCanvas;
    Image miraImagem;
    AudioSource fonteGolpe;

    void Awake()
    {
        // Se houver mais de um AtaqueEspada na cena (ex.: um no PLAYER e outro na espada), cada clique
        // daria o golpe duas vezes e tocaria dois sons. Fica só um: o que está no jogador.
        var todos = FindObjectsByType<AtaqueEspada>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        if (todos.Length <= 1) return;

        AtaqueEspada principal = null;
        foreach (var a in todos)
            if (a.GetComponent<PlayerMovement>() != null) { principal = a; break; }
        if (principal == null) principal = todos[0];

        if (principal != this)
        {
            Debug.LogWarning("Havia mais de um AtaqueEspada na cena; o de '" + name + "' foi desligado (fica só o de '" + principal.name + "').");
            enabled = false;
        }
    }

    void Start()
    {
        cam = Camera.main;
        if (mostrarMira) CriarMira();

        // Fonte de áudio só do golpe. O PlayerMovement chama Stop() na fonte do jogador sempre que
        // ele está parado, e isso cortava o som da espada quando você batia sem andar.
        var go = new GameObject("SomEspada");
        go.transform.SetParent(transform, false);
        fonteGolpe = go.AddComponent<AudioSource>();
        fonteGolpe.playOnAwake = false;
        fonteGolpe.loop = false;
        fonteGolpe.spatialBlend = 0f;
    }

    void OnDestroy()
    {
        if (miraCanvas != null) Destroy(miraCanvas);
    }

    void Update()
    {
        // Parado (tela de morte, pausa): sem golpe e sem mira
        bool parado = Time.timeScale == 0f;
        if (miraCanvas != null) miraCanvas.SetActive(!parado);
        if (parado) return;

        if (miraImagem != null)
            miraImagem.color = AcharAlvo() != null ? corMiraNoAlvo : corMira;

        if (Input.GetMouseButtonDown(0) && Time.time >= proximoAtaque)
        {
            Atacar();
            proximoAtaque = Time.time + intervaloAtaque;
        }

        // O dano é aplicado no meio da animação, olhando para onde a mira está NESSE momento
        if (golpePendente && Time.time >= momentoDoGolpe)
        {
            golpePendente = false;
            AplicarGolpe();
        }
    }

    void Atacar()
    {
        // Executar animação
        if (animacaoArma != null)
            animacaoArma.Play(nomeAnimacao, 0, 0f);

        // Reproduzir som
        if (fonteGolpe != null && somEspada != null)
        {
            fonteGolpe.pitch = Random.Range(0.95f, 1.05f);
            fonteGolpe.PlayOneShot(somEspada, volumeGolpe);
        }

        golpePendente = true;
        momentoDoGolpe = Time.time + Mathf.Max(0f, atrasoDoGolpe);
    }

    void AplicarGolpe()
    {
        VidaSkeleton alvo = AcharAlvo();

        if (alvo == null) alvo = AcharAlvoSemCollider();

        if (alvo != null)
        {
            alvo.ReceberDano(dano);
            Debug.Log("Espada acertou o esqueleto!");
        }
    }

    // Origem do golpe: a câmera do jogador (o centro da tela)
    Transform Origem()
    {
        if (cam == null) cam = Camera.main;
        return cam != null ? cam.transform : transform;
    }

    // Esqueleto que está sob a mira, dentro do alcance (ou null)
    VidaSkeleton AcharAlvo()
    {
        Transform o = Origem();
        RaycastHit[] acertos = Physics.SphereCastAll(o.position, raioGolpe, o.forward, alcance,
                                                     ~0, QueryTriggerInteraction.Ignore);

        System.Array.Sort(acertos, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in acertos)
        {
            // Ignora o próprio jogador (corpo, espada, etc.)
            if (h.collider.GetComponentInParent<VidaJogador>() != null) continue;

            VidaSkeleton esqueleto = h.collider.GetComponentInParent<VidaSkeleton>();

            if (esqueleto != null)
            {
                if (esqueleto.EstaMorto) continue; // atravessa o cadáver
                return esqueleto;
            }

            // Bateu em outra coisa (parede, árvore, chão): o golpe é bloqueado
            return null;
        }

        return null;
    }

    // Plano B: esqueletos sem nenhum Collider no prefab (nesse caso o golpe por colisão nunca os acharia)
    VidaSkeleton AcharAlvoSemCollider()
    {
        Transform o = Origem();

        foreach (VidaSkeleton e in FindObjectsByType<VidaSkeleton>(FindObjectsSortMode.None))
        {
            if (e.EstaMorto) continue;
            if (e.GetComponentInChildren<Collider>() != null) continue;

            Vector3 centro = e.transform.position + Vector3.up * 1f;
            Vector3 dir = centro - o.position;

            if (dir.magnitude > alcance + 0.5f) continue;
            if (Vector3.Angle(o.forward, dir) > 15f) continue;

            Debug.LogWarning("O esqueleto '" + e.name + "' não tem Collider. Adicione um Capsule Collider no prefab.");
            return e;
        }

        return null;
    }

    // ---------- Mira ----------
    void CriarMira()
    {
        miraCanvas = new GameObject("MiraCanvas", typeof(Canvas));
        var canvas = miraCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;

        var go = new GameObject("Mira", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(miraCanvas.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(tamanhoMira, tamanhoMira);

        miraImagem = go.GetComponent<Image>();
        miraImagem.sprite = CriarSpriteBolinha(32);
        miraImagem.color = corMira;
        miraImagem.raycastTarget = false;
    }

    // Bolinha branca com contorno escuro (aparece em qualquer fundo)
    static Sprite CriarSpriteBolinha(int tam)
    {
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float c = (tam - 1) * 0.5f;
        float raioFora = tam * 0.5f - 1f;
        float raioDentro = raioFora * 0.66f;

        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float aFora = Mathf.Clamp01(raioFora - d + 0.5f);
                float aDentro = Mathf.Clamp01(raioDentro - d + 0.5f);

                Color cor = Color.Lerp(new Color(0f, 0f, 0f, 0.75f), Color.white, aDentro);
                cor.a = Mathf.Max(aFora * (aDentro < 1f ? 0.75f : 1f), aDentro);
                if (aFora <= 0f) cor.a = 0f;

                tex.SetPixel(x, y, cor);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f);
    }
}

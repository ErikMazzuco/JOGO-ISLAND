using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Contador de moedas no HUD. Também guarda o total (ContadorMoedas.Total).
/// Normalmente é criado pelo menu  ISLAND > Criar Contador de Moedas.
/// </summary>
public class ContadorMoedas : MonoBehaviour
{
    // ---------- Total (compartilhado com as moedas) ----------
    public static int Total { get; private set; }
    public static event Action<int> TotalMudou;

    public static void Adicionar(int quantidade)
    {
        Total += quantidade;
        if (TotalMudou != null) TotalMudou(Total);
    }

    public static void Zerar()
    {
        Total = 0;
        if (TotalMudou != null) TotalMudou(Total);
    }

    // ---------- HUD ----------
    [Header("Números em imagem (estilo do Home)")]
    public Image[] digitos;            // da esquerda para a direita
    public Sprite[] spritesDigitos;    // 0 a 9
    public float alturaDigito = 50f;

    [Header("Texto alternativo (se não houver imagens dos números)")]
    public TMP_Text textoAlternativo;

    [Header("Efeito ao pegar uma moeda")]
    public float forcaPulso = 0.15f;
    public float velocidadePulso = 7f;

    Vector3 escalaOriginal;
    float pulso;

    void Awake()
    {
        escalaOriginal = transform.localScale;
        Total = 0; // começa a fase com zero moedas
    }

    void Start()
    {
        TotalMudou += AoMudarTotal;
        Atualizar(Total);
    }

    void OnDestroy()
    {
        TotalMudou -= AoMudarTotal;
    }

    void AoMudarTotal(int total)
    {
        pulso = 1f;
        Atualizar(total);
    }

    void Update()
    {
        if (pulso <= 0f) return;

        pulso = Mathf.MoveTowards(pulso, 0f, velocidadePulso * Time.unscaledDeltaTime);
        transform.localScale = escalaOriginal * (1f + forcaPulso * pulso);
    }

    void Atualizar(int total)
    {
        string texto = Mathf.Max(0, total).ToString();

        bool temImagens = digitos != null && digitos.Length > 0 &&
                          spritesDigitos != null && spritesDigitos.Length >= 10;

        if (!temImagens)
        {
            if (textoAlternativo != null) textoAlternativo.text = texto;
            return;
        }

        // Mostra só os últimos dígitos que couberem
        int quantidade = Mathf.Min(texto.Length, digitos.Length);
        string visivel = texto.Substring(texto.Length - quantidade);

        for (int i = 0; i < digitos.Length; i++)
        {
            Image img = digitos[i];
            if (img == null) continue;

            if (i >= quantidade)
            {
                img.gameObject.SetActive(false);
                continue;
            }

            Sprite sprite = spritesDigitos[visivel[i] - '0'];
            if (sprite == null)
            {
                img.gameObject.SetActive(false);
                continue;
            }

            img.sprite = sprite;
            float largura = alturaDigito * sprite.rect.width / sprite.rect.height;
            img.rectTransform.sizeDelta = new Vector2(largura, alturaDigito);
            img.gameObject.SetActive(true);
        }
    }
}

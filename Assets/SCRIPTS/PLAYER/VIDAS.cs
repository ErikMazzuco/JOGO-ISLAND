using System;
using UnityEngine;

public class VidaJogador : MonoBehaviour
{
    [Header("Vida")]
    public float vida = 100f;
    public float vidaMaxima = 100f;

    // Eventos usados pelo HUD (e por qualquer outro sistema, como som de dano ou tela de morte)
    public event Action<float, float> VidaMudou;   // (vida atual, vida máxima)
    public event Action<float> DanoRecebido;       // quantidade de dano
    public event Action Morreu;

    public bool Morto { get; private set; }

    public float Percentual
    {
        get { return vidaMaxima > 0f ? Mathf.Clamp01(vida / vidaMaxima) : 0f; }
    }

    void Awake()
    {
        // Se a vida inicial for maior que o máximo configurado, o máximo acompanha.
        if (vidaMaxima < vida) vidaMaxima = vida;
    }

    public void LevarDano(float dano)
    {
        if (Morto || dano <= 0f) return;

        vida = Mathf.Max(0f, vida - dano);
        Debug.Log("Vida: " + vida);

        if (DanoRecebido != null) DanoRecebido(dano);
        if (VidaMudou != null) VidaMudou(vida, vidaMaxima);

        if (vida <= 0f)
        {
            Morto = true;
            Debug.Log("Jogador morreu");
            if (Morreu != null) Morreu();
        }
    }

    public void Curar(float quantidade)
    {
        if (Morto || quantidade <= 0f) return;

        vida = Mathf.Min(vidaMaxima, vida + quantidade);
        if (VidaMudou != null) VidaMudou(vida, vidaMaxima);
    }
}

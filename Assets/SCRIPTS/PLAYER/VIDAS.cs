using UnityEngine;

public class VidaJogador : MonoBehaviour
{
    public float vida = 100f;

    public void LevarDano(float dano)
    {
        vida -= dano;
        Debug.Log("Vida: " + vida);
        if (vida <= 0) Debug.Log("Jogador morreu");
    }
}
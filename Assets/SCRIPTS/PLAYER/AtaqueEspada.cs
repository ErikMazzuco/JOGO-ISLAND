
using UnityEngine;

public class AtaqueEspada : MonoBehaviour
{
    [Header("Configurações")]
    public float alcance = 2.5f;
    public float dano = 50f;
    public float intervaloAtaque = 0.8f;

    private float proximoAtaque;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && Time.time >= proximoAtaque)
        {
            Atacar();
            proximoAtaque = Time.time + intervaloAtaque;
        }
    }

    void Atacar()
    {
        RaycastHit hit;

        if (Physics.Raycast(transform.position, transform.forward,
            out hit, alcance))
        {
            VidaSkeleton inimigo =
                hit.collider.GetComponentInParent<VidaSkeleton>();

            if (inimigo != null)
            {
                inimigo.ReceberDano(dano);
                Debug.Log("Espada acertou o esqueleto!");
            }
        }
    }
}

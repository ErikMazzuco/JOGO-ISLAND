
using UnityEngine;

public class FlutuarTitulo : MonoBehaviour
{
    public float altura = 15f;
    public float velocidade = 1.5f;

    private Vector3 posicaoInicial;

    void Start()
    {
        posicaoInicial = transform.localPosition;
    }

    void Update()
    {
        float movimento = Mathf.Sin(Time.time * velocidade) * altura;

        transform.localPosition = posicaoInicial +
            new Vector3(0, movimento, 0);
    }
}

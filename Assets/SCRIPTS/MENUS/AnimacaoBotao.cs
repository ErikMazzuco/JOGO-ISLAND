
using UnityEngine;
using UnityEngine.EventSystems;

public class AnimacaoBotao : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    public float escalaHover = 1.05f;
    public float velocidade = 12f;

    private Vector3 escalaOriginal;
    private Vector3 escalaAlvo;

    void Start()
    {
        escalaOriginal = transform.localScale;
        escalaAlvo = escalaOriginal;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            escalaAlvo,
            Time.deltaTime * velocidade
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        escalaAlvo = escalaOriginal * escalaHover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        escalaAlvo = escalaOriginal;
    }
}

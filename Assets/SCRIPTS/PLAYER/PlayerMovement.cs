
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimentação")]
    public float velocidade = 5f;
    public float velocidadeCorrida = 9f;
    public float alturaPulo = 1.5f;
    public float gravidade = -20f;
    private float tempoNoChao;

    [Header("Camera")]
    public Transform cameraPlayer;
    public float sensibilidadeMouse = 200f;

    private CharacterController controller;
    private Vector3 velocidadeVertical;
    private float rotacaoX = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Movimentar();
        ControlarCamera();
    }

    
    
void Movimentar()
{
    float x = Input.GetAxisRaw("Horizontal");
    float z = Input.GetAxisRaw("Vertical");

    bool correndo = Input.GetKey(KeyCode.LeftShift);

    float velocidadeAtual = correndo ? velocidadeCorrida : velocidade;

    // Verifica o chão ANTES de movimentar
    bool noChao = controller.isGrounded;

    if (noChao && velocidadeVertical.y < 0)
    {
        velocidadeVertical.y = -2f;
    }

    // Pulo
    if (Input.GetKeyDown(KeyCode.Space) && noChao)
    {
        velocidadeVertical.y = Mathf.Sqrt(alturaPulo * -2f * gravidade);
    }

    // Gravidade
    velocidadeVertical.y += gravidade * Time.deltaTime;

    // Movimento horizontal
    Vector3 movimento = transform.right * x + transform.forward * z;

    Vector3 movimentoFinal = movimento.normalized * velocidadeAtual;

    // Junta movimento horizontal e vertical
    movimentoFinal.y = velocidadeVertical.y;

    controller.Move(movimentoFinal * Time.deltaTime);
}



    void ControlarCamera()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensibilidadeMouse * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensibilidadeMouse * Time.deltaTime;

        rotacaoX -= mouseY;
        rotacaoX = Mathf.Clamp(rotacaoX, -90f, 90f);

        cameraPlayer.localRotation = Quaternion.Euler(rotacaoX, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }
}

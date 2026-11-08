
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimentação")]
    public float velocidade = 5f;
    public float velocidadeCorrida = 9f;
    public float alturaPulo = 1.5f;
    public float gravidade = -20f;

    [Header("Camera")]
    public Transform cameraPlayer;
    public float sensibilidadeMouse = 200f;

    [Header("Sons dos passos")]
    public AudioClip passosAndando;
    public AudioClip passosCorrendo;

    public float intervaloAndando = 0.48f;
    public float intervaloCorrendo = 0.30f;

    private AudioSource audioSource;
    private CharacterController controller;
    private Vector3 velocidadeVertical;
    private float rotacaoX = 0f;
    private float proximoPasso;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

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

        bool noChao = controller.isGrounded;

        if (noChao && velocidadeVertical.y < 0)
        {
            velocidadeVertical.y = -2f;
        }

        if (Input.GetKeyDown(KeyCode.Space) && noChao)
        {
            velocidadeVertical.y = Mathf.Sqrt(alturaPulo * -2f * gravidade);
        }

        velocidadeVertical.y += gravidade * Time.deltaTime;

        Vector3 movimento = transform.right * x + transform.forward * z;

        Vector3 movimentoFinal = movimento.normalized * velocidadeAtual;

        movimentoFinal.y = velocidadeVertical.y;

        controller.Move(movimentoFinal * Time.deltaTime);

        
        // Sons dos passos
        bool andando = new Vector2(x, z).magnitude > 0.1f;
        bool estaNoChao = controller.isGrounded;

        if (andando && estaNoChao)
        {
            if (!audioSource.isPlaying && Time.time >= proximoPasso)
            {
                AudioClip som = correndo && passosCorrendo != null
                    ? passosCorrendo
                    : passosAndando;

                if (som != null)
                {
                    audioSource.clip = som;
                    audioSource.Play();

                    proximoPasso = Time.time +
                        Mathf.Max(
                            correndo ? intervaloCorrendo : intervaloAndando,
                            som.length
                        );
                }
            }
        }
        else
        {
            audioSource.Stop();
            proximoPasso = 0f;
        }

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

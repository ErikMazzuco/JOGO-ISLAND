
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

    [Header("Pulo mais gostoso")]
    [Tooltip("Tempo extra (s) em que ainda dá para pular depois de sair da beirada do chão.")]
    public float tempoCoyote = 0.15f;
    [Tooltip("Tempo (s) que o pulo fica 'guardado' se você apertar antes de tocar no chão.")]
    public float tempoPuloAntecipado = 0.15f;
    [Tooltip("Soltar o espaço no meio do pulo faz um pulo mais baixo.")]
    public bool puloVariavel = true;
    [Range(0.1f, 1f)] public float corteDoPulo = 0.5f;

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
    private float ultimoNoChao = -10f;
    private float ultimoPuloApertado = -10f;

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

        // Guarda o último momento em que estava no chão (coyote time)
        if (noChao) ultimoNoChao = Time.time;

        // Guarda o último momento em que apertou pulo (pulo antecipado)
        if (Input.GetKeyDown(KeyCode.Space)) ultimoPuloApertado = Time.time;

        bool podePular = Time.time - ultimoNoChao <= tempoCoyote;
        bool querPular = Time.time - ultimoPuloApertado <= tempoPuloAntecipado;

        if (podePular && querPular)
        {
            velocidadeVertical.y = Mathf.Sqrt(alturaPulo * -2f * gravidade);

            // Gasta os dois, senão pularia duas vezes seguidas
            ultimoNoChao = -10f;
            ultimoPuloApertado = -10f;
        }

        // Soltou o espaço subindo: corta o pulo (pulo curto)
        if (puloVariavel && Input.GetKeyUp(KeyCode.Space) && velocidadeVertical.y > 0f)
            velocidadeVertical.y *= corteDoPulo;

        velocidadeVertical.y += gravidade * Time.deltaTime;

        Vector3 movimento = transform.right * x + transform.forward * z;

        Vector3 movimentoFinal = movimento.normalized * velocidadeAtual;

        movimentoFinal.y = velocidadeVertical.y;

        CollisionFlags colisao = controller.Move(movimentoFinal * Time.deltaTime);

        // Bateu a cabeça no teto: para de subir (senão fica "colado" no teto)
        if ((colisao & CollisionFlags.Above) != 0 && velocidadeVertical.y > 0f)
            velocidadeVertical.y = 0f;

        
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
        // A sensibilidade escolhida nas Opções multiplica o valor base
        float sensibilidade = sensibilidadeMouse * Configuracoes.Sensibilidade;

        float mouseX = Input.GetAxis("Mouse X") * sensibilidade * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensibilidade * Time.deltaTime;

        rotacaoX -= mouseY;
        rotacaoX = Mathf.Clamp(rotacaoX, -90f, 90f);

        cameraPlayer.localRotation = Quaternion.Euler(rotacaoX, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }
}

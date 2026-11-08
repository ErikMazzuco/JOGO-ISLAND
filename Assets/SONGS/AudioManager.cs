
using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    [Header("Sons de ambiente")]
    public AudioClip musicaFundo;
    public AudioClip somVento;

    [Header("Volume")]
    [Range(0f, 1f)]
    public float volumeMusica = 0.4f;

    [Range(0f, 1f)]
    public float volumeVento = 0.25f;

    [Header("Intervalo do vento")]
    public float intervaloMinimo = 8f;
    public float intervaloMaximo = 18f;

    private AudioSource audioMusica;
    private AudioSource audioVento;

    void Start()
    {
        // Música de fundo
        audioMusica = gameObject.AddComponent<AudioSource>();
        audioMusica.clip = musicaFundo;
        audioMusica.loop = true;
        audioMusica.playOnAwake = false;
        audioMusica.volume = volumeMusica;
        audioMusica.spatialBlend = 0f;

        // Vento
        audioVento = gameObject.AddComponent<AudioSource>();
        audioVento.clip = somVento;
        audioVento.loop = false;
        audioVento.playOnAwake = false;
        audioVento.volume = volumeVento;
        audioVento.spatialBlend = 0f;

        if (musicaFundo != null)
            audioMusica.Play();

        if (somVento != null)
            StartCoroutine(TocarVento());
    }

    IEnumerator TocarVento()
    {
        while (true)
        {
            float intervalo = Random.Range(
                intervaloMinimo,
                intervaloMaximo
            );

            yield return new WaitForSeconds(intervalo);

            audioVento.Play();
            
            yield return new WaitForSeconds(somVento.length);
        }
    }
}

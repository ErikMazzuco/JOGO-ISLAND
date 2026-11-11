
using UnityEngine;

public class AudioMenu : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip somClique;

    public void TocarClique()
    {
        audioSource.PlayOneShot(somClique);
    }
}

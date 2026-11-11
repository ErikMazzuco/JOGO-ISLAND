
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipal : MonoBehaviour
{
    public string nomeCenaJogo = "Island";

    public void Jogar()
    {
        SceneManager.LoadScene(nomeCenaJogo);
    }

    public void Sair()
    {
        Application.Quit();
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void RunSimulation()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void QuitSimulation()
    {
        Debug.Log("QUIT SIMULATION!");
        Application.Quit();
    }
}

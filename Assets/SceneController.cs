using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    private int currentSceneIndex;
    public int delay = 3;

    void Start()
    {
        currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
    }

    public void ReloadSceneWithNoDelay()
    {
        ReloadNoDelay();
    }

    private void ReloadNoDelay()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }

    public void ReloadSceneWithDelay()
    {
        StartCoroutine(ReloadAfterDelay());
    }

    private IEnumerator ReloadAfterDelay()
    {
        yield return new WaitForSeconds(delay);

        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }

    // Переход по индексу с задержкой
    public void LoadSceneByIndex(int sceneIndex, float delay = 0f)
    {
        if (sceneIndex >= 0 && sceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            StartCoroutine(LoadSceneWithDelay(sceneIndex));
        }
        else
        {
            Debug.Log("Levels finished");
        }
    }

    // Переход по условию с задержкой
    public void CheckAndLoadScene(bool condition, float delay = 0f)
    {
        if (condition)
        {
            LoadSceneByIndex(currentSceneIndex + 1, delay);
        }
        else
        {
            Debug.Log("Условие не выполнено, переход невозможен.");
        }
    }

    // Загрузка сцены с задержкой
    private IEnumerator LoadSceneWithDelay(int sceneIndex)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneIndex);
    }
}

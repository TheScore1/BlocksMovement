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

    // ������� �� ������� � ���������
    public void LoadSceneByIndex(int sceneIndex, float delay = 0f)
    {
        if (sceneIndex >= 0 && sceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            StartCoroutine(LoadSceneWithDelay(sceneIndex));
        }
        else
        {
            Debug.LogError("Incorrect Scene Index: " + sceneIndex);
        }
    }

    // ������� �� ������� � ���������
    public void CheckAndLoadScene(bool condition, float delay = 0f)
    {
        if (condition)
        {
            LoadSceneByIndex(currentSceneIndex + 1, delay);
        }
        else
        {
            Debug.Log("������� �� ���������, ������� ����������.");
        }
    }

    // �������� ����� � ���������
    private IEnumerator LoadSceneWithDelay(int sceneIndex)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneIndex);
    }
}

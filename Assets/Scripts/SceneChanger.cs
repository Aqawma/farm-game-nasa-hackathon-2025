using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    public TMP_InputField temperature;
    public TMP_InputField humidity;
    public TMP_InputField sunRadiation;
    public TMP_InputField rainfall;

    void Start()
    {

    }

    public void ChangeScene(int sceneNumber)
    {
        SceneManager.LoadScene(sceneNumber);
    }

    public void SaveDatas()
    {
        PlayerPrefs.SetFloat("temp", float.Parse(temperature.text));
        PlayerPrefs.SetFloat("humidity", float.Parse(humidity.text));
        PlayerPrefs.SetFloat("sun", float.Parse(sunRadiation.text));
        PlayerPrefs.SetFloat("rain", float.Parse(rainfall.text));
    }
}
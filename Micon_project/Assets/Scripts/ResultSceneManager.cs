using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;


public class ResultSceneManager : MonoBehaviour
{
    private int score;

    private float time_count = 0;
    [SerializeField] private TextMeshProUGUI scoreText;

    void Start()
    {
        score = DataManager.GetScore();
        if (scoreText != null)
        {
            scoreText.text = "" + score;
        }

        time_count = 0;
    }

    
    void Update()
    {
        time_count += Time.deltaTime;

        if (time_count >= 60f)
        {
            SceneManager.LoadScene("StartScene");
        }
    }
}

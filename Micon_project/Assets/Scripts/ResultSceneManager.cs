using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;


public class ResultSceneManager : MonoBehaviour
{
    private int score;

    private float time_count = 0;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Propeller propeller_L;
    [SerializeField] private Propeller propeller_R;

    void Start()
    {
        score = DataManager.GetScore();
        if (scoreText != null)
        {
            scoreText.text = "" + score;
        }

        time_count = 0;

        propeller_L.SetRotationSpeed(120f);
        propeller_R.SetRotationSpeed(50f);
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

using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Stone Prefabs")]
    [SerializeField] private GameObject[] stonePrefabs;

    [Header("Score UI")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Spawn Timing")]
    [SerializeField] private float spawn_timing_early = 6f;
    [SerializeField] private float spawn_timing_late = 10f;
    [SerializeField] private float spawn_timing_update = 30f;
    [SerializeField] private float spawn_timing_short_ratio = 0.85f;

    private float spawn_range_x_min = -24f;
    private float spawn_range_x_max = 24f;

    private float spawn_range_y_min = 0f;
    private float spawn_range_y_max = 20f;

    private float spawn_range_z_min = 200f;
    private float spawn_range_z_max = 250f;

    private float time_count = 0f;

    private float next_spawn_time = 0f;
    private float next_timing_update_time = 0f;

    private void Start()
    {
        time_count = 0f;

        DataManager.Initialize();

        SpawnStone();
        ScheduleNextSpawn();

        next_timing_update_time = spawn_timing_update;

        UpdateScoreText();
    }

    private void Update()
    {
        time_count += Time.deltaTime;

        if (time_count >= next_timing_update_time)
        {
            ShortenSpawnTiming();
            next_timing_update_time += spawn_timing_update;
        }

        if (time_count >= next_spawn_time)
        {
            SpawnStone();
            ScheduleNextSpawn();
        }

        DataManager.AddScore(1);
        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        if (scoreText == null) return;

        scoreText.text = "" + DataManager.GetScore();
    }

    private void ScheduleNextSpawn()
    {
        float randomTiming = Random.Range(spawn_timing_early, spawn_timing_late);
        next_spawn_time = time_count + randomTiming;
    }

    private void ShortenSpawnTiming()
    {
        spawn_timing_early *= spawn_timing_short_ratio;
        spawn_timing_late *= spawn_timing_short_ratio;

        Debug.Log("スポーン間隔を短縮: " + spawn_timing_early + " ～ " + spawn_timing_late);
    }

    private bool SpawnStone()
    {
        if (stonePrefabs == null || stonePrefabs.Length == 0)
        {
            Debug.LogWarning("岩のプレハブが設定されていません。");
            return false;
        }

        int randomIndex = Random.Range(0, stonePrefabs.Length);
        GameObject selectedStone = stonePrefabs[randomIndex];

        if (selectedStone == null)
        {
            Debug.LogWarning("選ばれた岩プレハブがnullです。");
            return false;
        }

        float x = Random.Range(spawn_range_x_min, spawn_range_x_max);
        float y = Random.Range(spawn_range_y_min, spawn_range_y_max);
        float z = Random.Range(spawn_range_z_min, spawn_range_z_max);

        Vector3 spawnPosition = new Vector3(x, y, z);

        Instantiate(selectedStone, spawnPosition, Quaternion.identity);

        return true;
    }

    public void FinishGame()
    {
        SceneManager.LoadScene("ResultScene");
    }
}
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Stone Prefabs")]
    [SerializeField] private GameObject[] stonePrefabs;

    [Header("Spawn Timing")]
    [SerializeField] private float spawn_timing_early = 6f;
    [SerializeField] private float spawn_timing_late = 10f;
    [SerializeField] private float spawn_timing_update = 30f;
    [SerializeField] private float spawn_timing_short_ratio = 0.85f;

    // [Header("Spawn Range")]
    private float spawn_range_x_min = -15f;
    private float spawn_range_x_max = 15f;

    private float spawn_range_y_min = 2f;
    private float spawn_range_y_max = 20f;

    private float spawn_range_z_min = 200f;
    private float spawn_range_z_max = 250f;

    [Header("UI")]
    [SerializeField] private AirplaneHpSlider airplaneHpSlider;

    private float time_count = 0f;

    private float next_spawn_time = 0f;
    private float next_timing_update_time = 0f;

    private void Start()
    {
        time_count = 0f;

        SpawnStone();
        ScheduleNextSpawn();

        next_timing_update_time = spawn_timing_update;
    }

    private void Update()
    {
        time_count += Time.deltaTime;

        // 一定時間ごとにスポーン間隔を短くする
        if (time_count >= next_timing_update_time)
        {
            ShortenSpawnTiming();

            next_timing_update_time += spawn_timing_update;
        }

        // 岩を生成する
        if (time_count >= next_spawn_time)
        {
            SpawnStone();

            ScheduleNextSpawn();
        }
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
}
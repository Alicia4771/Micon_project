using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Communication")]
    [SerializeField]
    private SensorReceiver sensorReceiver;

    [SerializeField]
    private radar radar;

    [Header("Radar Send Settings")]
    [SerializeField, Min(0.01f)]
    private float radar_send_interval = 1f;

    private float radar_send_time_count = 0f;

    [Header("Stone Prefabs")]
    [SerializeField]
    private GameObject[] stonePrefabs;

    [Header("Score UI")]
    [SerializeField]
    private TextMeshProUGUI scoreText;

    [Header("Spawn Timing")]
    [SerializeField]
    private float spawn_timing_early = 6f;

    [SerializeField]
    private float spawn_timing_late = 10f;

    [SerializeField]
    private float spawn_timing_update = 30f;

    [SerializeField]
    private float spawn_timing_short_ratio = 0.85f;

    private float spawn_range_x_min = -26f;
    private float spawn_range_x_max = 26f;

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
        radar_send_time_count = 0f;

        // 送信間隔に不正な値が設定されていた場合
        if (radar_send_interval <= 0f)
        {
            radar_send_interval = 1f;
        }

        DataManager.Initialize();

        SpawnStone();
        ScheduleNextSpawn();

        next_timing_update_time = spawn_timing_update;

        UpdateScoreText();
    }

    private void Update()
    {
        time_count += Time.deltaTime;

        // 一定時間ごとに岩の生成間隔を短縮する
        if (time_count >= next_timing_update_time)
        {
            ShortenSpawnTiming();

            next_timing_update_time +=
                spawn_timing_update;
        }

        // 岩を生成する
        if (time_count >= next_spawn_time)
        {
            SpawnStone();
            ScheduleNextSpawn();
        }

        // センサーデータを取得する
        if (sensorReceiver != null)
        {
            DataManager.SetSensorValue(
                sensorReceiver.GetSensorData()
            );
        }

        DataManager.AddScore(1);

        // レーダーの送信時間を加算する
        radar_send_time_count += Time.deltaTime;

        // 指定した秒数ごとにレーダー情報を送信する
        if (radar_send_time_count >= radar_send_interval)
        {
            /*
             * 余った時間を残すことで、
             * フレームレートによる送信間隔のずれを抑える
             */
            radar_send_time_count -=
                radar_send_interval;

            SendRadarData();
        }

        UpdateScoreText();
    }

    /// <summary>
    /// レーダー情報を取得し、Arduinoへ送信する
    /// </summary>
    private void SendRadarData()
    {
        if (radar == null)
        {
            Debug.LogWarning(
                "GameManagerのRadarが設定されていません。"
            );

            return;
        }

        if (sensorReceiver == null)
        {
            Debug.LogWarning(
                "GameManagerのSensor Receiverが設定されていません。"
            );

            return;
        }

        /*
         * 16×16のレーダーデータを、
         * 8個のuintに圧縮した配列として取得する
         */
        uint[] packedRadarData =
            radar.GetRadarData();

        if (packedRadarData == null ||
            packedRadarData.Length != 8)
        {
            Debug.LogWarning(
                "レーダーデータを正しく取得できませんでした。"
            );

            return;
        }

        // 8個のuintをCSV形式の文字列へ変換する
        string radarCsv =
            string.Join(",", packedRadarData);

        // Arduinoへ送信する
        sensorReceiver.SendToArduino(radarCsv);
    }

    /// <summary>
    /// スコア表示を更新する
    /// </summary>
    private void UpdateScoreText()
    {
        if (scoreText == null)
        {
            return;
        }

        scoreText.text =
            DataManager.GetScore().ToString();
    }

    /// <summary>
    /// 次に岩を生成する時間を決定する
    /// </summary>
    private void ScheduleNextSpawn()
    {
        float randomTiming = Random.Range(
            spawn_timing_early,
            spawn_timing_late
        );

        next_spawn_time =
            time_count + randomTiming;
    }

    /// <summary>
    /// 岩の生成間隔を短縮する
    /// </summary>
    private void ShortenSpawnTiming()
    {
        spawn_timing_early *=
            spawn_timing_short_ratio;

        spawn_timing_late *=
            spawn_timing_short_ratio;

        Debug.Log(
            "スポーン間隔を短縮: " +
            spawn_timing_early +
            " ～ " +
            spawn_timing_late
        );
    }

    /// <summary>
    /// 岩をランダムな位置に生成する
    /// </summary>
    private bool SpawnStone()
    {
        if (stonePrefabs == null ||
            stonePrefabs.Length == 0)
        {
            Debug.LogWarning(
                "岩のプレハブが設定されていません。"
            );

            return false;
        }

        int randomIndex = Random.Range(
            0,
            stonePrefabs.Length
        );

        GameObject selectedStone =
            stonePrefabs[randomIndex];

        if (selectedStone == null)
        {
            Debug.LogWarning(
                "選ばれた岩プレハブがnullです。"
            );

            return false;
        }

        float x = Random.Range(
            spawn_range_x_min,
            spawn_range_x_max
        );

        float y = Random.Range(
            spawn_range_y_min,
            spawn_range_y_max
        );

        float z = Random.Range(
            spawn_range_z_min,
            spawn_range_z_max
        );

        Vector3 spawnPosition =
            new Vector3(x, y, z);

        Instantiate(
            selectedStone,
            spawnPosition,
            Quaternion.identity
        );

        return true;
    }

    /// <summary>
    /// リザルト画面へ移動する
    /// </summary>
    public void FinishGame()
    {
        SceneManager.LoadScene("ResultScene");
    }
}
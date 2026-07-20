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
    [SerializeField, Min(0.01f),
     Tooltip("レーダー情報をArduinoへ送信する間隔（秒）")]
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

    [Header("Stone Spawn Range")]
    [SerializeField]
    private float spawn_range_x_min = -26f;

    [SerializeField]
    private float spawn_range_x_max = 26f;

    [SerializeField]
    private float spawn_range_y_min = 0f;

    [SerializeField]
    private float spawn_range_y_max = 20f;

    [SerializeField]
    private float spawn_range_z_min = 200f;

    [SerializeField]
    private float spawn_range_z_max = 250f;

    private float time_count = 0f;

    private float next_spawn_time = 0f;
    private float next_timing_update_time = 0f;

    private void Start()
    {
        time_count = 0f;
        radar_send_time_count = 0f;

        /*
         * 送信間隔に0以下が設定されていた場合は、
         * 1秒へ戻す。
         */
        if (radar_send_interval <= 0f)
        {
            radar_send_interval = 1f;
        }

        DataManager.Initialize();

        SpawnStone();
        ScheduleNextSpawn();

        next_timing_update_time =
            spawn_timing_update;

        UpdateScoreText();

        if (sensorReceiver == null)
        {
            Debug.LogWarning(
                "GameManagerのSensor Receiverが設定されていません。",
                this
            );
        }

        if (radar == null)
        {
            Debug.LogWarning(
                "GameManagerのRadarが設定されていません。",
                this
            );
        }
    }

    private void Update()
    {
        time_count += Time.deltaTime;

        /*
         * 一定時間ごとに、
         * 岩の生成間隔を短くする。
         */
        if (time_count >=
            next_timing_update_time)
        {
            ShortenSpawnTiming();

            next_timing_update_time +=
                spawn_timing_update;
        }

        /*
         * 次の生成時刻になったら岩を生成する。
         */
        if (time_count >= next_spawn_time)
        {
            SpawnStone();
            ScheduleNextSpawn();
        }

        /*
         * シリアル通信で受信した
         * センサーデータをDataManagerへ保存する。
         */
        if (sensorReceiver != null)
        {
            DataManager.SetSensorValue(
                sensorReceiver.GetSensorData()
            );
        }

        /*
         * 現在の仕様では毎フレーム1点加算する。
         */
        DataManager.AddScore(1);

        /*
         * レーダー送信用タイマーを進める。
         */
        radar_send_time_count +=
            Time.deltaTime;

        /*
         * Inspectorで設定した秒数ごとに、
         * レーダー情報を更新して送信する。
         */
        if (radar_send_time_count >=
            radar_send_interval)
        {
            /*
             * 0へ戻すのではなく間隔分を引くことで、
             * フレーム時間による誤差の蓄積を抑える。
             */
            radar_send_time_count -=
                radar_send_interval;

            SendRadarData();
        }

        UpdateScoreText();
    }

    /// <summary>
    /// レーダー情報と接近情報をArduinoへ送信する
    /// </summary>
    private void SendRadarData()
    {
        if (radar == null)
        {
            Debug.LogWarning(
                "GameManagerのRadarが設定されていません。",
                this
            );

            return;
        }

        if (sensorReceiver == null)
        {
            Debug.LogWarning(
                "GameManagerのSensor Receiverが設定されていません。",
                this
            );

            return;
        }

        /*
         * radar.csから9個のuintを取得する。
         *
         * packedRadarData[0]～[7]
         *     16×16のレーダーデータ
         *
         * packedRadarData[8]
         *     bit 0～7   ：下側接近度
         *     bit 8～15  ：上側接近度
         *     bit 16～23 ：障害物接近・振動強度
         */
        uint[] packedRadarData =
            radar.GetRadarData();

        if (packedRadarData == null)
        {
            Debug.LogWarning(
                "レーダーデータがnullです。",
                this
            );

            return;
        }

        if (packedRadarData.Length != 9)
        {
            Debug.LogWarning(
                "レーダーデータは9個必要です。" +
                $"現在は{packedRadarData.Length}個です。",
                this
            );

            return;
        }

        /*
         * 9個のuintをCSV形式へ変換する。
         *
         * 例：
         * 0,32,0,0,0,0,25165824,0,16744575
         */
        string radarCsv =
            string.Join(
                ",",
                packedRadarData
            );

        /*
         * 改行付きでArduinoへ送信する。
         */
        bool sendResult =
            sensorReceiver.SendToArduino(
                radarCsv
            );

        if (!sendResult)
        {
            Debug.LogWarning(
                "レーダーデータの送信に失敗しました。",
                this
            );
        }
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
            DataManager
                .GetScore()
                .ToString();
    }

    /// <summary>
    /// 次に岩を生成する時刻を決定する
    /// </summary>
    private void ScheduleNextSpawn()
    {
        /*
         * 最小値と最大値が逆になった場合にも
         * Random.Rangeが正しく使えるようにする。
         */
        float early =
            Mathf.Min(
                spawn_timing_early,
                spawn_timing_late
            );

        float late =
            Mathf.Max(
                spawn_timing_early,
                spawn_timing_late
            );

        float randomTiming =
            Random.Range(
                early,
                late
            );

        next_spawn_time =
            time_count + randomTiming;
    }

    /// <summary>
    /// 岩の生成間隔を短縮する
    /// </summary>
    private void ShortenSpawnTiming()
    {
        spawn_timing_short_ratio =
            Mathf.Clamp(
                spawn_timing_short_ratio,
                0.01f,
                1f
            );

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
    /// 岩をランダムな位置へ生成する
    /// </summary>
    private bool SpawnStone()
    {
        if (stonePrefabs == null ||
            stonePrefabs.Length == 0)
        {
            Debug.LogWarning(
                "岩のプレハブが設定されていません。",
                this
            );

            return false;
        }

        int randomIndex =
            Random.Range(
                0,
                stonePrefabs.Length
            );

        GameObject selectedStone =
            stonePrefabs[randomIndex];

        if (selectedStone == null)
        {
            Debug.LogWarning(
                "選ばれた岩プレハブがnullです。",
                this
            );

            return false;
        }

        float x =
            Random.Range(
                Mathf.Min(
                    spawn_range_x_min,
                    spawn_range_x_max
                ),
                Mathf.Max(
                    spawn_range_x_min,
                    spawn_range_x_max
                )
            );

        float y =
            Random.Range(
                Mathf.Min(
                    spawn_range_y_min,
                    spawn_range_y_max
                ),
                Mathf.Max(
                    spawn_range_y_min,
                    spawn_range_y_max
                )
            );

        float z =
            Random.Range(
                Mathf.Min(
                    spawn_range_z_min,
                    spawn_range_z_max
                ),
                Mathf.Max(
                    spawn_range_z_min,
                    spawn_range_z_max
                )
            );

        Vector3 spawnPosition =
            new Vector3(
                x,
                y,
                z
            );

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
        SceneManager.LoadScene(
            "ResultScene"
        );
    }
}
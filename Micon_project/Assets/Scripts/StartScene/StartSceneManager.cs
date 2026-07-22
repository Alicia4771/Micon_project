using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class StartSceneManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField]
    private string nextSceneName = "AirplaneChooseScene";

    [Header("Sensor Settings")]
    [SerializeField]
    private SensorReceiver sensorReceiver;

    [SerializeField, Tooltip("基準姿勢から何度傾いたら遷移するか")]
    private float tiltThresholdDegrees = 40.0f;

    [SerializeField, Tooltip("基準角度を取得するまでの待機時間")]
    private float calibrationDelaySeconds = 1.0f;

    // センサー受信前にSensorReceiverが返す初期値
    private const string DefaultSensorData =
        "0,0,0,0,0,0,0,0,0";

    // センサーの基準姿勢
    private Vector3 initialEulerAngle;

    // 基準姿勢を取得済みか
    private bool hasInitialEulerAngle = false;

    // シーン遷移を重複して実行しないためのフラグ
    private bool isChangingScene = false;

    // 最初に有効なセンサーデータを取得した時間
    private float firstValidSensorDataTime = -1.0f;

    private void Awake()
    {
        // Inspectorで設定されていない場合は自動で探す
        if (sensorReceiver == null)
        {
            sensorReceiver =
                FindFirstObjectByType<SensorReceiver>();
        }
    }

    private void Start()
    {
        if (sensorReceiver == null)
        {
            Debug.LogWarning(
                "SensorReceiverが見つかりません。" +
                "Enterキーによるシーン遷移のみ使用できます。"
            );
        }
    }

    private void Update()
    {
        if (isChangingScene)
        {
            return;
        }

        // Enterキーが押された場合
        if (IsEnterPressed())
        {
            ChangeScene("Enterキーが押されました。");
            return;
        }

        // センサーによる傾き判定
        CheckSensorTilt();
    }

    /// <summary>
    /// Enterキーが押されたか確認する
    /// </summary>
    private bool IsEnterPressed()
    {
#if ENABLE_INPUT_SYSTEM
        // 新しいInput System
        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        // 古いInput Manager
        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            return true;
        }
#endif

        return false;
    }

    /// <summary>
    /// センサーの傾きを確認する
    /// </summary>
    private void CheckSensorTilt()
    {
        if (sensorReceiver == null)
        {
            return;
        }

        // SensorReceiverから現在の受信文字列を取得
        string rawSensorData = sensorReceiver.GetSensorData();

        if (string.IsNullOrWhiteSpace(rawSensorData))
        {
            return;
        }

        rawSensorData = rawSensorData.Trim();

        /*
         * SensorReceiverは実際のデータを受信する前に
         * "0,0,0,0,0,0,0,0,0"を返すため除外する
         */
        if (rawSensorData == DefaultSensorData)
        {
            return;
        }

        // DataManagerにセンサー値を保存
        if (!DataManager.SetSensorValue(rawSensorData))
        {
            return;
        }

        Vector3 currentEulerAngle =
            DataManager.GetEulerSensorValue();

        /*
         * 最初の有効なデータを受け取った直後は、
         * センサー値が安定していない可能性があるため少し待つ
         */
        if (firstValidSensorDataTime < 0.0f)
        {
            firstValidSensorDataTime = Time.time;
            return;
        }

        if (Time.time - firstValidSensorDataTime <
            calibrationDelaySeconds)
        {
            return;
        }

        // 待機後の角度を基準姿勢として保存
        if (!hasInitialEulerAngle)
        {
            initialEulerAngle = currentEulerAngle;
            hasInitialEulerAngle = true;

            Debug.Log(
                "センサーの基準姿勢を取得しました。" +
                $" X={initialEulerAngle.x:F1}," +
                $" Y={initialEulerAngle.y:F1}," +
                $" Z={initialEulerAngle.z:F1}"
            );

            return;
        }

        /*
         * Mathf.DeltaAngleを使用することで、
         * 359度から1度への変化を358度ではなく
         * 2度として計算できる
         */
        float differenceX = Mathf.Abs(
            Mathf.DeltaAngle(
                initialEulerAngle.x,
                currentEulerAngle.x
            )
        );

        float differenceY = Mathf.Abs(
            Mathf.DeltaAngle(
                initialEulerAngle.y,
                currentEulerAngle.y
            )
        );

        float differenceZ = Mathf.Abs(
            Mathf.DeltaAngle(
                initialEulerAngle.z,
                currentEulerAngle.z
            )
        );

        // 確認用ログ。不要ならコメントアウト可能
        Debug.Log(
            $"角度差 X={differenceX:F1}, " +
            $"Y={differenceY:F1}, " +
            $"Z={differenceZ:F1}"
        );

        // X、Y、Zのどれかがしきい値以上変化した場合
        if (differenceX >= tiltThresholdDegrees ||
            differenceY >= tiltThresholdDegrees ||
            differenceZ >= tiltThresholdDegrees)
        {
            ChangeScene(
                "センサーがしきい値以上傾きました。" +
                $" X差={differenceX:F1}," +
                $" Y差={differenceY:F1}," +
                $" Z差={differenceZ:F1}"
            );
        }
    }

    /// <summary>
    /// 指定したシーンへ遷移する
    /// </summary>
    private void ChangeScene(string reason)
    {
        if (isChangingScene)
        {
            return;
        }

        isChangingScene = true;

        Debug.Log(
            reason +
            $" {nextSceneName}へ遷移します。"
        );

        SceneManager.LoadScene(nextSceneName);
    }
}
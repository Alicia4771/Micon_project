using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ResultSceneManager : MonoBehaviour
{
    [Header("Score")]
    [SerializeField]
    private TextMeshProUGUI scoreText;

    [Header("Propeller")]
    [SerializeField]
    private Propeller propeller_L;

    [SerializeField]
    private Propeller propeller_R;

    [Header("Scene Settings")]
    [SerializeField, Tooltip("戻るシーンの名前")]
    private string startSceneName = "StartScene";

    [SerializeField, Tooltip("自動的にStartSceneへ戻るまでの秒数")]
    private float returnDelaySeconds = 60.0f;

    [Header("Sensor Settings")]
    [SerializeField]
    private SensorReceiver sensorReceiver;

    [SerializeField, Tooltip("基準姿勢から何度傾いたらStartSceneへ戻るか")]
    private float tiltThresholdDegrees = 40.0f;

    [SerializeField, Tooltip("センサの基準姿勢を取得するまでの待機時間")]
    private float calibrationDelaySeconds = 1.0f;

    /*
     * SensorReceiverが実際のセンサデータを
     * 受信する前に返す初期値。
     */
    private const string DefaultSensorData =
        "0,0,0,0,0,0,0,0,0";

    private int score;

    // ResultSceneを表示してからの経過時間
    private float elapsedTime;

    // センサの基準姿勢
    private Vector3 initialEulerAngle;

    // 基準姿勢を取得済みか
    private bool hasInitialEulerAngle;

    // 最初に有効なセンサデータを取得した時間
    private float firstValidSensorDataTime = -1.0f;

    // シーン遷移の重複実行を防止する
    private bool isChangingScene;

    private void Awake()
    {
        /*
         * InspectorでSensorReceiverが設定されていない場合は、
         * シーン内から自動的に探す。
         */
        if (sensorReceiver == null)
        {
            sensorReceiver =
                FindFirstObjectByType<SensorReceiver>();
        }
    }

    private void Start()
    {
        score = DataManager.GetScore();

        if (scoreText != null)
        {
            scoreText.text = score.ToString();
        }

        elapsedTime = 0.0f;
        isChangingScene = false;

        /*
         * Propellerが設定されていない場合でも
         * NullReferenceExceptionが発生しないようにする。
         */
        if (propeller_L != null)
        {
            propeller_L.SetRotationSpeed(120.0f);
        }

        if (propeller_R != null)
        {
            propeller_R.SetRotationSpeed(50.0f);
        }

        if (sensorReceiver == null)
        {
            Debug.LogWarning(
                "SensorReceiverが見つかりません。" +
                "Enterキーまたは時間経過による遷移のみ使用できます。",
                this
            );
        }
    }

    private void Update()
    {
        if (isChangingScene)
        {
            return;
        }

        /*
         * Time.timeScaleが0になっている場合でも
         * 時間が進むようにunscaledDeltaTimeを使用する。
         */
        elapsedTime += Time.unscaledDeltaTime;

        // Enterキーが押された場合
        if (IsEnterPressed())
        {
            ChangeToStartScene(
                "Enterキーが押されました。"
            );

            return;
        }

        // 一定時間が経過した場合
        if (elapsedTime >= returnDelaySeconds)
        {
            ChangeToStartScene(
                $"{returnDelaySeconds:F1}秒経過しました。"
            );

            return;
        }

        // センサが一定角度以上傾いたか確認する
        CheckSensorTilt();
    }

    /// <summary>
    /// Enterキーが押されたか確認する。
    /// </summary>
    private bool IsEnterPressed()
    {
#if ENABLE_INPUT_SYSTEM
        /*
         * Unityの新しいInput System。
         * 通常のEnterとテンキー側のEnterの両方を判定する。
         */
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
    /// センサが基準姿勢から一定角度以上傾いたか確認する。
    /// </summary>
    private void CheckSensorTilt()
    {
        if (sensorReceiver == null)
        {
            return;
        }

        string rawSensorData =
            sensorReceiver.GetSensorData();

        if (string.IsNullOrWhiteSpace(rawSensorData))
        {
            return;
        }

        rawSensorData = rawSensorData.Trim();

        /*
         * 実際のセンサデータを受信する前の
         * 初期値は使用しない。
         */
        if (rawSensorData == DefaultSensorData)
        {
            return;
        }

        /*
         * 受信文字列をDataManagerへ保存する。
         * 形式が不正だった場合は処理しない。
         */
        if (!DataManager.SetSensorValue(rawSensorData))
        {
            return;
        }

        Vector3 currentEulerAngle =
            DataManager.GetEulerSensorValue();

        /*
         * 最初のデータを受け取った直後は
         * センサ値が安定していない可能性があるため、
         * 少し待ってから基準姿勢を取得する。
         */
        if (firstValidSensorDataTime < 0.0f)
        {
            firstValidSensorDataTime =
                Time.unscaledTime;

            return;
        }

        float timeSinceFirstData =
            Time.unscaledTime -
            firstValidSensorDataTime;

        if (timeSinceFirstData <
            calibrationDelaySeconds)
        {
            return;
        }

        // 現在の姿勢を最初の基準姿勢として保存する
        if (!hasInitialEulerAngle)
        {
            initialEulerAngle =
                currentEulerAngle;

            hasInitialEulerAngle = true;

            Debug.Log(
                "ResultSceneでセンサの基準姿勢を取得しました。" +
                $" X={initialEulerAngle.x:F1}," +
                $" Y={initialEulerAngle.y:F1}," +
                $" Z={initialEulerAngle.z:F1}",
                this
            );

            return;
        }

        /*
         * Mathf.DeltaAngleを使用すると、
         * 359度から1度への変化を358度ではなく
         * 2度として計算できる。
         */
        float differenceX =
            Mathf.Abs(
                Mathf.DeltaAngle(
                    initialEulerAngle.x,
                    currentEulerAngle.x
                )
            );

        float differenceY =
            Mathf.Abs(
                Mathf.DeltaAngle(
                    initialEulerAngle.y,
                    currentEulerAngle.y
                )
            );

        float differenceZ =
            Mathf.Abs(
                Mathf.DeltaAngle(
                    initialEulerAngle.z,
                    currentEulerAngle.z
                )
            );

        /*
         * X、Y、Zのどれか1つでも
         * しきい値以上ならStartSceneへ戻る。
         */
        if (differenceX >= tiltThresholdDegrees ||
            differenceY >= tiltThresholdDegrees ||
            differenceZ >= tiltThresholdDegrees)
        {
            ChangeToStartScene(
                "センサが一定角度以上傾きました。" +
                $" X差={differenceX:F1}," +
                $" Y差={differenceY:F1}," +
                $" Z差={differenceZ:F1}"
            );
        }
    }

    /// <summary>
    /// StartSceneへ遷移する。
    /// </summary>
    private void ChangeToStartScene(string reason)
    {
        if (isChangingScene)
        {
            return;
        }

        isChangingScene = true;

        Debug.Log(
            reason +
            $" {startSceneName}へ遷移します。",
            this
        );

        SceneManager.LoadScene(startSceneName);
    }
}
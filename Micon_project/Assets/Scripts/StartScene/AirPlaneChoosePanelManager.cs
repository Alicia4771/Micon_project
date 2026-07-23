using UnityEngine;
using UnityEngine.SceneManagement;

public class AirPlaneChoosePanelManager : MonoBehaviour
{
    /// <summary>
    /// DataManager内のVector3のどの軸を使用するか。
    /// </summary>
    private enum EulerAxis
    {
        X,
        Y,
        Z
    }

    [Header("Camera")]
    [SerializeField]
    private StartSceneCamera startSceneCamera;

    [Header("Arrow Buttons")]
    [SerializeField]
    private GameObject leftButton;

    [SerializeField]
    private GameObject rightButton;

    [Header("Scene Names")]
    [SerializeField, Tooltip("左の飛行機を選択したときに遷移するシーン")]
    private string leftAirplaneSceneName = "LeftAirplaneScene";

    [SerializeField, Tooltip("中央の飛行機を選択したときに遷移するシーン")]
    private string centerAirplaneSceneName = "CenterAirplaneScene";

    [SerializeField, Tooltip("右の飛行機を選択したときに遷移するシーン")]
    private string rightAirplaneSceneName = "RightAirplaneScene";

    [Header("Sensor")]
    [SerializeField, Tooltip("シリアル通信を行っているSensorReceiver")]
    private SensorReceiver sensorReceiver;

    [SerializeField, Tooltip("ロール角として使用するオイラー角の軸")]
    private EulerAxis rollAxis = EulerAxis.Y;

    [SerializeField, Tooltip("ピッチ角として使用するオイラー角の軸")]
    private EulerAxis pitchAxis = EulerAxis.Z;

    [SerializeField, Tooltip("左右移動が発生するロール角の差")]
    private float rollThresholdDegrees = 25.0f;

    [SerializeField, Tooltip("決定操作が発生するピッチ角の差")]
    private float pitchThresholdDegrees = 25.0f;

    [SerializeField, Tooltip("次の操作を受け付けるために戻す角度")]
    private float neutralReleaseDegrees = 10.0f;

    [SerializeField, Tooltip("センサの基準角度を取得するまでの待機時間")]
    private float calibrationDelaySeconds = 1.0f;

    [SerializeField, Tooltip("右と左の判定が逆の場合に有効にする")]
    private bool invertRollDirection = false;

    // SensorReceiverの初期値
    private const string DefaultSensorData =
        "0,0,0,0,0,0,0,0,0";

    // シーン遷移の二重実行を防ぐ
    private bool isLoadingScene;

    // 最初に有効なデータを受信した時間
    private float firstValidSensorDataTime = -1.0f;

    /*
     * 一度傾けた後、水平付近へ戻すまでは
     * 次の入力を受け付けないためのフラグ。
     */
    private bool sensorActionLocked;

    private void Awake()
    {
        /*
         * Inspectorで設定されていなければ、
         * シーン内から自動的に検索する。
         */
        if (sensorReceiver == null)
        {
            sensorReceiver =
                FindFirstObjectByType<SensorReceiver>();
        }
    }

    private void Start()
    {
        isLoadingScene = false;

        /*
         * すでに水平姿勢がある場合は、
         * 前のシーンで傾けた状態を引き継がないよう、
         * 一度水平付近へ戻るまで入力をロックする。
         */
        sensorActionLocked =
            DataManager.HasInitialEulerSensorValue();

        UpdateArrowButtons();

        if (startSceneCamera == null)
        {
            Debug.LogError(
                "StartSceneCameraが設定されていません。",
                this
            );
        }

        if (sensorReceiver == null)
        {
            Debug.LogWarning(
                "SensorReceiverが見つかりません。" +
                "画面上のボタン操作のみ使用できます。",
                this
            );
        }
    }

    private void Update()
    {
        if (isLoadingScene)
        {
            return;
        }

        UpdateSensorControl();
    }

    /// <summary>
    /// センサの傾きから左右移動と決定操作を行う。
    /// </summary>
    private void UpdateSensorControl()
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
         * SensorReceiverの初期値は、
         * 実際のセンサ値として使用しない。
         */
        if (rawSensorData == DefaultSensorData)
        {
            return;
        }

        /*
         * CSVをDataManagerへ保存する。
         * データが9個でなかった場合などはfalseになる。
         */
        if (!DataManager.SetSensorValue(rawSensorData))
        {
            return;
        }

        Vector3 currentEulerAngle =
            DataManager.GetEulerSensorValue();

        /*
         * まだ水平姿勢を一度も保存していない場合だけ、
         * 最初の有効データを受信してから約1秒待って保存する。
         *
         * すでに保存済みなら待たずに既存の値を使用する。
         */
        if (!EnsureInitialEulerAngle(currentEulerAngle))
        {
            return;
        }

        Vector3 initialEulerAngle =
            DataManager.GetInitialEulerSensorValue();

        float initialRoll =
            GetEulerAxisValue(
                initialEulerAngle,
                rollAxis
            );

        float currentRoll =
            GetEulerAxisValue(
                currentEulerAngle,
                rollAxis
            );

        float initialPitch =
            GetEulerAxisValue(
                initialEulerAngle,
                pitchAxis
            );

        float currentPitch =
            GetEulerAxisValue(
                currentEulerAngle,
                pitchAxis
            );

        /*
         * 359度から1度になった場合でも、
         * 角度差を2度として計算する。
         */
        float rollDifference =
            Mathf.DeltaAngle(
                initialRoll,
                currentRoll
            );

        float pitchDifference =
            Mathf.DeltaAngle(
                initialPitch,
                currentPitch
            );

        float absoluteRollDifference =
            Mathf.Abs(rollDifference);

        float absolutePitchDifference =
            Mathf.Abs(pitchDifference);

        /*
         * 一度操作した後は、センサを水平付近へ戻すまで
         * 次の操作を受け付けない。
         */
        if (sensorActionLocked)
        {
            bool rollReturned =
                absoluteRollDifference <=
                neutralReleaseDegrees;

            bool pitchReturned =
                absolutePitchDifference <=
                neutralReleaseDegrees;

            if (rollReturned && pitchReturned)
            {
                sensorActionLocked = false;
            }

            return;
        }

        /*
         * ピッチによる決定を先に判定する。
         * ロールとピッチが同時に傾いた場合でも、
         * シーン決定を優先する。
         */
        if (absolutePitchDifference >=
            pitchThresholdDegrees)
        {
            sensorActionLocked = true;

            Debug.Log(
                $"ピッチ操作で飛行機を決定します。" +
                $" 角度差={pitchDifference:F1}",
                this
            );

            OnClickChooseButton();
            return;
        }

        /*
         * ロール角による左右移動。
         */
        if (absoluteRollDifference >=
            rollThresholdDegrees)
        {
            bool tiltedToRight =
                rollDifference > 0.0f;

            /*
             * センサの取り付け方向によって
             * 左右が逆になる場合の補正。
             */
            if (invertRollDirection)
            {
                tiltedToRight = !tiltedToRight;
            }

            if (tiltedToRight)
            {
                if (startSceneCamera != null &&
                    startSceneCamera.CanMoveRight())
                {
                    sensorActionLocked = true;

                    Debug.Log(
                        $"右へのロールを検出しました。" +
                        $" 角度差={rollDifference:F1}",
                        this
                    );

                    OnClickRightButton();
                }
            }
            else
            {
                if (startSceneCamera != null &&
                    startSceneCamera.CanMoveLeft())
                {
                    sensorActionLocked = true;

                    Debug.Log(
                        $"左へのロールを検出しました。" +
                        $" 角度差={rollDifference:F1}",
                        this
                    );

                    OnClickLeftButton();
                }
            }
        }
    }

    /// <summary>
    /// DataManagerに水平姿勢がなければ、約1秒待って一度だけ保存する。
    /// すでに保存済みなら、保存済みの水平姿勢をそのまま使用する。
    /// </summary>
    private bool EnsureInitialEulerAngle(
        Vector3 currentEulerAngle
    )
    {
        if (DataManager.HasInitialEulerSensorValue())
        {
            return true;
        }

        if (firstValidSensorDataTime < 0.0f)
        {
            firstValidSensorDataTime =
                Time.unscaledTime;

            return false;
        }

        float elapsed =
            Time.unscaledTime -
            firstValidSensorDataTime;

        if (elapsed < calibrationDelaySeconds)
        {
            return false;
        }

        bool saved =
            DataManager.TrySetInitialEulerSensorValue(
                currentEulerAngle
            );

        if (saved)
        {
            Debug.Log(
                "最初の水平姿勢をDataManagerに保存しました。" +
                $" X={currentEulerAngle.x:F1}," +
                $" Y={currentEulerAngle.y:F1}," +
                $" Z={currentEulerAngle.z:F1}",
                this
            );
        }

        return DataManager.HasInitialEulerSensorValue();
    }

    /// <summary>
    /// 指定されたVector3の軸の値を取得する。
    /// </summary>
    private float GetEulerAxisValue(
        Vector3 eulerAngle,
        EulerAxis axis
    )
    {
        switch (axis)
        {
            case EulerAxis.X:
                return eulerAngle.x;

            case EulerAxis.Y:
                return eulerAngle.y;

            case EulerAxis.Z:
                return eulerAngle.z;

            default:
                return 0.0f;
        }
    }

    /// <summary>
    /// 左矢印ボタンを押したときに実行する。
    /// </summary>
    public void OnClickLeftButton()
    {
        if (isLoadingScene)
        {
            return;
        }

        if (startSceneCamera == null)
        {
            Debug.LogError(
                "StartSceneCameraが設定されていません。",
                this
            );

            return;
        }

        startSceneCamera.MoveLeft();

        UpdateArrowButtons();
    }

    /// <summary>
    /// 右矢印ボタンを押したときに実行する。
    /// </summary>
    public void OnClickRightButton()
    {
        if (isLoadingScene)
        {
            return;
        }

        if (startSceneCamera == null)
        {
            Debug.LogError(
                "StartSceneCameraが設定されていません。",
                this
            );

            return;
        }

        startSceneCamera.MoveRight();

        UpdateArrowButtons();
    }

    /// <summary>
    /// 「これにする」ボタンを押したときに実行する。
    /// </summary>
    public void OnClickChooseButton()
    {
        if (isLoadingScene)
        {
            return;
        }

        if (startSceneCamera == null)
        {
            Debug.LogError(
                "StartSceneCameraが設定されていません。",
                this
            );

            return;
        }

        int currentIndex =
            startSceneCamera.GetCurrentIndex();

        string nextSceneName;

        switch (currentIndex)
        {
            case 0:
                nextSceneName =
                    leftAirplaneSceneName;
                break;

            case 1:
                nextSceneName =
                    centerAirplaneSceneName;
                break;

            case 2:
                nextSceneName =
                    rightAirplaneSceneName;
                break;

            default:
                Debug.LogError(
                    $"不正な飛行機番号です。" +
                    $" index={currentIndex}",
                    this
                );

                return;
        }

        LoadSelectedScene(
            nextSceneName,
            currentIndex
        );
    }

    /// <summary>
    /// 指定されたシーンを読み込む。
    /// </summary>
    private void LoadSelectedScene(
        string sceneName,
        int airplaneIndex
    )
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError(
                $"飛行機{airplaneIndex}の" +
                "遷移先シーン名が空です。",
                this
            );

            return;
        }

        isLoadingScene = true;

        Debug.Log(
            $"飛行機{airplaneIndex}を選択しました。" +
            $"{sceneName}へ遷移します。",
            this
        );

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 現在の飛行機位置に応じて、
    /// 左右の矢印ボタンの表示を変更する。
    /// </summary>
    private void UpdateArrowButtons()
    {
        if (startSceneCamera == null)
        {
            return;
        }

        if (leftButton != null)
        {
            leftButton.SetActive(
                startSceneCamera.CanMoveLeft()
            );
        }

        if (rightButton != null)
        {
            rightButton.SetActive(
                startSceneCamera.CanMoveRight()
            );
        }
    }
}
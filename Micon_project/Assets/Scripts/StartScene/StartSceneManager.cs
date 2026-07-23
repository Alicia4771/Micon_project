using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class StartSceneManager : MonoBehaviour
{
    private const int MatrixSize = 16;

    // 16×16＝256ビットを32ビットずつ格納するため8個
    private const int LedWordCount = 8;

    /*
     * radar.csと送信個数を合わせるため9個にする。
     *
     * [0]～[7]：16×16 LEDマトリクス
     * [8]     ：未使用なので0
     */
    private const int TransmitWordCount = 9;

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

    [SerializeField, Tooltip("シーン遷移直後、再び入力を受け付ける水平付近の角度")]
    private float neutralReleaseDegrees = 10.0f;

    [Header("Start LED Matrix")]

    [SerializeField, Tooltip("スタート画面でLED表示を送信する")]
    private bool sendStartLedMessage = true;

    [SerializeField, Min(0.05f),
     Tooltip("「傾けてね」を繰り返し送信する間隔")]
    private float ledContinuousSendIntervalSeconds = 0.1f;

    [SerializeField, Min(0.0f),
     Tooltip("すでに水平取得済みの場合、シーン開始後に送信し続ける時間")]
    private float ledSendDurationWhenAlreadyCalibratedSeconds = 1.0f;

    [SerializeField, Min(0.0f),
     Tooltip("このシーンで水平を取得した後も送信し続ける時間")]
    private float ledContinueAfterCalibrationSeconds = 1.0f;

    [SerializeField,
     Tooltip("LED表示の左右が逆の場合に有効にする")]
    private bool mirrorLedHorizontal = false;

    [SerializeField,
     Tooltip("LED表示の上下が逆の場合に有効にする")]
    private bool mirrorLedVertical = false;

    // センサー受信前にSensorReceiverが返す初期値
    private const string DefaultSensorData =
        "0,0,0,0,0,0,0,0,0";

    /*
     * すでに水平姿勢が保存されている状態でこのシーンに入った場合、
     * 前のシーンで使った傾きを引き継がないよう、
     * 一度水平付近に戻るまで入力を待つ。
     */
    private bool waitForNeutralAfterSceneLoad;

    // シーン遷移を重複して実行しないためのフラグ
    private bool isChangingScene = false;

    // 最初に有効なセンサーデータを取得した時間
    private float firstValidSensorDataTime = -1.0f;

    // LED送信用コルーチン
    private Coroutine ledSendCoroutine;

    // StartSceneへ入った時点ですでに水平姿勢を取得済みだったか
    private bool hadInitialCalibrationAtSceneStart;

    // StartSceneを開始した時刻
    private float sceneStartedAtUnscaledTime;

    // このシーンで水平姿勢の取得完了を検出した時刻
    private float calibrationCompletedAtUnscaledTime = -1.0f;

    /*
     * 16×16のLED表示データ
     *
     * startLedData[行, 列]
     *
     * 0：消灯
     * 1：点灯
     */
    private readonly int[,] startLedData =
        new int[MatrixSize, MatrixSize];

    /*
     * 各文字は8×8ドット。
     *
     * byteの左端のビットが文字の左側。
     * 1が点灯、0が消灯。
     *
     * 「傾」は8×8では複雑なため、
     * 読み取れる範囲で簡略化している。
     */
    private static readonly byte[] TiltCharacter =
    {
        0b01001110,
        0b01100100,
        0b01111000,
        0b11101110,
        0b01101110,
        0b01101000,
        0b01011110,
        0b01001010
    };

    private static readonly byte[] KeCharacter =
    {
        0b01000100,
        0b01011110,
        0b01000100,
        0b01000100,
        0b01000100,
        0b01001100,
        0b00001000,
        0b00000000
    };

    private static readonly byte[] TeCharacter =
    {
        0b00000000,
        0b00111110,
        0b00001000,
        0b00010000,
        0b00010000,
        0b00010000,
        0b00011000,
        0b00000100
    };

    private static readonly byte[] NeCharacter =
    {
        0b00101100,
        0b00110010,
        0b00100010,
        0b01100010,
        0b01101110,
        0b00101011,
        0b00101100,
        0b00000000
    };

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
        /*
         * StartSceneへ入った時点で、
         * DataManagerに水平姿勢が保存済みかを記録する。
         */
        hadInitialCalibrationAtSceneStart =
            DataManager.HasInitialEulerSensorValue();

        /*
         * すでに水平姿勢がある場合、
         * 前のシーンで傾けたまま遷移している可能性がある。
         * 一度水平へ戻るまでは入力を受け付けない。
         */
        waitForNeutralAfterSceneLoad =
            hadInitialCalibrationAtSceneStart;

        sceneStartedAtUnscaledTime =
            Time.unscaledTime;

        calibrationCompletedAtUnscaledTime =
            -1.0f;

        /*
         * SensorReceiverの有無に関係なく、
         * 先に「傾けてね」の配列を作成する。
         */
        CreateStartLedMessage();

        if (sensorReceiver == null)
        {
            Debug.LogWarning(
                "SensorReceiverが見つかりません。" +
                "Enterキーによるシーン遷移のみ使用できます。"
            );

            return;
        }

        if (sendStartLedMessage)
        {
            ledSendCoroutine =
                StartCoroutine(
                    SendStartLedMessageCoroutine()
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
            ChangeScene(
                "Enterキーが押されました。"
            );

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

        string rawSensorData =
            sensorReceiver.GetSensorData();

        if (string.IsNullOrWhiteSpace(rawSensorData))
        {
            return;
        }

        rawSensorData =
            rawSensorData.Trim();

        /*
         * SensorReceiverは実際のデータを受信する前に
         * "0,0,0,0,0,0,0,0,0"を返すため除外する。
         */
        if (rawSensorData == DefaultSensorData)
        {
            return;
        }

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

        /*
         * Mathf.DeltaAngleを使うことで、
         * 359度から1度への変化を2度として計算する。
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
         * 同じ水平値を全シーンで使うと、
         * 前のシーンで傾けた状態がそのまま次の入力になる。
         * そのため、シーン遷移直後は一度水平付近へ戻るまで待つ。
         */
        if (waitForNeutralAfterSceneLoad)
        {
            bool returnedToNeutral =
                differenceX <= neutralReleaseDegrees &&
                differenceY <= neutralReleaseDegrees &&
                differenceZ <= neutralReleaseDegrees;

            if (returnedToNeutral)
            {
                waitForNeutralAfterSceneLoad = false;

                Debug.Log(
                    "水平付近へ戻ったため、" +
                    "センサー入力を有効にしました。"
                );
            }

            return;
        }

        Debug.Log(
            $"角度差 X={differenceX:F1}, " +
            $"Y={differenceY:F1}, " +
            $"Z={differenceZ:F1}"
        );

        // X、Y、Zのいずれかがしきい値以上になった場合
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
    /// DataManagerに水平姿勢がなければ、
    /// 約1秒待って一度だけ保存する。
    ///
    /// すでに保存済みなら、
    /// 保存済みの水平姿勢をそのまま使用する。
    /// </summary>
    private bool EnsureInitialEulerAngle(
        Vector3 currentEulerAngle
    )
    {
        if (DataManager.HasInitialEulerSensorValue())
        {
            return true;
        }

        /*
         * 最初に有効なセンサデータを取得した時刻を保存する。
         */
        if (firstValidSensorDataTime < 0.0f)
        {
            firstValidSensorDataTime =
                Time.unscaledTime;

            return false;
        }

        float elapsed =
            Time.unscaledTime -
            firstValidSensorDataTime;

        /*
         * センサ値が安定するまで待機する。
         */
        if (elapsed < calibrationDelaySeconds)
        {
            return false;
        }

        /*
         * 最初の1回だけ、
         * 現在の姿勢を水平姿勢としてDataManagerへ保存する。
         */
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
                $" Z={currentEulerAngle.z:F1}"
            );
        }

        return DataManager.HasInitialEulerSensorValue();
    }

    //==============================================================
    // LEDマトリクス
    //==============================================================

    /// <summary>
    /// 16×16の各領域に「傾」「け」「て」「ね」を配置する
    ///
    /// 左上：傾
    /// 右上：け
    /// 左下：て
    /// 右下：ね
    /// </summary>
    private void CreateStartLedMessage()
    {
        ClearStartLedData();

        Draw8x8Character(
            TiltCharacter,
            startRow: 0,
            startColumn: 0
        );

        Draw8x8Character(
            KeCharacter,
            startRow: 0,
            startColumn: 8
        );

        Draw8x8Character(
            TeCharacter,
            startRow: 8,
            startColumn: 0
        );

        Draw8x8Character(
            NeCharacter,
            startRow: 8,
            startColumn: 8
        );
    }

    /// <summary>
    /// 8×8文字を16×16配列へ描画する
    /// </summary>
    private void Draw8x8Character(
        byte[] characterData,
        int startRow,
        int startColumn
    )
    {
        if (characterData == null ||
            characterData.Length != 8)
        {
            Debug.LogError(
                "文字データは8行である必要があります。"
            );

            return;
        }

        for (int characterRow = 0;
             characterRow < 8;
             characterRow++)
        {
            byte rowData =
                characterData[characterRow];

            for (int characterColumn = 0;
                 characterColumn < 8;
                 characterColumn++)
            {
                /*
                 * byteのbit7を左端、
                 * bit0を右端として読み取る。
                 */
                int bitIndex =
                    7 - characterColumn;

                bool isOn =
                    (rowData &
                     (1 << bitIndex)) != 0;

                int matrixRow =
                    startRow + characterRow;

                int matrixColumn =
                    startColumn + characterColumn;

                if (matrixRow < 0 ||
                    matrixRow >= MatrixSize ||
                    matrixColumn < 0 ||
                    matrixColumn >= MatrixSize)
                {
                    continue;
                }

                startLedData[
                    matrixRow,
                    matrixColumn
                ] = isOn ? 1 : 0;
            }
        }
    }

    /// <summary>
    /// LED配列をすべて消灯状態にする
    /// </summary>
    private void ClearStartLedData()
    {
        for (int row = 0;
             row < MatrixSize;
             row++)
        {
            for (int column = 0;
                 column < MatrixSize;
                 column++)
            {
                startLedData[
                    row,
                    column
                ] = 0;
            }
        }
    }

    /// <summary>
    /// 「傾けてね」を必要な期間、繰り返し送信する。
    ///
    /// 水平姿勢が未取得の場合：
    /// 水平を取得するまで送信し、
    /// 取得後も指定秒数だけ送信する。
    ///
    /// 水平姿勢が取得済みの場合：
    /// StartScene開始後、指定秒数だけ送信する。
    /// </summary>
    private IEnumerator SendStartLedMessageCoroutine()
    {
        /*
         * 待機せず、最初のフレームから送信を始める。
         *
         * シリアルポートがまだ開いていない場合は
         * SendStartLedMessage()がfalseを返すが、
         * 次のループで再試行する。
         */
        while (!isChangingScene)
        {
            /*
             * 成否に関係なく、
             * 指定間隔で繰り返し送信する。
             */
            SendStartLedMessage();

            float currentTime =
                Time.unscaledTime;

            if (hadInitialCalibrationAtSceneStart)
            {
                /*
                 * すでに水平取得済みの場合は、
                 * StartScene開始から指定時間だけ送信する。
                 */
                float elapsedFromSceneStart =
                    currentTime -
                    sceneStartedAtUnscaledTime;

                if (elapsedFromSceneStart >=
                    ledSendDurationWhenAlreadyCalibratedSeconds)
                {
                    ledSendCoroutine = null;

                    Debug.Log(
                        "水平姿勢は取得済みだったため、" +
                        "開始後のLED連続送信を終了しました。"
                    );

                    yield break;
                }
            }
            else if (DataManager.HasInitialEulerSensorValue())
            {
                /*
                 * このStartScene内で水平姿勢を取得した場合、
                 * 取得完了を検出した時刻を一度だけ保存する。
                 */
                if (calibrationCompletedAtUnscaledTime < 0.0f)
                {
                    calibrationCompletedAtUnscaledTime =
                        currentTime;

                    Debug.Log(
                        "水平姿勢の取得を確認しました。" +
                        $"あと{ledContinueAfterCalibrationSeconds:F1}秒、" +
                        "「傾けてね」を送信します。"
                    );
                }

                float elapsedAfterCalibration =
                    currentTime -
                    calibrationCompletedAtUnscaledTime;

                /*
                 * 水平取得後、指定時間が経過したら
                 * LEDデータの送信を終了する。
                 */
                if (elapsedAfterCalibration >=
                    ledContinueAfterCalibrationSeconds)
                {
                    ledSendCoroutine = null;

                    Debug.Log(
                        "水平取得後のLED連続送信を終了しました。"
                    );

                    yield break;
                }
            }

            yield return new WaitForSecondsRealtime(
                Mathf.Max(
                    0.05f,
                    ledContinuousSendIntervalSeconds
                )
            );
        }

        ledSendCoroutine = null;
    }

    /// <summary>
    /// 「傾けてね」を圧縮してArduinoへ送信する
    /// </summary>
    private bool SendStartLedMessage()
    {
        if (sensorReceiver == null)
        {
            return false;
        }

        uint[] packedData =
            PackStartLedData();

        /*
         * CSV形式：
         *
         * data0,data1,...,data7,0
         *
         * SensorReceiver.SendToArduino()が
         * 最後に改行を追加する。
         */
        string packet =
            string.Join(",", packedData);

        bool result =
            sensorReceiver.SendToArduino(packet);

        if (result)
        {
            Debug.Log(
                "スタート画面のLEDデータを送信しました：" +
                packet
            );
        }

        return result;
    }

    /// <summary>
    /// 16×16の256ビットを8個のuintへ圧縮する
    /// </summary>
    private uint[] PackStartLedData()
    {
        uint[] packedData =
            new uint[TransmitWordCount];

        for (int outputRow = 0;
             outputRow < MatrixSize;
             outputRow++)
        {
            for (int outputColumn = 0;
                 outputColumn < MatrixSize;
                 outputColumn++)
            {
                int sourceRow =
                    mirrorLedVertical
                        ? MatrixSize - 1 - outputRow
                        : outputRow;

                int sourceColumn =
                    mirrorLedHorizontal
                        ? MatrixSize - 1 - outputColumn
                        : outputColumn;

                int index =
                    outputRow * MatrixSize +
                    outputColumn;

                int uintIndex =
                    index / 32;

                int bitIndex =
                    index % 32;

                if (startLedData[
                        sourceRow,
                        sourceColumn
                    ] == 1)
                {
                    packedData[uintIndex] |=
                        1u << bitIndex;
                }
            }
        }

        /*
         * radar.csでは9個目に接近情報を入れているが、
         * スタート画面では使用しない。
         */
        packedData[LedWordCount] = 0u;

        return packedData;
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

        /*
         * シーン遷移前にLED送信コルーチンを停止する。
         */
        if (ledSendCoroutine != null)
        {
            StopCoroutine(ledSendCoroutine);
            ledSendCoroutine = null;
        }

        Debug.Log(
            reason +
            $" {nextSceneName}へ遷移します。"
        );

        SceneManager.LoadScene(nextSceneName);
    }
}
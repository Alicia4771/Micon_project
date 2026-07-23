using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Threading;

public class SensorReceiver : MonoBehaviour
{
    /// <summary>
    /// センサー値が不足または空欄だった場合の補完方法。
    /// </summary>
    public enum MissingValueFillMode
    {
        [InspectorName("直前の値で補完")]
        PreviousValue,

        [InspectorName("0で補完")]
        Zero
    }

    // Arduinoから受信する値の個数
    // [e1, e2, e3, g1, g2, g3, a1, a2, a3]
    private const int SensorValueCount = 9;

    private static readonly string[] SensorValueNames =
    {
        "e1", "e2", "e3",
        "g1", "g2", "g3",
        "a1", "a2", "a3"
    };

    //==================================================
    // Serial settings
    //==================================================

    [Header("Serial Settings")]

    [SerializeField]
    private string portName =
        "/dev/cu.usbserial-AQ01LGU4";

    [SerializeField]
    private int baudRate = 115200;

    //==================================================
    // Missing-value completion settings
    //==================================================

    [Header("Missing Sensor Value Settings")]

    [SerializeField]
    [Tooltip("不足または空欄だったセンサー値の補完方法")]
    private MissingValueFillMode missingValueFillMode =
        MissingValueFillMode.PreviousValue;

    /*
     * 読み取りスレッドから参照する補完方法。
     *
     * Inspectorを再生中に変更した場合にも反映できるよう、
     * Update()でmissingValueFillModeから同期する。
     */
    private int activeMissingValueFillMode;

    //==================================================
    // Received sensor data
    //==================================================

    /*
     * 最後に利用可能な状態で受信したセンサーデータ。
     *
     * 必ず9個のCSV形式になる。
     */
    private string sensor_raw_data =
        "0,0,0,0,0,0,0,0,0";

    /*
     * 各項目について、
     * 直前に実際に受信できた正常値を保存する。
     *
     * 補完によって作られた値では、
     * この履歴を上書きしない。
     */
    private readonly string[] previousSensorValues =
    {
        "0", "0", "0",
        "0", "0", "0",
        "0", "0", "0"
    };

    /*
     * 各項目について、
     * 一度でも正常値を受信したか。
     *
     * 直前値がまだ存在しない項目は0で補完する。
     */
    private readonly bool[] hasPreviousSensorValue =
        new bool[SensorValueCount];

    /*
     * 前回の取得後に新しいデータを受信したか。
     */
    private bool hasNewSensorData;

    //==================================================
    // Serial
    //==================================================

    private SerialPort serial;

    private Thread readThread;

    private volatile bool running;

    //==================================================
    // Error status
    //==================================================

    /*
     * 補完できず破棄したデータの累計。
     */
    private int invalidSensorLineCount;

    /*
     * 最後に破棄したデータ。
     */
    private string lastInvalidSensorLine = "";

    /*
     * 読み取りスレッドから
     * メインスレッドへ渡すエラー。
     */
    private string pendingSerialError;

    /*
     * 補完を行った際の
     * Debug.LogError用キュー。
     */
    private readonly Queue<string>
        pendingCompletionErrors =
            new Queue<string>();

    private int lastReportedInvalidCount;

    private float nextInvalidLogTime;

    //==================================================
    // Locks
    //==================================================

    private readonly object dataLockObj =
        new object();

    private readonly object writeLockObj =
        new object();

    private readonly object statusLockObj =
        new object();

    //==================================================
    // Start
    //==================================================

    private void Start()
    {
        Volatile.Write(
            ref activeMissingValueFillMode,
            (int)missingValueFillMode
        );

        serial = new SerialPort(
            portName,
            baudRate
        );

        /*
         * Arduino側の改行と合わせる。
         */
        serial.NewLine = "\n";

        /*
         * ReadLine()が永久に停止しないようにする。
         */
        serial.ReadTimeout = 50;

        /*
         * 書き込み処理が長時間停止しないようにする。
         */
        serial.WriteTimeout = 100;

        try
        {
            serial.Open();

            /*
             * ポートを開く前から残っていた
             * 古い受信データを破棄する。
             */
            serial.DiscardInBuffer();

            /*
             * Unity側に残っていた
             * 古い送信データを破棄する。
             */
            serial.DiscardOutBuffer();

            Debug.Log(
                "SerialPort Opened: " +
                portName,
                this
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "SerialPort Open Failed: " +
                e.Message,
                this
            );

            return;
        }

        running = true;

        readThread = new Thread(
            ReadSerialLoop
        );

        readThread.IsBackground = true;

        readThread.Start();
    }

    //==================================================
    // Update
    //==================================================

    private void Update()
    {
        /*
         * 再生中にInspectorの選択を変更した場合にも、
         * 読み取りスレッド側へ反映する。
         */
        Volatile.Write(
            ref activeMissingValueFillMode,
            (int)missingValueFillMode
        );

        OutputPendingSerialError();

        OutputInvalidDataWarning();

        OutputCompletionErrors();
    }

    /// <summary>
    /// 読み取りスレッドで発生した
    /// シリアルエラーを出力する。
    /// </summary>
    private void OutputPendingSerialError()
    {
        string serialError = null;

        lock (statusLockObj)
        {
            if (!string.IsNullOrEmpty(
                    pendingSerialError
                ))
            {
                serialError =
                    pendingSerialError;

                pendingSerialError = null;
            }
        }

        if (!string.IsNullOrEmpty(serialError))
        {
            Debug.LogError(
                "Serial Read Error: " +
                serialError,
                this
            );
        }
    }

    /// <summary>
    /// 補完できず破棄したデータについて
    /// 警告を出力する。
    /// </summary>
    private void OutputInvalidDataWarning()
    {
        int currentInvalidCount =
            Volatile.Read(
                ref invalidSensorLineCount
            );

        if (
            currentInvalidCount ==
            lastReportedInvalidCount
        )
        {
            return;
        }

        /*
         * Consoleを埋め尽くさないよう、
         * 最大1秒に1回だけ表示する。
         */
        if (
            Time.unscaledTime <
            nextInvalidLogTime
        )
        {
            return;
        }

        string invalidLine;

        lock (statusLockObj)
        {
            invalidLine =
                lastInvalidSensorLine;
        }

        Debug.LogWarning(
            "[SERIAL DATA WARNING] " +
            "補完できない、または数値として解析できない" +
            "受信データを破棄しました。" +
            $" 累計={currentInvalidCount}" +
            $" Data=[{invalidLine}]",
            this
        );

        lastReportedInvalidCount =
            currentInvalidCount;

        nextInvalidLogTime =
            Time.unscaledTime + 1.0f;
    }

    /// <summary>
    /// センサー値を補完したことを
    /// Debug.LogErrorで出力する。
    /// </summary>
    private void OutputCompletionErrors()
    {
        while (true)
        {
            string completionError = null;

            lock (statusLockObj)
            {
                if (
                    pendingCompletionErrors.Count > 0
                )
                {
                    completionError =
                        pendingCompletionErrors.Dequeue();
                }
            }

            if (string.IsNullOrEmpty(
                    completionError
                ))
            {
                break;
            }

            Debug.LogError(
                completionError,
                this
            );
        }
    }

    //==================================================
    // Read
    //==================================================

    /// <summary>
    /// Arduinoからシリアルデータを
    /// 継続的に読み取る。
    /// </summary>
    private void ReadSerialLoop()
    {
        while (
            running &&
            serial != null &&
            serial.IsOpen
        )
        {
            try
            {
                string line =
                    serial.ReadLine().Trim();

                bool normalizeResult =
                    TryNormalizeSensorLine(
                        line,
                        out string normalizedLine,
                        out string completionError
                    );

                if (!normalizeResult)
                {
                    Interlocked.Increment(
                        ref invalidSensorLineCount
                    );

                    lock (statusLockObj)
                    {
                        lastInvalidSensorLine =
                            line;
                    }

                    continue;
                }

                /*
                 * 補完を行った場合、
                 * メインスレッド側で
                 * Debug.LogErrorを出力する。
                 */
                if (!string.IsNullOrEmpty(
                        completionError
                    ))
                {
                    lock (statusLockObj)
                    {
                        pendingCompletionErrors.Enqueue(
                            completionError
                        );
                    }
                }

                /*
                 * 使用可能な9個のデータを保存する。
                 */
                lock (dataLockObj)
                {
                    sensor_raw_data =
                        normalizedLine;

                    hasNewSensorData = true;
                }
            }
            catch (TimeoutException)
            {
                /*
                 * ReadTimeoutは正常動作なので無視する。
                 */
            }
            catch (Exception e)
            {
                if (running)
                {
                    lock (statusLockObj)
                    {
                        pendingSerialError =
                            e.Message;
                    }
                }
            }
        }
    }

    //==================================================
    // Validation and completion
    //==================================================

    /// <summary>
    /// 受信したデータを9個へ正規化する。
    ///
    /// 9個：
    /// そのまま使用する。
    ///
    /// 10個：
    /// 最後の1個を削除し、最初の9個を使用する。
    ///
    /// 8個：
    /// 不足したa3を選択中の方式で補完する。
    ///
    /// 7個：
    /// 不足したa2、a3を選択中の方式で補完する。
    ///
    /// 空欄：
    /// そのインデックスを選択中の方式で補完する。
    /// </summary>
    private bool TryNormalizeSensorLine(
        string line,
        out string normalizedLine,
        out string completionError
    )
    {
        normalizedLine = null;

        completionError = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string[] receivedValues =
            line.Split(',');

        int originalValueCount =
            receivedValues.Length;

        /*
         * 10個の場合は、
         * 余分な最後の1個を削除する。
         */
        if (receivedValues.Length == 10)
        {
            Array.Resize(
                ref receivedValues,
                SensorValueCount
            );
        }

        /*
         * 7個・8個・9個以外は、
         * この処理では補完せず破棄する。
         */
        if (
            receivedValues.Length != 7 &&
            receivedValues.Length != 8 &&
            receivedValues.Length != 9
        )
        {
            return false;
        }

        string[] completedValues =
            new string[SensorValueCount];

        /*
         * 今回、本当に受信できた項目を記録する。
         *
         * 補完した値を直前値として保存しないために
         * 使用する。
         */
        bool[] receivedSuccessfully =
            new bool[SensorValueCount];

        List<string> completionDetails =
            new List<string>();

        for (int i = 0;
             i < SensorValueCount;
             i++)
        {
            bool indexExists =
                i < receivedValues.Length;

            bool valueExists =
                indexExists &&
                !string.IsNullOrWhiteSpace(
                    receivedValues[i]
                );

            if (valueExists)
            {
                string valueText =
                    receivedValues[i].Trim();

                bool parseResult =
                    float.TryParse(
                        valueText,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float parsedValue
                    );

                /*
                 * 値が存在するが数値として解析できない場合は、
                 * 受信データ全体を破棄する。
                 */
                if (!parseResult)
                {
                    return false;
                }

                if (
                    float.IsNaN(parsedValue) ||
                    float.IsInfinity(parsedValue)
                )
                {
                    return false;
                }

                /*
                 * 必ず小数点が「.」になる形式で保存する。
                 */
                completedValues[i] =
                    parsedValue.ToString(
                        "R",
                        CultureInfo.InvariantCulture
                    );

                receivedSuccessfully[i] = true;
            }
            else
            {
                completedValues[i] =
                    GetCompletionValue(
                        i,
                        out string completionSource
                    );

                completionDetails.Add(
                    $"{SensorValueNames[i]}=" +
                    completionSource
                );
            }
        }

        /*
         * 今回実際に受信できた項目だけを
         * 直前値として保存する。
         *
         * 補完した項目では履歴を上書きしない。
         */
        for (int i = 0;
             i < SensorValueCount;
             i++)
        {
            if (!receivedSuccessfully[i])
            {
                continue;
            }

            previousSensorValues[i] =
                completedValues[i];

            hasPreviousSensorValue[i] = true;
        }

        /*
         * 補完を行った場合は、
         * Consoleへ出力する内容を作成する。
         */
        if (completionDetails.Count > 0)
        {
            MissingValueFillMode currentMode =
                GetActiveMissingValueFillMode();

            string selectedModeText =
                currentMode ==
                MissingValueFillMode.PreviousValue
                    ? "直前の値で補完"
                    : "0で補完";

            string completedCsv =
                string.Join(
                    ",",
                    completedValues
                );

            completionError =
                "[SENSOR DATA COMPLETION] " +
                $"受信値が{originalValueCount}個、" +
                "または一部が空欄だったため補完しました。" +
                $" 設定=[{selectedModeText}]" +
                $" 補完内容=[" +
                $"{string.Join(", ", completionDetails)}]" +
                $" Received=[{line}]" +
                $" Completed=[{completedCsv}]";
        }

        normalizedLine =
            string.Join(
                ",",
                completedValues
            );

        return true;
    }

    /// <summary>
    /// Inspectorで選択した方式に従って
    /// 不足値を補完する。
    /// </summary>
    private string GetCompletionValue(
        int index,
        out string completionSource
    )
    {
        MissingValueFillMode currentMode =
            GetActiveMissingValueFillMode();

        if (
            currentMode ==
            MissingValueFillMode.PreviousValue
        )
        {
            /*
             * この項目について、
             * 以前に正常値を受信済みなら
             * その直前値を使用する。
             */
            if (hasPreviousSensorValue[index])
            {
                completionSource =
                    $"直前値(" +
                    $"{previousSensorValues[index]})";

                return previousSensorValues[index];
            }

            /*
             * 直前値で補完する設定でも、
             * その項目をまだ一度も正常受信していなければ
             * 直前値が存在しないため0を使用する。
             */
            completionSource =
                "0(直前値なし)";

            return "0";
        }

        completionSource = "0";

        return "0";
    }

    /// <summary>
    /// 読み取りスレッド側で使用する
    /// 現在の補完方法を取得する。
    /// </summary>
    private MissingValueFillMode
        GetActiveMissingValueFillMode()
    {
        int modeValue =
            Volatile.Read(
                ref activeMissingValueFillMode
            );

        if (
            modeValue ==
            (int)MissingValueFillMode.Zero
        )
        {
            return MissingValueFillMode.Zero;
        }

        return MissingValueFillMode.PreviousValue;
    }

    //==================================================
    // Write
    //==================================================

    /// <summary>
    /// Arduinoへ文字列を1行送信する。
    /// </summary>
    public bool SendToArduino(
        string data
    )
    {
        if (string.IsNullOrEmpty(data))
        {
            Debug.LogWarning(
                "送信データが空です。",
                this
            );

            return false;
        }

        if (
            serial == null ||
            !serial.IsOpen
        )
        {
            Debug.LogWarning(
                "シリアルポートが開かれていません。",
                this
            );

            return false;
        }

        try
        {
            /*
             * Unity内の複数箇所から同時に送信しても、
             * 送信文字列が混ざらないようにする。
             */
            lock (writeLockObj)
            {
                serial.WriteLine(data);
            }

            Debug.Log(
                "Arduinoへ送信: " +
                data,
                this
            );

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "Serial Write Error: " +
                e.Message,
                this
            );

            return false;
        }
    }

    /// <summary>
    /// 20個の0または1を
    /// CSV形式でArduinoへ送信する。
    /// </summary>
    public bool SendCsvData(
        int[] values
    )
    {
        if (values == null)
        {
            Debug.LogWarning(
                "送信する配列がnullです。",
                this
            );

            return false;
        }

        if (values.Length != 20)
        {
            Debug.LogWarning(
                "送信データは20個必要です。" +
                $"現在は{values.Length}個です。",
                this
            );

            return false;
        }

        for (int i = 0;
             i < values.Length;
             i++)
        {
            if (
                values[i] != 0 &&
                values[i] != 1
            )
            {
                Debug.LogWarning(
                    $"values[{i}]に" +
                    "0または1以外の値が入っています：" +
                    values[i],
                    this
                );

                return false;
            }
        }

        string csvData =
            string.Join(
                ",",
                values
            );

        return SendToArduino(
            csvData
        );
    }

    //==================================================
    // Get sensor data
    //==================================================

    /// <summary>
    /// 最後に利用可能な状態で受信した
    /// センサーデータを取得する。
    ///
    /// [e1, e2, e3, g1, g2, g3, a1, a2, a3]
    /// </summary>
    public string GetSensorData()
    {
        lock (dataLockObj)
        {
            return sensor_raw_data;
        }
    }

    /// <summary>
    /// 前回の取得後に新しいデータを
    /// 受信している場合だけ取得する。
    /// </summary>
    public bool TryGetSensorData(
        out string sensorData
    )
    {
        lock (dataLockObj)
        {
            if (!hasNewSensorData)
            {
                sensorData = null;

                return false;
            }

            sensorData =
                sensor_raw_data;

            /*
             * 同じデータを何度も
             * 新規データとして取得しないようにする。
             */
            hasNewSensorData = false;

            return true;
        }
    }

    /// <summary>
    /// これまでに破棄したデータ数を取得する。
    /// </summary>
    public int GetInvalidSensorLineCount()
    {
        return Volatile.Read(
            ref invalidSensorLineCount
        );
    }

    //==================================================
    // Close
    //==================================================

    private void OnDestroy()
    {
        CloseSerialPort();
    }

    private void OnApplicationQuit()
    {
        CloseSerialPort();
    }

    /// <summary>
    /// 読み取りスレッドと
    /// シリアルポートを終了する。
    /// </summary>
    private void CloseSerialPort()
    {
        running = false;

        /*
         * ReadTimeoutが50msなので、
         * 通常は短時間で読み取りスレッドが終了する。
         */
        if (
            readThread != null &&
            readThread.IsAlive
        )
        {
            readThread.Join();
        }

        if (serial != null)
        {
            try
            {
                if (serial.IsOpen)
                {
                    serial.Close();

                    Debug.Log(
                        "SerialPort Closed",
                        this
                    );
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "SerialPort Close Error: " +
                    e.Message,
                    this
                );
            }
            finally
            {
                serial.Dispose();

                serial = null;
            }
        }

        readThread = null;
    }
}
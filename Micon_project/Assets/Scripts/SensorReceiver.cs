using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Threading;

public class SensorReceiver : MonoBehaviour
{
    //==================================================
    // Constants
    //==================================================

    /*
     * Arduinoから送られるセンサー値の個数。
     *
     * [e1, e2, e3, g1, g2, g3, a1, a2, a3]
     */
    private const int SensorValueCount = 9;

    /*
     * 各インデックスの名称。
     * 補完時のエラーメッセージに使用する。
     */
    private static readonly string[] SensorValueNames =
    {
        "e1",
        "e2",
        "e3",
        "g1",
        "g2",
        "g3",
        "a1",
        "a2",
        "a3"
    };


    //==================================================
    // Serial settings
    //==================================================

    [Header("Serial Settings")]

    // Macで使用するシリアルポート名
    private string portName =
        "/dev/cu.usbserial-AQ01LGU4";

    [SerializeField]
    private int baudRate = 115200;


    //==================================================
    // Received sensor data
    //==================================================

    /*
     * 最後に使用可能な状態で受信した
     * センサーデータ。
     *
     * 順番：
     *
     * オイラー角3個
     * ジャイロ3個
     * 加速度3個
     */
    private string sensor_raw_data =
        "0,0,0,0,0,0,0,0,0";

    /*
     * 最後に9個すべて正常に受信できた値。
     *
     * まだ正常な9個を一度も受信していない場合は、
     * 初期値の0を不足部分の補完に使用する。
     */
    private readonly string[] lastCompleteSensorValues =
    {
        "0",
        "0",
        "0",
        "0",
        "0",
        "0",
        "0",
        "0",
        "0"
    };

    /*
     * 9個すべて正常なデータを
     * 一度でも受信したか。
     */
    private bool hasCompleteSensorValues = false;

    /*
     * 新しい使用可能なセンサーデータを
     * 受信したかどうか。
     */
    private bool hasNewSensorData = false;


    //==================================================
    // Serial
    //==================================================

    private SerialPort serial;

    private Thread readThread;

    /*
     * 別スレッドから読み書きするため、
     * volatileを付ける。
     */
    private volatile bool running = false;


    //==================================================
    // Error status
    //==================================================

    /*
     * 補完できなかった破損データの累計数。
     */
    private int invalidSensorLineCount = 0;

    /*
     * 最後に受信した、
     * 補完できなかった破損データ。
     */
    private string lastInvalidSensorLine = "";

    /*
     * 読み取りスレッドで発生したシリアルエラーを
     * Unityのメインスレッドへ渡す。
     */
    private string pendingSerialError = null;

    /*
     * 7個または8個だったため補完した情報を、
     * Unityのメインスレッドへ渡す。
     */
    private readonly Queue<string>
        pendingCompletionErrors =
            new Queue<string>();

    /*
     * 前回Consoleへ表示した
     * 破損データの累計数。
     */
    private int lastReportedInvalidCount = 0;

    /*
     * 破損データの警告を次に表示できる時刻。
     */
    private float nextInvalidLogTime = 0f;


    //==================================================
    // Locks
    //==================================================

    /*
     * センサーデータを安全に共有するためのロック。
     */
    private readonly object dataLockObj =
        new object();

    /*
     * UnityからArduinoへの送信処理が
     * 同時に実行されないようにするロック。
     */
    private readonly object writeLockObj =
        new object();

    /*
     * エラー情報を安全に共有するためのロック。
     */
    private readonly object statusLockObj =
        new object();


    //==================================================
    // Start
    //==================================================

    private void Start()
    {
        serial =
            new SerialPort(
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
                portName
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

        readThread =
            new Thread(
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
        OutputPendingSerialError();

        OutputInvalidDataWarning();

        OutputCompletionErrors();
    }


    /// <summary>
    /// 読み取りスレッドで発生した
    /// シリアルエラーをConsoleへ出力する
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
    /// 補完できなかった破損データについて
    /// Consoleへ警告を出力する
    /// </summary>
    private void OutputInvalidDataWarning()
    {
        int currentInvalidCount =
            Volatile.Read(
                ref invalidSensorLineCount
            );

        /*
         * 破損データが連続した場合でも、
         * Consoleを埋め尽くさないように
         * 最大1秒に1回だけ表示する。
         */
        if (
            currentInvalidCount ==
            lastReportedInvalidCount
        )
        {
            return;
        }

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
            Time.unscaledTime + 1f;
    }


    /// <summary>
    /// 7個または8個のデータを補完した場合の
    /// エラーをConsoleへ出力する
    /// </summary>
    private void OutputCompletionErrors()
    {
        while (true)
        {
            string completionError = null;

            lock (statusLockObj)
            {
                if (pendingCompletionErrors.Count > 0)
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

            /*
             * UnityにはDebug.Errorは存在しないため、
             * Debug.LogErrorを使用する。
             */
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
    /// Arduinoからのシリアルデータを
    /// 別スレッドで継続的に読み取る
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
                /*
                 * 改行までを1つのデータとして受信する。
                 */
                string line =
                    serial.ReadLine().Trim();

                /*
                 * データの確認と、
                 * 7個・8個の場合の補完を行う。
                 */
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

                    /*
                     * 補完できないデータは
                     * sensor_raw_dataへ保存しない。
                     */
                    continue;
                }

                /*
                 * 値を補完した場合は、
                 * メインスレッドでDebug.LogErrorを
                 * 実行するためキューへ追加する。
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
                 * ReadTimeoutによるタイムアウトは
                 * 正常な動作なので無視する。
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
    /// 受信したセンサーデータを
    /// 9個のデータへ正規化する。
    ///
    /// 受信順：
    ///
    /// [e1, e2, e3, g1, g2, g3, a1, a2, a3]
    ///
    /// 9個：
    /// そのまま使用し、前回正常値として保存する。
    ///
    /// 10個：
    /// 最後の1個を削除して、最初の9個を使用する。
    ///
    /// 8個：
    /// a3を前回正常値または0で補完する。
    ///
    /// 7個：
    /// a2とa3を前回正常値または0で補完する。
    ///
    /// 空欄：
    /// 空欄になっているインデックスを
    /// 前回正常値または0で補完する。
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
         * 最後のインデックス9を削除する。
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
         * この処理では補完できないため破棄する。
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

        List<string> missingValueNames =
            new List<string>();


        //==================================================
        // Parse received values
        //==================================================

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
                 * 値が存在するのに数値へ変換できない場合は、
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
            }
            else
            {
                /*
                 * 値が不足または空欄の場合。
                 *
                 * 完全な9個を以前に受信していれば
                 * そのときの同じインデックスを使用する。
                 *
                 * まだ完全な9個を受信していなければ
                 * 初期値の0を使用する。
                 */
                completedValues[i] =
                    hasCompleteSensorValues
                        ? lastCompleteSensorValues[i]
                        : "0";

                missingValueNames.Add(
                    SensorValueNames[i]
                );
            }
        }


        //==================================================
        // Save complete values
        //==================================================

        if (missingValueNames.Count == 0)
        {
            /*
             * 今回のデータに不足がなかった場合だけ、
             * 新しい前回正常値として保存する。
             *
             * 補完したデータは前回正常値にしない。
             */
            Array.Copy(
                completedValues,
                lastCompleteSensorValues,
                SensorValueCount
            );

            hasCompleteSensorValues = true;
        }
        else
        {
            string completionSource =
                hasCompleteSensorValues
                    ? "前回正常に受信した値"
                    : "0";

            string completedCsv =
                string.Join(
                    ",",
                    completedValues
                );

            completionError =
                "[SENSOR DATA COMPLETION] " +
                $"受信値が{originalValueCount}個だったため、" +
                $"不足部分 [{string.Join(", ", missingValueNames)}] を" +
                $"{completionSource}で補完しました。" +
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


    //==================================================
    // Write
    //==================================================

    /// <summary>
    /// Arduinoへ文字列を1行送信する
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
                /*
                 * WriteLine()によって、
                 * 最後にNewLineの"\n"が付加される。
                 */
                serial.WriteLine(data);
            }

            Debug.Log(
                "Arduinoへ送信: " +
                data
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
    /// CSV形式でArduinoへ送信する
    /// </summary>
    /// <param name="values">
    /// 0または1が入った長さ20の配列
    /// </param>
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
    /// 最後に使用可能な状態で受信した
    /// センサーデータを取得する。
    ///
    /// 戻り値の順番：
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
    /// 受信している場合だけ取得する
    /// </summary>
    /// <param name="sensorData">
    /// 新しく受信したセンサーデータ
    /// </param>
    /// <returns>
    /// 新しいデータが存在する場合はtrue
    /// </returns>
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
    /// これまでに破棄した、
    /// 補完できないデータの数を取得する
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
    /// シリアルポートを安全に終了する
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
                        "SerialPort Closed"
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
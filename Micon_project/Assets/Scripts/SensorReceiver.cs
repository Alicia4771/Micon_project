using UnityEngine;
using System.IO.Ports;
using System.Threading;

public class SensorReceiver : MonoBehaviour
{
    [Header("Serial Settings")]
    [SerializeField]
    private string portName = "/dev/cu.usbserial-120";

    [SerializeField]
    private int baudRate = 115200;

    private string sensor_raw_data = "0,0,0,0,0,0,0,0,0";

    private SerialPort serial;
    private Thread readThread;
    private bool running = false;

    // 受信データを安全に共有するためのロック
    private readonly object dataLockObj = new object();

    // 複数の処理から同時に送信されないようにするためのロック
    private readonly object writeLockObj = new object();

    private void Start()
    {
        serial = new SerialPort(portName, baudRate);

        serial.ReadTimeout = 50;
        serial.WriteTimeout = 100;

        // Unityから送る1行の終端文字
        serial.NewLine = "\n";

        try
        {
            serial.Open();
            Debug.Log("SerialPort Opened: " + portName);
        }
        catch (System.Exception e)
        {
            Debug.LogError("SerialPort Open Failed: " + e.Message);
            return;
        }

        running = true;

        readThread = new Thread(ReadSerialLoop);
        readThread.IsBackground = true;
        readThread.Start();
    }

    private void ReadSerialLoop()
    {
        while (running && serial != null && serial.IsOpen)
        {
            try
            {
                string line = serial.ReadLine().Trim();

                lock (dataLockObj)
                {
                    sensor_raw_data = line;
                }
            }
            catch (System.TimeoutException)
            {
                // タイムアウトは無視
            }
            catch (System.Exception e)
            {
                if (running)
                {
                    Debug.LogError("Serial Read Error: " + e.Message);
                }
            }
        }
    }

    /// <summary>
    /// Arduinoへ文字列を1行送信する
    /// </summary>
    public bool SendToArduino(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            Debug.LogWarning("送信データが空です。");
            return false;
        }

        if (serial == null || !serial.IsOpen)
        {
            Debug.LogWarning("シリアルポートが開かれていません。");
            return false;
        }

        try
        {
            lock (writeLockObj)
            {
                // 最後に改行を付けて送信
                serial.WriteLine(data);
            }

            Debug.Log("Arduinoへ送信: " + data);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Serial Write Error: " + e.Message);
            return false;
        }
    }

    /// <summary>
    /// 5つのint型データをCSV形式でArduinoへ送信する
    /// </summary>
    public bool SendCsvData(
        int a,
        int b,
        int c,
        int d,
        int e)
    {
        string csvData = $"{a},{b},{c},{d},{e}";

        return SendToArduino(csvData);
    }

    /// <summary>
    /// センサーデータを取得する
    /// </summary>
    /// <returns>
    /// センサの生データ
    /// [a1, a2, a3, g1, g2, g3, e1, e2, e3]
    /// </returns>
    public string GetSensorData()
    {
        lock (dataLockObj)
        {
            return sensor_raw_data;
        }
    }

    private void OnDestroy()
    {
        CloseSerialPort();
    }

    private void OnApplicationQuit()
    {
        CloseSerialPort();
    }

    private void CloseSerialPort()
    {
        running = false;

        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join();
        }

        if (serial != null && serial.IsOpen)
        {
            serial.Close();
            Debug.Log("SerialPort Closed");
        }
    }
}
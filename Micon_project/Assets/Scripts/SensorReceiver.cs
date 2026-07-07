using UnityEngine;
using System.IO.Ports;
using System.Threading;

public class SensorReceiver : MonoBehaviour
{
    [Header("Serial Settings")]
    // [SerializeField] private string portName = "/dev/cu.usbserial-1140";
    private string portName = "/dev/cu.usbserial-120";      // rin's Mac
    [SerializeField] private int baudRate = 115200;
    
    private string sensor_raw_data;

    private SerialPort serial;
    private Thread readThread;
    private bool running = false;
    private object lockObj = new object(); // スレッド安全用


    private void Start()
    {
        serial = new SerialPort(portName, baudRate);
        serial.ReadTimeout = 50;

        try
        {
            serial.Open();
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
                string line = serial.ReadLine();
                lock (lockObj)
                {
                    // スレッドセーフに更新
                    sensor_raw_data = line;
                }
            }
            catch (System.TimeoutException)
            {
                // タイムアウトは無視
            }
            catch (System.Exception e)
            {
                Debug.LogError("Serial Read Error: " + e.Message);
            }
        }
    }

    private void OnDestroy()
    {
        running = false;
        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(); // スレッド終了待ち
        }

        if (serial != null && serial.IsOpen)
        {
            serial.Close();
        }
    }

    /// <summary>
    /// センサーデータを取得するメソッド
    /// </summary>
    /// <returns>string センサの生データ [a1, a2, a3, g1, g2, g3, e1, e2, e3]</returns>
    public string GetSensorData()
    {
        lock (lockObj)
        {
            return sensor_raw_data;
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

public static class DataManager
{
    private static Vector3 acceleration_sensor_value;
    private static Vector3 gyro_sensor_value;
    private static Vector3 euler_sensor_value;

    // ゲーム起動後に最初に取得した水平姿勢
    private static Vector3 initial_euler_sensor_value;

    // 水平姿勢を一度でも取得したか
    private static bool has_initial_euler_sensor_value = false;

    private static bool has_received_sensor_data = false;

    private static int score = 0;

    private static List<string> obstacle_list = new();


    public static void Initialize()
    {
        acceleration_sensor_value = Vector3.zero;
        gyro_sensor_value = Vector3.zero;
        euler_sensor_value = Vector3.zero;

        has_received_sensor_data = false;

        /*
         * initial_euler_sensor_value と
         * has_initial_euler_sensor_value はここでは初期化しない。
         *
         * Initialize()がシーン遷移時に呼ばれても、
         * 最初に取得した水平姿勢を維持するため。
         */

        obstacle_list.Clear();
        
        score = 0;
    }







    /// <summary>
    /// センサー値を設定する
    /// </summary>
    /// <param name="sensor_raw_data">string センサの生データ</param>
    /// <returns></returns>
    public static bool SetSensorValue(string sensor_raw_data)
    {
        string[] sensor_values = sensor_raw_data.Split(',');

        if (sensor_values.Length != 9)
        {
            Debug.LogError("Invalid sensor data length: " + sensor_values.Length);
            return false;
        }

        try
        {
            acceleration_sensor_value.x = float.Parse(sensor_values[0]);
            acceleration_sensor_value.y = float.Parse(sensor_values[1]);
            acceleration_sensor_value.z = float.Parse(sensor_values[2]);

            gyro_sensor_value.x = float.Parse(sensor_values[3]);
            gyro_sensor_value.y = float.Parse(sensor_values[4]);
            gyro_sensor_value.z = float.Parse(sensor_values[5]);

            euler_sensor_value.x = float.Parse(sensor_values[6]);
            euler_sensor_value.y = float.Parse(sensor_values[7]);
            euler_sensor_value.z = float.Parse(sensor_values[8]);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to parse sensor data: " + e.Message);
            return false;
        }

        has_received_sensor_data = true;
        return true;
    }

    /// <summary>
    /// 加速度センサー値を取得する
    /// </summary>
    /// <returns></returns>
    public static Vector3 GetAccelerationSensorValue()
    {
        return acceleration_sensor_value;
    }

    /// <summary>
    /// ジャイロセンサー値を取得する
    /// </summary>
    /// <returns></returns>
    public static Vector3 GetGyroSensorValue()
    {
        return gyro_sensor_value;
    }

    /// <summary>
    /// オイラー角センサー値を取得する
    /// </summary>
    /// <returns></returns>
    public static Vector3 GetEulerSensorValue()
    {
        return euler_sensor_value;
    }


    /// <summary>
    /// 最初の水平姿勢を一度だけ保存する。
    /// すでに保存済みの場合は上書きしない。
    /// </summary>
    /// <returns>
    /// 今回保存できた場合はtrue。
    /// すでに保存済みの場合はfalse。
    /// </returns>
    public static bool TrySetInitialEulerSensorValue(
        Vector3 initial_euler_value
    )
    {
        if (has_initial_euler_sensor_value)
        {
            return false;
        }

        initial_euler_sensor_value =
            initial_euler_value;

        has_initial_euler_sensor_value = true;

        return true;
    }

    /// <summary>
    /// 水平姿勢を一度でも取得したか。
    /// </summary>
    public static bool HasInitialEulerSensorValue()
    {
        return has_initial_euler_sensor_value;
    }

    /// <summary>
    /// 最初に取得した水平姿勢を返す。
    /// </summary>
    public static Vector3 GetInitialEulerSensorValue()
    {
        return initial_euler_sensor_value;
    }

    /// <summary>
    /// 水平姿勢を取り直したい場合に呼ぶ。
    /// 通常のシーン遷移では呼ばない。
    /// </summary>
    public static void ResetInitialEulerSensorValue()
    {
        initial_euler_sensor_value =
            Vector3.zero;

        has_initial_euler_sensor_value = false;
    }

    /// <summary>
    /// センサデータを1回以上正常に受信したか
    /// </summary>
    public static bool HasReceivedSensorData()
    {
        return has_received_sensor_data;
    }

    /// <summary>
    /// 加算するスコアの値を設定する
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool AddScore(int value)
    {
        if (value < 0) return false;

        score += value;
        return true;
    }

    /// <summary>
    /// 現在のスコアを取得する
    /// </summary>
    /// <returns></returns>
    public static int GetScore()
    {
        return score;
    }
}

using UnityEngine;
using System.Collections.Generic;
using System.Globalization;

public static class DataManager
{
    private static Vector3 acceleration_sensor_value;
    private static Vector3 gyro_sensor_value;
    private static Vector3 euler_sensor_value;

    // ゲーム起動後に最初に取得した水平姿勢
    private static Vector3 initial_euler_sensor_value;

    // 水平姿勢を一度でも取得したか
    private static bool has_initial_euler_sensor_value = false;

    // センサーデータを1回以上受信したか
    private static bool has_received_sensor_data = false;

    private static int score = 0;

    private static List<string> obstacle_list = new();


    public static void Initialize()
    {
        acceleration_sensor_value =
            Vector3.zero;

        gyro_sensor_value =
            Vector3.zero;

        euler_sensor_value =
            Vector3.zero;

        has_received_sensor_data = false;

        /*
         * initial_euler_sensor_value と
         * has_initial_euler_sensor_value は
         * ここでは初期化しない。
         *
         * Initialize()がシーン遷移時に呼ばれても、
         * 最初に取得した水平姿勢を維持するため。
         */

        obstacle_list.Clear();

        score = 0;
    }


    /// <summary>
    /// センサー値を設定する。
    ///
    /// Arduinoから送られる値の順番：
    ///
    /// [e1, e2, e3, g1, g2, g3, a1, a2, a3]
    ///
    /// e1～e3：オイラー角
    /// g1～g3：ジャイロ
    /// a1～a3：加速度
    /// </summary>
    /// <param name="sensor_raw_data">
    /// センサから受信したCSV形式の生データ
    /// </param>
    /// <returns>
    /// センサー値を設定できた場合はtrue
    /// </returns>
    public static bool SetSensorValue(
        string sensor_raw_data
    )
    {
        if (string.IsNullOrWhiteSpace(
                sensor_raw_data
            ))
        {
            return false;
        }

        string[] sensor_values =
            sensor_raw_data.Split(',');

        /*
         * SensorReceiverで9個に正規化されるが、
         * 念のためDataManager側でも確認する。
         */
        if (sensor_values.Length != 9)
        {
            Debug.LogError(
                "[PARSE ERROR] " +
                $"Length={sensor_values.Length} " +
                $"Data=[{sensor_raw_data}]"
            );

            return false;
        }

        /*
         * 途中まで代入した状態で解析失敗しないように、
         * まず9個すべてをfloat配列へ変換する。
         */
        float[] parsed_values =
            new float[9];

        for (int i = 0;
             i < parsed_values.Length;
             i++)
        {
            string value_text =
                sensor_values[i].Trim();

            bool parse_result =
                float.TryParse(
                    value_text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsed_values[i]
                );

            if (!parse_result)
            {
                Debug.LogError(
                    "[PARSE ERROR] " +
                    $"Index={i} " +
                    $"Value=[{value_text}] " +
                    $"Data=[{sensor_raw_data}]"
                );

                return false;
            }

            if (
                float.IsNaN(parsed_values[i]) ||
                float.IsInfinity(parsed_values[i])
            )
            {
                Debug.LogError(
                    "[PARSE ERROR] " +
                    $"Index={i}にNaNまたはInfinityが" +
                    "入っています。" +
                    $" Data=[{sensor_raw_data}]"
                );

                return false;
            }
        }


        //==================================================
        // Euler
        //==================================================

        /*
         * インデックス0～2：
         * オイラー角
         */
        euler_sensor_value.x =
            parsed_values[0];

        euler_sensor_value.y =
            parsed_values[1];

        euler_sensor_value.z =
            parsed_values[2];


        //==================================================
        // Gyro
        //==================================================

        /*
         * インデックス3～5：
         * ジャイロ
         */
        gyro_sensor_value.x =
            parsed_values[3];

        gyro_sensor_value.y =
            parsed_values[4];

        gyro_sensor_value.z =
            parsed_values[5];


        //==================================================
        // Acceleration
        //==================================================

        /*
         * インデックス6～8：
         * 加速度
         */
        acceleration_sensor_value.x =
            parsed_values[6];

        acceleration_sensor_value.y =
            parsed_values[7];

        acceleration_sensor_value.z =
            parsed_values[8];


        has_received_sensor_data = true;

        return true;
    }


    /// <summary>
    /// 加速度センサー値を取得する
    /// </summary>
    public static Vector3 GetAccelerationSensorValue()
    {
        return acceleration_sensor_value;
    }


    /// <summary>
    /// ジャイロセンサー値を取得する
    /// </summary>
    public static Vector3 GetGyroSensorValue()
    {
        return gyro_sensor_value;
    }


    /// <summary>
    /// オイラー角センサー値を取得する
    /// </summary>
    public static Vector3 GetEulerSensorValue()
    {
        return euler_sensor_value;
    }


    /// <summary>
    /// 最初の水平姿勢を一度だけ保存する。
    /// すでに保存済みの場合は上書きしない。
    /// </summary>
    /// <param name="initial_euler_value">
    /// 保存する最初のオイラー角
    /// </param>
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
    /// 水平姿勢を一度でも取得したか
    /// </summary>
    public static bool HasInitialEulerSensorValue()
    {
        return has_initial_euler_sensor_value;
    }


    /// <summary>
    /// 最初に取得した水平姿勢を返す
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
    /// センサーデータを1回以上受信したか
    /// </summary>
    public static bool HasReceivedSensorData()
    {
        return has_received_sensor_data;
    }


    /// <summary>
    /// スコアを加算する
    /// </summary>
    public static bool AddScore(int value)
    {
        if (value < 0)
        {
            return false;
        }

        score += value;

        return true;
    }


    /// <summary>
    /// 現在のスコアを取得する
    /// </summary>
    public static int GetScore()
    {
        return score;
    }
}
using UnityEngine;
using System.Collections.Generic;

public static class DataManager
{
    private static Vector3 acceleration_sensor_value;
    private static Vector3 gyro_sensor_value;
    private static Vector3 euler_sensor_value;

    private static int score = 0;


    public static void Initialize()
    {
        score = 0;
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

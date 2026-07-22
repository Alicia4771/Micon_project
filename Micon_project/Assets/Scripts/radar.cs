using UnityEngine;

public class radar : MonoBehaviour
{
    private const int MatrixSize = 16;
    private const int LastObstacleRow = 13;

    private const int RadarWordCount = 8;
    private const int TransmitWordCount = 9;

    [Header("Radar Target")]
    [SerializeField, Tooltip("操作する飛行機のTransform")]
    private Transform airplane;

    [SerializeField, Tooltip("障害物に設定されているTag")]
    private string obstacleTag = "Stone";

    [Header("Horizontal Radar Range")]
    [SerializeField, Min(0.01f),
     Tooltip("左右方向のレーダー検出範囲")]
    private float radarHorizontalRange = 30f;

    [SerializeField, Min(0.01f),
     Tooltip("前方方向のレーダー検出範囲")]
    private float radarRange = 200f;

    [Header("Radar Direction")]
    [SerializeField,
     Tooltip("飛行機モデルの正面が+Z方向でない場合の補正角度")]
    private float forwardAngleOffset = 0f;

    [SerializeField,
     Tooltip("LEDマトリクスの左右が逆の場合に有効にする")]
    private bool mirrorHorizontal = false;

    [Header("Vertical Approach")]
    [SerializeField,
     Tooltip("海水面オブジェクト。TransformのY座標を海水面として使用する")]
    private Transform seaSurface;

    [SerializeField, Min(0.01f),
     Tooltip("上下方向の接近を検知し始める距離")]
    private float verticalApproachRange = 20f;

    [SerializeField, Min(0.01f),
     Tooltip("上下方向の判定対象にする障害物の水平方向距離")]
    private float verticalObstacleHorizontalRange = 10f;

    [Header("Vibration")]
    [SerializeField, Min(0.01f),
     Tooltip("偏心モータを振動させ始める3次元距離")]
    private float vibrationApproachRange = 30f;

    [SerializeField, Min(0f),
     Tooltip("衝突後に最大振動を維持する秒数。送信間隔以上に設定する")]
    private float collisionVibrationHoldSeconds = 1.5f;

    /*
     * radarData[行, 列]
     *
     * 0～13行目：障害物
     * 14～15行目：自機
     */
    private readonly int[,] radarData =
        new int[MatrixSize, MatrixSize];

    // それぞれ0～255
    private int lowerApproachValue = 0;
    private int upperApproachValue = 0;
    private int obstacleApproachValue = 0;

    // この時刻までは衝突による最大振動を出す
    private float collisionVibrationUntilTime = -1f;

    private bool tagErrorDisplayed = false;

    private void Start()
    {
        UpdateRadarData();
    }

    /// <summary>
    /// レーダーおよび接近情報を更新する
    /// </summary>
    private void UpdateRadarData()
    {
        ClearRadar();
        DrawAirplane();

        lowerApproachValue = 0;
        upperApproachValue = 0;
        obstacleApproachValue = 0;

        if (airplane == null)
        {
            Debug.LogWarning(
                "radarのAirplaneに飛行機のTransformを設定してください。",
                this
            );

            return;
        }

        float safeHorizontalRange =
            Mathf.Max(0.01f, radarHorizontalRange);

        float safeForwardRange =
            Mathf.Max(0.01f, radarRange);

        float safeVerticalRange =
            Mathf.Max(0.01f, verticalApproachRange);

        float safeVerticalHorizontalRange =
            Mathf.Max(0.01f, verticalObstacleHorizontalRange);

        float safeVibrationRange =
            Mathf.Max(0.01f, vibrationApproachRange);

        /*
         * 海水面への接近度を下側LEDの値へ反映する。
         */
        UpdateSeaSurfaceApproach(safeVerticalRange);

        /*
         * 飛行機の前方向をX、Z平面上で求める。
         */
        Vector3 forward =
            Quaternion.AngleAxis(
                forwardAngleOffset,
                Vector3.up
            ) * airplane.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        forward.Normalize();

        Vector3 right =
            Vector3.Cross(
                Vector3.up,
                forward
            ).normalized;

        GameObject[] obstacles;

        try
        {
            obstacles =
                GameObject.FindGameObjectsWithTag(
                    obstacleTag
                );
        }
        catch (UnityException)
        {
            if (!tagErrorDisplayed)
            {
                Debug.LogError(
                    $"Tag「{obstacleTag}」が登録されていません。",
                    this
                );

                tagErrorDisplayed = true;
            }

            return;
        }

        foreach (GameObject obstacle in obstacles)
        {
            if (obstacle == null)
            {
                continue;
            }

            Vector3 difference3D =
                obstacle.transform.position -
                airplane.position;

            /*
             * 3次元ユークリッド距離による振動強度を求める。
             */
            float distance3D =
                difference3D.magnitude;

            int vibrationValue =
                DistanceToBrightness(
                    distance3D,
                    safeVibrationRange
                );

            obstacleApproachValue =
                Mathf.Max(
                    obstacleApproachValue,
                    vibrationValue
                );

            /*
            * 飛行機の近くを上下方向に通る障害物だけ、
            * 上側・下側LEDの対象にする。
            */
            float horizontalDistance =
                new Vector2(
                    difference3D.x,
                    difference3D.z
                ).magnitude;

            if (horizontalDistance <= safeVerticalHorizontalRange)
            {
                Collider obstacleCollider =
                    obstacle.GetComponentInChildren<Collider>();

                float obstacleTopY =
                    obstacle.transform.position.y;

                float obstacleBottomY =
                    obstacle.transform.position.y;

                /*
                * Colliderがある場合は、
                * 障害物の中心ではなく上端・下端を使う。
                */
                if (obstacleCollider != null)
                {
                    obstacleTopY =
                        obstacleCollider.bounds.max.y;

                    obstacleBottomY =
                        obstacleCollider.bounds.min.y;
                }

                float airplaneY =
                    airplane.position.y;


                //==================================================
                // 上側を独立して判定
                //==================================================

                /*
                * 障害物の上端が飛行機より上にある場合は、
                * 上側に障害物が存在する。
                */
                if (obstacleTopY > airplaneY)
                {
                    /*
                    * 障害物全体が上にある場合：
                    * 飛行機から障害物の下端までの距離。
                    *
                    * 障害物が飛行機の高さまで重なっている場合：
                    * 距離を0として最大値255にする。
                    */
                    float upperDistance =
                        Mathf.Max(
                            0f,
                            obstacleBottomY - airplaneY
                        );

                    int upperValue =
                        DistanceToBrightness(
                            upperDistance,
                            safeVerticalRange
                        );

                    upperApproachValue =
                        Mathf.Max(
                            upperApproachValue,
                            upperValue
                        );
                }


                //==================================================
                // 下側を独立して判定
                //==================================================

                /*
                * 障害物の下端が飛行機より下にある場合は、
                * 下側に障害物が存在する。
                */
                if (obstacleBottomY < airplaneY)
                {
                    /*
                    * 障害物全体が下にある場合：
                    * 障害物の上端から飛行機までの距離。
                    *
                    * 障害物が飛行機の高さまで重なっている場合：
                    * 距離を0として最大値255にする。
                    */
                    float lowerDistance =
                        Mathf.Max(
                            0f,
                            airplaneY - obstacleTopY
                        );

                    int lowerValue =
                        DistanceToBrightness(
                            lowerDistance,
                            safeVerticalRange
                        );

                    lowerApproachValue =
                        Mathf.Max(
                            lowerApproachValue,
                            lowerValue
                        );
                }
                Debug.Log("obstacleTopY: " + obstacleTopY);
                Debug.Log("obstacleBottomY: " + obstacleBottomY);
            }
            // Debug.Log("horizontalDistance: " + horizontalDistance);

            /*
             * X、Z平面レーダーへ障害物を追加する。
             */
            AddObstacleToHorizontalRadar(
                difference3D,
                forward,
                right,
                safeHorizontalRange,
                safeForwardRange
            );
        }

        /*
         * 実際に衝突した直後は、
         * 距離による値にかかわらず最大振動にする。
         */
        if (Time.time <= collisionVibrationUntilTime)
        {
            obstacleApproachValue = 255;
        }

        lowerApproachValue =
            Mathf.Clamp(lowerApproachValue, 0, 255);

        upperApproachValue =
            Mathf.Clamp(upperApproachValue, 0, 255);

        obstacleApproachValue =
            Mathf.Clamp(obstacleApproachValue, 0, 255);

        Debug.Log("ue: " + upperApproachValue + ", shita: " + lowerApproachValue);
    }

    /// <summary>
    /// 海水面への接近度を下側の値へ反映する
    /// </summary>
    private void UpdateSeaSurfaceApproach(
        float safeVerticalRange
    )
    {
        if (seaSurface == null)
        {
            return;
        }

        /*
         * 飛行機が海水面より上にいる場合：
         * 正の距離になる。
         *
         * 海水面以下の場合：
         * 最大値255にする。
         */
        float seaSurfaceDistance =
            airplane.position.y -
            seaSurface.position.y;

        int seaSurfaceValue;

        if (seaSurfaceDistance <= 0f)
        {
            seaSurfaceValue = 255;
        }
        else
        {
            seaSurfaceValue =
                DistanceToBrightness(
                    seaSurfaceDistance,
                    safeVerticalRange
                );
        }

        lowerApproachValue =
            Mathf.Max(
                lowerApproachValue,
                seaSurfaceValue
            );
    }

    /// <summary>
    /// 障害物をX、Z平面レーダーへ追加する
    /// </summary>
    private void AddObstacleToHorizontalRadar(
        Vector3 difference3D,
        Vector3 forward,
        Vector3 right,
        float safeHorizontalRange,
        float safeForwardRange
    )
    {
        Vector3 difference2D =
            new Vector3(
                difference3D.x,
                0f,
                difference3D.z
            );

        if (difference2D.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float localRight =
            Vector3.Dot(
                difference2D,
                right
            );

        float localForward =
            Vector3.Dot(
                difference2D,
                forward
            );

        // 後方は表示しない
        if (localForward < 0f)
        {
            return;
        }

        if (localForward > safeForwardRange)
        {
            return;
        }

        if (Mathf.Abs(localRight) >
            safeHorizontalRange)
        {
            return;
        }

        float horizontalRatio =
            Mathf.InverseLerp(
                -safeHorizontalRange,
                safeHorizontalRange,
                localRight
            );

        int column =
            Mathf.RoundToInt(
                horizontalRatio *
                (MatrixSize - 1)
            );

        if (mirrorHorizontal)
        {
            column =
                MatrixSize - 1 - column;
        }

        float forwardRatio =
            Mathf.InverseLerp(
                0f,
                safeForwardRange,
                localForward
            );

        int row =
            Mathf.RoundToInt(
                (1f - forwardRatio) *
                LastObstacleRow
            );

        row = Mathf.Clamp(
            row,
            0,
            LastObstacleRow
        );

        column = Mathf.Clamp(
            column,
            0,
            MatrixSize - 1
        );

        radarData[row, column] = 1;
    }

    /// <summary>
    /// 距離を0～255の接近度へ変換する
    ///
    /// 距離がrange以上：0
    /// 距離が0：255
    /// </summary>
    private int DistanceToBrightness(
        float distance,
        float range
    )
    {
        if (distance >= range)
        {
            return 0;
        }

        float ratio =
            1f - Mathf.Clamp01(
                distance / range
            );

        return Mathf.RoundToInt(
            ratio * 255f
        );
    }

    private void ClearRadar()
    {
        for (int row = 0;
             row < MatrixSize;
             row++)
        {
            for (int column = 0;
                 column < MatrixSize;
                 column++)
            {
                radarData[row, column] = 0;
            }
        }
    }

    private void DrawAirplane()
    {
        radarData[14, 7] = 1;
        radarData[14, 8] = 1;

        radarData[15, 7] = 1;
        radarData[15, 8] = 1;
    }

    /// <summary>
    /// 衝突時にAirplaneから呼び出す
    /// </summary>
    public void NotifyObstacleCollision()
    {
        collisionVibrationUntilTime =
            Time.time +
            collisionVibrationHoldSeconds;

        /*
         * 次にGetRadarData()が呼ばれる前でも、
         * 現在値を最大にしておく。
         */
        obstacleApproachValue = 255;
    }

    /// <summary>
    /// 8個のレーダー値と1個の接近情報を返す
    ///
    /// 戻り値：
    /// [0]～[7] レーダーデータ
    /// [8]      下・上・振動を圧縮した値
    /// </summary>
    public uint[] GetRadarData()
    {
        UpdateRadarData();

        uint[] packedData =
            new uint[TransmitWordCount];

        /*
         * 16×16のレーダーを8個のuintへ圧縮する。
         */
        for (int row = 0;
             row < MatrixSize;
             row++)
        {
            for (int column = 0;
                 column < MatrixSize;
                 column++)
            {
                int index =
                    row * MatrixSize + column;

                int uintIndex =
                    index / 32;

                int bitIndex =
                    index % 32;

                if (radarData[row, column] == 1)
                {
                    packedData[uintIndex] |=
                        1u << bitIndex;
                }
            }
        }

        /*
         * 9個目へ3つの0～255を圧縮する。
         *
         * bit 0～7   ：下側
         * bit 8～15  ：上側
         * bit 16～23 ：障害物接近・振動
         */
        packedData[RadarWordCount] =
            PackApproachData(
                lowerApproachValue,
                upperApproachValue,
                obstacleApproachValue
            );

        return packedData;
    }

    /// <summary>
    /// 3個の0～255を1個のuintへ圧縮する
    /// </summary>
    private uint PackApproachData(
        int lowerValue,
        int upperValue,
        int obstacleValue
    )
    {
        uint lower =
            (uint)Mathf.Clamp(
                lowerValue,
                0,
                255
            );

        uint upper =
            (uint)Mathf.Clamp(
                upperValue,
                0,
                255
            );

        uint obstacle =
            (uint)Mathf.Clamp(
                obstacleValue,
                0,
                255
            );

        return lower |
               (upper << 8) |
               (obstacle << 16);
    }

    public int GetLowerApproachValue()
    {
        return lowerApproachValue;
    }

    public int GetUpperApproachValue()
    {
        return upperApproachValue;
    }

    public int GetObstacleApproachValue()
    {
        return obstacleApproachValue;
    }

    public int[,] GetRadarMatrix()
    {
        UpdateRadarData();

        int[,] copiedData =
            new int[MatrixSize, MatrixSize];

        for (int row = 0;
             row < MatrixSize;
             row++)
        {
            for (int column = 0;
                 column < MatrixSize;
                 column++)
            {
                copiedData[row, column] =
                    radarData[row, column];
            }
        }

        return copiedData;
    }
}
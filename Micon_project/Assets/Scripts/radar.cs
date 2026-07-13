using System.Text;
using UnityEngine;

public class radar : MonoBehaviour
{
    private const int MatrixSize = 16;

    // 障害物の表示に使用する最後の行
    // 14行目と15行目は自機表示に使用する
    private const int LastObstacleRow = 13;

    [Header("Radar Target")]
    [SerializeField]
    private Transform airplane;

    [SerializeField]
    private string obstacleTag = "Stone";

    [Header("Radar Settings")]
    [SerializeField, Min(1f)]
    private float radarRange = 250f;

    [SerializeField, Min(0.02f)]
    private float radarUpdateInterval = 0.2f;

    [Tooltip("飛行機モデルの先頭が+Z方向でない場合に補正する角度")]
    [SerializeField]
    private float forwardAngleOffset = 0f;

    [Tooltip("LEDマトリクスの左右が逆に表示された場合に有効にする")]
    [SerializeField]
    private bool mirrorHorizontal = false;

    [Header("Serial Communication")]
    [SerializeField]
    private SensorReceiver sensorReceiver;

    [SerializeField]
    private bool sendToArduino = true;

    [Header("Debug")]
    [SerializeField]
    private bool showRadarInConsole = false;

    // radarData[行, 列]
    // 行：0が上、15が下
    // 列：0が左、15が右
    private readonly int[,] radarData =
        new int[MatrixSize, MatrixSize];

    private float updateTimer = 0f;
    private bool tagErrorDisplayed = false;

    private void Start()
    {
        ClearRadar();
        DrawAirplane();

        if (airplane == null)
        {
            Debug.LogError(
                "radarのAirplaneに飛行機のTransformを設定してください。"
            );
        }

        if (sensorReceiver == null && sendToArduino)
        {
            Debug.LogWarning(
                "radarのSensor Receiverが設定されていないため、" +
                "レーダーデータは送信されません。"
            );
        }
    }

    private void Update()
    {
        updateTimer += Time.deltaTime;

        if (updateTimer < radarUpdateInterval)
        {
            return;
        }

        updateTimer -= radarUpdateInterval;

        UpdateRadar();
    }

    /// <summary>
    /// レーダー配列を更新する
    /// </summary>
    private void UpdateRadar()
    {
        ClearRadar();
        DrawAirplane();

        if (airplane == null)
        {
            return;
        }

        /*
         * 飛行機の前方向をx,z平面に投影する。
         *
         * airplane.forwardを利用するため、
         * 飛行機が旋回するとレーダーも自動的に回転する。
         */
        Vector3 forward =
            Quaternion.AngleAxis(
                forwardAngleOffset,
                Vector3.up
            ) * airplane.forward;

        // 今回はx,z平面だけで考えるので、y成分を無視する
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        forward.Normalize();

        // 飛行機から見た右方向
        Vector3 right =
            Vector3.Cross(Vector3.up, forward).normalized;

        GameObject[] obstacles;

        try
        {
            obstacles =
                GameObject.FindGameObjectsWithTag(obstacleTag);
        }
        catch (UnityException)
        {
            if (!tagErrorDisplayed)
            {
                Debug.LogError(
                    $"Tag「{obstacleTag}」が登録されていません。" +
                    "UnityのTags and Layersから登録してください。"
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

            // 飛行機から障害物までのベクトル
            Vector3 difference =
                obstacle.transform.position - airplane.position;

            // x,z平面だけで距離と方向を計算する
            difference.y = 0f;

            /*
             * 飛行機の右方向と前方向に対して内積を取ることで、
             * 障害物を飛行機基準の座標に変換する。
             */
            float localRight =
                Vector3.Dot(difference, right);

            float localForward =
                Vector3.Dot(difference, forward);

            /*
             * localForwardが負の場合は飛行機の後方。
             *
             * 0以上だけを表示することで、
             * 正面から左右90度の範囲になる。
             */
            if (localForward < 0f)
            {
                continue;
            }

            float distance = Mathf.Sqrt(
                localRight * localRight +
                localForward * localForward
            );

            // 自機とほぼ同じ場所、またはレーダー範囲外
            if (distance < 0.01f || distance > radarRange)
            {
                continue;
            }

            /*
             * 横方向を0～15列に変換する。
             *
             * -radarRange → 0列
             * 0           → 7～8列付近
             * radarRange  → 15列
             */
            float horizontalRatio =
                Mathf.InverseLerp(
                    -radarRange,
                    radarRange,
                    localRight
                );

            int column = Mathf.RoundToInt(
                horizontalRatio * (MatrixSize - 1)
            );

            if (mirrorHorizontal)
            {
                column = MatrixSize - 1 - column;
            }

            /*
             * 前方向の距離を0～13行に変換する。
             *
             * 遠い障害物 → 上側の0行目
             * 近い障害物 → 下側の13行目
             */
            float forwardRatio =
                Mathf.Clamp01(localForward / radarRange);

            int row = Mathf.RoundToInt(
                (1f - forwardRatio) * LastObstacleRow
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

        if (sendToArduino && sensorReceiver != null)
        {
            string sendData = CreateSerialData();

            sensorReceiver.SendToArduino(sendData);
        }

        if (showRadarInConsole)
        {
            Debug.Log(CreateDebugText());
        }
    }

    /// <summary>
    /// レーダー配列をすべて0にする
    /// </summary>
    private void ClearRadar()
    {
        for (int row = 0; row < MatrixSize; row++)
        {
            for (int column = 0; column < MatrixSize; column++)
            {
                radarData[row, column] = 0;
            }
        }
    }

    /// <summary>
    /// 自機を下中央の2×2に表示する
    /// </summary>
    private void DrawAirplane()
    {
        radarData[14, 7] = 1;
        radarData[14, 8] = 1;

        radarData[15, 7] = 1;
        radarData[15, 8] = 1;
    }

    /// <summary>
    /// Arduinoへ送信する文字列を作る
    ///
    /// 形式：
    /// R:000000...000
    ///
    /// R:の後ろに、0または1が256個並ぶ
    /// </summary>
    private string CreateSerialData()
    {
        StringBuilder builder =
            new StringBuilder(2 + MatrixSize * MatrixSize);

        // レーダーデータであることを示すヘッダー
        builder.Append("R:");

        for (int row = 0; row < MatrixSize; row++)
        {
            for (int column = 0; column < MatrixSize; column++)
            {
                builder.Append(
                    radarData[row, column] == 0 ? '0' : '1'
                );
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// 他のスクリプトからレーダー配列を取得する
    /// </summary>
    public int[,] GetRadarData()
    {
        // 元の配列を書き換えられないようにコピーを返す
        return (int[,])radarData.Clone();
    }

    /// <summary>
    /// UnityのConsoleでレーダーを確認する
    /// </summary>
    [ContextMenu("Print Radar")]
    public void PrintRadar()
    {
        Debug.Log(CreateDebugText());
    }

    private string CreateDebugText()
    {
        StringBuilder builder = new StringBuilder();

        for (int row = 0; row < MatrixSize; row++)
        {
            builder.Append("[");

            for (int column = 0; column < MatrixSize; column++)
            {
                builder.Append(radarData[row, column]);

                if (column < MatrixSize - 1)
                {
                    builder.Append(", ");
                }
            }

            builder.AppendLine("]");
        }

        return builder.ToString();
    }
}
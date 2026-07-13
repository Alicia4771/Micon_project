using UnityEngine;

public class radar : MonoBehaviour
{
    private const int MatrixSize = 16;

    // 14行目・15行目は自機の表示に使用する
    private const int LastObstacleRow = 13;

    [Header("Radar Target")]
    [SerializeField]
    private Transform airplane;

    [SerializeField]
    private string obstacleTag = "Stone";

    [Header("Radar Settings")]
    [SerializeField, Min(1f)]
    private float radarRange = 250f;

    [Tooltip("飛行機モデルの正面が+Z方向ではない場合の補正角度")]
    [SerializeField]
    private float forwardAngleOffset = 0f;

    [Tooltip("LEDマトリクスの左右が逆の場合に有効にする")]
    [SerializeField]
    private bool mirrorHorizontal = false;

    // radarData[行, 列]
    // 行：0が上、15が下
    // 列：0が左、15が右
    private readonly int[,] radarData =
        new int[MatrixSize, MatrixSize];

    private bool tagErrorDisplayed = false;

    private void Start()
    {
        UpdateRadarData();
    }

    /// <summary>
    /// 16×16のレーダー配列を作り直す
    /// </summary>
    private void UpdateRadarData()
    {
        ClearRadar();
        DrawAirplane();

        if (airplane == null)
        {
            Debug.LogWarning(
                "radarのAirplaneに飛行機のTransformを設定してください。"
            );

            return;
        }

        // 飛行機の前方向を取得する
        Vector3 forward =
            Quaternion.AngleAxis(
                forwardAngleOffset,
                Vector3.up
            ) * airplane.forward;

        // x,z平面だけで計算する
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
                    $"Tag「{obstacleTag}」が登録されていません。"
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

            // 飛行機から障害物へのベクトル
            Vector3 difference =
                obstacle.transform.position - airplane.position;

            // 高さはレーダーに使用しない
            difference.y = 0f;

            float distance = difference.magnitude;

            if (distance < 0.01f || distance > radarRange)
            {
                continue;
            }

            // 飛行機基準での左右方向と前後方向
            float localRight =
                Vector3.Dot(difference, right);

            float localForward =
                Vector3.Dot(difference, forward);

            /*
             * 後方の障害物を除外する。
             *
             * localForwardが0以上なら、
             * 正面から左右90度以内にある。
             */
            if (localForward < 0f)
            {
                continue;
            }

            /*
             * 正面を0度として障害物の角度を求める。
             *
             * 左端：-90度
             * 正面：0度
             * 右端：90度
             */
            float angle = Mathf.Atan2(
                localRight,
                localForward
            ) * Mathf.Rad2Deg;

            // -90～90度を0～15列に変換する
            float horizontalRatio =
                Mathf.InverseLerp(-90f, 90f, angle);

            int column = Mathf.RoundToInt(
                horizontalRatio * (MatrixSize - 1)
            );

            if (mirrorHorizontal)
            {
                column = MatrixSize - 1 - column;
            }

            /*
             * 距離を0～13行に変換する。
             *
             * 遠い障害物：0行目
             * 近い障害物：13行目
             */
            float distanceRatio =
                Mathf.Clamp01(distance / radarRange);

            int row = Mathf.RoundToInt(
                (1f - distanceRatio) * LastObstacleRow
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
    }

    /// <summary>
    /// 配列をすべて0にする
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
    /// 最新のレーダーデータを
    /// 0,0,1,0,... のCSV形式で返す
    /// </summary>
    public string GetRadarData()
    {
        UpdateRadarData();

        int[] result = new int[MatrixSize * MatrixSize];

        for (int row = 0; row < MatrixSize; row++)
        {
            for (int column = 0; column < MatrixSize; column++)
            {
                int index = row * MatrixSize + column;

                result[index] = radarData[row, column];
            }
        }

        return string.Join(",", result);
    }
}
using UnityEngine;

public class Airplane : MonoBehaviour
{
    private float rotation_x;
    private float rotation_y;
    private float rotation_z;

    private float move_x;
    private float move_y;
    private float move_z;

    private float move_x_adjustment = 0.3f;
    private float move_y_adjustment = 0.3f;
    private float move_z_adjustment = 0f;

    private float rotation_x_move_threshold = 1f;
    private float rotation_y_move_threshold = 1f;
    private float rotation_z_move_threshold = 1f;

    [Header("HP")]
    [SerializeField]
    private AirplaneHpSlider airplaneHpSlider;

    [Header("Radar Feedback")]
    [SerializeField, Tooltip("レーダー処理を行うradarコンポーネント")]
    private radar radarScript;

    private void Start()
    {
        rotation_x = 0f;
        rotation_y = 0f;
        rotation_z = 0f;

        move_x = 0f;
        move_y = 0f;
        move_z = 0f;

        if (radarScript == null)
        {
            Debug.LogWarning(
                "AirplaneのRadar Scriptが設定されていません。",
                this
            );
        }
    }

    private void Update()
    {
        SetMyRotation();

        /*
         * X軸回転によって、
         * 飛行機をY軸方向へ移動させる。
         */
        if (Mathf.Abs(rotation_x) >
            rotation_x_move_threshold)
        {
            move_y =
                rotation_x *
                move_x_adjustment *
                -1f;
        }
        else
        {
            move_y = 0f;
        }

        /*
         * Y軸回転によって、
         * 飛行機をX軸方向へ移動させる。
         */
        if (Mathf.Abs(rotation_y) >
            rotation_y_move_threshold)
        {
            move_x =
                rotation_y *
                move_y_adjustment;
        }
        else
        {
            move_x = 0f;
        }

        /*
         * Z軸回転による移動を使用する場合は、
         * 次の処理を有効にする。
         */
        /*
        if (Mathf.Abs(rotation_z) >
            rotation_z_move_threshold)
        {
            move_z =
                rotation_z *
                move_z_adjustment;
        }
        else
        {
            move_z = 0f;
        }
        */

        MoveMyPosition();
    }

    /// <summary>
    /// 飛行機がTriggerへ侵入したときの処理
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(
            "Triggerに入りました: " +
            other.gameObject.name
        );

        /*
         * 接触したCollider自身からStoneを取得する。
         */
        Stone stone =
            other.GetComponent<Stone>();

        /*
         * ColliderがStone本体の子オブジェクトに
         * 付いている場合は、親から取得する。
         */
        if (stone == null)
        {
            stone =
                other.GetComponentInParent<Stone>();
        }

        /*
         * Stoneではないオブジェクトとの接触なら、
         * これ以降の処理は行わない。
         */
        if (stone == null)
        {
            return;
        }

        /*
         * 岩との衝突をレーダーへ通知する。
         *
         * radar.cs側では、この通知を受けた後、
         * 一定時間、障害物接近値を255にする。
         */
        if (radarScript != null)
        {
            radarScript.NotifyObstacleCollision();
        }
        else
        {
            Debug.LogWarning(
                "Radar Scriptが設定されていないため、" +
                "衝突時の最大振動を通知できません。",
                this
            );
        }

        /*
         * 岩に設定されているダメージ量を取得する。
         */
        int damage =
            stone.GetDamage();

        /*
         * HPを減少させる。
         */
        if (airplaneHpSlider != null)
        {
            airplaneHpSlider.Damage(damage);
        }
        else
        {
            Debug.LogWarning(
                "Airplane HP Sliderが設定されていません。",
                this
            );
        }

        Debug.Log(
            $"岩に衝突しました。ダメージ: {damage}"
        );

        /*
         * 衝突した岩を削除する。
         */
        Destroy(stone.gameObject);
    }

    /// <summary>
    /// 現在の飛行機の回転角度を取得する
    /// </summary>
    private void SetMyRotation()
    {
        Vector3 rotation =
            transform.rotation.eulerAngles;

        rotation_x =
            Mathf.DeltaAngle(
                0f,
                rotation.x
            );

        rotation_y =
            Mathf.DeltaAngle(
                0f,
                rotation.y
            );

        rotation_z =
            Mathf.DeltaAngle(
                0f,
                rotation.z
            );
    }

    /// <summary>
    /// 計算した移動量を飛行機へ反映する
    /// </summary>
    private void MoveMyPosition()
    {
        Vector3 move =
            new Vector3(
                move_x,
                move_y,
                move_z
            );

        transform.position +=
            move * Time.deltaTime;
    }
}
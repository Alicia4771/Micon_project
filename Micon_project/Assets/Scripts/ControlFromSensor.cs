using UnityEngine;

[DisallowMultipleComponent]
public class ControlFromSensor : MonoBehaviour
{
    [Header("Control Mode")]
    [SerializeField, Tooltip("現在は3のみ使用します")]
    private int control_mode = 3;       // 1: ジャイロセンサのみ
                                        // 2: ジャイロ＋加速度センサの融合（センサフュージョン）
                                        // 3: ９軸ジャイロセンサ

    [Header("Pitch Settings")]
    [SerializeField, Tooltip("飛行機が前後に傾く最大角度")]
    private float max_pitch_angle = 30f;

    [SerializeField, Tooltip("センサのPitchに掛ける倍率")]
    private float pitch_sensitivity = 1f;

    [SerializeField, Tooltip("前後の傾きを反転する")]
    private bool invert_pitch = false;

    [Header("Roll Settings")]
    [SerializeField, Tooltip("飛行機が左右に傾く最大角度")]
    private float max_roll_angle = 45f;

    [SerializeField, Tooltip("センサのRollに掛ける倍率")]
    private float roll_sensitivity = 1f;

    [SerializeField, Tooltip("左右の傾きを反転する")]
    private bool invert_roll = false;

    [Header("Smoothing")]
    [SerializeField, Tooltip("値が大きいほど素早くセンサ角度へ追従する")]
    private float rotation_smooth_speed = 8f;

    [Header("Calibration")]
    [SerializeField, Tooltip("このキーを押したときのセンサ角度を水平として設定する")]
    private KeyCode calibration_key = KeyCode.C;

    private Rigidbody airplane_rigidbody;

    // 飛行機の初期姿勢
    private Quaternion initial_airplane_rotation;

    // センサを水平とみなす基準角度
    private float base_roll;
    private float base_pitch;

    private bool is_calibrated;

    // Rigidbodyへ適用する目標姿勢
    private Quaternion target_rotation;

    private void Awake()
    {
        airplane_rigidbody = GetComponent<Rigidbody>();

        if (airplane_rigidbody == null)
        {
            Debug.LogError(
                "ControlFromSensorにはRigidbodyが必要です。",
                this
            );
        }
    }

    private void Start()
    {
        initial_airplane_rotation = transform.rotation;
        target_rotation = initial_airplane_rotation;

        if (airplane_rigidbody != null)
        {
            airplane_rigidbody.interpolation =
                RigidbodyInterpolation.Interpolate;
        }
    }

    private void Update()
    {
        // 今はモード3だけを実装する
        if (control_mode != 3)
        {
            return;
        }

        // センサ値をまだ受け取っていない
        if (!DataManager.HasReceivedSensorData())
        {
            return;
        }

        // Cキーで現在の持ち方を水平として再設定する
        if (Input.GetKeyDown(calibration_key))
        {
            CalibrateSensor();
        }

        Vector3 euler_sensor_value =
            DataManager.GetEulerSensorValue();

        // 最初に正常な値を取得したときに自動調整する
        if (!is_calibrated)
        {
            CalibrateSensor();
            return;
        }

        /*
         * DataManager上では、次の割り当てを想定しています。
         *
         * X = Heading / Yaw
         * Y = Roll
         * Z = Pitch
         */
        float sensor_roll = Mathf.DeltaAngle(
            base_roll,
            euler_sensor_value.y
        );

        float sensor_pitch = Mathf.DeltaAngle(
            base_pitch,
            euler_sensor_value.z
        );

        sensor_roll *= roll_sensitivity;
        sensor_pitch *= pitch_sensitivity;

        if (invert_roll)
        {
            sensor_roll *= -1f;
        }

        if (invert_pitch)
        {
            sensor_pitch *= -1f;
        }

        // 飛行機が傾きすぎないように制限する
        float airplane_roll = Mathf.Clamp(
            sensor_roll,
            -max_roll_angle,
            max_roll_angle
        );

        float airplane_pitch = Mathf.Clamp(
            sensor_pitch,
            -max_pitch_angle,
            max_pitch_angle
        );

        /*
         * 初期姿勢を基準にして、
         * X軸をPitch、Z軸をRollとして傾ける。
         *
         * Yawは今回は操作しない。
         */
        Quaternion sensor_rotation = Quaternion.Euler(
            airplane_pitch,
            0f,
            airplane_roll
        );

        target_rotation =
            initial_airplane_rotation * sensor_rotation;
    }

    private void FixedUpdate()
    {
        if (airplane_rigidbody == null)
        {
            return;
        }

        float smooth_ratio =
            1f - Mathf.Exp(
                -rotation_smooth_speed *
                Time.fixedDeltaTime
            );

        Quaternion next_rotation = Quaternion.Slerp(
            airplane_rigidbody.rotation,
            target_rotation,
            smooth_ratio
        );

        airplane_rigidbody.MoveRotation(next_rotation);
    }

    /// <summary>
    /// 現在のセンサ角度を水平状態として記録する
    /// </summary>
    public void CalibrateSensor()
    {
        if (!DataManager.HasReceivedSensorData())
        {
            return;
        }

        Vector3 euler_sensor_value =
            DataManager.GetEulerSensorValue();

        base_roll = euler_sensor_value.y;
        base_pitch = euler_sensor_value.z;

        is_calibrated = true;

        Debug.Log(
            $"センサを調整しました。Roll={base_roll}, Pitch={base_pitch}"
        );
    }
}
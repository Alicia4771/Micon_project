using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ControlFromSensor : MonoBehaviour
{
    private enum SensorAxis
    {
        X,
        Y,
        Z
    }

    [Header("Control Mode")]
    [SerializeField, Tooltip("現在は3のみ使用します")]
    private int control_mode = 3;

    /*
     * 1：ジャイロセンサのみ
     * 2：ジャイロ＋加速度センサ
     * 3：オイラー角を目標角度、
     *    ジャイロを回転速度として使用
     */


    //==================================================
    // Pitch
    //==================================================

    [Header("Pitch Settings")]

    [SerializeField, Tooltip("飛行機が前後に傾く最大角度")]
    private float max_pitch_angle = 30f;

    [SerializeField, Tooltip("センサのPitch角度に掛ける倍率")]
    private float pitch_sensitivity = 1f;

    [SerializeField, Tooltip("Pitch方向を反転する")]
    private bool invert_pitch = false;


    //==================================================
    // Yaw
    //==================================================

    [Header("Yaw Settings")]

    [SerializeField, Tooltip("飛行機が左右を向く最大角度")]
    private float max_yaw_angle = 180f;

    [SerializeField, Tooltip("センサのYaw角度に掛ける倍率")]
    private float yaw_sensitivity = 1f;

    [SerializeField, Tooltip("Yaw方向を反転する")]
    private bool invert_yaw = false;


    //==================================================
    // Roll
    //==================================================

    [Header("Roll Settings")]

    [SerializeField, Tooltip("飛行機が左右に傾く最大角度")]
    private float max_roll_angle = 45f;

    [SerializeField, Tooltip("センサのRoll角度に掛ける倍率")]
    private float roll_sensitivity = 1f;

    [SerializeField, Tooltip("Roll方向を反転する")]
    private bool invert_roll = false;


    //==================================================
    // Gyro
    //==================================================

    [Header("Gyro Axis Settings")]

    [SerializeField, Tooltip("Pitchの角速度として使用するジャイロ軸")]
    private SensorAxis pitch_gyro_axis = SensorAxis.X;

    [SerializeField, Tooltip("Yawの角速度として使用するジャイロ軸")]
    private SensorAxis yaw_gyro_axis = SensorAxis.Z;

    [SerializeField, Tooltip("Rollの角速度として使用するジャイロ軸")]
    private SensorAxis roll_gyro_axis = SensorAxis.Y;

    [SerializeField,
     Tooltip("受信しているジャイロ値がrad/sの場合は有効にする")]
    private bool gyro_values_are_radians_per_second = true;

    [SerializeField, Min(0f),
     Tooltip("ジャイロ角速度に掛ける倍率")]
    private float gyro_speed_scale = 1f;

    [SerializeField, Min(0f),
     Tooltip("ジャイロ値がほぼ0でも目標へ追従する最低回転速度")]
    private float minimum_angular_speed = 10f;

    [SerializeField, Min(0.01f),
     Tooltip("飛行機が回転できる最大角速度（度/秒）")]
    private float maximum_angular_speed = 360f;


    //==================================================
    // Smoothing
    //==================================================

    [Header("Rotation Smoothing")]

    [SerializeField, Min(0.001f),
     Tooltip("小さいほど素早く目標角度へ追従する")]
    private float rotation_smooth_time = 0.03f;


    //==================================================
    // Calibration
    //==================================================

    [Header("Calibration")]

    [SerializeField,
     Tooltip("このキーを押したときのセンサ姿勢を基準にする")]
    private Key calibration_key = Key.C;


    //==================================================
    // Rigidbody
    //==================================================

    private Rigidbody airplane_rigidbody;

    /*
     * キャリブレーションした時点の
     * Unityオブジェクトの姿勢。
     */
    private Quaternion base_airplane_rotation;


    //==================================================
    // Sensor base angles
    //==================================================

    /*
     * DataManagerのオイラー角の割り当て：
     *
     * X：Heading / Yaw
     * Y：Roll
     * Z：Pitch
     */
    private float base_pitch;
    private float base_yaw;
    private float base_roll;

    private bool is_calibrated = false;


    //==================================================
    // Target angles
    //==================================================

    private float target_pitch;
    private float target_yaw;
    private float target_roll;


    //==================================================
    // Current angles
    //==================================================

    private float current_pitch;
    private float current_yaw;
    private float current_roll;


    //==================================================
    // SmoothDamp internal velocities
    //==================================================

    /*
     * SmoothDampAngleが内部で使用する値。
     * BNO055の角速度とは別の値。
     */
    private float pitch_smooth_velocity;
    private float yaw_smooth_velocity;
    private float roll_smooth_velocity;


    //==================================================
    // Sensor angular speeds
    //==================================================

    private float sensor_pitch_angular_speed;
    private float sensor_yaw_angular_speed;
    private float sensor_roll_angular_speed;


    //==================================================
    // Awake
    //==================================================

    private void Awake()
    {
        airplane_rigidbody =
            GetComponent<Rigidbody>();

        if (airplane_rigidbody == null)
        {
            Debug.LogError(
                "ControlFromSensorにはRigidbodyが必要です。",
                this
            );

            enabled = false;
            return;
        }

        max_pitch_angle =
            Mathf.Abs(max_pitch_angle);

        max_yaw_angle =
            Mathf.Clamp(
                Mathf.Abs(max_yaw_angle),
                0f,
                180f
            );

        max_roll_angle =
            Mathf.Abs(max_roll_angle);

        maximum_angular_speed =
            Mathf.Max(
                0.01f,
                maximum_angular_speed
            );

        minimum_angular_speed =
            Mathf.Clamp(
                minimum_angular_speed,
                0f,
                maximum_angular_speed
            );

        rotation_smooth_time =
            Mathf.Max(
                0.001f,
                rotation_smooth_time
            );
    }


    //==================================================
    // Start
    //==================================================

    private void Start()
    {
        base_airplane_rotation =
            transform.rotation;

        target_pitch = 0f;
        target_yaw = 0f;
        target_roll = 0f;

        current_pitch = 0f;
        current_yaw = 0f;
        current_roll = 0f;

        pitch_smooth_velocity = 0f;
        yaw_smooth_velocity = 0f;
        roll_smooth_velocity = 0f;

        sensor_pitch_angular_speed = 0f;
        sensor_yaw_angular_speed = 0f;
        sensor_roll_angular_speed = 0f;

        if (airplane_rigidbody != null)
        {
            airplane_rigidbody.interpolation =
                RigidbodyInterpolation.Interpolate;
        }
    }


    //==================================================
    // Update
    //==================================================

    private void Update()
    {
        if (control_mode != 3)
        {
            return;
        }

        if (!DataManager.HasReceivedSensorData())
        {
            return;
        }

        /*
         * Cキーを押した時点のセンサ姿勢を
         * 新しい基準姿勢にする。
         */
        if (
            Keyboard.current != null &&
            Keyboard.current[calibration_key]
                .wasPressedThisFrame
        )
        {
            CalibrateSensor();
            return;
        }

        /*
         * 最初にセンサデータを受信した時点で
         * 自動キャリブレーションする。
         */
        if (!is_calibrated)
        {
            CalibrateSensor();
            return;
        }

        UpdateTargetAngles();
        UpdateSensorAngularSpeeds();
    }


    //==================================================
    // FixedUpdate
    //==================================================

    private void FixedUpdate()
    {
        if (control_mode != 3)
        {
            return;
        }

        if (!is_calibrated)
        {
            return;
        }

        if (airplane_rigidbody == null)
        {
            return;
        }

        /*
         * 各軸について、センサ角速度をもとに
         * 追従時の最大角速度を求める。
         */
        float pitch_max_speed =
            CalculateFollowSpeed(
                sensor_pitch_angular_speed,
                current_pitch,
                target_pitch
            );

        float yaw_max_speed =
            CalculateFollowSpeed(
                sensor_yaw_angular_speed,
                current_yaw,
                target_yaw
            );

        float roll_max_speed =
            CalculateFollowSpeed(
                sensor_roll_angular_speed,
                current_roll,
                target_roll
            );


        /*
         * X軸：Pitch
         */
        current_pitch =
            Mathf.SmoothDampAngle(
                current_pitch,
                target_pitch,
                ref pitch_smooth_velocity,
                rotation_smooth_time,
                pitch_max_speed,
                Time.fixedDeltaTime
            );


        /*
         * Y軸：Yaw
         */
        current_yaw =
            Mathf.SmoothDampAngle(
                current_yaw,
                target_yaw,
                ref yaw_smooth_velocity,
                rotation_smooth_time,
                yaw_max_speed,
                Time.fixedDeltaTime
            );


        /*
         * Z軸：Roll
         */
        current_roll =
            Mathf.SmoothDampAngle(
                current_roll,
                target_roll,
                ref roll_smooth_velocity,
                rotation_smooth_time,
                roll_max_speed,
                Time.fixedDeltaTime
            );


        /*
         * キャリブレーション時の姿勢に、
         * Pitch・Yaw・Rollを加える。
         */
        Quaternion relative_rotation =
            Quaternion.Euler(
                current_pitch,
                current_yaw,
                current_roll
            );

        Quaternion next_rotation =
            base_airplane_rotation *
            relative_rotation;

        airplane_rigidbody.MoveRotation(
            next_rotation
        );
    }


    //==================================================
    // Target angles
    //==================================================

    /// <summary>
    /// センサのオイラー角から、
    /// Pitch・Yaw・Rollの目標角度を求める
    /// </summary>
    private void UpdateTargetAngles()
    {
        Vector3 euler_sensor_value =
            DataManager.GetEulerSensorValue();

        /*
         * DataManagerの格納順：
         *
         * X：Heading / Yaw
         * Y：Roll
         * Z：Pitch
         */
        float sensor_yaw =
            Mathf.DeltaAngle(
                base_yaw,
                euler_sensor_value.x
            );

        float sensor_roll =
            Mathf.DeltaAngle(
                base_roll,
                euler_sensor_value.y
            );

        float sensor_pitch =
            Mathf.DeltaAngle(
                base_pitch,
                euler_sensor_value.z
            );


        sensor_pitch *=
            pitch_sensitivity;

        sensor_yaw *=
            yaw_sensitivity;

        sensor_roll *=
            roll_sensitivity;


        if (invert_pitch)
        {
            sensor_pitch *= -1f;
        }

        if (invert_yaw)
        {
            sensor_yaw *= -1f;
        }

        if (invert_roll)
        {
            sensor_roll *= -1f;
        }


        target_pitch =
            Mathf.Clamp(
                sensor_pitch,
                -max_pitch_angle,
                max_pitch_angle
            );

        target_yaw =
            Mathf.Clamp(
                sensor_yaw,
                -max_yaw_angle,
                max_yaw_angle
            );

        target_roll =
            Mathf.Clamp(
                sensor_roll,
                -max_roll_angle,
                max_roll_angle
            );
    }


    //==================================================
    // Sensor angular speeds
    //==================================================

    /// <summary>
    /// ジャイロセンサから各軸の角速度を取得する
    /// </summary>
    private void UpdateSensorAngularSpeeds()
    {
        Vector3 gyro_sensor_value =
            DataManager.GetGyroSensorValue();

        float pitch_gyro_value =
            GetAxisValue(
                gyro_sensor_value,
                pitch_gyro_axis
            );

        float yaw_gyro_value =
            GetAxisValue(
                gyro_sensor_value,
                yaw_gyro_axis
            );

        float roll_gyro_value =
            GetAxisValue(
                gyro_sensor_value,
                roll_gyro_axis
            );


        /*
         * 回転方向は目標角度との差から決まるため、
         * ここでは角速度の絶対値を使用する。
         */
        pitch_gyro_value =
            Mathf.Abs(pitch_gyro_value);

        yaw_gyro_value =
            Mathf.Abs(yaw_gyro_value);

        roll_gyro_value =
            Mathf.Abs(roll_gyro_value);


        /*
         * 受信値がrad/sの場合は、
         * degree/sへ変換する。
         */
        if (gyro_values_are_radians_per_second)
        {
            pitch_gyro_value *=
                Mathf.Rad2Deg;

            yaw_gyro_value *=
                Mathf.Rad2Deg;

            roll_gyro_value *=
                Mathf.Rad2Deg;
        }


        sensor_pitch_angular_speed =
            Mathf.Clamp(
                pitch_gyro_value *
                gyro_speed_scale,
                0f,
                maximum_angular_speed
            );

        sensor_yaw_angular_speed =
            Mathf.Clamp(
                yaw_gyro_value *
                gyro_speed_scale,
                0f,
                maximum_angular_speed
            );

        sensor_roll_angular_speed =
            Mathf.Clamp(
                roll_gyro_value *
                gyro_speed_scale,
                0f,
                maximum_angular_speed
            );
    }


    //==================================================
    // Follow speed
    //==================================================

    /// <summary>
    /// 目標角度へ追従するときの最大角速度を求める
    /// </summary>
    private float CalculateFollowSpeed(
        float sensor_angular_speed,
        float current_angle,
        float target_angle
    )
    {
        float angle_error =
            Mathf.Abs(
                Mathf.DeltaAngle(
                    current_angle,
                    target_angle
                )
            );

        /*
         * ほぼ目標角度へ到達している場合。
         */
        if (angle_error < 0.05f)
        {
            return maximum_angular_speed;
        }

        return Mathf.Clamp(
            Mathf.Max(
                sensor_angular_speed,
                minimum_angular_speed
            ),
            minimum_angular_speed,
            maximum_angular_speed
        );
    }


    //==================================================
    // Axis selection
    //==================================================

    /// <summary>
    /// Vector3から指定した軸の値を取得する
    /// </summary>
    private float GetAxisValue(
        Vector3 value,
        SensorAxis axis
    )
    {
        switch (axis)
        {
            case SensorAxis.X:
                return value.x;

            case SensorAxis.Y:
                return value.y;

            case SensorAxis.Z:
                return value.z;

            default:
                return 0f;
        }
    }


    //==================================================
    // Calibration
    //==================================================

    /// <summary>
    /// 現在のセンサ姿勢と飛行機の姿勢を基準にする
    /// </summary>
    public void CalibrateSensor()
    {
        if (!DataManager.HasReceivedSensorData())
        {
            Debug.LogWarning(
                "センサ値をまだ受信していないため、調整できません。",
                this
            );

            return;
        }

        Vector3 euler_sensor_value =
            DataManager.GetEulerSensorValue();

        base_yaw =
            euler_sensor_value.x;

        base_roll =
            euler_sensor_value.y;

        base_pitch =
            euler_sensor_value.z;


        /*
         * キャリブレーションした時点の
         * Unityオブジェクトの姿勢を基準にする。
         */
        if (airplane_rigidbody != null)
        {
            base_airplane_rotation =
                airplane_rigidbody.rotation;
        }
        else
        {
            base_airplane_rotation =
                transform.rotation;
        }


        target_pitch = 0f;
        target_yaw = 0f;
        target_roll = 0f;

        current_pitch = 0f;
        current_yaw = 0f;
        current_roll = 0f;

        pitch_smooth_velocity = 0f;
        yaw_smooth_velocity = 0f;
        roll_smooth_velocity = 0f;

        sensor_pitch_angular_speed = 0f;
        sensor_yaw_angular_speed = 0f;
        sensor_roll_angular_speed = 0f;

        is_calibrated = true;

        Debug.Log(
            "センサを調整しました。" +
            $" Yaw={base_yaw}," +
            $" Roll={base_roll}," +
            $" Pitch={base_pitch}",
            this
        );
    }


    //==================================================
    // Control mode
    //==================================================

    /// <summary>
    /// 制御モードを設定する
    /// </summary>
    public bool SetControlMode(int mode)
    {
        if (mode < 1 || mode > 3)
        {
            Debug.LogWarning(
                "ControlFromSensor: " +
                "無効な制御モードです。" +
                "1～3の範囲で指定してください。",
                this
            );

            return false;
        }

        control_mode = mode;

        Debug.Log(
            "ControlFromSensor: " +
            $"制御モードを{control_mode}に設定しました。",
            this
        );

        return true;
    }
}
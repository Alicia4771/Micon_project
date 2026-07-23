using UnityEngine;

public class tmpSceneManager : MonoBehaviour
{
    [Header("Sensor Settings")]
    [SerializeField]
    private SensorReceiver sensorReceiver;
    
    void Start()
    {
        
    }

    void Update()
    {
        DataManager.SetSensorValue(sensorReceiver.GetSensorData());

        Debug.Log("------------------------------");
        // Debug.Log("acceleration : " + DataManager.GetAccelerationSensorValue());
        // Debug.Log("gyro         : " + DataManager.GetGyroSensorValue());
        Debug.Log("euler        : " + DataManager.GetEulerSensorValue());

    }
}

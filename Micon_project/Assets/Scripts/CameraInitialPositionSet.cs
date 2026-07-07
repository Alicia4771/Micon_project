using UnityEngine;
using System.Collections.Generic;

public class CameraInitialPositionSet : MonoBehaviour
{
    private Vector3 camera_initial_position;
    private Vector3 camera_initial_rotation_euler;

    private void Awake()
    {
        camera_initial_position = this.transform.position;
        camera_initial_rotation_euler = this.transform.rotation.eulerAngles;
    }
    
    private void Start()
    {
        this.transform.position = camera_initial_position;
        this.transform.rotation = Quaternion.Euler(camera_initial_rotation_euler);
    }
}

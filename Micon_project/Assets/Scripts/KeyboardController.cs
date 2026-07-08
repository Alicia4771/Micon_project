using UnityEngine;
using UnityEngine.InputSystem;

public class KeyboardController : MonoBehaviour
{
    [SerializeField] private GameObject airplane;

    private float rotate_speed_x = 20f;
    private float rotate_speed_y = 20f;
    private float rotate_speed_z = 20f;


    void Update()
    {
        if (airplane == null) return;
        if (Keyboard.current == null) return;

        Vector3 rotation = Vector3.zero;

        // Y軸回転
        if (Keyboard.current.dKey.isPressed)
        {
            rotation.y += 1f * rotate_speed_y; // Y軸+
            Debug.Log("dKey is pressed");
        }

        if (Keyboard.current.aKey.isPressed)
        {
            rotation.y -= 1f * rotate_speed_y; // Y軸-
        }

        // X軸回転
        if (Keyboard.current.wKey.isPressed)
        {
            rotation.x -= 1f * rotate_speed_x; // X軸-
        }

        if (Keyboard.current.sKey.isPressed)
        {
            rotation.x += 1f * rotate_speed_x; // X軸+
        }

        // Z軸回転
        if (Keyboard.current.qKey.isPressed)
        {
            rotation.z += 1f * rotate_speed_z; // Z軸+
        }

        if (Keyboard.current.eKey.isPressed)
        {
            rotation.z -= 1f * rotate_speed_z; // Z軸-
        }

        airplane.transform.Rotate(rotation * Time.deltaTime, Space.Self);
    }
}
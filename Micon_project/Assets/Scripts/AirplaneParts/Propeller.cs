using UnityEngine;

public class Propeller : MonoBehaviour
{
    private float rotation_speed = 1000f;

    private void Update()
    {
        transform.Rotate(0f, 0f, rotation_speed * Time.deltaTime);
    }

    public bool SetRotationSpeed(float speed)
    {
        if (speed < 0f) return false;
        rotation_speed = speed;
        return true;
    }
}

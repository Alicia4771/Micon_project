using UnityEngine;
using System.Collections.Generic;

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
    private float move_z_adjustment = 0;

    private float rotation_x_move_threshold = 1f;
    private float rotation_y_move_threshold = 1f;
    private float rotation_z_move_threshold = 1f;

    void Start()
    {
        rotation_x = 0f;
        rotation_y = 0f;
        rotation_z = 0f;
    }

    void Update()
    {
        SetMyRotation();

        if (Mathf.Abs(rotation_x) > rotation_x_move_threshold)
        {
            move_y = rotation_x * move_x_adjustment * (-1);
        }
        else
        {
            move_y = 0f;
        }

        if (Mathf.Abs(rotation_y) > rotation_y_move_threshold)
        {
            move_x = rotation_y * move_y_adjustment;
        }
        else
        {
            move_x = 0f;
        }

        // if (Mathf.Abs(rotation_z) > rotation_z_move_threshold)
        // {
        //     move_z = rotation_z * move_z_adjustment;
        // }
        // else
        // {
        //     move_z = 0f;
        // }

        MoveMyPosition();
    }

    private void SetMyRotation()
    {
        Vector3 rotation = this.transform.rotation.eulerAngles;
        rotation_x = Mathf.DeltaAngle(0f, rotation.x);
        rotation_y = Mathf.DeltaAngle(0f, rotation.y);
        rotation_z = Mathf.DeltaAngle(0f, rotation.z);
    }

    private void MoveMyPosition()
    {
        Vector3 move = new Vector3(move_x, move_y, move_z);

        transform.position += move * Time.deltaTime;
    }
}

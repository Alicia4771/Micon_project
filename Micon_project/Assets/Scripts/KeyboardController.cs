using UnityEngine;
using UnityEngine.InputSystem;

public class KeyboardController : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current.qKey.isPressed)
        {
            Debug.Log("Qキーが押されています");
        }

        if (Keyboard.current.eKey.isPressed)
        {
            Debug.Log("Eキーが押されています");
        }

        if (Keyboard.current.wKey.isPressed)
        {
            Debug.Log("Wキーが押されています");
        }

        if (Keyboard.current.aKey.isPressed)
        {
            Debug.Log("Aキーが押されています");
        }

        if (Keyboard.current.sKey.isPressed)
        {
            Debug.Log("Sキーが押されています");
        }

        if (Keyboard.current.dKey.isPressed)
        {
            Debug.Log("Dキーが押されています");
        }
    }
}

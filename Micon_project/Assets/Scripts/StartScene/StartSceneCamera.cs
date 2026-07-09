using UnityEngine;

public class StartSceneCamera : MonoBehaviour
{
    private int airplaneCount = 3;
    private int currentIndex = 0; // 最初に見る飛行機 0:左, 1:中央, 2:右
    private float cameraMoveX = 8f;

    private Vector3 leftMostCameraPosition;

    private void Start()
    {
        // 現在のカメラ位置を currentIndex 番目の位置として考え、
        // そこから左端のカメラ位置を逆算する
        leftMostCameraPosition = transform.position - new Vector3(cameraMoveX * currentIndex, 0f, 0f);

        SetCameraIndex(currentIndex);
    }

    public void MoveLeft()
    {
        SetCameraIndex(currentIndex - 1);
    }

    public void MoveRight()
    {
        SetCameraIndex(currentIndex + 1);
    }

    public void SetCameraIndex(int index)
    {
        currentIndex = Mathf.Clamp(index, 0, airplaneCount - 1);

        Vector3 position = leftMostCameraPosition;
        position.x += cameraMoveX * currentIndex;

        transform.position = position;
    }

    public int GetCurrentIndex()
    {
        return currentIndex;
    }

    public int GetAirplaneCount()
    {
        return airplaneCount;
    }

    public bool CanMoveLeft()
    {
        return currentIndex > 0;
    }

    public bool CanMoveRight()
    {
        return currentIndex < airplaneCount - 1;
    }
}
using UnityEngine;

public class AirPlaneChoosePanelManager : MonoBehaviour
{
    [SerializeField] private StartSceneCamera startSceneCamera;

    [SerializeField] private GameObject leftButton;
    [SerializeField] private GameObject rightButton;

    private void Start()
    {
        UpdateArrowButtons();
    }

    public void OnClickLeftButton()
    {
        if (startSceneCamera == null) return;

        startSceneCamera.MoveLeft();
        UpdateArrowButtons();
    }

    public void OnClickRightButton()
    {
        if (startSceneCamera == null) return;

        startSceneCamera.MoveRight();
        UpdateArrowButtons();
    }

    private void UpdateArrowButtons()
    {
        if (startSceneCamera == null) return;

        if (leftButton != null)
        {
            leftButton.SetActive(startSceneCamera.CanMoveLeft());
        }

        if (rightButton != null)
        {
            rightButton.SetActive(startSceneCamera.CanMoveRight());
        }
    }
}
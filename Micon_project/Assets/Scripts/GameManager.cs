using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject stone;
    [SerializeField] private AirplaneHpSlider airplaneHpSlider;
    
    
    private float time_count = 0;
    private float time_count_before = 0;
    
    void Start()
    {
        time_count = 0;
        time_count_before = 0;
    }

    void Update()
    {
        time_count += Time.deltaTime;

        if (time_count >= 1f && time_count_before < 1f)
        {
            airplaneHpSlider.Damage(50);
            time_count = 0;
        }

        time_count_before = time_count;
    }

    private bool SpawnStone()
    {
        if (stone == null) return false;

        GameObject new_stone = Instantiate(stone, new Vector3(0f, 0f, 10f), Quaternion.identity);
        return true;
    }
}

using UnityEngine;
using UnityEngine.UI;

public class AirplaneHpSlider : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private RawImage airplaneRawImage;

    [Header("Fill Colors")]
    [SerializeField] private Color normalColor = Color.green;
    [SerializeField] private Color cautionColor = Color.yellow;
    [SerializeField] private Color dangerColor = Color.red;

    [Header("Airplane Images")]
    [SerializeField] private Texture normalTexture;
    [SerializeField] private Texture cautionTexture;
    [SerializeField] private Texture dangerTexture;

    [Header("Damage Effects")]
    [SerializeField] private GameObject smokeEffect;
    [SerializeField] private GameObject fireEffect;

    private int hp_max = 1000;
    private int hp_min = 0;
    private int hp_now = 1000;

    private int hp_danger_threshold = 200;
    private int hp_caution_threshold = 500;


    [SerializeField] private GameManager gameManager;

    private void Start()
    {
        hp_now = hp_max;

        if (hpSlider == null)
        {
            hpSlider = GetComponent<Slider>();
        }

        hpSlider.minValue = hp_min;
        hpSlider.maxValue = hp_max;

        // 最初はエフェクトを非表示
        if (smokeEffect != null)
        {
            smokeEffect.SetActive(false);
        }

        if (fireEffect != null)
        {
            fireEffect.SetActive(false);
        }

        UpdateHpUI();
    }

    private void UpdateHpUI()
    {
        hpSlider.value = hp_now;

        float hpRate = (float)hp_now / hp_max;

        if (hpRate <= (float)hp_danger_threshold / hp_max)
        {
            // 危険状態
            fillImage.color = dangerColor;
            airplaneRawImage.texture = dangerTexture;

            if (smokeEffect != null)
            {
                smokeEffect.SetActive(true);
            }

            if (fireEffect != null)
            {
                fireEffect.SetActive(true);
            }
        }
        else if (hpRate <= (float)hp_caution_threshold / hp_max)
        {
            // 注意状態
            fillImage.color = cautionColor;
            airplaneRawImage.texture = cautionTexture;

            if (smokeEffect != null)
            {
                smokeEffect.SetActive(true);
            }

            if (fireEffect != null)
            {
                fireEffect.SetActive(false);
            }
        }
        else
        {
            // 通常状態
            fillImage.color = normalColor;
            airplaneRawImage.texture = normalTexture;

            if (smokeEffect != null)
            {
                smokeEffect.SetActive(false);
            }

            if (fireEffect != null)
            {
                fireEffect.SetActive(false);
            }
        }
    }

    public void Damage(int damage)
    {
        hp_now -= damage;

        if (hp_now < hp_min)
        {
            hp_now = hp_min;

            gameManager.FinishGame();
        }

        UpdateHpUI();
    }

    public void Heal(int heal)
    {
        hp_now += heal;

        if (hp_now > hp_max)
        {
            hp_now = hp_max;
        }

        UpdateHpUI();
    }
}
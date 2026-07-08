using UnityEngine;

public class Stone : MonoBehaviour
{
    private Rigidbody rb;

    [SerializeField] private float force = 0.6f;

    [Header("Stone Textures")]
    [SerializeField] private Texture[] rockTextures;

    [Header("Damage")]
    [SerializeField] private int damage = 100;

    [SerializeField] private float damage_value_ratio = 0.1f;

    private int default_damage = 100;

    private void Awake()
    {
        if (damage <= 0)
        {
            damage = default_damage;
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        SetRandomDamage();
        SetRandomTexture();
    }

    private void FixedUpdate()
    {
        if (rb != null)
        {
            rb.AddForce(Vector3.back * force);
        }
    }

    private void Update()
    {
        if (transform.position.z < -200f)
        {
            Destroy(gameObject);
        }
    }

    private void SetRandomDamage()
    {
        int baseDamage = damage;

        int minDamage = Mathf.RoundToInt(baseDamage - baseDamage * damage_value_ratio);
        int maxDamage = Mathf.RoundToInt(baseDamage + baseDamage * damage_value_ratio);

        damage = Random.Range(minDamage, maxDamage + 1);

        // Debug.Log("岩のダメージ: " + damage);
    }

    private void SetRandomTexture()
    {
        if (rockTextures == null || rockTextures.Length == 0)
        {
            Debug.LogWarning("岩のテクスチャが設定されていません。");
            return;
        }

        Renderer renderer = GetComponent<Renderer>();

        if (renderer == null)
        {
            Debug.LogWarning("Rendererが見つかりません。");
            return;
        }

        int randomIndex = Random.Range(0, rockTextures.Length);
        Texture selectedTexture = rockTextures[randomIndex];

        renderer.material.SetTexture("_BaseMap", selectedTexture);
    }

    public int GetDamage()
    {
        return damage;
    }
}
using UnityEngine;

public class Stone : MonoBehaviour
{
    private Rigidbody rb;
    private float force = 0.6f;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        rb.AddForce(Vector3.back * force);

        if (this.transform.position.z < -200f)
        {
            Destroy(this.gameObject);
        }
    }
}

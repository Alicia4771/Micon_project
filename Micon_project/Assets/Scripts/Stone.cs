using UnityEngine;

public class Stone : MonoBehaviour
{
    private Rigidbody rb;
    private float force = 0.1f;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        rb.AddForce(Vector3.back * force);
    }
}

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FoodItem : MonoBehaviour
{

    private Rigidbody rb;
    public Rigidbody Rb => rb;

    private void Start()
    {
        // Set fooditem to the fooditem layer
        gameObject.layer = (int)Robot.Layers.FoodItems;

        // Set RigidBody
        rb = GetComponent<Rigidbody>();

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void RemoveFoodItem()
    {
        Destroy(gameObject);
    }
}

using UnityEngine;

public class GrabSystem : MonoBehaviour
{
    [SerializeField]
    private Transform itemSlot;

    public void PickItem(FoodItem item)
    {
        // Disable rigidbody and reset velocities
        item.Rb.isKinematic = true;
        item.Rb.velocity = Vector3.zero;
        item.Rb.angularVelocity = Vector3.zero;

        // Disable colldier 
        item.GetComponent<CapsuleCollider>().enabled = false;

        // Set the robot's slot as parent
        item.transform.SetParent(itemSlot);

        // Reset position and rotation 
        item.transform.localPosition = Vector3.zero;
        item.transform.localEulerAngles = Vector3.zero;

        // Change item layer so other robots cannot target the food anymore.
        item.gameObject.layer = (int)Robot.Layers.TakenFood;
    }

    public void DropItem(FoodItem item)
    {
        // Remove parent
        item.transform.SetParent(null);

        // Enable rigidbody
        item.Rb.isKinematic = false;

        // Enable colldier
        item.GetComponent<CapsuleCollider>().enabled = true;

        // Destroy the food item after 2 seconds
        item.RemoveFoodItem();
    }
}

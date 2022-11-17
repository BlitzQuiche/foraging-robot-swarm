using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RobotController : MonoBehaviour
{
    Rigidbody robotRigidBody;
    Vector3 velocity;

    void Start()
    {
        robotRigidBody = GetComponent<Rigidbody>();
        robotRigidBody.freezeRotation = true;
    }

    public void Move(Vector3 _velocity)
    {
        velocity = _velocity;
    }

    public void FixedUpdate()
    {
        robotRigidBody.MovePosition(robotRigidBody.position + velocity * Time.deltaTime);
    }
}

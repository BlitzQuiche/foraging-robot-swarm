using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RobotController : MonoBehaviour
{
    
    Rigidbody robotRigidBody;
    Vector3 velocity;

    private const int CageDiagonal = 224;
    float MIN_X = -160;
    float MAX_X = 160;
    float MIN_Z = -160;
    float MAX_Z = 160;

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
        Vector3 newPosition = robotRigidBody.position + velocity * Time.deltaTime;

        newPosition.x = Mathf.Clamp(newPosition.x, MIN_X, MAX_X);
        newPosition.z = Mathf.Clamp(newPosition.z, MIN_Z, MAX_Z);

        var Abs_X_Z = Mathf.Abs(newPosition.x) + Mathf.Abs(newPosition.z);
        if (CageDiagonal < Abs_X_Z)
        {
            Debug.Log("OUT OF BOUNDS ATTEMPT!");
            // Calculate how far the new distance will put the robot outside the cage
            var outsideDistance = Abs_X_Z - CageDiagonal;
            
            // Set new positions to that robot remains just inside the cage
            if (newPosition.x > 0) newPosition.x -= outsideDistance / 2 - 10;
            else                   newPosition.x += outsideDistance / 2 + 10;

            if (newPosition.z > 0) newPosition.z -= outsideDistance / 2 - 10;
            else                   newPosition.z += outsideDistance / 2 + 10;

        }

        newPosition.y = 1;

        robotRigidBody.MovePosition(newPosition);
    }
}

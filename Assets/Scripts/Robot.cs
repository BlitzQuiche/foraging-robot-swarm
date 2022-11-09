using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent (typeof(RobotController))]
[RequireComponent (typeof(GrabSystem))]
public class Robot : MonoBehaviour
{
    // Robot Positional Information 
    public float maxSpeed = 5;
    public float wanderStrength = 0.8f;
    float randomWalkDirectionTime;

    // Scanner
    public float scanRadius = 5;

    Vector3 velocity;
    Vector3 direction;

    // Target food item
    Collider targetFoodItem;

    // Robot Threshold Information 
    float randomWalkDirectionThreshold;

    float searchingTime;
    float thresholdSearching;
    float restingTime;
    float thresholdResting;
    float effort;

    // Current robot state
    enum States
    {
        LeavingHome,
        RandomWalk,
        ScanArea,
        MoveToFood,
        GrabFood,
        Homing,
        MoveToHome,
        Deposit,
        Resting,
        Avoidance
    }
    States state;

    public enum Layers
    {
        FoodItems = 6,
        TakenFood = 7,
        Robots = 8
    }

    Vector3 nestPosition;
    RobotController controller;
    GrabSystem grabber;

    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<RobotController>();
        grabber = GetComponent<GrabSystem>();
        nestPosition = GameObject.Find("Nest").transform.position;

        // Iniitalise Robot time thresholds
        thresholdResting = 5;
        thresholdSearching = 5;
        randomWalkDirectionThreshold = 1;

        // Initalise robot times
        searchingTime = 0;
        restingTime = 0;
        randomWalkDirectionTime = 0;

        // Initialise State
        state = States.Resting;

    }

    // Update is called once per frame
    void Update()
    {
        switch(state)
        {

            case States.Resting:
                // How long have we been resting for?
                restingTime += Time.deltaTime;

                // If we have rested long enough, leave the home ! 
                if(restingTime > thresholdResting)
                {
                    direction = GetRandomDirection();
                    state = States.LeavingHome;
                    restingTime = 0;
                }
                break;

            case States.RandomWalk:
                searchingTime += Time.deltaTime;

                // Have we ran out of time to look for food? 
                if(searchingTime > thresholdSearching)
                {
                    // Let's go home.
                    state = States.Homing;
                    ChangeAntenaColor(Color.red);
                    Debug.Log("RandomWalk -> Homing");
                }

                // Check if we can see any food! 
                targetFoodItem = ScanAndTargetFood();
                if(targetFoodItem != null)
                {
                    // We have found a food item! 
                    // Let's move towards it
                    state = States.MoveToFood;
                    ChangeAntenaColor(Color.yellow);
                    Debug.Log("RandomWalk -> MoveToFood");
                    break;
                }

                // Get a new direction to randomWalk and move that way ! 
                MoveRobot(RandomWalkDirection());
                break;

            case States.MoveToFood:
                ChangeAntenaColor(Color.yellow);
                searchingTime += Time.deltaTime;

                // Have we ran out of time to look for food? 
                if (searchingTime > thresholdSearching)
                {
                    // Let's go home.
                    state = States.Homing;
                    ChangeAntenaColor(Color.red);
                    Debug.Log("RandomWalk -> Homing");
                }

                // Have we lost sight of the food ?
                if (!ScanForFood().Contains(targetFoodItem))
                {
                    // We have lost the food! Scan the area again to find it. 
                    state = States.Homing;
                    ChangeAntenaColor(Color.red);
                    break;
                }

                // Are we close enough to grab the food?
                if(Vector3.Distance(targetFoodItem.transform.position, transform.position) < 1.5)
                {
                    // Grab the food and go home !
                    grabber.PickItem(targetFoodItem.GetComponent<FoodItem>());
                    Debug.Log("MoveToFood --> MoveToHome; Found some food!");
                    state = States.MoveToHome;
                    ChangeAntenaColor(Color.green);
                    break;
                }

                var foodDirection = (targetFoodItem.transform.position - transform.position).normalized;
                MoveRobot(foodDirection);

                break;

            case States.MoveToHome:
                // Change robot antenna colour to MOVETOHOME
                if (Vector3.Distance(nestPosition, transform.position) > 1.5)
                {
                    // Move robot towards the nest ! 
                    MoveRobot((GameObject.Find("Nest").transform.position - transform.position).normalized);
                    break;
                }

                // If we have got home with some food, let's deposit it.
                grabber.DropItem(targetFoodItem.GetComponent<FoodItem>());
                targetFoodItem = null;

                // Let us rest
                state = States.Resting;
                ChangeAntenaColor(Color.white);
                Debug.Log("MoveToHome --> Resting; Returned home and deposited food");
                StopRobot();
                break;

            case States.Homing:
                if(Vector3.Distance(nestPosition, transform.position) > 1.5)
                {                    
                    // Move robot towards the nest ! 
                    MoveRobot((GameObject.Find("Nest").transform.position - transform.position).normalized);
                    break;
                }
                
                // Let us rest.
                Debug.Log("Homing -> Resting");
                ChangeAntenaColor(Color.white);
                state = States.Resting;
                StopRobot();
                break;

            case States.LeavingHome:
                if (Vector3.Distance(nestPosition, transform.position) < 5)
                {
                    // Move robot in whichever direction we were previously going to leave the nest.
                    MoveRobot(direction);
                    break;
                }

                Debug.Log("LeavingHome -> RandomWalk");
                ChangeAntenaColor(Color.blue);
                // We have left the nest, let's start searching
                state = States.RandomWalk;
                searchingTime = 0;
                break;
        }
        
    }

    // Checks scanners for food, return a random food item if there are any. 
    private Collider ScanAndTargetFood()
    {
        var visibleFoodItems = ScanForFood();
        if (visibleFoodItems.Length > 0)
        {
            // We can see some food! Pick one at random.
            Collider food = visibleFoodItems[Random.Range(0, visibleFoodItems.Length)];
            return food;
        }

        // Scanners have not detected any food. 
        return null;
    }

    // Scan for food items in robots scan radius.
    private Collider[] ScanForFood()
    {
        return Physics.OverlapSphere(transform.position, scanRadius, LayerMask.GetMask("FoodItems"));
    }

    // Gets a new random direction to walk towards if we have been walking in same direction long enough. 
    private Vector3 RandomWalkDirection()
    {
        randomWalkDirectionTime += Time.deltaTime;

        if (randomWalkDirectionTime > randomWalkDirectionThreshold)
        {
            Vector3 randomDirection = GetRandomDirection();
            randomWalkDirectionTime = 0f;
            return randomDirection;
        } 
        else
        {
            // If we don't need to change direction yet, just keep going the same way. 
            return direction;
        }
    }

    private Vector3 GetRandomDirection()
    {
        return (direction + Random.insideUnitSphere * wanderStrength).normalized;
    }

    // Calculates the new velocity of a robot given a new direction and passes velocity to controller.
    void MoveRobot(Vector3 newDirection)
    {
        // Set the current direction we are going to the new direction
        direction = newDirection;

        // Since robot is acting in 2D space, it's direction in the Y axis is always 0.
        direction.y = 0f;

        //Debug.Log("Direction: " + direction.ToString());

        //var looking = direction + transform.position;
        //looking.y = 1f
        //transform.LookAt(looking);

        // Calcultate and update robots velocity. 
        velocity = direction.normalized * maxSpeed;
        //Debug.Log("Velocity: " + velocity.ToString());

        // Pass new velocity to robot controller.
        controller.Move(velocity);
    }

    private void StopRobot()
    {
        velocity = Vector3.zero;
        controller.Move(velocity);
    }

    private void ChangeAntenaColor(Color colour)
    {
        transform.GetChild(0).GetComponent<MeshRenderer>().material.color = colour;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        
        Gizmos.DrawWireSphere(transform.position, scanRadius);
    }
}

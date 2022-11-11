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

    // Scanner
    public float foodScanRadius = 5;
    float proximityScanRadius = 2;


    Vector3 velocity;
    Vector3 direction;
    Vector3 looking;

    // Target food item
    Collider targetFoodItem;

    // Robot Timings 
    float randomWalkDirectionTime;
    float randomWalkDirectionThreshold;

    float searchingTime;
    float thresholdSearching;
    
    float restingTime;
    float thresholdResting;

    float scanAreaTime;
    float scanAreaThreshold;

    // Robot Effort
    float effort;

    // Current robot state
    enum States
    {
        LeavingHome,
        RandomWalk,
        ScanArea,
        MoveToFood,
        Homing,
        MoveToHome,
        Resting,
        Avoidance
    }
    States state;
    States avoidancePreviousState;
    Color avoidancePreviousColour;

    Color[] colours =
    {
        Color.cyan, 
        Color.blue,
        Color.magenta,
        Color.yellow,
        Color.red,
        Color.green,
        Color.white,
        Color.black
    };

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

        // Initialise Robot layer
        gameObject.layer = (int)Layers.Robots;

        // Iniitalise Robot time thresholds
        thresholdResting = 5;
        thresholdSearching = 5;
        randomWalkDirectionThreshold = 1;
        scanAreaThreshold = 0.25f;

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
                    ChangeAntenaColor(colours[(int)state]);
                    restingTime = 0;
                }
                break;

            case States.Avoidance:
                var obsticles = ScanForCollisions();
                if (obsticles.Count > 0 )
                {
                    // If we proximity scan some obsticles, calculate and move in a new direction 
                    // to avoid all of these obsticles.
                    
                    MoveRobot(avoid(obsticles));
                    break;
                }

                // We no longer need to avoid obsticles !
                // Return to the previous state we were in.
                state = avoidancePreviousState;
                ChangeAntenaColor(avoidancePreviousColour);
                break;

            case States.RandomWalk:
                // Are we going to hit another robot?
                if (checkAvoidance()) break;

                searchingTime += Time.deltaTime;

                // Have we ran out of time to look for food? 
                if(searchingTime > thresholdSearching)
                {
                    // Let's go home.
                    state = States.Homing;
                    ChangeAntenaColor(colours[(int)state]);
                    Debug.Log("RandomWalk -> Homing");
                }

                // Check if we can see any food! 
                targetFoodItem = ScanAndTargetFood();
                if(targetFoodItem != null)
                {
                    // We have found a food item! 
                    // Let's move towards it
                    state = States.MoveToFood;
                    ChangeAntenaColor(colours[(int)state]);
                    Debug.Log("RandomWalk -> MoveToFood");
                    break;
                }

                // Get a new direction to randomWalk and move that way ! 
                MoveRobot(RandomWalkDirection());
                break;

            case States.MoveToFood:
                if (checkAvoidance()) break;
                searchingTime += Time.deltaTime;

                // Have we ran out of time to look for food? 
                if (searchingTime > thresholdSearching)
                {
                    // Let's go home.
                    state = States.Homing;
                    ChangeAntenaColor(colours[(int)state]);
                    Debug.Log("RandomWalk -> Homing");
                }

                // Have we lost sight of the food ?
                if (!ScanForFood().Contains(targetFoodItem))
                {
                    // We have lost the food! Scan the area again to find it. 
                    state = States.ScanArea;
                    ChangeAntenaColor(colours[(int)state]);
                    StopRobot();
                    break;
                }

                // Are we close enough to grab the food?
                if(Vector3.Distance(targetFoodItem.transform.position, transform.position) < 1.5)
                {
                    // Grab the food and go home !
                    grabber.PickItem(targetFoodItem.GetComponent<FoodItem>());
                    Debug.Log("MoveToFood --> MoveToHome; Found some food!");
                    state = States.MoveToHome;
                    ChangeAntenaColor(colours[(int)state]);
                    break;
                }

                // If we have enough time to keep searching and we can still see the food,
                // but aren't close enough to grab it, keep moving towards it !
                var foodDirection = (targetFoodItem.transform.position - transform.position).normalized;
                MoveRobot(foodDirection);

                break;

            case States.ScanArea:
                if (checkAvoidance()) break;
                searchingTime += Time.deltaTime;
                scanAreaTime += Time.deltaTime;

                // Have we ran out of time to look for food? 
                if (searchingTime > thresholdSearching)
                {
                    // Let's go home.
                    state = States.Homing;
                    ChangeAntenaColor(colours[(int)state]);
                    Debug.Log("RandomWalk -> Homing");
                }

                // Have we scanned for long enough for the lost food?
                if (scanAreaTime > scanAreaThreshold)
                {
                    // Let's look somewhere else instead
                    state = States.RandomWalk;
                    ChangeAntenaColor(colours[(int)state]);
                    Debug.Log("ScanArea -> RandomWalk");
                }

                // Check if we can see the target food! 
                if (ScanForFood().Contains(targetFoodItem))
                {
                    // We have re-found the food! Let's move towards it!. 
                    state = States.MoveToFood;
                    ChangeAntenaColor(colours[(int)state]);
                    break;
                }

                // If we don't re-find the target food, keep trying until for as long scanAreaTime decides. 

                break;

            case States.MoveToHome:
                if (checkAvoidance()) break;
                // Change robot antenna colour to MOVETOHOME
                if (Vector3.Distance(nestPosition, transform.position) > Random.Range(1, 8))
                {
                    // Move robot towards the nest ! 
                    MoveRobot((GameObject.Find("Nest").transform.position - transform.position).normalized);
                    break;
                }

                // If we have got home with some food, let's deposit it.
                grabber.DropItem(targetFoodItem.GetComponent<FoodItem>());
                targetFoodItem = null;

                Debug.Log("MoveToHome --> Resting; Returned home and deposited food");

                // Let us rest
                state = States.Resting;
                ChangeAntenaColor(colours[(int)state]);

                StopRobot();
                break;

            case States.Homing:
                if (checkAvoidance()) break;
                if (Vector3.Distance(nestPosition, transform.position) > Random.Range(1, 8))
                {                    
                    // Move robot towards the nest ! 
                    MoveRobot((GameObject.Find("Nest").transform.position - transform.position).normalized);
                    break;
                }
                
                // Let us rest.
                Debug.Log("Homing -> Resting");
                state = States.Resting;

                // White means resting.
                ChangeAntenaColor(colours[(int)state]);

                // Stop Robot from moving whilst it is resting.
                StopRobot();

                break;

            case States.LeavingHome:
                if(checkAvoidance()) break;
                if (Vector3.Distance(nestPosition, transform.position) < 12)
                {
                    // Move robot in whichever direction we were previously going to leave the nest.
                    MoveRobot(direction);
                    break;
                }

                Debug.Log("LeavingHome -> RandomWalk");

                // We have left the nest, let's start searching
                state = States.RandomWalk;
                searchingTime = 0;

                // Blue means searching. 
                ChangeAntenaColor(colours[(int)state]);

                break;
        }
        
    }


    private bool checkAvoidance()
    {
        if (ScanForCollisions().Count > 0)
        {
            StopRobot();

            avoidancePreviousColour = colours[(int)state];
            avoidancePreviousState = state;

            state = States.Avoidance;
            ChangeAntenaColor(colours[(int)state]);
            return true;
        }
        return false;
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
        return Physics.OverlapSphere(transform.position, foodScanRadius, LayerMask.GetMask("FoodItems"));
    }

    // Avoidance algorithm
    // Calculates opposite direction from all potential collisons.
    // Robot will then move in that direction. 
    private Vector3 avoid(List<Collider> obsticles)
    { 
        Vector3 avoidanceDirection = Vector3.zero;
        foreach (Collider obsticle in obsticles)
        {
            avoidanceDirection += (obsticle.transform.position);
        }
        avoidanceDirection /= obsticles.Count;

        avoidanceDirection = (avoidanceDirection - transform.position).normalized;

        //Debug.Log("--- Robot Calculating Avoidance ---");
        //Debug.Log(direction);
        //Debug.Log(avoidanceDirection);
        //Debug.Log(-(avoidanceDirection));

        return -(avoidanceDirection);
    }

    // Check proximity scanners to see if we are about to hit any other robots.
    private List<Collider> ScanForCollisions()
    {
        var collisions = Physics.OverlapSphere(transform.position, proximityScanRadius, LayerMask.GetMask("Robots"));
        var collisionsList = collisions.ToList();
        var currentRobot = collisionsList.SingleOrDefault(x => x.gameObject == gameObject);

        collisionsList.Remove(currentRobot);

        return collisionsList;
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

        looking = direction + transform.position;
        transform.LookAt(looking);

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
        
        Gizmos.DrawWireSphere(transform.position, foodScanRadius);

        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(transform.position, proximityScanRadius);
    }
}

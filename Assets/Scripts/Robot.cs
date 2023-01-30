using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(RobotController))]
[RequireComponent(typeof(GrabSystem))]
public class Robot : MonoBehaviour
{
    int id;

    // Robot Positional Information 
    float maxSpeed = 5;
    float wanderStrength = 0.8f;

    Vector3 velocity;
    Vector3 direction;
    Vector3 looking;

    // Scanner
    float foodScanRadius = 5;
    float proximityScanRadius = 2.5f;

    // Target food item
    Collider targetFoodItem;

    // Robot Timings 
    float randomWalkDirectionTime;
    float randomWalkDirectionThreshold;
    float scanAreaTime;
    float scanAreaThreshold;
    float avoidanceCheckTime;
    float avoidanceCheckThreshold = 2;

    // Robot Timings affected by cues
    float searchingTime;
    float thresholdSearching;
    float thresholdSearchingMin = 5;
    float thresholdSearchingMax = 50;

    float restingTime;
    float thresholdResting;
    float thresholdRestingMax = 100;

    
    
    // Environental Cues
    // Avoidance Rest Increase
    float ari = 5;
    // Avoidance Search Decrease
    float asd = 5;

    // Internal Cues
    // Failure Rest Increase
    float fri = 20;
    // Success Rest Decrease
    float srd = 20;

    // Social Cues
    // Teamate Success Rest Decrease
    float tsrd = 2;
    // Teammate Failure Rest Increase
    float tfri = 2;
    // Teammate Success Search Increase
    float tssi = 2;
    // Teammate Failure Search Decrease
    float tfsd = 2;

    public float successSocialCue;
    public float failureSocialCue;

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
    SimulationData simulation;


    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<RobotController>();
        grabber = GetComponent<GrabSystem>();
        nestPosition = GameObject.Find("Nest").transform.position;

        // Initialise Robot layer
        gameObject.layer = (int)Layers.Robots;

        // Iniitalise Robot time thresholds
        thresholdResting = 0;
        thresholdSearching = 20;
        randomWalkDirectionThreshold = 1;
        scanAreaThreshold = 0.25f;

        // Initalise robot times
        searchingTime = 0;
        restingTime = 0;
        randomWalkDirectionTime = 0;

        // Initialise State
        state = States.Resting;

        // Initialise robot id 
        id = GetInstanceID();

        // Get the simulationData instance
        simulation = GameObject.Find("World").GetComponent<SimulationData>();

        // Initialise Robot state in simulation data
        simulation.UpdateState(id, (int)state);

    }

    // Update is called once per frame
    void Update()
    {
        switch (state)
        {

            case States.Resting:
                // Check for any new social cues!
                
                // If we have recived any social cues, make relevant updates ! 
                if(successSocialCue > 0 || failureSocialCue > 0)
                {
                    Debug.Log($"Resting Robot social update, {successSocialCue}, {failureSocialCue}, br {thresholdResting}, bs {thresholdSearching}");
                    thresholdResting = thresholdResting - (tsrd * successSocialCue) + (tfri * failureSocialCue);
                    thresholdSearching = thresholdSearching + (tssi * successSocialCue) - (tfsd * failureSocialCue);

                    if (thresholdResting < 0) thresholdResting = 0;
                    if (thresholdSearching < thresholdSearchingMin) thresholdSearching = thresholdSearchingMin;
                    if (thresholdSearching > thresholdSearchingMax) thresholdSearching = thresholdSearchingMax;

                    Debug.Log($"Resting Robot social update, ar {thresholdResting}, as {thresholdSearching}");

                    successSocialCue = 0;
                    failureSocialCue = 0;
                }
                

                // How long have we been resting for?
                restingTime += Time.deltaTime;

                // If we have rested long enough, leave the home ! 
                if (restingTime > thresholdResting)
                {
                    direction = GetRandomDirection();

                    state = States.LeavingHome;
                    simulation.UpdateState(id, (int)state);

                    ChangeAntenaColor(colours[(int)state]);

                    restingTime = 0;
                }
                break;

            case States.Avoidance:
                
                var collisions = ScanForCollisions();
                if (collisions.Count > 0)
                {
                    // If we proximity scan some obsticles, calculate and add a force in direction
                    // away from all of these obsticles
                    Avoid(collisions);

                    
                    // Keep moving in whichever direction we were moving previously.
                    MoveRobot(direction);
                    break;
                }

                // We no longer need to avoid obsticles !
                // Return to the previous state we were in.
                state = avoidancePreviousState;
                simulation.UpdateState(id, (int)state);
                ChangeAntenaColor(avoidancePreviousColour);
                break;

            case States.RandomWalk:
                // Are we going to hit another robot?
                if (CheckAvoidance()) break;

                searchingTime += Time.deltaTime;

                // Have we ran out of time to look for food? 
                if (searchingTime > thresholdSearching)
                {
                    // Broadcast to everyone that we have failed to find any food. 
                    simulation.BroadcastFailure(id);

                    // Let's go home.
                    state = States.Homing;
                    simulation.UpdateState(id, (int)state);
                    ChangeAntenaColor(colours[(int)state]);
                    Debug.Log("RandomWalk -> Homing");
                }

                // Are we searching in the home? 
                if (Vector3.Distance(nestPosition, transform.position) < 10)
                {
                    // Move robot away from the nest ! 
                    MoveRobot(-(nestPosition - transform.position).normalized);
                    break;
                }

                // Check if we can see any food! 
                targetFoodItem = ScanAndTargetFood();

                if (targetFoodItem != null)
                {
                    // We have found a food item! 
                    // Let's move towards it
                    state = States.MoveToFood;
                    simulation.UpdateState(id, (int)state);
                    ChangeAntenaColor(colours[(int)state]);
                    Debug.Log("RandomWalk -> MoveToFood");
                    break;
                }

                // Get a new direction to randomWalk and move that way ! 
                MoveRobot(RandomWalkDirection());
                break;

            case States.MoveToFood:
                if (CheckAvoidance()) break;
                searchingTime += Time.deltaTime;

                // Have we ran out of time to look for food? 
                if (searchingTime > thresholdSearching)
                {
                    // Broadcast to everyone that we have failed to find any food. 
                    simulation.BroadcastFailure(id);

                    // Let's go home.
                    state = States.Homing;
                    simulation.UpdateState(id, (int)state);
                    ChangeAntenaColor(colours[(int)state]);
                    Debug.Log("RandomWalk -> Homing");
                }

                // Have we lost sight of the food ?
                if (!ScanForFood().Contains(targetFoodItem))
                {
                    // We have lost the food! Scan the area again to find it. 
                    state = States.ScanArea;
                    simulation.UpdateState(id, (int)state);
                    ChangeAntenaColor(colours[(int)state]);
                    StopRobot();
                    break;
                }

                // Are we close enough to grab the food?
                if (Vector3.Distance(targetFoodItem.transform.position, transform.position) < 1.5)
                {
                    // Grab the food !
                    grabber.PickItem(targetFoodItem.GetComponent<FoodItem>());
                    Debug.Log("MoveToFood --> MoveToHome; Found some food!");

                    // Broadcast social cue to tell everyone we have found some food! Hurray!
                    simulation.BroadcastSuccess(id);

                    // Update our state and start moving home.
                    state = States.MoveToHome;
                    simulation.UpdateState(id, (int)state);
                    ChangeAntenaColor(colours[(int)state]);
                    break;
                }

                // If we have enough time to keep searching and we can still see the food,
                // but aren't close enough to grab it, keep moving towards it !
                var foodDirection = (targetFoodItem.transform.position - transform.position).normalized;
                MoveRobot(foodDirection);

                break;

            case States.ScanArea:
                if (CheckAvoidance()) break;
                searchingTime += Time.deltaTime;
                scanAreaTime += Time.deltaTime;

                // Have we ran out of time to look for food? 
                if (searchingTime > thresholdSearching)
                {
                    // Broadcast to everyone that we have failed to find any food. 
                    simulation.BroadcastFailure(id);

                    // Let's go home.
                    state = States.Homing;
                    simulation.UpdateState(id, (int)state);
                    ChangeAntenaColor(colours[(int)state]);
                    Debug.Log("RandomWalk -> Homing");
                }

                // Have we scanned for long enough for the lost food?
                if (scanAreaTime > scanAreaThreshold)
                {
                    // Let's look somewhere else instead
                    state = States.RandomWalk;
                    simulation.UpdateState(id, (int)state);
                    ChangeAntenaColor(colours[(int)state]);
                    Debug.Log("ScanArea -> RandomWalk");
                }

                // Check if we can see the target food! 
                if (ScanForFood().Contains(targetFoodItem))
                {
                    // We have re-found the food! Let's move towards it!. 
                    state = States.MoveToFood;
                    simulation.UpdateState(id, (int)state);
                    ChangeAntenaColor(colours[(int)state]);
                    break;
                }

                // If we don't re-find the target food, keep trying until for as long scanAreaTime decides. 

                break;

            case States.MoveToHome:
                if (Vector3.Distance(nestPosition, transform.position) > 10)
                {
                    // Do colision avoidance if we are on our way back to the nest.
                    if (CheckAvoidance()) break;
                    // Move robot towards the nest ! 
                    MoveRobot((nestPosition - transform.position).normalized);
                    break;
                }
                else if (Vector3.Distance(nestPosition, transform.position) > 6)
                {
                    // Turn off colision avoidance when we are entering the nest
                    MoveRobot((nestPosition - transform.position).normalized);
                    break;
                }

                // If we have got home with some food, let's deposit it.
                grabber.DropItem(targetFoodItem.GetComponent<FoodItem>());
                targetFoodItem = null;

                // Tell the simulation that we have deposited some food
                simulation.DepositFood();

                // Update resting threshold with interal cues
                thresholdResting -= srd;
                if (thresholdResting < 0) thresholdResting = 0;
                

                Debug.Log("MoveToHome --> Resting; Returned home and deposited food");

                // Let us rest
                state = States.Resting;
                simulation.UpdateState(id, (int)state);
                ChangeAntenaColor(colours[(int)state]);

                StopRobot();
                break;

            case States.Homing:
                if (Vector3.Distance(nestPosition, transform.position) > 10)
                {
                    // Do colision avoidance if we are on our way back to the nest.
                    if (CheckAvoidance()) break;
                    // Move robot towards the nest ! 
                    MoveRobot((nestPosition - transform.position).normalized);
                    break;
                }
                else if (Vector3.Distance(nestPosition, transform.position) > 6)
                {
                    // Turn off colision avoidance when we are entering the nest
                    MoveRobot((nestPosition - transform.position).normalized);
                    break;
                }

                // We have not found food, update resting threshold with internal cue
                thresholdResting += fri;
                if (thresholdResting > thresholdRestingMax)
                {
                    thresholdResting = thresholdRestingMax;
                }

                // Let us rest.
                Debug.Log("Homing -> Resting");
                state = States.Resting;
                simulation.UpdateState(id, (int)state);

                // White means resting.
                ChangeAntenaColor(colours[(int)state]);

                // Stop Robot from moving whilst it is resting.
                StopRobot();

                break;

            case States.LeavingHome:
                // We decide not to do any colision avoidance when leaving the nest. 
                // Assumption that robots can find their own way to the edge of nest 
                // without bumping into other robots. 

                if (Vector3.Distance(nestPosition, transform.position) < 12)
                {
                    // Move robot in whichever direction we were previously going to leave the nest.
                    MoveRobot(direction);
                    break;
                }

                Debug.Log("LeavingHome -> RandomWalk");

                // We have left the nest, let's start searching
                state = States.RandomWalk;
                simulation.UpdateState(id, (int)state);
                searchingTime = 0;

                // Blue means searching. 
                ChangeAntenaColor(colours[(int)state]);

                break;
        }
    }

    private bool CheckAvoidance()
    {
        var collisions = ScanForCollisions();
        if (collisions.Count > 0)
        {
            StopRobot();

            // Check if we are going to collide with any robots !
            if (collisions.Any(c => c.GetComponent<Robot>() != null))
            {
                //Debug.Log("Robot Collision Detected!");
                // Update Thresholds with environmental cues
                thresholdResting += ari;
                if (thresholdResting > thresholdRestingMax)
                {
                    thresholdResting = thresholdRestingMax;
                }

                thresholdSearching -= asd;
                if (thresholdSearching < thresholdSearchingMin)
                {
                    thresholdSearching = thresholdSearchingMin;
                }
            }

            avoidancePreviousColour = colours[(int)state];
            avoidancePreviousState = state;

            state = States.Avoidance;
            simulation.UpdateState(id, (int)state);
            ChangeAntenaColor(colours[(int)state]);

            
            return true;
        }
        return false;
    }

    public void IncrementSuccessSocialCue()
    {
        successSocialCue += 1;
    }

    public void IncrementFailureSocialCue()
    {
        failureSocialCue += 1;
    }

    // Avoidance algorithm
    // Calculates opposite direction from all potential collisons.
    // Robot will then move in that direction. 
    private void Avoid(List<Collider> collisions)
    {
        Vector3 avoidanceDirection = Vector3.zero;
        foreach (Collider collision in collisions)
        {
            avoidanceDirection += Physics.ClosestPoint(transform.position, collision, collision.transform.position, collision.transform.rotation);
        }
                    
        avoidanceDirection /= collisions.Count;

        avoidanceDirection = (avoidanceDirection - transform.position).normalized;
        //Debug.Log(avoidanceDirection);

        var randomAngle = Random.Range(-30, 30);
        direction = Quaternion.AngleAxis(randomAngle, Vector3.up) * -avoidanceDirection.normalized;

        //direction = -avoidanceDirection.normalized;

        //gameObject.GetComponent<Rigidbody>().AddForce(-avoidanceDirection.normalized);
    }

    // Check proximity scanners to see if we are about to hit any other robots.
    private List<Collider> ScanForCollisions()
    {
        var collisions = Physics.OverlapSphere(transform.position, proximityScanRadius, LayerMask.GetMask("Robots", "Cage"));
        var collisionsList = collisions.ToList();
        var currentRobot = collisionsList.SingleOrDefault(x => x.gameObject == gameObject);

        collisionsList.Remove(currentRobot);

        return collisionsList;
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

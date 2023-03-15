using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(RobotController))]
[RequireComponent(typeof(GrabSystem))]
public class Robot : MonoBehaviour
{
    int id;

    // Robot Positional Information 
    float maxSpeed = 3;
    float wanderStrength = 0.8f;

    Vector3 velocity;
    Vector3 direction;
    Vector3 looking;

    // Scanner
    float foodScanRadius = 40;
    float foodScanFOV = 30;
    float proximityScanRadius = 9.2f;
    float grabDistance = 6.7f;

    // Target food item
    Collider targetFoodItem;

    // Robot Timings 
    float randomWalkDirectionTime;
    float randomWalkDirectionThreshold;
    float scanAreaTime;
    float scanAreaThreshold;

    // Robot Social Learning
    // Robot self assesment score
    // Equal to the number of food items collected in the past mutationPeriod seconds.
    public float selfAssesmentScore;

    // Robot internal perception of how long it has been alive for.
    bool socialLearning = true;
    float existanceTime;
    List<float> foodCollectionTimes = new();
   
    // If True, robots will not accept any incoming broadcast requests.
    bool currentlyMaturing;
    // How long robots will mature for!
    // ie how long they will keep track of food items they have collected for assesment score.
    float maturationPeriod = 1000;
    float maturationTime;

    // Cue paramter mutation
    float mutationSigma = 1;

    // Robot Timings affected by cues
    public float searchingTime;
    public float thresholdSearching;
    float thresholdSearchingMin = 20;
    float thresholdSearchingMax = 200;

    public float restingTime;
    public float thresholdResting;
    float thresholdRestingMax = 2000;

    /* // Environental Cues
     // Avoidance Rest Increase
     public float ari = 5;
     // Avoidance Search Decrease
     public float asd = 5;

     // Internal Cues
     // Failure Rest Increase
     public float fri = 20;
     // Success Rest Decrease
     public float srd = 20;

     // Social Cues
     // Teamate Success Rest Decrease
     public float tsrd = 10;
     // Teammate Failure Rest Increase
     public float tfri = 40;
     // Teammate Success Search Increase
     public float tssi = 10;
     // Teammate Failure Search Decrease
     public float tfsd = 20;*/

    const string ARI = "ari";
    const string ASD = "asd";
    const string FRI = "fri";
    const string SRD = "srd";
    const string TSRD = "tsrd";
    const string TFRI = "tfri";
    const string TSSI = "tssi";
    const string TFSD = "tfsd";

    public Dictionary<string, float> cueParameters = new()
    {
        { ARI, 5 },
        { ASD, 5 },
        { FRI, 20 },
        { SRD, 20 },
        { TSRD, 10 },
        { TFRI, 40 },
        { TSSI, 10 },
        { TFSD, 20 }
    };


    public float successSocialCue;
    public float failureSocialCue;
    private float successAttenuation = 0.1f;
    private float failureAttenuation = 0.1f;

    // Current robot state
    public enum States
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

        // Social learning
        selfAssesmentScore = 0;
        existanceTime = 0;
        currentlyMaturing = true;
        maturationTime = maturationPeriod;

        // Initialise Robot layer
        gameObject.layer = (int)Layers.Robots;

        // Initalise Robot time thresholds 
        thresholdResting = 0;
        thresholdSearching = thresholdSearchingMax;
        randomWalkDirectionThreshold = 1;
        scanAreaThreshold = 0.25f;

        // Initalise robot times
        searchingTime = 0;
        restingTime = 0;
        randomWalkDirectionTime = 0;
        scanAreaTime = 0;

        // Initialise State
        state = States.Resting;

        // Initialise robot id 
        id = GetInstanceID();

        // Get the simulationData instance
        simulation = GameObject.Find("World").GetComponent<SimulationData>();

    }

    // Update is called once per frame
    void Update()
    {
        // Increment how long we have existed for
        existanceTime += Time.deltaTime;

        // If we are currently maturing, check if we have matured for long enough
        if (currentlyMaturing)
        {
            if (maturationTime < existanceTime)
            {
                // We have been maturing for long enough, stop maturing and allow social learning broadcasts
                currentlyMaturing = false;
                //Debug.Log($"Stop maturing");
            }
        }

        successSocialCue -= successAttenuation * Time.deltaTime;
        if (successSocialCue < 0)
        {
            successSocialCue = 0;
        }

        failureSocialCue -= failureAttenuation * Time.deltaTime;
        if (failureSocialCue < 0)
        {
            failureSocialCue = 0;
        }


        // Check whether we need to forget about any food items in our self assesment score.
        var foodForgetTime = foodCollectionTimes.FirstOrDefault();
        if (foodForgetTime != 0 & foodForgetTime < existanceTime)
        {
            //Debug.Log($"RobotId: {id}, forgetting: {foodForgetTime}");
            // TODO: Fix Self assesment score below 0 BUG!
            foodCollectionTimes.RemoveAt(0);
            selfAssesmentScore -= 1;
            if (foodCollectionTimes.Any() & selfAssesmentScore == 0)
            {
                Debug.Log($"{id} Forget list non-empty and score is 0");
                foreach (var item in foodCollectionTimes)
                {
                    Debug.Log($"{id}: {item}");
                }
                Debug.Log($"{id} -------------");
            }
        }

        switch (state)
        {

            case States.Resting:
                // Check for any new social cues!
                // If we have recived any social cues, make relevant updates ! 
                if (successSocialCue > 0 || failureSocialCue > 0)
                {
                    //Debug.Log("FSC " + failureSocialCue);
                    //Debug.Log("SSC " + successSocialCue);
                    //Debug.Log("TR " + thresholdResting);
                    thresholdResting = thresholdResting - (cueParameters[TSRD] * successSocialCue) + (cueParameters[TFRI] * failureSocialCue);
                    //Debug.Log("TRA " + thresholdResting);

                    //Debug.Log("TS " + thresholdSearching);
                    thresholdSearching = thresholdSearching + (cueParameters[TSSI] * successSocialCue) - (cueParameters[TFSD] * failureSocialCue);
                    //Debug.Log("TSA " + thresholdSearching);

                    if (thresholdResting < 0) thresholdResting = 0;
                    if (thresholdResting > thresholdRestingMax) thresholdResting = thresholdRestingMax;
                    if (thresholdSearching < thresholdSearchingMin) thresholdSearching = thresholdSearchingMin;
                    if (thresholdSearching > thresholdSearchingMax) thresholdSearching = thresholdSearchingMax;


                    successSocialCue = 0;
                    failureSocialCue = 0;
                }

                // How long have we been resting for?
                restingTime += Time.deltaTime;

                // If we have rested long enough, leave the home ! 
                if (restingTime > thresholdResting)
                {
                    direction = transform.position - nestPosition;
                    state = States.LeavingHome;
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
                ChangeAntenaColor(avoidancePreviousColour);
                break;

            case States.RandomWalk:
                // Are we going to hit another robot?
                if (CheckAvoidance()) break;

                searchingTime += Time.deltaTime;

                // Have we ran out of time to look for food? 
                if (searchingTime > thresholdSearching)
                {
                    // Let's go home.
                    state = States.Homing;
                    ChangeAntenaColor(colours[(int)state]);
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
                    ChangeAntenaColor(colours[(int)state]);
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
                    ChangeAntenaColor(colours[(int)state]);
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
                if (Vector3.Distance(targetFoodItem.transform.position, transform.position) < grabDistance)
                {
                    // Grab the food !
                    grabber.PickItem(targetFoodItem.GetComponent<FoodItem>());

                    // Update our state and start moving home.
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
                    ChangeAntenaColor(colours[(int)state]);
                }

                // Have we scanned for long enough for the lost food?
                if (scanAreaTime > scanAreaThreshold)
                {
                    // Let's look somewhere else instead
                    state = States.RandomWalk;
                    ChangeAntenaColor(colours[(int)state]);
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
                if (Vector3.Distance(nestPosition, transform.position) > 24)
                {
                    // Do colision avoidance if we are on our way back to the nest.
                    if (CheckAvoidance()) break;
                    // Move robot towards the nest ! 
                    MoveRobot((nestPosition - transform.position).normalized);
                    break;
                }
                else if (Vector3.Distance(nestPosition, transform.position) > 10)
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

                // Increase our current self assesement score and remember the time we need to
                // forget about this food item in our self assement score
                selfAssesmentScore += 1;
                foodCollectionTimes.Add(existanceTime + maturationPeriod);
                //Debug.Log($"{id} found item, score {selfAssesmentScore}, forget {existanceTime + maturationPeriod}");

                // Broadcast our current self assesment score and paramters for social learning
                if (!currentlyMaturing) broadcastSocialTransfer();

                // Broadcast social cue to tell everyone we have found some food! Hurray!
                simulation.BroadcastSuccess(id);

                // Update resting threshold with interal cues
                thresholdResting -= cueParameters[SRD];
                if (thresholdResting < 0) thresholdResting = 0;

                // Let us rest
                state = States.Resting;
                ChangeAntenaColor(colours[(int)state]);

                StopRobot();
                break;

            case States.Homing:
                if (Vector3.Distance(nestPosition, transform.position) > 24)
                {
                    // Do colision avoidance if we are on our way back to the nest.
                    if (CheckAvoidance()) break;
                    // Move robot towards the nest ! 
                    MoveRobot((nestPosition - transform.position).normalized);
                    break;
                }
                else if (Vector3.Distance(nestPosition, transform.position) > 10)
                {
                    // Turn off colision avoidance when we are entering the nest
                    MoveRobot((nestPosition - transform.position).normalized);
                    break;
                }

                // We have not found food, update resting threshold with internal cue
                thresholdResting += cueParameters[FRI];
                if (thresholdResting > thresholdRestingMax)
                {
                    thresholdResting = thresholdRestingMax;
                }

                // Broadcast our current self assesment score and paramters for social learning
                if (!currentlyMaturing) broadcastSocialTransfer();

                // Broadcast to everyone that we have failed to find any food. 
                simulation.BroadcastFailure(id);

                // Let us rest.
                state = States.Resting;

                // White means resting.
                ChangeAntenaColor(colours[(int)state]);

                // Stop Robot from moving whilst it is resting.
                StopRobot();

                break;

            case States.LeavingHome:
                // We decide not to do any colision avoidance when leaving the nest. 
                // Assumption that robots can find their own way to the edge of nest 
                // without bumping into other robots. 
                
                if (Vector3.Distance(nestPosition, transform.position) < 20)
                {
                    // Move robot in whichever direction we were previously going to leave the nest.
                    MoveRobot(direction);
                    break;
                }

                // We have left the nest, let's start searching
                state = States.RandomWalk;
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
                // Update Thresholds with environmental cues
                thresholdResting += cueParameters[ARI];
                if (thresholdResting > thresholdRestingMax)
                {
                    thresholdResting = thresholdRestingMax;
                }

                thresholdSearching -= cueParameters[ASD];
                if (thresholdSearching < thresholdSearchingMin)
                {
                    thresholdSearching = thresholdSearchingMin;
                }
            }

            avoidancePreviousColour = colours[(int)state];
            avoidancePreviousState = state;

            state = States.Avoidance;
            ChangeAntenaColor(colours[(int)state]);


            return true;
        }
        return false;
    }

    // Incrememnt success cue due to another robots success broadcast.
    public void IncrementSuccessSocialCue()
    {
        successSocialCue += 1;
    }

    // Incrememnt failure cue due to another robots failure broadcast.
    public void IncrementFailureSocialCue()
    {
        failureSocialCue += 1;
    }

    // Social learning diffusion function. If a robot has recieved a message from a greater recievedAssesment
    // than its own selfAsssesment, it will copy the recievedParamters into its own !
    // Robot will only accept the message if it is not currently in maturation period and recieved assesment 
    // is greater than its own selfAssesmentScore.
    public void RecieveSocialTransfer(float recievedAssesment, (string,float)[] recievedParameters)
    {
        if (socialLearning & !currentlyMaturing & recievedAssesment > selfAssesmentScore)
        {
            // We start maturing as we have recived new paramters!
            currentlyMaturing = true;
            // Set the alarm to stop maturing for maturationPeriod seconds in the future.
            maturationTime = existanceTime + maturationPeriod;

            // Robot resets its current score before entering mutation period
            selfAssesmentScore = 0;
            foodCollectionTimes.Clear();

            /*Debug.Log($"Robot {id} accepting social transfer");
            Debug.Log(string.Join(", ", recievedParameters));
            Debug.Log($"SelfAssesment: {selfAssesmentScore}, recieved {recievedAssesment}");
            Debug.Log($"Will stop maturing at {maturationTime}, current exist time: {existanceTime}");*/

            // Overwrite our current parameters with new ones !
            // TODO: Implement gaussian mutation here? 
            // TODO: Fix paramters going negative, min values for paramters etc
            var mutationVal = Random.Range(-mutationSigma, mutationSigma);
            
            foreach(var kvp in recievedParameters)
            {
                // Item1 is the name of the cue paramter, item2 is its value from the broadcasting robot!
                var newValue = kvp.Item2 + mutationVal;
                if (newValue < 0) cueParameters[kvp.Item1] = 0;
                else cueParameters[kvp.Item1] = newValue;
            }

            //Debug.Log(string.Join(", ", cueParameters.ToArray()));
        }
    }

    // Broadcast to other robots in the nest our score and paramters to diffuse via social learning.
    public void broadcastSocialTransfer()
    {
        // Get this robots current parameters and prepare them for broadcasting!
        var parameters = new (string, float)[]
        { (ARI, cueParameters[ARI]), 
          (ASD, cueParameters[ASD]), 
          (FRI, cueParameters[FRI]),
          (SRD, cueParameters[SRD]),
          (TSRD, cueParameters[TSRD]), 
          (TFRI, cueParameters[TFRI]), 
          (TSSI, cueParameters[TSSI]), 
          (TFSD, cueParameters[TFSD])
        };

        // Select 4 random parameters to broadcast to other robots !
        //parameters = parameters.OrderBy(x => Random.Range(0, 7)).Take(4).ToArray();

        //Debug.Log(string.Join(", ", parameters));

        simulation.Transfer(id, selfAssesmentScore, parameters);
    }

    public States GetState()
    {
        return state;
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

        var randomAngle = Random.Range(-30, 30);
        direction = Quaternion.AngleAxis(randomAngle, Vector3.up) * -avoidanceDirection.normalized;
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
        if (visibleFoodItems.Count > 0)
        {
            // We can see some food! Pick one at random.
            Collider food = visibleFoodItems[Random.Range(0, visibleFoodItems.Count)];
            return food;
        }

        // Scanners have not detected any food. 
        return null;
    }

    // Scan for food items in robots scan radius.
    private List<Collider> ScanForFood()
    {
        List<Collider> visibleFoodItems = new();

        // OverlapSphere returns all food items that are visible over 360 degrees
        Collider[] foodItemsInRad = Physics.OverlapSphere(transform.position, foodScanRadius, LayerMask.GetMask("FoodItems"));
        for (int i = 0; i < foodItemsInRad.Length; i++)
        {
            // Calculate robot's direction towards the food item.
            Vector3 targetDir = foodItemsInRad[i].transform.position - transform.position;
            targetDir.y = 0;
            float angle = Vector3.Angle(transform.forward, targetDir);
            if (angle <= foodScanFOV)
            {
                visibleFoodItems.Add(foodItemsInRad[i]);
            }
        }

        return visibleFoodItems;
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

        looking = direction + transform.position;
        transform.LookAt(looking);

        // Calcultate and update robots velocity. 
        velocity = direction.normalized * maxSpeed;

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
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.forward + transform.position, transform.forward * 4 + transform.position);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(Quaternion.Euler(0, -foodScanFOV, 0) * transform.forward + transform.position, Quaternion.Euler(0, -foodScanFOV, 0) * transform.forward * foodScanRadius + transform.position);
        Gizmos.DrawLine(Quaternion.Euler(0, foodScanFOV, 0) * transform.forward + transform.position, Quaternion.Euler(0, foodScanFOV, 0) * transform.forward * foodScanRadius + transform.position);

        //Gizmos.DrawWireSphere(transform.position, foodScanRadius);
        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(transform.position, proximityScanRadius);

        //Gizmos.DrawWireSphere(transform.position, grabDistance);
    }
}

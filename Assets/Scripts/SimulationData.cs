using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationData : MonoBehaviour
{
    // Class to keep track of all relevant simulation data for the foraging swarm. 
    public GameObject foodItemPrefab;
    public GameObject robotPrefab;

    int currentSwarmEnergy;
    int foodEnergyValue = 2000;

    // Dictionary of Robot ID to robot state.
    Dictionary<int, int> robotStates = new();
    // Robots!
    List<Robot> robots = new();

    int[] stateEnergyConsumption =
    {
        6, 8, 8, 8, 6, 12, 1, 6, 9
    };

    // Main Menu Inputs
    int robotNumber;
    float pNew;


    int robotsForaging;
    float probabilityNew;

    private float spawnFoodMinDistance = 12;
    private float spawnFoodMaxDistance = 40;

    private float spawnRobotMinDistance = 1;
    private float spawnRobotMaxDistance = 8;

    // Social cue attenuation factor 
    private float successAttenuation = 0.1f;
    private float failureAttenuation = 0.1f;

    void Start()
    {
        probabilityNew = 200;
        robotNumber = MenuInput.NumRobotsInput;
        pNew = MenuInput.ProbabilityNew;
        Debug.Log(robotNumber);
        SpawnRobots(robotNumber);
        StartCoroutine(CollectDataAndSpawn());
    }


    void Update()
    {

        // Social cue pheromone like gradual decay 
        foreach (Robot robot in robots)
        {
            robot.successSocialCue -= successAttenuation * Time.deltaTime;
            if (robot.successSocialCue < 0)
            {
                robot.successSocialCue = 0;
            }

            robot.failureSocialCue -= failureAttenuation * Time.deltaTime;
            if (robot.failureSocialCue < 0)
            {
                robot.failureSocialCue = 0;
            }
        }
    }

    IEnumerator CollectDataAndSpawn()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            // Calculate whether we should place a food item or not
            var random = Random.Range(0, 1000);
            if (random <= probabilityNew)
            {
                Vector3 spawnPos = Random.insideUnitCircle.normalized;
                spawnPos.z = spawnPos.y;
                spawnPos.y = 0.01f;
                spawnPos *= Random.Range(spawnFoodMinDistance, spawnFoodMaxDistance);

                Instantiate(foodItemPrefab, spawnPos, Quaternion.identity);
            }

            // Collecting Robot energy data
            var energyUsed = 0;

            foreach (KeyValuePair<int, int> kvp in robotStates)
            {
                //string message = string.Format("Robot ID = {0}, State = {1}, EnergyConsumed = {2},Time = {3}", kvp.Key, kvp.Value, stateEnergyConsumption[kvp.Value], Time.time.ToString());
                //Debug.Log(message);
                energyUsed += stateEnergyConsumption[kvp.Value];
            }

            currentSwarmEnergy -= energyUsed;

        }
    }

    // Method to initalise a number of robots in the nest. 
    public void SpawnRobots(int numberOfRobots)
    {
        for(int i = 0; i < numberOfRobots; i++)
        {
            // Calculate a random point in nest to spawn.
            Vector3 spawnPos = Random.insideUnitCircle.normalized;
            spawnPos.z = spawnPos.y;
            spawnPos.y = 0.01f;
            spawnPos *= Random.Range(spawnRobotMinDistance, spawnRobotMaxDistance);

            // Insantiate a robot and add to list of robots.
            Robot r = Instantiate(robotPrefab, spawnPos, Quaternion.identity).GetComponent<Robot>();
            robots.Add(r);
        }
    }

    public void UpdateState(int robotId, int robotState)
    {
        robotStates[robotId] = robotState;
    }

    public void DepositFood()
    {
        currentSwarmEnergy += foodEnergyValue;
    }

    public void UseEnergy(int energyUsed)
    {
        currentSwarmEnergy -= energyUsed;
    }

    // Allows robots to tell others if they find food. Success social Cue!
    public void BroadcastSuccess(int informingRobotId)
    {
        foreach(var robot in robots)
        {
            // Check to ensure current key does not represent the informing robot!
            if (robot.GetInstanceID() != informingRobotId)
            {
                // Increment the success value of all other robots.
                robot.IncrementSuccessSocialCue();
            }
        }
    }

    // Allows robots to tell others if they fail to find food. Failure social Cue!
    public void BroadcastFailure(int informingRobotId)
    {
        foreach (var robot in robots)
        {
            // Check to ensure current key does not represent the informing robot!
            if (robot.GetInstanceID() != informingRobotId)
            {
                // Increment the failure value of all other robots.
                robot.IncrementFailureSocialCue();
            }
        }
    }

    private void OnGUI()
    {
        // Make a background box
        GUI.Box(new Rect(10, 10, 200, 50), "Current Swarm Energy");
        // Write Current swarm energy to box
        GUI.Box(new Rect(60, 30, 80, 20), currentSwarmEnergy.ToString());
    }
}

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SimulationData : MonoBehaviour
{
    // Class to keep track of all relevant simulation data for the foraging swarm. 
    public GameObject foodItemPrefab;
    public GameObject robotPrefab;

    // Robots!
    List<Robot> robots = new();

    // Data collection coroutine
    private IEnumerator dataCollectionCoroutine;

    int[] stateEnergyConsumption =
    {
        6, 8, 8, 8, 6, 12, 1, 6, 9
    };
    int foodEnergyValue = 2000;
    float currentSwarmEnergy;
    int currentSearching;

    // Dependent Variables
    int foodItemsProduced;
    int foodItemsCollected;
    List<float> swarmEnergy = new();
    List<int> searchingRecordings = new();
    List<int> timeRecordings = new();

    // Variable used to mimic time in the simulation world in case of speedup
    int simulationTime;

    // Constant used to speedup the simulation
    float speedUpConstant; // Run the sim 10x faster

    // Main Menu Inputs
    int robotNumberInput;
    float pNewInput;
    float simulationRuntimeThreshold;

    string outputFilename = "";

    // Food Spawning
    float probabilityNew;
    private float spawnFoodMinDistance = 12;
    private float spawnFoodMaxDistance = 150;

    // Robot Spawnign
    private float spawnRobotMinDistance = 1;
    private float spawnRobotMaxDistance = 8;

    // Social cue attenuation factor 
    private float successAttenuation = 0.1f;
    private float failureAttenuation = 0.1f;

    void Start()
    {
        simulationRuntimeThreshold = MenuInput.SimulationRuntime;
        robotNumberInput = MenuInput.NumRobotsInput;
        pNewInput = MenuInput.ProbabilityNew;
        speedUpConstant = MenuInput.SpeedUpInput;

        SpawnRobots(robotNumberInput);

        // Calculate value to use as probabilityNew
        probabilityNew = pNewInput * 100;
        Debug.Log(probabilityNew);

        outputFilename = Application.dataPath + "/simulationData.csv";

        dataCollectionCoroutine = CollectDataAndSpawn();
        StartCoroutine(dataCollectionCoroutine);
    }


    void Update()
    {
        if (simulationTime >= simulationRuntimeThreshold)
        {
            // Switch to end of simulation menu / scene
            // Pause the simulation 
            StopCoroutine(dataCollectionCoroutine);
            // Export data as a csv
            WriteCSV();
            // Exit the sim
            Application.Quit();
            Debug.Log("SIMULATION COMPLETE!");
        }


        // Social cue pheromone like gradual decay 
        foreach (Robot robot in robots)
        {
            robot.successSocialCue -= successAttenuation * Time.deltaTime * speedUpConstant;
            if (robot.successSocialCue < 0)
            {
                robot.successSocialCue = 0;
            }

            robot.failureSocialCue -= failureAttenuation * Time.deltaTime * speedUpConstant;
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
            yield return new WaitForSeconds(1f / speedUpConstant);
            // Calculate whether we should place a food item or not
            var random = Random.Range(0, 100);
            if (random <= probabilityNew)
            {
                Vector3 spawnPos = Random.insideUnitCircle.normalized;
                spawnPos.z = spawnPos.y;
                spawnPos.y = 0.01f;
                spawnPos *= Random.Range(spawnFoodMinDistance, spawnFoodMaxDistance);

                Instantiate(foodItemPrefab, spawnPos, Quaternion.identity);
                foodItemsProduced += 1;
            }

            // Collecting Robot energy data
            float energyUsed = 0;
            var robotsSearching = 0;
            foreach (Robot robot in robots)
            {
                var state = robot.GetState();
                
                // Multiply state consumption by effort to account for increased energy costs for
                // higher effort. Robot isn't moving when resting or scanarea, so no cost increase.
                if (state == Robot.States.Resting |
                    state == Robot.States.ScanArea)
                {
                    energyUsed += stateEnergyConsumption[(int)state];
                } else
                {
                    energyUsed += stateEnergyConsumption[(int)state] * robot.effort;
                }
                
                if (state == Robot.States.RandomWalk |
                    state == Robot.States.MoveToFood |
                    state == Robot.States.ScanArea)
                {
                    robotsSearching += 1;
                }

            }

            // Swarm energy and searching robots analytics.
            currentSwarmEnergy -= energyUsed;
            currentSearching = robotsSearching;
            swarmEnergy.Add(currentSwarmEnergy);
            searchingRecordings.Add(currentSearching);

            // 1 Second has passed in simulation time
            simulationTime += 1;
            timeRecordings.Add(simulationTime);
        }
    }

    // Method to initalise a number of robots in the nest. 
    public void SpawnRobots(int numberOfRobots)
    {
        for (int i = 0; i < numberOfRobots; i++)
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
        OrbitRobots.AddRobots(robots);
    }

    public void DepositFood()
    {
        currentSwarmEnergy += foodEnergyValue;
        foodItemsCollected += 1;
    }

    // Allows robots to tell others if they find food. Success social Cue!
    public void BroadcastSuccess(int informingRobotId)
    {
        foreach (var robot in robots)
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

    private void WriteCSV()
    {
        TextWriter tw = new StreamWriter(outputFilename, false);
        tw.WriteLine("Time,Energy,Searching");
        tw.Close();

        tw = new StreamWriter(outputFilename, true);
        for (int i = 0; i < timeRecordings.Count; i++)
        {
            tw.WriteLine(timeRecordings[i] + "," + swarmEnergy[i] + "," + searchingRecordings[i]);
        }
        tw.Close();
    }

    private void OnGUI()
    {
        // Make a background box
        GUI.Box(new Rect(10, 10, 200, 40), "Current Swarm Energy");
        // Write Current swarm energy to box
        GUI.Box(new Rect(60, 30, 80, 20), currentSwarmEnergy.ToString());

        GUI.Box(new Rect(10, 60, 200, 40), "Simulation Time");

        GUI.Box(new Rect(60, 80, 80, 20), simulationTime.ToString());

        GUI.Box(new Rect(10, 110, 200, 40), "Number of Robots Searching");

        GUI.Box(new Rect(60, 130, 80, 20), currentSearching.ToString());
    }
}

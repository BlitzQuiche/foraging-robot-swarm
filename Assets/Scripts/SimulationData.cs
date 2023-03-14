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
    float currentRobotScore;

    // Current mean cue paramter values for the simulation
    float ari;
    float asd;
    float fri;
    float srd;
    float tsrd;
    float tfri;
    float tssi;
    float tfsd;

    // Dependent Variables
    int foodItemsProduced;
    int foodItemsCollected;
    List<float> swarmEnergy = new();
    List<int> searchingRecordings = new();
    List<int> timeRecordings = new();
    List<(float, float, float, float, float, float, float, float)> cueRecordings = new();
    List<float> robotScores = new();

    // Constant used to speedup the simulation
    float speedUpConstant; 
    
    // Time since the begining of the simulation
    float simulationTime;

    // Main Menu Inputs
    int robotNumberInput;
    float pNewInput;
    float simulationRuntimeThreshold;

    string outputFilename = "";
    string outputCueFilename = "";
    string outputFoodFilename = "";

    // Food Spawning
    float probabilityNew;
    private float spawnFoodMinDistance = 25;
    private float spawnFoodMaxDistance = 155;
    private float spawnFoodTime;

    // Robot Spawning
    private float spawnRobotMinDistance = 1;
    private float spawnRobotMaxDistance = 8;

    void Start()
    {
        simulationRuntimeThreshold = MenuInput.SimulationRuntime;
        robotNumberInput = MenuInput.NumRobotsInput;
        pNewInput = MenuInput.ProbabilityNew;
        speedUpConstant = MenuInput.SpeedUpInput;

        spawnFoodTime = 0;

        SpawnRobots(robotNumberInput);

        // Calculate value to use as probabilityNew
        probabilityNew = 1 - pNewInput;
        Debug.Log(probabilityNew);

        Time.timeScale = speedUpConstant;
        Time.fixedDeltaTime *= Time.timeScale; 

        outputFilename = Application.dataPath + "/simulationData.csv";
        outputCueFilename = Application.dataPath + "/simulationCueData.csv";
        outputFoodFilename = Application.dataPath + "/simulationFoodData.txt";

        dataCollectionCoroutine = CollectData();
        StartCoroutine(dataCollectionCoroutine);
    }


    void Update()
    {
        if (simulationTime > simulationRuntimeThreshold)
        {
            // Switch to end of simulation menu / scene
            // Pause the simulation 
            StopCoroutine(dataCollectionCoroutine);
            Time.timeScale = 0;
            // Export data as a csv
            WriteCSV();
            // Exit the sim
            Application.Quit();
            Debug.Log("SIMULATION COMPLETE!");
        }

        // Calculate whether we should place a food item or not
        spawnFoodTime += Time.deltaTime;
        if (spawnFoodTime > 1f)
        {
            if (Random.value >= probabilityNew)
            {
                Vector3 spawnPos = Random.insideUnitCircle.normalized;
                spawnPos.z = spawnPos.y;
                spawnPos *= Random.Range(spawnFoodMinDistance, spawnFoodMaxDistance);
                spawnPos.y = 1f;

                Instantiate(foodItemPrefab, spawnPos, Quaternion.identity);
                foodItemsProduced += 1;
            }
            spawnFoodTime = 0;
        }

        // Collecting Robot energy data and decreasing social cues !
        float energyUsed = 0;
        var robotsSearching = 0;
        float robotScore = 0;
        
        float ari_c = 0;
        float asd_c = 0;
        float fri_c = 0;
        float srd_c = 0;
        float tsrd_c = 0;
        float tfri_c = 0;
        float tssi_c = 0;
        float tfsd_c = 0;
        foreach (Robot robot in robots)
        {

            var state = robot.GetState();
            int energyConsumption = stateEnergyConsumption[(int)state];

            energyUsed += energyConsumption * Time.deltaTime;
 
            if (state != Robot.States.Resting)
            {
                robotsSearching += 1;
            }

            robotScore = robot.selfAssesmentScore;

            ari_c += robot.cueParameters["ari"];
            asd_c += robot.cueParameters["asd"];
            fri_c += robot.cueParameters["fri"];
            srd_c += robot.cueParameters["srd"];
            tsrd_c += robot.cueParameters["tsrd"];
            tfri_c += robot.cueParameters["tfri"];
            tssi_c += robot.cueParameters["tssi"];
            tfsd_c += robot.cueParameters["tfsd"];

        }
        // Swarm energy and searching robots analytics.
        currentSwarmEnergy -= energyUsed;
        currentSearching = robotsSearching;

        currentRobotScore = robotScore / robotNumberInput;

        ari = ari_c / robotNumberInput;
        asd = asd_c / robotNumberInput;
        fri = fri_c / robotNumberInput;
        srd = srd_c / robotNumberInput;
        tsrd = tsrd_c / robotNumberInput;
        tfri = tfri_c / robotNumberInput;
        tssi = tssi_c / robotNumberInput;
        tfsd = tfsd_c / robotNumberInput;
    }

    IEnumerator CollectData()
    {
        while (true)
        {
            // Execute this coroutine once every (scaled) second
            yield return new WaitForSeconds(1f);

            // Log current statistics 
            swarmEnergy.Add(currentSwarmEnergy);
            searchingRecordings.Add(currentSearching);
            cueRecordings.Add((ari, asd, fri, srd, tsrd, tfri, tssi, tfsd));
            robotScores.Add(currentRobotScore);

            // Increment simulation time by 1 second 
            simulationTime += 1f;
            timeRecordings.Add((int)simulationTime);
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

            // Set robot cue parameters to random values !
            r.cueParameters["ari"] = Random.Range(1, 15);
            r.cueParameters["asd"] = Random.Range(1, 15);
            r.cueParameters["fri"] = Random.Range(10, 30);
            r.cueParameters["srd"] = Random.Range(10, 30);
            r.cueParameters["tsrd"]= Random.Range(10, 30);
            r.cueParameters["tfri"]= Random.Range(30, 50);
            r.cueParameters["tssi"]= Random.Range(5, 30);
            r.cueParameters["tfsd"]= Random.Range(10, 30);

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

    public void Transfer(int informingRobotId, float recievedAssesment, (string, float)[] recievedParameters)
    {
        foreach (var robot in robots)
        {
            // Broadcast to to other resting robots our current self assesment score and parameters
            if (robot.GetInstanceID() != informingRobotId & robot.GetState() == Robot.States.Resting)
            {
                robot.RecieveSocialTransfer(recievedAssesment, recievedParameters);
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

        TextWriter tw2 = new StreamWriter(outputFoodFilename, false);
        tw2.WriteLine("Produced,Collected");
        tw2.WriteLine(foodItemsProduced + "," + foodItemsCollected);
        tw2.Close();

        TextWriter tw3 = new StreamWriter(outputCueFilename, false);
        tw3.WriteLine("Time,ari,asd,fri,srd,tsrd,tfri,tssi,tfsd,score");
        for (int i = 0; i < timeRecordings.Count; i++)
        {
            tw3.WriteLine(timeRecordings[i] + "," + cueRecordings[i].Item1 + "," + cueRecordings[i].Item2 + "," + cueRecordings[i].Item3 + "," + cueRecordings[i].Item4 + "," + cueRecordings[i].Item5 + "," + cueRecordings[i].Item6 + "," + cueRecordings[i].Item7 + "," + cueRecordings[i].Item8 + "," + robotScores[i]);
        }
        tw3.Close();

    }

    private void OnGUI()
    {
        // Make a background box
        GUI.Box(new Rect(10, 10, 200, 40), "Current Swarm Energy");
        // Write Current swarm energy to box
        GUI.Box(new Rect(60, 30, 80, 20), ((int)currentSwarmEnergy).ToString());

        GUI.Box(new Rect(10, 60, 200, 40), "Simulation Time");

        GUI.Box(new Rect(60, 80, 80, 20), simulationTime.ToString());

        GUI.Box(new Rect(10, 110, 200, 40), "Number of Robots Searching");

        GUI.Box(new Rect(60, 130, 80, 20), currentSearching.ToString());
    }
}

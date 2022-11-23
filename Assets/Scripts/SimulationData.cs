using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationData : MonoBehaviour
{
    // Class to keep track of all relevant simulation data for the foraging swarm. 
    public GameObject foodItemPrefab;

    int currentSwarmEnergy;
    int foodEnergyValue = 2000;

    // Dictionary of Robot ID to robot state.
    Dictionary<int, int> robotStates = new();

    int[] stateEnergyConsumption =
    {
        6, 8, 8, 8, 6, 12, 1, 6, 9
    };

    int robotsForaging;

    int ProbabilityNew;

    private float spawnMinDistance = 12;
    private float spawnMaxDistance = 40;

    void Start()
    {
        ProbabilityNew = 200;
        StartCoroutine(CollectDataAndSpawn());
    }


    void Update()
    {

    }

    IEnumerator CollectDataAndSpawn()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            // Calculate whether we should place a food item or not
            var random = Random.Range(0, 1000);
            if (random <= ProbabilityNew)
            {
                Vector3 spawnPos = Random.insideUnitCircle.normalized;
                spawnPos.z = spawnPos.y;
                spawnPos.y = 0.01f;
                spawnPos *= Random.Range(spawnMinDistance, spawnMaxDistance);

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

    private void OnGUI()
    {
        // Make a background box
        GUI.Box(new Rect(10, 10, 200, 50), "Current Swarm Energy");
        // Write Curretn swarm energy to box
        GUI.Box(new Rect(60, 30, 80, 20), currentSwarmEnergy.ToString());
    }
}

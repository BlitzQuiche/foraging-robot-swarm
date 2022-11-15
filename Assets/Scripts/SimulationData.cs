using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationData : MonoBehaviour
{
    // Class to keep track of all relevant simulation data for the foraging swarm. 
    public GameObject foodItemPrefab;

    static int currentSwarmEnergy;
    static int foodEnergyValue = 2000;

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
                spawnPos.y = 0.1f;
                spawnPos *= Random.Range(spawnMinDistance, spawnMaxDistance);

                Instantiate(foodItemPrefab, spawnPos, Quaternion.identity);
            }
        }
    }

    public static void DepositFood()
    {
        currentSwarmEnergy += foodEnergyValue;
    }

    public static void UseEnergy(int energyUsed)
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

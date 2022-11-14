using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationData : MonoBehaviour
{
    // Class to keep track of all relevant simulation data for the foraging swarm. 

    static int currentSwarmEnergy;
    static int foodEnergyValue = 2000;

    float time;

    int robotsForaging;
    
    



    void Start()
    {
        
    }

    
    void Update()
    {
        
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

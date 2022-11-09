using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FoodItem : MonoBehaviour
{   
    // Retrieval of food will deliver 2000 units of energy to the swarm. 
    int EnergyValue = 2000;

    private void Start()
    {
        gameObject.layer = (int) Robot.Layers.FoodItems;
        Debug.Log(gameObject.layer.ToString());
    }
}

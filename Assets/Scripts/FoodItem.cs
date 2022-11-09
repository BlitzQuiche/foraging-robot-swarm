using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FoodItem : MonoBehaviour
{   
    // Retrieval of food will deliver 2000 units of energy to the swarm. 
    int EnergyValue = 2000;

    private Rigidbody rb;
    public Rigidbody Rb => rb;

    private void Start()
    {
        // Set fooditem to the fooditem layer
        gameObject.layer = (int) Robot.Layers.FoodItems;
        Debug.Log(gameObject.layer.ToString());

        // Set RigidBody
        rb = GetComponent<Rigidbody>();
    }
}

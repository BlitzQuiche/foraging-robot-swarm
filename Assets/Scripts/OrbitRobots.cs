using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class OrbitRobots : MonoBehaviour
{
    static List<Transform> robotTransforms;
    static Transform robotInView;
    int index = 0;

    float turnSpeed = 4.0f;
    float height = 10f;
    float distance = 30f;

    Vector3 offsetX;
    Vector3 offsetY;

    void Start()
    {
        offsetX = new Vector3(0, height, distance);
        robotTransforms = new List<Transform>();
    }

    private void LateUpdate()
    {
        if (robotTransforms.Any())
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                index += 1;
                if (index >= robotTransforms.Count) index = 0;
                robotInView = robotTransforms[index];
            }

            if (robotInView != null)
            {
                offsetX = Quaternion.AngleAxis(Input.GetAxis("Mouse X") * turnSpeed, Vector3.up) * offsetX;
                transform.position = robotInView.position + offsetX;
                transform.LookAt(robotInView.position);
            }
        }
    }

    public static void AddRobots(List<Robot> robots)
    {
        foreach (Robot r in robots)
        {
            robotTransforms.Add(r.transform);
        }
        robotInView = robotTransforms[0];
    }



}

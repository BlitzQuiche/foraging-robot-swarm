using UnityEngine;

public class MenuInput : MonoBehaviour
{
    public static int NumRobotsInput = 6;
    public static float ProbabilityNew = 0.03f;


    public void ParseNumberOfRobotsString(string s)
    {
        try
        {
            NumRobotsInput = int.Parse(s);
        }
        catch (System.FormatException)
        {
            NumRobotsInput = 6;
            Debug.Log("Robot Number Input Error: Enter an Integer value");
        }

        Debug.Log($"Number Of Robots Input: {NumRobotsInput}");
    }

    public void ParseProbabilityNewString(string s)
    {
        try
        {
            ProbabilityNew = float.Parse(s);
        }
        catch (System.FormatException)
        {
            ProbabilityNew = 0.03f;
            Debug.Log("Probability Input Error: Enter an decimal value between 0 and 1");
        }

        if (ProbabilityNew > 1 | ProbabilityNew < 0)
        {
            ProbabilityNew = 0.03f;
        }

        Debug.Log($"Probability New Input: {ProbabilityNew}");
    }
}

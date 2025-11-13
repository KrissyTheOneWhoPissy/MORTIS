using UnityEngine;

public static class PersistentSceneCleaner
{
    public static void DisablePersistentFloor()
    {
        var plane = GameObject.FindGameObjectWithTag("PersistentFloor");
        if (plane) plane.SetActive(false);
    }
}
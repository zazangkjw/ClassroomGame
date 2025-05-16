using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FindMissingScript : MonoBehaviour
{
    string getObjectHierarchy(GameObject go)
    {
        string path = go.name;
        Transform tr = go.transform;

        while (tr.parent != null)
        {
            path = tr.parent.name + " / " + path;
            tr = tr.parent;
        }

        return path;
    }

    void Start()
    {
        GameObject[] all = FindObjectsOfType<GameObject>();

        foreach (GameObject go in all)
        {
            Component[] components = go.GetComponents<Component>();

            foreach (Component c in components)
            {
                if (c == null)
                {
                    string fullPath = getObjectHierarchy(go);
                    Debug.Log(fullPath + " has missing script!");
                }
            }
        }
    }
}
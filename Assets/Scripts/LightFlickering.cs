using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightFlickering : MonoBehaviour
{
    Light flickeringLight;
    float intensity;

    public float minIntensity;
    public float delay = 0.1f;
    WaitForSeconds flickeringDelay;

    void Start()
    {
        flickeringLight = GetComponent<Light>();
        intensity = flickeringLight.intensity;
        flickeringDelay = new WaitForSeconds(delay);
        StartCoroutine(FlickeringCoroutine());
    }

    IEnumerator FlickeringCoroutine()
    {
        while (true)
        {
            yield return flickeringDelay;
            flickeringLight.intensity = Random.Range(minIntensity, intensity);
        }
    }
}

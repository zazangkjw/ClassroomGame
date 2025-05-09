using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class LightFlickering : MonoBehaviour
{
    Light flickeringLight;
    HDAdditionalLightData flickeringLightHdrp;
    float intensity;

    public bool hdrp;
    public float minIntensity;
    public float delay = 0.1f;
    WaitForSeconds flickeringDelay;

    void Start()
    {
        if (hdrp)
        {
            flickeringLightHdrp = GetComponent<HDAdditionalLightData>();
            intensity = flickeringLightHdrp.intensity;
        }
        else
        {
            flickeringLight = GetComponent<Light>();
            intensity = flickeringLight.intensity;
        }

        flickeringDelay = new WaitForSeconds(delay);
        StartCoroutine(FlickeringCoroutine());
    }

    IEnumerator FlickeringCoroutine()
    {
        while (true)
        {
            yield return flickeringDelay;
            if (hdrp)
            {
                flickeringLightHdrp.intensity = Random.Range(minIntensity, intensity);
            }
            else
            {
                flickeringLight.intensity = Random.Range(minIntensity, intensity);
            }
        }
    }
}

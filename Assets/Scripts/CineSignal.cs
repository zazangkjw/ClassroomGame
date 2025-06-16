using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CineSignal : MonoBehaviour
{
    [SerializeField] Transform pos;

    public void MainCamPos()
    {
        CameraFollow.Singleton.transform.position = pos.position;
    }

    public void TurnOffMainCam()
    {
        CameraFollow.Singleton.gameObject.SetActive(false);
    }

    public void TurnOnMainCam()
    {
        CameraFollow.Singleton.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }

    public void Black()
    {
        UIManager.Singleton.Black();
    }

    public void UnBlack()
    {
        UIManager.Singleton.UnBlack();
    }
}




    

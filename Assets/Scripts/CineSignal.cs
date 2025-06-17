using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

public class CineSignal : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private Transform pos;
    [SerializeField] private float skipPoint;

    public void SetSkipPoint(float t)
    {
        skipPoint = t;
    }

    public void Skip()
    {
        StartCoroutine(SkipCoroutine());
    }

    private IEnumerator SkipCoroutine()
    {
        UIManager.Singleton.SkipText.SetActive(true);

        while (true)
        {
            if (Input.GetKey(KeyCode.Escape))
            {
                director.Pause();
                UIManager.Singleton.SkipText.SetActive(false);
                director.time = skipPoint;
                director.Play();
                break;
            }

            if (director.time > skipPoint)
            {
                UIManager.Singleton.SkipText.SetActive(false);
                break;
            }

            yield return null;
        }
    }

    public void MainCamPos()
    {
        CameraFollow.Singleton.transform.position = pos.position;
        CameraFollow.Singleton.transform.rotation = pos.rotation;
    }

    public void SelectModeScreen()
    {
        UIManager.Singleton.SelectModeScreen.SetActive(true);
    }
    public void TurnOnMainCam()
    {
        CameraFollow.Singleton.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }

    public void TurnOffMainCam()
    {
        CameraFollow.Singleton.gameObject.SetActive(false);
    }

    public void CursorOn()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void CursorOff()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Black()
    {
        UIManager.Singleton.Black();
    }

    public void UnBlack()
    {
        UIManager.Singleton.UnBlack();
    }

    public void TitleDisappear()
    {
        UIManager.Singleton.TitleDisappear();
    }
}




    

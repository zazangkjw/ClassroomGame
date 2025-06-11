using Fusion;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

public class GameLogicLobby : NetworkBehaviour
{
    [SerializeField] private Collider[] readyChairColliders;
    [SerializeField] private Light thunderLight;
    [SerializeField] private AudioSource thunderSfx;
    [SerializeField] private VideoPlayer countdownVideo;
    [SerializeField] private TextMeshProUGUI projectorText;

    [Networked] private TickTimer ThunderTickTimer { get; set; }
    [Networked, OnChangedRender(nameof(CountDown))] private bool IsCountdown { get; set; }

    private Coroutine thunderRoutine;
    private WaitForSeconds oneSeconds = new WaitForSeconds(1);
    private WaitForSeconds thunderDelay1 = new WaitForSeconds(5f);
    private WaitForSeconds thunderDelay2 = new WaitForSeconds(0.1f);

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);
        countdownVideo.loopPointReached += OnCountdownEnd;

        if (Runner.IsServer && !UIManager.Singleton.IsFirstJoin)
        {
            GameStateManager.Singleton.SpawnCharacter();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (thunderRoutine != null)
        {
            StopCoroutine(thunderRoutine);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && Runner.IsForward)
        {
            CheckReady();
        }

        Thunder();
        UpdateProjectorText();
    }

    public void CheckReady()
    {
        foreach (var player in GameStateManager.Singleton.Players)
        {
            if (!player.Value.IsReady)
            {
                if (IsCountdown)
                {
                    IsCountdown = false;
                    CountDown();
                }

                return;
            }
        }
        
        if (!IsCountdown)
        {
            IsCountdown = true;
            CountDown();
        }
    }

    private void CountDown()
    {
        if (IsCountdown)
        {
            projectorText.gameObject.SetActive(false);
            countdownVideo.gameObject.SetActive(true);
            countdownVideo.Play();
        }
        else
        {
            projectorText.gameObject.SetActive(true);
            countdownVideo.gameObject.SetActive(false);
            countdownVideo.Stop();
        }
    }

    private void OnCountdownEnd(VideoPlayer vp)
    {
        foreach (var col in readyChairColliders)
        {
            col.enabled = false;
        }

        UIManager.Singleton.CloseAllUI();

        // ¾À ÀüÈ¯
        if (HasStateAuthority)
        {
            Debug.Log(GameStateManager.Singleton.SelectedVideoIndex);
            UIManager.Singleton._MenuConnection.CurrentLobby.SetPrivate();
            Runner.LoadScene(UIManager.Singleton.VideoListOriginal[GameStateManager.Singleton.SelectedVideoIndex]);
        }
    }

    private void Thunder()
    {
        if (ThunderTickTimer.ExpiredOrNotRunning(Runner))
        {
            if (thunderRoutine != null)
            {
                StopCoroutine(thunderRoutine);
            }
            thunderRoutine = StartCoroutine(ThunderRoutine());

            if (HasStateAuthority)
            {
                ThunderTickTimer = TickTimer.CreateFromSeconds(Runner, 10f);
            }
        }
    }

    private IEnumerator ThunderRoutine()
    {
        thunderLight.enabled = true;
        yield return thunderDelay2;
        yield return thunderDelay2;
        yield return thunderDelay2;
        thunderLight.enabled = false;
        yield return thunderDelay2;
        thunderLight.enabled = true;
        yield return thunderDelay2;
        yield return thunderDelay2;
        thunderLight.enabled = false;

        yield return oneSeconds;
        thunderSfx.Play();
        thunderRoutine = null;
    }

    private void UpdateProjectorText()
    {
        if (projectorText.text != UIManager.Singleton.VideoList[GameStateManager.Singleton.SelectedVideoIndex])
        {
            projectorText.text = UIManager.Singleton.VideoList[GameStateManager.Singleton.SelectedVideoIndex];
        }
    }
}

using Fusion;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

public class GameLogicLivingRoom : NetworkBehaviour
{
    public NetworkPrefabRef PlayerPrefab;

    [SerializeField] private Light thunderLight;
    [SerializeField] private AudioSource thunderSfx;

    [Networked] private TickTimer ThunderTickTimer { get; set; }

    private Coroutine thunderRoutine;
    private WaitForSeconds oneSeconds = new WaitForSeconds(1);
    private WaitForSeconds thunderDelay = new WaitForSeconds(0.1f);

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);

        if (Runner.IsServer && !UIManager.Singleton.IsFirstJoin)
        {
            GameStateManager.Singleton.SpawnCharacter(PlayerPrefab);
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
            
        }

        Thunder();
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
                ThunderTickTimer = TickTimer.CreateFromSeconds(Runner, 30f);
            }
        }
    }

    private IEnumerator ThunderRoutine()
    {
        thunderLight.enabled = true;
        yield return thunderDelay;
        yield return thunderDelay;
        yield return thunderDelay;
        thunderLight.enabled = false;
        yield return thunderDelay;
        thunderLight.enabled = true;
        yield return thunderDelay;
        yield return thunderDelay;
        thunderLight.enabled = false;

        yield return oneSeconds;
        thunderSfx.Play();
        thunderRoutine = null;
    }
}

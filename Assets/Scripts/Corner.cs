using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Corner : SimulationBehaviour
{
    public int Number;
    public List<CornerGamePlayer> ThisCornerPlayers = new();
    public TextMeshProUGUI CornerText;

    [SerializeField] private GameLogicCornerGame gameLogicCornerGame;

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.parent.TryGetComponent(out CornerGamePlayer player))
        {
            ThisCornerPlayers.Add(player);

            if (!gameLogicCornerGame.IsStarted)
            {
                CornerText.text = $"{ThisCornerPlayers.Count} / {(Number == 0 ? 2 : 1)}";
            }
            else
            {
                if (player.IsTagger && player.Goal == Number)
                {
                    ThisCornerPlayers[0].Goal = (Number + 1) % 4;
                    ThisCornerPlayers[0].IsTagger = true;
                    player.IsTagger = false;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.transform.parent.TryGetComponent(out CornerGamePlayer cornerGamePlayer))
        {
            ThisCornerPlayers.Remove(cornerGamePlayer);

            if (!gameLogicCornerGame.IsStarted)
            {
                CornerText.text = $"{ThisCornerPlayers.Count} / {(Number == 0 ? 2 : 1)}";
            }
            else
            {
                if (!cornerGamePlayer.IsTagger && cornerGamePlayer.TryGetComponent(out Player player) && player.HasStateAuthority)
                {
                    player.TeleportQueue.Enqueue((transform.position, cornerGamePlayer.transform.rotation, true, true));
                }
            }
        }
    }
}

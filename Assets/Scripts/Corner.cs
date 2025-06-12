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
            if (gameLogicCornerGame.IsStarted)
            {
                if (player.IsTagger && player.Goal == Number)
                {
                    ThisCornerPlayers[0].Goal = (Number + 1) % 4;
                    ThisCornerPlayers[0].IsTagger = true;
                    player.IsTagger = false;
                }
            }

            ThisCornerPlayers.Add(player);
            CornerText.text = $"{ThisCornerPlayers.Count} / {(Number == 0 ? 2 : 1)}";
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.transform.parent.TryGetComponent(out CornerGamePlayer player))
        {
            ThisCornerPlayers.Remove(player);
            CornerText.text = $"{ThisCornerPlayers.Count} / {(Number == 0 ? 2 : 1)}";

            if (gameLogicCornerGame.IsStarted)
            {
                if (!player.IsTagger)
                {
                    player.GetComponent<Player>().Teleport(transform.position, player.transform.rotation);
                }
            }
        }
    }
}

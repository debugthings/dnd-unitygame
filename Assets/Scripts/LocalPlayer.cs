using Photon.Realtime;
using UnityEngine;

public class LocalPlayer : LocalPlayerBase<Player>
{
    public override void PlayerLeftGame()
    {
    }
    
    public void ItsYourTurn(bool toggle)
    {
        var canvas = GetComponentInChildren<Canvas>(true);
        canvas.gameObject.SetActive(toggle);
    }

    public void DimCardsThatCantBePlayed(bool toggle, Card currentCard)
    {
        foreach (var item in Hand)
        {
            if (!item.CanPlay(currentCard))
            {
                item.Dim(toggle);
            }
        }
    }
}
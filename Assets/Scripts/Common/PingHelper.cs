using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public static class PingHelper
{
    private static float pingRate = 1.0f;
    private static float pingAccumulator = 0.0f;

    static public void Ping(float delta)
    {
        pingAccumulator += delta;
        if (pingAccumulator > pingRate)
        {
            PhotonNetwork.SendAllOutgoingCommands();
            pingAccumulator = 0.0f;
        }
    }
}
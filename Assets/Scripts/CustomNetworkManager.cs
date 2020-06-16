using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
//using UnityEngine.SceneManagement;

/// <summary> Clase que hereda de NetworkManager y que se ocupa del comportamiento del servidor cuando se queda un jugador sólo en la partida. </summary>
public class CustomNetworkManager : NetworkManager
{
    public PolePositionManager polePositionManager;

    /// <summary> Override de la función OnServerDisconnect de la clase NetworkManager.
    /// <para> Cuando detecta que sólo queda un jugador en una partida ya comenzada que admite más de un jugador, la finaliza prematuramente.</para>
    /// </summary>
    public override void OnServerDisconnect(NetworkConnection conn)
    {
        base.OnServerDisconnect(conn);
        if (numPlayers == 1 && polePositionManager != null && (polePositionManager.countdown < polePositionManager.MAXCOUNTDOWN) && polePositionManager.Player_Count > 1)
        {
            polePositionManager.FinishGame();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System;
//using UnityEngine.SceneManagement;

/// <summary> Clase que hereda de NetworkManager y que se ocupa del comportamiento del servidor cuando se queda un jugador sólo en la partida. </summary>
public class CustomNetworkManager : NetworkManager
{
    //Esto no es escalable
    private bool[] usedPositions = new bool[4];
    private int[] startingIds = new int[4];
    private bool closing;
    public PolePositionManager polePositionManager;

    /// <summary> Override de la función OnServerDisconnect de la clase NetworkManager.
    /// <para> Cuando detecta que sólo queda un jugador en una partida ya comenzada que admite más de un jugador, la finaliza prematuramente.</para>
    /// </summary>
    public override void OnServerDisconnect(NetworkConnection conn)
    {
        base.OnServerDisconnect(conn);

        for (int i = 0; i < 4; i++)
        {
            if (startingIds[i] == conn.connectionId)
            {
                usedPositions[i] = false;
                break;
            }
        }

        if (!closing && numPlayers == 1 && polePositionManager != null && (polePositionManager.countdown < polePositionManager.MAXCOUNTDOWN) && polePositionManager.Player_Count > 1)
        {
            polePositionManager.FinishGame();
        }
    }

    /// <summary> Override de la función OnStopServer de la clase NetworkManager.
    /// <para> Cuando el servidor se está cerrando, evita que se mande el mensaje de victoria al desconectar a los clientes. </para> </summary>
    public override void OnStopServer() 
    {
        closing = true;
    }
    
    /// <summary> Override de la función OnServerAddPlayer de la clase NetworkManager.
    /// <para> Cuando se añade un jugador a la escena, se colocará en la primera posición libre. </para> </summary>
    public override void OnServerAddPlayer(NetworkConnection conn)
        {
            Transform startPos = null;
            int pos = 0;
            while (pos < 4)
            {
                if (!usedPositions[pos])
                {
                    startPos = startPositions[pos];
                    usedPositions[pos] = true;
                    startingIds[pos] = conn.connectionId;
                    break;
                }
                pos++;
            }
            GameObject player = startPos != null
                ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                : Instantiate(playerPrefab);

            NetworkServer.AddPlayerForConnection(conn, player);
        }

        /// <summary> Override de la función OnServerConnect de la clase NetworkManager.
        /// <para> Si se intenta unir un jugador pero la partida ya ha comenzado, se le desconectará inmediatamente. </para> </summary>
        public override void OnServerConnect(NetworkConnection conn) {
            if (polePositionManager.countdownStarted)
                conn.Disconnect();
        }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
//using UnityEngine.SceneManagement;

public class CustomNetworkManager : NetworkManager
{
    public PolePositionManager polePositionManager;
    public override void OnServerConnect(NetworkConnection conn)
    {
        base.OnServerConnect(conn);
        print("Se unio alguien. Ahora son " + numPlayers);
    }
    public override void OnServerDisconnect(NetworkConnection conn)
    {
        base.OnServerDisconnect(conn);
        print("Se fue alguien. Ahora son " + numPlayers + ". esto viene de " + conn.connectionId);
        if (numPlayers == 1 && polePositionManager != null)
        {
            polePositionManager.FinishGame();
        }

        //print("Se fue alguien. Ahora son " + numPlayers);
        /*if (numPlayers == 0)
        {
            StopServer();
        }*/
    }
    /*public override void OnStopServer()
    {
        base.OnStopServer();
        //Destroy(this.gameObject); //No tan importante destruirlo porque total se va a destruir cuando vea que hay uno nuevo o igual no eh
    }*/
    /*public override void OnDestroy()
    {
        base.OnDestroy();
        //StopServer(); //No se si funcionará bien con server only
        //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }*/

    /*public override void OnClientDisconnect(NetworkConnection conn)
    {
        base.OnClientDisconnect(conn);
        StopServer();
    }*/

}

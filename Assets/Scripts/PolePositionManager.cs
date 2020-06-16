using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;
using System.Threading;
using System.Diagnostics;
using UnityEngine.UI;

/// <summary> Clase que maneja casi toda la lógica interna del juego. </summary>
public class PolePositionManager : NetworkBehaviour
{
    //public int numPlayers;
    public readonly float MAXCOUNTDOWN = 3;
    public CustomNetworkManager networkManager;
    public UIManager UI_m;
    public Transform cameraPosition;
    public float countdown;
    public bool gameStarted, timerStarted;
    public Transform[] checkPoints;
    public Dropdown Drop_Players;

    private readonly List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    private CircuitController m_CircuitController;
    private GameObject[] m_DebuggingSpheres;
    private SetupPlayer m_LocalSetupPlayer;
    private Stopwatch timer;
    private int oldPlayersLeft = -1;

    [SyncVar] public bool countdownStarted;
    [SyncVar] public int Player_Count = 1;
    
    /// <summary> En Awake se asigna al countdown inicial su valor máximo, y se inicializa timer como un objeto de la clase Stopwatch. </summary>
    private void Awake()
    {
        countdown = MAXCOUNTDOWN;
        if (networkManager == null) networkManager = FindObjectOfType<CustomNetworkManager>();
        networkManager.polePositionManager = this;
        if (m_CircuitController == null) m_CircuitController = FindObjectOfType<CircuitController>();

        m_DebuggingSpheres = new GameObject[networkManager.maxConnections];
        for (int i = 0; i < networkManager.maxConnections; ++i)
        {
            m_DebuggingSpheres[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_DebuggingSpheres[i].GetComponent<SphereCollider>().enabled = false;
        }
        timer = new Stopwatch();
    }
    
    /// <summary> En Update, si la partida no ha comenzado, se muestra un texto si faltan jugadores o se actualiza la cuenta atrás una vez están todos conetados.
    /// <para> Además, se llama a la función UpdateRaceProgress. </para> </summary>
    private void Update()
    {
        if (!gameStarted)
        {
            if (m_Players.Count <= Player_Count-1 && countdown >= MAXCOUNTDOWN)
            {
                int playersLeft = Player_Count-m_Players.Count;
                if(UI_m.GetCountDown()=="" || playersLeft != oldPlayersLeft)
                {
                    oldPlayersLeft = playersLeft;
                    UI_m.SetCountDown("Waiting for " + playersLeft + "\n"+" player" + ((playersLeft == 1)?"":"s"));
                }
            }

            else if (countdown > -1.5)
            {
                countdown -= Time.deltaTime;
                if (countdown > 0)
                    UI_m.EditCountDown(countdown);
                else if (countdown <= 0)
                {
                    UI_m.SetCountDown("GO!");
                    if (!timerStarted)
                    {
                        if (isServer)
                        {
                            RpcStartGame();
                            timer.Start();
                            countdownStarted = true;
                        }
                        timerStarted = true;
                    }
                }
            }
            else
            {
                UI_m.SetCountDown("");
                gameStarted = true;
            }
        }
        UpdateRaceProgress();
    }

    /// <summary> Añade un PlayerInfo a m_Players. </summary>
    public void AddPlayer(PlayerInfo player)
    {
        m_Players.Add(player);
    }

    /// <summary> Asigna la referencia al SetupPlayer que representa al jugador local. </summary>
    public void SetLocalPlayer(SetupPlayer sp)
    {
        m_LocalSetupPlayer = sp;
        m_LocalSetupPlayer.SetCheckPoints(checkPoints);
    }

    /// <summary> Clase que sirve para comparar objetos de la clase PlayerInfo utilizando floats. </summary>
    private class PlayerInfoComparer : Comparer<PlayerInfo>
    {
        float[] m_ArcLengths;

        public PlayerInfoComparer(float[] arcLengths)
        {
            m_ArcLengths = arcLengths;
        }

        public override int Compare(PlayerInfo x, PlayerInfo y)
        {
            if (this.m_ArcLengths[x.CurrentPosition] < (m_ArcLengths[y.CurrentPosition]))
                return 1;
            else return -1;
        }
    }
    
    /// <summary> Toma un array ordenado y lo muestra por pantalla. </summary>
    public void UpdateRaceProgress()
    {
        PlayerInfo[] arr = SortPlayers();

        string myRaceOrder = "";
        foreach (var _player in arr)
        {
            myRaceOrder += _player.Name + "\n";
        }

        UI_m.SetTextPosition(myRaceOrder);
        
        if (m_LocalSetupPlayer != null)
        {
            UI_m.SetCurTime(m_LocalSetupPlayer.GetPlayerInfo(), timer.Elapsed);
        }
    }

    /// <summary> Calcula la posición del jugador en el recorrido si es el jugador local, y la toma directamente si se trata de otro jugador.
    /// <para> En caso de que un jugador se haya desconectado, devuelve un valor para indicar que ese jugador debe ser eliminado. </para> </summary>
    float ComputeCarArcLength(int ID)
    {
        // Compute the projection of the car position to the closest circuit 
        // path segment and accumulate the arc-length along of the car along
        // the circuit.
        if (this.m_Players[ID] == null)
        {
            return -999999;
        }
        else if (m_LocalSetupPlayer != null)
        {
            if (this.m_Players[ID].ID != m_LocalSetupPlayer.GetPlayerInfo().ID)
            {
                return m_Players[ID].controller.arcLength;
            }
        }
        else
        {
            return m_Players[ID].controller.arcLength;
        }
        Vector3 carPos = this.m_Players[ID].transform.position;

        int segIdx;
        float carDist;
        Vector3 carProj;

        float minArcL =
            this.m_CircuitController.ComputeClosestPointArcLength(carPos, out segIdx, out carProj, out carDist);
        
        if (m_Players[ID].segIdx == 23 && segIdx < 17)
        {
            changeLap(this.m_Players[ID]);
        }

        m_Players[ID].segIdx = segIdx;

        this.m_DebuggingSpheres[ID].transform.position = carProj;

        

        bool isBehind = false;
        if (this.m_Players[ID].CheckPoint == 1 || (this.m_Players[ID].CheckPoint != 1 && !this.m_Players[ID].CanChangeLap))
        {
            if (segIdx > 3)
                isBehind = true;
        }

        if (isBehind)
        {
            if (this.m_Players[ID].controller.CurrentLap-1 == 0)
            {
                minArcL -= m_CircuitController.CircuitLength;
            }
            else
            {
                minArcL += m_CircuitController.CircuitLength * (m_Players[ID].controller.CurrentLap - 2);
            
            }    
        }
        else if (this.m_Players[ID].controller.CurrentLap == 0)
        {
            minArcL -= m_CircuitController.CircuitLength;
        }
        else
        {
            minArcL += m_CircuitController.CircuitLength *
                       (m_Players[ID].controller.CurrentLap - 1);
        }

        //Command para que el server asigne la SyncVar minArcL
        m_LocalSetupPlayer.m_PlayerController.CmdUpdateArcLength(minArcL);
        return minArcL;

    }
    
    /// <summary> Función que, si el jugador puede cambiar de vuelta y está en el checkpoint 0, incrementa en 1 el contador de vueltas, 
    /// guarda los tiempos necesarios, y finaliza la partida si ha terminado la última vuelta. </summary>
    public void changeLap(PlayerInfo player)
    {
        if (player.CanChangeLap)
        {
            if (player.CheckPoint == 0)
            {
                switch (player.controller.CurrentLap)
                {
                    case 1:
                        if (player.controller.isLocalPlayer)
                            player.controller.CmdChangeTimes(0, timer.Elapsed.Ticks);
                        player.time1 = timer.Elapsed;
                        break;
                        
                    case 2:
                        if (player.controller.isLocalPlayer)
                            player.controller.CmdChangeTimes(1, timer.Elapsed.Ticks);
                        player.time2 = timer.Elapsed;
                        break;
                        
                    case 3:
                        timer.Stop();
                        if (player.controller.isLocalPlayer)
                            player.controller.CmdChangeTimes(2, timer.Elapsed.Ticks);
                        player.time3 = timer.Elapsed;
                        if (player.controller.isLocalPlayer)
                            FinishGame();
                        break;
                }
                if (player.controller.isLocalPlayer)
                {
                    player.controller.CmdIncreaseLap();
                    player.CanChangeLap = false;
                }
            }
        }
    }
    
    /// <summary> Función que se ocupa de que tanto servidor como clientes terminen la partida correctamente. </summary>
    public void FinishGame()
    {
        if (isServerOnly)
        {
            RpcFinishGame();
            UI_m.FadeOut();
        }
        else
        {
            m_LocalSetupPlayer.CmdFinishGame();
        }
    }


    /// <summary> Función que se ejecuta tras el fadeout y que activa la interfaz de la pantalla final. 
    /// <para> En el caso del servidor, calcula además los tiempos de todos los jugadores y los envía a todos los clientes. </para> </summary>
    public void PostFadeOut()
    {
        UI_m.SetEndingUI();

        if (isServer)
        {
            string names = "Players\n\n";
            string lap1 = "Lap 1\n\n";
            string lap2 = "Lap 2\n\n";
            string lap3 = "Lap 3\n\n";
            string total = "Total\n\n";
            string t1, t2, t3, tt, bt;
            string[] laps = new string[3];
            string bestLap = "Best Lap\n\n";
            TimeSpan ts, bestTs;
            TimeSpan zeroTs = new TimeSpan(0);

            PlayerInfo[] playerArray = SortPlayers();

            foreach (PlayerInfo p in playerArray)
            {
                ts = p.time1;
                bestTs = ts;
                if (p.time1 > zeroTs)
                    t1 = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
                else
                    t1 = "--:--.---";

                ts = p.time2 - p.time1;
                if (ts < bestTs && ts > zeroTs)
                    bestTs = ts;

                if (p.time1 > zeroTs && p.time2 > zeroTs)
                    t2 = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
                else
                    t2 = "--:--.---";
                
                ts = p.time3 - p.time2;
                if (ts < bestTs && ts > zeroTs)
                    bestTs = ts;

                if (p.time2 > zeroTs && p.time3 > zeroTs)
                {
                    print("entro aqui");
                    t3 = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
                }
                else
                    t3 = "--:--.---";
                
                if (p.time3 > zeroTs)
                {
                    ts = p.time3;
                    tt = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
                }
                else
                {
                    ts = timer.Elapsed;
                    tt = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
                }

                if (bestTs > zeroTs)
                {
                    bt = String.Format("{0:00}:{1:00}.{2:000}", bestTs.Minutes, bestTs.Seconds, bestTs.Milliseconds);
                }
                else
                {
                    bt = "--:--.---";
                }
                    
                names = names + p.Name + "\n\n";
                lap1 = lap1 + t1 + "\n\n";
                lap2 = lap2 + t2 + "\n\n";
                lap3 = lap3 + t3 + "\n\n";
                total = total + tt + "\n\n";
                bestLap = bestLap + bt + "\n\n";

            }
            laps[0] = lap1;
            laps[1] = lap2;
            laps[2] = lap3;
            
            RpcChangeScores(names, laps, bestLap, total);
            if (isServerOnly)
                ChangeScores(names, laps, bestLap, total);
        }
    }

    /// <summary> Función que actualiza los valores de la interfaz del final de la partida, y que para al cliente/servidor local, si existe. </summary>
    private void ChangeScores(string names, string[] laps, string bestLap, string total)
    {
        UI_m.SetEndingUI();
        UI_m.SetScores(names, laps, bestLap, total);
        if (isClient)
        {
            NetworkManager.singleton.StopClient();
        }
        if(isServer)
            NetworkManager.singleton.StopServer();

        //Mover la cámara del jugador a donde toca
        cameraPosition.position = new Vector3(0,2.82f,-10);
        cameraPosition.localEulerAngles = Vector3.zero;
        
        //FadeIn
        UI_m.FadeIn();
    }

    /// <summary> Función que devuelve un array de jugadores ordenado.
    /// <para> Si se ha detectado que falta un jugador, se elimina de la lista. </para> </summary>
    private PlayerInfo[] SortPlayers()
    {
        // Update car arc-lengths
        float[] arcLengths = new float[m_Players.Count];

        for (int i = 0; i < m_Players.Count; ++i)
        {
            arcLengths[i] = ComputeCarArcLength(i);
        }

        int newId = 0;
        for (int i = 0; i < arcLengths.Length; i++)
        {
            if (arcLengths[i] == -999999)
            {
                this.m_Players.RemoveAt(i);
            }
            else
            {
                m_Players[i].CurrentPosition = newId;
                newId++;
            }
        }

        PlayerInfo[] arr = m_Players.ToArray();
        Array.Sort(arr, new PlayerInfoComparer(arcLengths));
        return arr;
    }

    /// <summary> Cuando se inicia el servidor, se asigna el número máximo de jugadores previamente seleccionado por el host. </summary>
    public override void OnStartServer()
    {
        Player_Count = Drop_Players.value+2;
        Drop_Players.gameObject.SetActive(false);
    }

    /// <summary> Cuando se inicia el cliente, se desactiva la interfaz de espera. </summary>
    public override void OnStartClient()
    {
        UI_m.ToggleWaitingHUD(false);
        Drop_Players.gameObject.SetActive(false);
    }

    #region ClientRpcs

    /// <summary> Activa el PlayerController de todos los clientes e inicia el temporizador. </summary>
    [ClientRpc]
    void RpcStartGame()
    {
        m_LocalSetupPlayer.StartGame();

        timer.Start();
    }

    /// <summary> Desactiva el PlayerController de todos los clientes e inicia el FadeOut. </summary>
    [ClientRpc]
    public void RpcFinishGame()
    {
        m_LocalSetupPlayer.EndGame();

        UI_m.FadeOut();
    }    
    
    /// <summary> Asigna los valores de la pantalla de puntuaciones a todos los clientes. </summary>
    [ClientRpc]
    void RpcChangeScores(string names, string[] laps, string bestLap, string total)
    {
        ChangeScores(names, laps, bestLap, total);
    }

    #endregion

}
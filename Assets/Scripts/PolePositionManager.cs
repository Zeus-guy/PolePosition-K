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
    public Transform[] startingPoints;
    public Dropdown Drop_Players;
    [SyncVar(hook=nameof(ClassLapHook))] public bool classLap = false;
    [SyncVar(hook=nameof(MaxLapsHook))] public int maxLaps = 3;

    public readonly List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    private CircuitController m_CircuitController;
    private GameObject[] m_DebuggingSpheres;
    public SetupPlayer m_LocalSetupPlayer;
    private Stopwatch timer;
    private int oldPlayersLeft = -1;
    public bool resetedAfterClassLap;

    [SyncVar] public bool countdownStarted;
    [SyncVar(hook=nameof(PlayerCountHook))] public int Player_Count = 1;
    public bool lobbyEnded;
    
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
        if (!gameStarted && lobbyEnded)
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
                            timer.Restart();
                            //No protegemos countdownStarted porque sólo la modifica el servidor y siempre pasa a tener el mismo valor.
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
        UpdateRaceProgress(false);
    }

    /// <summary> Añade un PlayerInfo a m_Players. </summary>
    public void AddPlayer(PlayerInfo player)
    {
        m_Players.Add(player);
        UpdateUINames();
        
    }

    /// <summary> Actualiza los nombres en el lobby. </summary>
    public void UpdateUINames()
    {
        string[] names = new string[m_Players.Count];
        bool[] ready = new bool[m_Players.Count];
        for (int i = 0; i < m_Players.Count; i++)
        {
            names[i] = m_Players[i].Name;
            ready[i] = m_Players[i].controller.ready;
        }

        UI_m.UpdateNames(names, ready);
    }

    /// <summary> Asigna la referencia al SetupPlayer que representa al jugador local. </summary>
    public void SetLocalPlayer(SetupPlayer sp)
    {
        m_LocalSetupPlayer = sp;
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
            if (this.m_ArcLengths[x.ArrayPosition] < (m_ArcLengths[y.ArrayPosition]))
                return 1;
            else return -1;
        }
    }
    
    /// <summary> Toma un array ordenado y lo muestra por pantalla si la posición de alguno de los jugadores ha cambiado. </summary>
    public void UpdateRaceProgress(bool forceShow)
    {
        bool playerLeft = false;
        PlayerInfo[] arr = SortPlayers(ref playerLeft);

        string myRaceOrder = "";

        bool updatePosition = false;
        bool startRace = true;
        for (int i = 0; i < arr.Length; i++)
        {
            if (!arr[i].classified && classLap)
            {
                startRace = false;
            }
            else
            {
                if (arr[i].CurrentPosition != i)
                {
                    updatePosition = true;
                    arr[i].CurrentPosition = i;
                }
                myRaceOrder += arr[i].Name + "\n";
            }

        }
        
        if (updatePosition || forceShow || playerLeft)
        {
            UI_m.SetTextPosition(myRaceOrder);
        }
        if (isServerOnly)
            UI_m.UpdateServerNames(myRaceOrder);
        
        if (m_LocalSetupPlayer != null)
        {
            UI_m.SetCurTime(timer.Elapsed);
        }
        
        if (startRace && resetedAfterClassLap)
        {
            ResetClassLapPositions(arr);
        }
    }


    /// <summary> Función que se ocupa de resetear la información de la carrera y ordenar a los jugadores cuando todos finalizan
    /// la vuelta de clasificación. No protegemos classLap porque siempre se le asigna el valor false. </summary>
    private void ResetClassLapPositions(PlayerInfo[] arr)
    {
        classLap = false;
        resetedAfterClassLap = false;
        Array.Sort(arr, ((a, b) => a.times[0].Ticks.CompareTo(b.times[0].Ticks)));
        for (int i = 0; i < arr.Length; i++)
        {
            arr[i].gameObject.SetActive(true);
            arr[i].transform.position = startingPoints[i].transform.position;
            arr[i].transform.eulerAngles = new Vector3(0,-90,0);
            arr[i].controller.m_Rigidbody.velocity = Vector3.zero;
            arr[i].controller.m_Rigidbody.angularVelocity = Vector3.zero;
            arr[i].controller.m_Rigidbody.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
            arr[i].CanChangeLap = true;
            arr[i].times.Clear();
            if (arr[i].controller.isLocalPlayer)
                arr[i].controller.CmdResetLap();
        }
        gameStarted = false;
        timerStarted = false;
        countdown = MAXCOUNTDOWN;
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
                if (player.controller.CurrentLap != 0)
                {
                    if (player.controller.CurrentLap == 1 && (maxLaps != 1 || classLap))
                    {
                        player.times.Add(timer.Elapsed);
                        if (classLap)
                        {
                            if (player.controller.isLocalPlayer)
                            {
                                player.controller.CmdChangeTimes(timer.Elapsed.Ticks);
                                timer.Stop();
                                player.controller.enabled = false;
                                UI_m.SetCountDown("WAITING FOR\n OTHER PLAYERS");
                                player.controller.CmdSetClassified();
                            }
                        }
                        else
                        {
                            if (player.controller.isLocalPlayer)
                                player.controller.CmdChangeTimes(timer.Elapsed.Ticks);
                            UI_m.SetLapTime(player.times[0]);
                        }
                    }
                    else if (player.controller.CurrentLap == maxLaps)
                    {
                        timer.Stop();
                        if (player.controller.isLocalPlayer)
                            player.controller.CmdChangeTimes(timer.Elapsed.Ticks);
                        player.times.Add(timer.Elapsed);
                        if (maxLaps == 1)
                            UI_m.SetLapTime(player.times[maxLaps-1]);
                        else
                            UI_m.SetLapTime(player.times[maxLaps-1]-player.times[maxLaps-2]);
                        if (player.controller.isLocalPlayer)
                            FinishGame();
                    }
                    else
                    {
                        if (player.controller.isLocalPlayer)
                            player.controller.CmdChangeTimes(timer.Elapsed.Ticks);
                        player.times.Add(timer.Elapsed);
                        UI_m.SetLapTime(player.times[player.controller.CurrentLap-1]-player.times[player.controller.CurrentLap-2]);
                    }
                }
                if (player.controller.isLocalPlayer)
                {
                    if (!classLap || player.CurrentLap == 0)
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
            string total = "Total\n\n";
            string tt, bt;
            string[] laps = new string[maxLaps];
            string bestLap = "Best Lap\n\n";
            TimeSpan ts, bestTs;
            TimeSpan zeroTs = new TimeSpan(0);

            bool forceChange = false;
            PlayerInfo[] playerArray = SortPlayers(ref forceChange);

            for (int i = 0; i < maxLaps; i++)
            {
                laps[i] = "Lap " + i + "\n\n";
            }

            foreach (PlayerInfo p in playerArray)
            {
                bestTs = zeroTs;
                for (int i = 0; i < maxLaps; i++)
                {
                    if (i < p.times.Count)
                    {
                        if (i == 0)
                        {
                            bestTs = p.times[0];
                            laps[0] += String.Format("{0:00}:{1:00}.{2:000}", p.times[0].Minutes, p.times[0].Seconds, p.times[0].Milliseconds) + "\n\n";
                        }
                        else
                        {
                            ts = p.times[i]-p.times[i-1];
                            if (ts < bestTs)
                                bestTs = ts;
                            laps[i] += String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds) + "\n\n";
                        }
                    }
                    else
                    {
                        laps[i] += "--:--.---\n\n";
                    }
                }
                
                if (p.times.Count == maxLaps)
                {
                    ts = p.times[maxLaps-1];
                    tt = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
                }
                else
                {
                    tt = "--:--.---";
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
                total = total + tt + "\n\n";
                bestLap = bestLap + bt + "\n\n";

            }
            
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
    private PlayerInfo[] SortPlayers(ref bool playerRemoved)
    {
        // Update car arc-lengths
        float[] arcLengths = new float[m_Players.Count];

        for (int i = 0; i < m_Players.Count; ++i)
        {
            arcLengths[i] = ComputeCarArcLength(i);
        }

        int newId = 0;
        bool removedPlayer = m_Players.RemoveAll(p => p == null) > 0;
        for (int i = 0; i < m_Players.Count; i++)
        {
            m_Players[i].ArrayPosition = newId;
            newId++;
        }

        if (removedPlayer)
        {
            playerRemoved = true;
            UpdateUINames();
        }

        PlayerInfo[] arr = m_Players.ToArray();
        Array.Sort(arr, new PlayerInfoComparer(arcLengths));

        return arr;
    }

    /// <summary> Cuando se inicia el servidor, se asigna el número máximo de jugadores previamente seleccionado por el host. No se protege Player_Count, pues sólo se ejecuta cuando se inicia el servidor.</summary>
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

        timer.Restart();
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
        UI_m.AddEndDropdownLaps(laps.Length);
    }

    #endregion


    /// <summary> Hook que actualiza el número máximo de jugadores en el lobby. </summary>
    public void PlayerCountHook(int oldVal, int newVal)
    {
        UI_m.UpdateClientMaxPlayers();
    }

    /// <summary> Hook que actualiza un booleano interno dependiente de si hay o no vuelta de clasificación. </summary>
    public void ClassLapHook(bool oldVal, bool newVal)
    {
        resetedAfterClassLap = newVal;
        UI_m.ClientChangeClassLap(newVal);
    }

    /// <summary> Hook que actualiza el número máximo de vueltas. </summary>
    public void MaxLapsHook(int oldVal, int newVal)
    {
        UI_m.ClientChangeNumLaps(newVal);
            UI_m.SetLap(0, maxLaps);
    }
}
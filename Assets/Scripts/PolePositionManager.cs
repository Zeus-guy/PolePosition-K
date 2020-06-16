using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;
using System.Threading;
using System.Diagnostics;
using UnityEngine.UI;

public class PolePositionManager : NetworkBehaviour
{
    public int numPlayers;
    public CustomNetworkManager networkManager;
    public UIManager UI_m;
    public Transform cameraPosition;

    private readonly List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    private CircuitController m_CircuitController;
    private GameObject[] m_DebuggingSpheres;
    private const float MAXCOUNTDOWN = 3;
    private float countdown = MAXCOUNTDOWN;
    public bool gameStarted, timerStarted;
    [SyncVar] public bool countdownStarted;
    private SetupPlayer m_LocalSetupPlayer;

    private Stopwatch timer;

    [SyncVar] public int Player_Count = 1;
    public Transform[] checkPoints;
    private int oldPlayersLeft = -1;
    public Dropdown Drop_Players;
    

    private void Awake()
    {
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

    private void Update()
    {
        /*if (countdown < MAXCOUNTDOWN && m_Players.Count <= 1 && Player_Count > 1)
        {
            FinishGame();
            return;
        }*/

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
                //return;
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

    [ClientRpc]
    void RpcStartGame()
    {
        m_LocalSetupPlayer.StartGame();
        timer.Start();
    }

    public void AddPlayer(PlayerInfo player)
    {
        m_Players.Add(player);
    }
    public void SetLocalPlayer(SetupPlayer sp)
    {
        m_LocalSetupPlayer = sp;
        m_LocalSetupPlayer.SetCheckPoints(checkPoints);
    }

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
            //UI_m.SetLap(m_LocalSetupPlayer.GetLap());
            UI_m.SetCurTime(m_LocalSetupPlayer.GetPlayerInfo(), timer.Elapsed);
            //m_LocalSetupPlayer.SetCheckPoints(checkPoints);
        }

        //Debug.Log("El orden de carrera es: " + myRaceOrder);
    }

    float ComputeCarArcLength(int ID)
    {
        if (!isClient)
        {
            return -999990;
        }
        // Compute the projection of the car position to the closest circuit 
        // path segment and accumulate the arc-length along of the car along
        // the circuit.
        if (this.m_Players[ID] == null)
        {
            //m_LocalSetupPlayer.m_PlayerController.CmdUpdateArcLength(-999999);
            return -999999;
        }
        else if (this.m_Players[ID].ID != m_LocalSetupPlayer.GetPlayerInfo().ID)
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
            print(minArcL);
            if (this.m_Players[ID].controller.CurrentLap-1 == 0)
            {
                minArcL -= m_CircuitController.CircuitLength;
            }
            else
            {
                minArcL += m_CircuitController.CircuitLength * (m_Players[ID].controller.CurrentLap - 2);
            
            }    
            print(minArcL);    
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
    public void changeLap(PlayerInfo player)
    {
        //print(player.Name + ": checkpoint " + player.CheckPoint + ", vuelta " + player.CurrentLap + ", puede cambiar? " + player.CanChangeLap + ", dist: " + dist);
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
                        print("venga vamos aqui entramos");
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
                //player.CanChangeLap = false;
            }
        }
    }
    
    public void FinishGame()
    {
        m_LocalSetupPlayer.CmdFinishGame();
    }

    [ClientRpc]
    public void RpcFinishGame()
    {
        m_LocalSetupPlayer.EndGame();

        UI_m.FadeOut();
        
    }

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
            //NetworkManager.singleton.StopServer();
        }
    }
    [ClientRpc]
    void RpcChangeScores(string names, string[] laps, string bestLap, string total)
    {
        print("hola buenas: " + isClient + ", " + isClientOnly + ", " + isServer + ", " + isServerOnly);
        UI_m.SetEndingUI();
        UI_m.SetScores(names, laps, bestLap, total);
        if (isClient)
        {
            print("hasta luego crack");
            NetworkManager.singleton.StopClient();
        }
        //Mover la cámara del jugador a donde toca
        cameraPosition.position = new Vector3(0,2.82f,-10);
        cameraPosition.localEulerAngles = Vector3.zero;
        
        //FadeIn
        UI_m.FadeIn();
    }

    private PlayerInfo[] SortPlayers()
    {
        // Update car arc-lengths
        float[] arcLengths = new float[m_Players.Count];

        for (int i = 0; i < m_Players.Count; ++i)
        {
            arcLengths[i] = /*m_Players[i].controller.arcLength;*/ComputeCarArcLength(i);
            //changeLap(m_Players[i], arcLengths[i]);
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

        //m_Players.Sort(new PlayerInfoComparer(arcLengths));
        PlayerInfo[] arr = m_Players.ToArray();
        Array.Sort(arr, new PlayerInfoComparer(arcLengths));
        return arr;
    }

    public override void OnStartServer()
    {
        
        Player_Count = Drop_Players.value+2;
        Drop_Players.gameObject.SetActive(false);
    }
    public override void OnStartClient()
    {
        UI_m.ToggleWaitingHUD(false);
        Drop_Players.gameObject.SetActive(false);
    }

}
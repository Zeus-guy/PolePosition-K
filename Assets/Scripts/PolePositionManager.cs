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
    public NetworkManager networkManager;
    public UIManager UI_m;
    public Transform cameraPosition;

    private readonly List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    private CircuitController m_CircuitController;
    private GameObject[] m_DebuggingSpheres;
    private float countdown = 3;
    public bool gameStarted, timerStarted;
    private SetupPlayer m_LocalSetupPlayer;

    private Stopwatch timer;

    [SyncVar] public int Player_Count = 1;
    public Transform[] checkPoints;
    private int oldPlayersLeft = -1;
    public Dropdown Drop_Players;
    

    private void Awake()
    {
        if (networkManager == null) networkManager = FindObjectOfType<NetworkManager>();
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
        
        if (countdown < 3 && m_Players.Count <= 1 && Player_Count > 1)
        {
            FinishGame();
            return;
        }

        if (!gameStarted)
        {
            if (m_Players.Count <= Player_Count-1)
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
            if (this.m_ArcLengths[x.sortID] < (m_ArcLengths[y.sortID]))
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
            UI_m.SetLap(m_LocalSetupPlayer.GetLap());
            UI_m.SetCurTime(m_LocalSetupPlayer.GetPlayerInfo(), timer.Elapsed);
            m_LocalSetupPlayer.SetCheckPoints(checkPoints);
        }

        //Debug.Log("El orden de carrera es: " + myRaceOrder);
    }

    float ComputeCarArcLength(int ID)
    {
        if (!isClient)
            return -999990;
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
        //Debug.Log(ID + ", " + this.m_Players[ID].Name);

        int segIdx;
        float carDist;
        Vector3 carProj;

        float minArcL =
            this.m_CircuitController.ComputeClosestPointArcLength(carPos, out segIdx, out carProj, out carDist);

        m_Players[ID].segIdx = segIdx;

        this.m_DebuggingSpheres[ID].transform.position = carProj;

        if (this.m_Players[ID].CurrentLap == 0)
        {
            minArcL -= m_CircuitController.CircuitLength;
        }
        else
        {
            minArcL += m_CircuitController.CircuitLength *
                       (m_Players[ID].CurrentLap - 1);
        }
//Debug.Log("i = " + ID + ", minArcL = " + minArcL);

        bool isBehind = false;
        if (this.m_Players[ID].CheckPoint == 1 || (this.m_Players[ID].CheckPoint != 1 && !this.m_Players[ID].CanChangeLap))
        {
            switch (this.m_Players[ID].CurrentLap)
            {
                case 0:
                    if (minArcL > -200)
                        isBehind = true;
                    break;
                    
                case 1:
                    if (minArcL > 100)
                        isBehind = true;
                    break;
                    
                case 2:
                    if (minArcL > 500)
                        isBehind = true;
                    break;
                    
                case 3:
                    if (minArcL > 1000)
                        isBehind = true;
                    break;
            }
        }
        if (isBehind)
        {
            if (this.m_Players[ID].CurrentLap-1 == 0)
            {
                minArcL -= m_CircuitController.CircuitLength;
            }
            else
            {
                minArcL += m_CircuitController.CircuitLength * (m_Players[ID].CurrentLap - 2);
            
            }        
        }


        //Command para que el server asigne la SyncVar minArcL
        m_LocalSetupPlayer.m_PlayerController.CmdUpdateArcLength(minArcL);
        return minArcL;

    }
    public void changeLap(PlayerInfo player, float dist)
    {
        if (player.CanChangeLap)
        {
            if (player.CheckPoint == 0)
            {
                switch (player.CurrentLap)
                {
                    case 0:
                        if (dist > -200)
                            return;
                        break;
                        
                    case 1:
                        if (dist > 100)
                            return;
                        else
                            player.time1 = timer.Elapsed;
                        break;
                        
                    case 2:
                        if (dist > 500)
                            return;
                        else
                            player.time2 = timer.Elapsed;
                        break;
                        
                    case 3:
                        if (dist > 1000)
                            return;
                        else
                        {
                            timer.Stop();
                            player.time3 = timer.Elapsed;
                            if (player.controller.isLocalPlayer)
                                FinishGame();
                        }
                        break;
                    default:
                        return;
                }
                player.CurrentLap++;
                player.CanChangeLap = false;
            }
        }
    }
    
    private void FinishGame()
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

        string names = "Players\n\n";
        string lap1 = "Lap 1\n\n";
        string lap2 = "Lap 2\n\n";
        string lap3 = "Lap 3\n\n";
        string total = "Total\n\n";
        string t1, t2, t3, tt;
        TimeSpan ts;
        TimeSpan zeroTs = new TimeSpan(0);

        PlayerInfo[] playerArray = SortPlayers();

        foreach (PlayerInfo p in playerArray)
        {
            ts = p.time1;
            if (p.time1 > zeroTs)
                t1 = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
            else
                t1 = "--:--.---";

            ts = p.time2 - p.time1;
            if (p.time1 > zeroTs && p.time2 > zeroTs)
                t2 = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
            else
                t2 = "--:--.---";
            
            ts = p.time3 - p.time2;
            if (p.time2 > zeroTs && p.time3 > zeroTs)
                t3 = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
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
                tt = "--:--.---";
            }
                

            names = names + p.Name + "\n\n";
            lap1 = lap1 + t1 + "\n\n";
            lap2 = lap2 + t2 + "\n\n";
            lap3 = lap3 + t3 + "\n\n";
            total = total + tt + "\n\n";
        }

        if (isServer)
        {
            RpcChangeScores(names, lap1, lap2, lap3, total);
            //NetworkManager.singleton.StopServer();
        }
    }
    [ClientRpc]
    void RpcChangeScores(string names, string lap1, string lap2, string lap3, string total)
    {
        UI_m.SetEndingUI();
        UI_m.SetScores(names, lap1, lap2, lap3, total);
        if (isClient)
        {
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
            changeLap(m_Players[i], arcLengths[i]);
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
                m_Players[i].sortID = newId;
                newId++;
            }
        }
        /*if (isClient)
            ComputeCarArcLength(m_LocalSetupPlayer.GetPlayerInfo().sortID);*/

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
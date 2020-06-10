﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;
using System.Threading;
using System.Diagnostics;

public class PolePositionManager : NetworkBehaviour
{
    public int numPlayers;
    public NetworkManager networkManager;
    public UIManager UI_m;

    private readonly List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    private CircuitController m_CircuitController;
    private GameObject[] m_DebuggingSpheres;
    private float countdown = 3;
    public bool gameStarted;
    private SetupPlayer m_LocalSetupPlayer;

    private Stopwatch timer;
    

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
        if (m_Players.Count <= 0)
        {
            if(UI_m.GetCountDown()=="")
                UI_m.SetCountDown("Waiting for another"+"\n"+" player");
            return;
        }

        if (!gameStarted)
        {
            if (countdown > -1.5)
            {
                countdown -= Time.deltaTime;
                if (countdown > 0)
                    UI_m.EditCountDown(countdown);
                else if (countdown <= 0)
                    UI_m.SetCountDown("GO!");
            }
            else
            {
                UI_m.SetCountDown("");
                gameStarted = true;
                if (isServer)
                {
                    RpcStartGame();
                }
                    timer.Start();
            }
        }

        UpdateRaceProgress();
    }

    [ClientRpc]
    void RpcStartGame()
    {
        m_LocalSetupPlayer.StartGame();
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
            if (this.m_ArcLengths[x.ID] < (m_ArcLengths[y.ID]))
                return 1;
            else return -1;
        }
    }

    public void UpdateRaceProgress()
    {
        // Update car arc-lengths
        float[] arcLengths = new float[m_Players.Count];

        for (int i = 0; i < m_Players.Count; ++i)
        {
            arcLengths[i] = ComputeCarArcLength(i);
            changeLap(m_Players[i], arcLengths[i]);
        }

        //m_Players.Sort(new PlayerInfoComparer(arcLengths));
        PlayerInfo[] arr = m_Players.ToArray();
        Array.Sort(arr, new PlayerInfoComparer(arcLengths));

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
        }

        //Debug.Log("El orden de carrera es: " + myRaceOrder);
    }

    float ComputeCarArcLength(int ID)
    {
        // Compute the projection of the car position to the closest circuit 
        // path segment and accumulate the arc-length along of the car along
        // the circuit.
        Vector3 carPos = this.m_Players[ID].transform.position;
        //Debug.Log(ID + ", " + this.m_Players[ID].Name);

        int segIdx;
        float carDist;
        Vector3 carProj;

        float minArcL =
            this.m_CircuitController.ComputeClosestPointArcLength(carPos, out segIdx, out carProj, out carDist);

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
}
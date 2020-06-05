﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;
using System.Threading;

public class PolePositionManager : NetworkBehaviour
{
    public int numPlayers;
    public NetworkManager networkManager;

    private readonly List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    private CircuitController m_CircuitController;
    private GameObject[] m_DebuggingSpheres;
    private float countdown = 3;
    public bool gameStarted;
    private SetupPlayer m_LocalSetupPlayer;

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
    }

    private void Update()
    {
        if (m_Players.Count <= 1)
            return;

        if (isServer && !gameStarted)
        {
            if (countdown > 0)
            {
                countdown -= Time.deltaTime;
            }
            else
            {
                gameStarted = true;
                RpcStartGame();
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
            if (this.m_ArcLengths[x.ID] < m_ArcLengths[y.ID])
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
        }

        m_Players.Sort(new PlayerInfoComparer(arcLengths));

        string myRaceOrder = "";
        foreach (var _player in m_Players)
        {
            myRaceOrder += _player.Name + " ";
        }

        Debug.Log("El orden de carrera es: " + myRaceOrder);
    }

    float ComputeCarArcLength(int ID)
    {
        // Compute the projection of the car position to the closest circuit 
        // path segment and accumulate the arc-length along of the car along
        // the circuit.
        Vector3 carPos = this.m_Players[ID].transform.position;

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

        return minArcL;
    }
}
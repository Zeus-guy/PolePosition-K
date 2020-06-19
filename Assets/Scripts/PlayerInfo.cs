using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using System;

/// <summary> Clase que almacena información importante sobre el jugador. </summary>
public class PlayerInfo : MonoBehaviour
{
    public string Name { get; set; }

    public int ID { get; set; }

    public int ArrayPosition { get; set; }
    public int CurrentPosition { get; set; }

    public int CurrentLap;

    public override string ToString()
    {
        return Name;
    }

    public int CheckPoint { get; set; }
    public int LastCheckPoint { get; set; }
    public bool CanChangeLap { get; set; }

    public List<TimeSpan> times = new List<TimeSpan>();

    public int segIdx = 23;

    public PlayerController controller;

    public bool classified;
}
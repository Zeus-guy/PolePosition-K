using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using System;

public class PlayerInfo : MonoBehaviour
{
    public string Name { get; set; }

    public int ID { get; set; }

    public int CurrentPosition { get; set; }

    public int CurrentLap { get; set; }

    public override string ToString()
    {
        return Name;
    }

    public int CheckPoint { get; set; }
    public int LastCheckPoint { get; set; }
    public bool CanChangeLap { get; set; }

    public TimeSpan time1;
    public TimeSpan time2;
    public TimeSpan time3;

}
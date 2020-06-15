using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EndLapController : MonoBehaviour
{
    public Text LapText;
    private string[] m_lapsArray;

    public void SetLapText(int pos)
    {
        LapText.text = m_lapsArray[pos];
    }
    public void SetLap(string[] lap)
    {
        m_lapsArray = lap;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary> Clase que sirve para cambiar los tiempos mostrados en la pantalla final según indique el desplegable que aparece en la misma. </summary>
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Esta clase se ocupa de llamar a ciertas funciones cuando acaban las animaciones de fundido a negro. </summary>
public class FadeController : MonoBehaviour
{
    public PolePositionManager ppm;
    public void OnFadeEnd()
    {
        ppm.PostFadeOut();
    }

    public void OnFadeInEnd()
    {
        gameObject.SetActive(false);
    }
}

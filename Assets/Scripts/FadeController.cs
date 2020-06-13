using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FadeController : MonoBehaviour
{
    // Start is called before the first frame update
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

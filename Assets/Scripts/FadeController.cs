using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FadeController : MonoBehaviour
{
    // Start is called before the first frame update
    public PolePositionManager ppm;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void OnFadeEnd()
    {
        ppm.PostFadeOut();
    }

    public void OnFadeInEnd()
    {
        gameObject.SetActive(false);
    }
}

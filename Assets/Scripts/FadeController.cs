using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

/// <summary> Esta clase se ocupa de llamar a ciertas funciones cuando acaban las animaciones de fundido a negro. </summary>
public class FadeController : MonoBehaviour
{
    public GameObject connectionLostPrompt;
    private bool fadedIn;
    public PolePositionManager ppm;

    /// <summary> Tarea que espera 5 segundos y, si no ha llegado información del servidor, muestra un mensaje que permite volver al menú principal. </summary>
    private void ConnectionDelayTask()
    {
        Task.Delay(5000).Wait();
        if (!fadedIn)
            connectionLostPrompt.SetActive(true);
    }

    /// <summary> Función que se ejecuta al terminar la animación de fade. </summary>
    public void OnFadeEnd()
    {
        ppm.PostFadeOut();
        new Task(()=>ConnectionDelayTask()).Start();
    }

    /// <summary> Función que se ejecuta al terminar la animación de fade-in. </summary>
    public void OnFadeInEnd()
    {
        gameObject.SetActive(false);
        CustomNetworkManager.singleton.StopServer();
    }

    /// <summary> Función que activa la animación de fade-in. </summary>
    public void FadeIn()
    {
        GetComponent<Animator>().SetTrigger("FadeIn");
        fadedIn = true;
    }
}

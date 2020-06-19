using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

/// <summary> Clase que se ocupa de controlar el chat. </summary>
public class CustomChatScript : MonoBehaviour
{
    public InputField chatMessage;
    public Text chatHistory;
    public Scrollbar scrollbar;

    /// <summary> En Awake se añade la función de enviar mensajes al delegado de SetupPlayer. </summary>
    public void Awake()
    {
        SetupPlayer.OnMessage += OnPlayerMessage;
    }

    /// <summary> En OnDestroy se elimina la función de enviar mensajes al delegado de SetupPlayer. </summary>
    public void OnDestroy()
    {
        SetupPlayer.OnMessage -= OnPlayerMessage;
    }

    /// <summary> Función que se ejecuta al recibir un mensaje. </summary>
    private void OnPlayerMessage(SetupPlayer player, string message)
    {
        string prettyMessage = player.isLocalPlayer ?
            $"<color=red>{player.GetPlayerInfo().Name}: </color> {message}" :
            $"<color=blue>{player.GetPlayerInfo().Name}: </color> {message}";
        AppendMessage(prettyMessage);

        Debug.Log(message);
    }

    /// <summary> Función que envía un mensaje. </summary>
    public void OnSend()
    {
        if (chatMessage.text.Trim() == "")
            return;

        // get our player
        SetupPlayer player = NetworkClient.connection.identity.GetComponent<SetupPlayer>();

        // send a message
        player.CmdSend(chatMessage.text.Trim());

        chatMessage.text = "";
    }

    internal void AppendMessage(string message)
    {
        StartCoroutine(AppendAndScroll(message));
    }

    IEnumerator AppendAndScroll(string message)
    {
        chatHistory.text += message + "\n";

        // it takes 2 frames for the UI to update ?!?!
        yield return null;
        yield return null;

        // slam the scrollbar down
        scrollbar.value = 0;
    }
}

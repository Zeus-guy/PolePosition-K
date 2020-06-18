using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

/// <summary> Clase que se encarga de controlar las diversas interfaces del juego. </summary>
public class UIManager : MonoBehaviour
{
    public bool showGUI = true;

    private NetworkManager m_NetworkManager;

    #region Editor references
    [Header("Main Menu")] [SerializeField] private GameObject mainMenu;
    [SerializeField] private Button buttonHost;
    [SerializeField] private Button buttonClient;
    [SerializeField] private Button buttonServer;
    [SerializeField] private InputField inputFieldIP;

    [Header("In-Game HUD")] [SerializeField]
    private GameObject inGameHUD;

    [SerializeField] private Text textSpeed;
    [SerializeField] private Text textLaps;
    [SerializeField] private Text textPosition;
    [SerializeField] private Text textCountDown;
    [SerializeField] private Text curTimeText;
    [SerializeField] private Text totalTimeText;
    [SerializeField] private Image fade;
    
    [SerializeField] private GameObject endingHUD;
    [SerializeField] private Text endNames;
    [SerializeField] private Text endLapText;
    [SerializeField] private Text endBestLap;
    [SerializeField] private Text endTotal;
    [SerializeField] private GameObject nameField;
    [SerializeField] private GameObject colorField;
    [SerializeField] private Dropdown maxPlayers;
    [SerializeField] private GameObject waitingBox;
    [SerializeField] private Text waitingText;
    [SerializeField] private GameObject waitingReset;
    [SerializeField] private EndLapController m_EndLapController;
    [SerializeField] private GameObject quitButton;
    [SerializeField] private GameObject quitConfirmationPanel;
    [SerializeField] private GameObject quitButtonServer;
    [SerializeField] private GameObject quitConfirmationPanelServer;
    [SerializeField] private PolePositionManager m_PolePositionManager;
    [SerializeField] private GameObject serverHUD;
    [SerializeField] private Text serverText;
    [SerializeField] private Text serverNames;
    #endregion
    private int dots = 0;

    private void Awake()
    {
        m_NetworkManager = FindObjectOfType<NetworkManager>();
    }

    /// <summary> En Start se asignan funciones a los delegados de los botones del menú principal. </summary>
    private void Start()
    {
        buttonHost.onClick.AddListener(() => StartHost());
        buttonClient.onClick.AddListener(() => StartClient());
        buttonServer.onClick.AddListener(() => StartServer());
        ActivateMainMenu();
    }

    /// <summary> Tarea que incrementa la variable dots seis veces, esperando un segundo tras incrementarla.
    /// Si la interfaz correspondiente está activa al llegar al último punto, se activa el botón que permite volver al menú principal. 
    /// Si el cliente consigue conectarse, la función se cancela prematuramente. </summary>
    private void UpdateDotsTask()
    {
        for (int i = 0; i < 6; i++)
        {
            if (!waitingBox.activeSelf)
                return;
            dots = i;
            UpdateDots();
            Task.Delay(1000).Wait();
        }
        if (waitingBox.activeSelf)
            waitingReset.SetActive(true);
    }

    /// <summary> Función que actualiza la velocidad en la interfaz. </summary>
    public void UpdateSpeed(int speed)
    {
        textSpeed.text = "Speed " + speed + " Km/h";
    }

    /// <summary> Función que activa la interfaz del menú principal, y desactiva las demás. </summary>
    public void ActivateMainMenu()
    {
        mainMenu.SetActive(true);
        inGameHUD.SetActive(false);
        endingHUD.SetActive(false);
        nameField.SetActive(true);
        colorField.SetActive(true);
    }

    /// <summary> Función que activa la interfaz in-game, y desactiva las demás. </summary>
    private void ActivateInGameHUD()
    {
        mainMenu.SetActive(false);
        inGameHUD.SetActive(true);
    }
    
    /// <summary> Función que activa la interfaz del servidor dedicado, y desactiva las demás. </summary>
    private void ActivateServerHUD()
    {
        mainMenu.SetActive(false);
        nameField.SetActive(false);
        colorField.SetActive(false);
        serverHUD.SetActive(true);
    }

    /// <summary> Función que activa o desactiva la interfaz que indica que se está intentando realizar una conexión con el servidor. </summary>
    public void ToggleWaitingHUD(bool state)
    {
        waitingBox.SetActive(state);
        quitButton.SetActive(!state);
    }

    /// <summary> Función que inicia un host, que funciona como servidor y cliente simultáneamente. </summary>
    private void StartHost()
    {
        m_NetworkManager.maxConnections = maxPlayers.value+2;
        m_NetworkManager.StartHost();
        ActivateInGameHUD();
    }

    /// <summary> Función que inicia un cliente. </summary>
    private void StartClient()
    {
        ToggleWaitingHUD(true);
        m_NetworkManager.StartClient();
        m_NetworkManager.networkAddress = inputFieldIP.text;
        ActivateInGameHUD();
        new Task(()=>UpdateDotsTask()).Start();
    }

    /// <summary> Función que inicia un servidor dedicado. </summary>
    private void StartServer()
    {
        m_NetworkManager.maxConnections = maxPlayers.value+2;
        m_NetworkManager.StartServer();
        ActivateServerHUD();
    }

    /// <summary> Función que asigna a la interfaz de la cuenta atrás un texto determinado. </summary>
    public void SetCountDown(string t)
    {
        if (textCountDown.color.a != 1)
        {
            Color color = textCountDown.color;
            color.a = 1;
            textCountDown.color = color;
        }
        if (textCountDown.fontSize != 100)
            textCountDown.fontSize = 100;

        textCountDown.text = t;
    }

    
    /// <summary> Función que devuelve el texto de la interfaz de la cuenta atrás. </summary>
    public string GetCountDown()
    {
        return textCountDown.text;
    }

    /// <summary> Función que, dado un float t, asigna al texto de la interfaz de la cuenta atrás la parte entera del número, 
    /// y utiliza la parte decimal para controlar la opacidad y el tamaño del mismo. </summary>
    public void EditCountDown(float t)
    {
        double cd = Math.Ceiling(t);
        textCountDown.text = cd.ToString();
        Color color = textCountDown.color;
        float ratio = t-(float)cd+1;
        color.a = ratio;
        textCountDown.color = color;
        textCountDown.fontSize = (int)(100*(ratio+0.5f));
    }

    /// <summary> [TO-DO] Función que todavía no se usa para nada. </summary>
    public void SetBackwardsText(float t)
    {
        float cd = t/1;
        textCountDown.text = "TURN AROUND";
        Color color = textCountDown.color;
        color.a = cd;
        textCountDown.color = color;
    }

    /// <summary> Función que asigna al texto de las posiciones un valor. </summary>
    public void SetTextPosition(string text)
    {
        textPosition.text = text;
    }

    /// <summary> Función que asigna un valor (entre 1 y el número máximo de vueltas) al texto que muestra las vueltas que lleva el jugador. </summary>
    public void SetLap(int lap)
    {
        if (lap < 1) lap = 1;
        if (lap > 3) lap = 3;

        textLaps.text = "Lap: " + lap + "/3";
    }

    /// <summary> Función que muestra en la interfaz el tiempo actual. </summary>
    public void SetCurTime(TimeSpan curTime)
    {
        totalTimeText.text = String.Format("{0:00}:{1:00}.{2:000}", curTime.Minutes, curTime.Seconds, curTime.Milliseconds);
    }
    
    /// <summary> Función que muestra en la interfaz el tiempo de la vuelta anterior. </summary>
    public void SetLapTime(TimeSpan curTime)
    {
        curTimeText.text = String.Format("{0:00}:{1:00}.{2:000}", curTime.Minutes, curTime.Seconds, curTime.Milliseconds);
    }

    /// <summary> Función que activa el objeto FadeOut. </summary>
    public void FadeOut()
    {
        fade.gameObject.SetActive(true);
    }

    /// <summary> Función que activa la interfaz de la pantalla de puntuaciones. </summary>
    public void SetEndingUI()
    {
        mainMenu.SetActive(false);
        inGameHUD.SetActive(false);
        endingHUD.SetActive(true);
        serverHUD.SetActive(false);
    }

    /// <summary> Función que actualiza el valor de las puntuaciones al final de la partida. </summary>
    public void SetScores(string names, string[] laps, string bestLap, string total)
    {
        endNames.text = names;
        endLapText.text = laps[0];
        endBestLap.text = bestLap;
        endTotal.text = total;
        m_EndLapController.SetLap(laps);
    }

    /// <summary> Función que vuelve a mostrar la pantalla tras un FadeOut. </summary>
    public void FadeIn()
    {
        fade.GetComponent<Animator>().SetTrigger("FadeIn");
    }

    /// <summary> Función que resetea el juego: Destruye el NetworkManager y recarga la escena. </summary>
    public void ResetGame()
    {
        Destroy(m_NetworkManager.gameObject);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary> Función que actualiza la interfaz de intento de conexión según el número de puntos calculado por la task anterior. </summary>
    private void UpdateDots()
    {
        string dotString = "";
        for (int i = 0; i < 6; i++)
        {
            if (i <= dots)
                dotString += ".";
        }
        dotString = "Connecting to server\nPlease Wait\n" + dotString;
        if (dots >= 5)
        {
            dotString += "\nCouldn't connect to server";
        }
        waitingText.text = dotString;
    }

    /// <summary> Función que activa la interfaz que indica que se ha perdido la conexión con el servidor. </summary>
    public void ServerCrashMessage()
    {
        ToggleWaitingHUD(true);
        waitingReset.SetActive(true);
        waitingText.text = "Lost connection";
    }

    /// <summary> Función que activa el panel de confirmación de abandonar la partida. </summary>
    public void OnButtonQuitGame()
    {
        quitButton.SetActive(false);
        quitConfirmationPanel.SetActive(true);
    }

    /// <summary> Función que desactiva el panel de confirmación de abandonar la partida. </summary>
    public void OnButtonCancelQuit()
    {
        quitButton.SetActive(true);
        quitConfirmationPanel.SetActive(false);
    }

    /// <summary> Función que activa el panel de confirmación de cerrar el servidor. </summary>
        public void OnButtonQuitServer()
    {
        quitButtonServer.SetActive(false);
        quitConfirmationPanelServer.SetActive(true);
    }
    
    /// <summary> Función que desactiva el panel de confirmación de cerrar el servidor. </summary>
    public void OnButtonCancelQuitServer()
    {
        quitButtonServer.SetActive(true);
        quitConfirmationPanelServer.SetActive(false);
    }
    
    /// <summary> Función que para el cliente y el servidor, si están activos, y resetea el juego. </summary>
    public void OnButtonConfirmQuit()
    {
        NetworkManager.singleton.StopClient();
        NetworkManager.singleton.StopServer();
        ResetGame();
    }
    public void UpdateServerRemaining(int numPlayers)
    {
        string newText = "";
        if (numPlayers > 0)
            newText = "WAITING FOR " + numPlayers + " PLAYER" + ((numPlayers == 1)?"":"s");
        serverText.text = "<b>SERVER RUNNING</b>\n" + newText;
    }

    public void UpdateServerNames(string names)
    {
        serverNames.text = names;
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

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
    [SerializeField] private PolePositionManager m_PolePositionManager;
    #endregion
    private int dots = 0;

    private void Awake()
    {
        m_NetworkManager = FindObjectOfType<NetworkManager>();
    }

    private void Start()
    {
        buttonHost.onClick.AddListener(() => StartHost());
        buttonClient.onClick.AddListener(() => StartClient());
        buttonServer.onClick.AddListener(() => StartServer());
        ActivateMainMenu();
    }

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

    public void UpdateSpeed(int speed)
    {
        textSpeed.text = "Speed " + speed + " Km/h";
    }

    public void ActivateMainMenu()
    {
        mainMenu.SetActive(true);
        inGameHUD.SetActive(false);
        endingHUD.SetActive(false);
        nameField.SetActive(true);
        colorField.SetActive(true);
    }

    private void ActivateInGameHUD()
    {
        mainMenu.SetActive(false);
        inGameHUD.SetActive(true);
    }

    public void ToggleWaitingHUD(bool state)
    {
        waitingBox.SetActive(state);
    }
    private void StartHost()
    {
        m_NetworkManager.maxConnections = maxPlayers.value+2;
        m_NetworkManager.StartHost();
        ActivateInGameHUD();
    }

    private void StartClient()
    {
        ToggleWaitingHUD(true);
        m_NetworkManager.StartClient();
        m_NetworkManager.networkAddress = inputFieldIP.text;
        ActivateInGameHUD();
        new Task(()=>UpdateDotsTask()).Start();
    }

    private void StartServer()
    {
        m_NetworkManager.StartServer();
        ActivateInGameHUD();
    }

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

    public string GetCountDown()
    {
        return textCountDown.text;
    }

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

    public void SetBackwardsText(float t)
    {
        float cd = t/1;
        textCountDown.text = "TURN AROUND";
        Color color = textCountDown.color;
        color.a = cd;
        textCountDown.color = color;
    }

    public void SetTextPosition(string text)
    {
        textPosition.text = text;
    }

    public void SetLap(int lap)
    {
        if (lap < 1) lap = 1;
        if (lap > 3) lap = 3;

        textLaps.text = lap + "/3";
    }

    public void SetCurTime(PlayerInfo player, TimeSpan curTime)
    {
        TimeSpan ts;
        switch (player.controller.CurrentLap)
        {
            case 2:
                ts = player.time1;
                curTimeText.text = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
                break;
                
            case 3:
                ts = player.time2 - player.time1;
                curTimeText.text = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
                break;
            
        }
        totalTimeText.text = String.Format("{0:00}:{1:00}.{2:000}", curTime.Minutes, curTime.Seconds, curTime.Milliseconds);
    }

    public void FadeOut()
    {
        fade.gameObject.SetActive(true);
    }

    public void SetEndingUI()
    {
        mainMenu.SetActive(false);
        inGameHUD.SetActive(false);
        endingHUD.SetActive(true);
    }

    public void SetScores(string names, string[] laps, string bestLap, string total)
    {
        endNames.text = names;
        endLapText.text = laps[0];
        endBestLap.text = bestLap;
        endTotal.text = total;
        m_EndLapController.SetLap(laps);
    }
    public void FadeIn()
    {
        fade.GetComponent<Animator>().SetTrigger("FadeIn");
    }

    public void ResetGame()
    {
        Destroy(m_NetworkManager.gameObject);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

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
    public void ServerCrashMessage()
    {
        ToggleWaitingHUD(true);
        waitingReset.SetActive(true);
        waitingText.text = "Lost connection";
    }

    public void OnButtonQuitGame()
    {
        quitButton.SetActive(false);
        quitConfirmationPanel.SetActive(true);
    }
    public void OnButtonCancelQuit()
    {
        quitButton.SetActive(true);
        quitConfirmationPanel.SetActive(false);
    }
    public void OnButtonConfirmQuit()
    {
        NetworkManager.singleton.StopClient();
        NetworkManager.singleton.StopServer();
        ResetGame();
    }
}
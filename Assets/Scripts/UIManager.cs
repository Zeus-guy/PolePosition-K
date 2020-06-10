using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public bool showGUI = true;

    private NetworkManager m_NetworkManager;

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

    public void UpdateSpeed(int speed)
    {
        textSpeed.text = "Speed " + speed + " Km/h";
    }

    private void ActivateMainMenu()
    {
        mainMenu.SetActive(true);
        inGameHUD.SetActive(false);
    }

    private void ActivateInGameHUD()
    {
        mainMenu.SetActive(false);
        inGameHUD.SetActive(true);
    }

    private void StartHost()
    {
        m_NetworkManager.StartHost();
        ActivateInGameHUD();
    }

    private void StartClient()
    {
        m_NetworkManager.StartClient();
        m_NetworkManager.networkAddress = inputFieldIP.text;
        ActivateInGameHUD();
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
        switch (player.CurrentLap)
        {
            case 2:
                ts = player.time1;
                curTimeText.text = String.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                break;
                
            case 3:
                ts = player.time2 - player.time1;
                curTimeText.text = String.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                break;
            
        }
        totalTimeText.text = String.Format("{0:00}:{1:00}.{2:00}", curTime.Minutes, curTime.Seconds, curTime.Milliseconds / 10);
    }
}
﻿using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
    [SerializeField] private Image fade;
    
    [SerializeField] private GameObject endingHUD;
    [SerializeField] private Text endNames;
    [SerializeField] private Text endLap1;
    [SerializeField] private Text endLap2;
    [SerializeField] private Text endLap3;
    [SerializeField] private Text endTotal;
    [SerializeField] private GameObject nameField;
    [SerializeField] private GameObject colorField;
    [SerializeField] private Dropdown maxPlayers;
    [SerializeField] private GameObject waitingBox;
    [SerializeField] private Text waitingText;
    [SerializeField] private GameObject waitingReset;
    private float timeout = -1;
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

    private void Update()
    {
        if (timeout != -1)
        {
            timeout += Time.deltaTime;
            if (Mathf.FloorToInt(timeout) == dots)
            {
                UpdateDots();
                dots++;
            }
            if (dots == 6)
            {
                timeout = -1;
                waitingReset.SetActive(true);
            }
        }
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
        if (!state)
            timeout = -1;
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
        timeout = 0;
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
        switch (player.CurrentLap)
        {
            case 2:
                ts = player.time1;
                curTimeText.text = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                break;
                
            case 3:
                ts = player.time2 - player.time1;
                curTimeText.text = String.Format("{0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                break;
            
        }
        totalTimeText.text = String.Format("{0:00}:{1:00}.{2:000}", curTime.Minutes, curTime.Seconds, curTime.Milliseconds / 10);
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

    public void SetScores(string names, string lap1, string lap2, string lap3, string total)
    {
        endNames.text = names;
        endLap1.text = lap1;
        endLap2.text = lap2;
        endLap3.text = lap3;
        endTotal.text = total;
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
            /*else
                dotString += " ";*/
        }
        dotString = "Connecting to server\nPlease Wait\n" + dotString;
        if (dots == 5)
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
}
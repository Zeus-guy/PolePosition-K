using System;
using Mirror;
using UnityEngine;
using Random = System.Random;
using UnityEngine.UI;
using TMPro;
/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

public class SetupPlayer : NetworkBehaviour
{
    [SyncVar] private int m_ID;
    [SyncVar (hook = nameof(SetName))] private string m_Name;
    [SyncVar (hook = nameof(SetColour))] private int m_Colour;

    [SerializeField] private Renderer CarSkin;
    [SerializeField] private Material material_1;
    [SerializeField] private Material material_2;
    [SerializeField] private Material material_3;
    [SerializeField] private Material material_4;

    private UIManager m_UIManager;
    private NetworkManager m_NetworkManager;
    public PlayerController m_PlayerController { get; set; }
    private PlayerInfo m_PlayerInfo;
    private PolePositionManager m_PolePositionManager;
    private InputField _name;
    private bool gameEnded;

    #region Start & Stop Callbacks

    /// <summary>
    /// This is invoked for NetworkBehaviour objects when they become active on the server.
    /// <para>This could be triggered by NetworkServer.Listen() for objects in the scene, or by NetworkServer.Spawn() for objects that are dynamically created.</para>
    /// <para>This will be called for objects on a "host" as well as for object on a dedicated server.</para>
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        m_ID = connectionToClient.connectionId;
    }

    /// <summary>
    /// Called on every NetworkBehaviour when it is activated on a client.
    /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        m_PlayerInfo.ID = m_ID;
        m_PlayerInfo.controller = m_PlayerController;

        //Asignación del nombre del jugador
        SetName("", m_Name);

        SetColour(0, m_Colour);
        
        //m_PlayerController.CurrentLap = 0;
        m_PolePositionManager.AddPlayer(m_PlayerInfo);
        m_PlayerInfo.CheckPoint = 0;
        m_PlayerInfo.CanChangeLap = true;
    }

    //Manda el nombre al servidor
    [Command]
    private void CmdChangeName(string name)
    {
        //GetComponent<NetworkIdentity>().AssignClientAuthority(this.GetComponent<NetworkIdentity>().connectionToClient);
        m_Name = name;
    }
    //Manda el color al servidor
    [Command]
    private void CmdChangeColour(int color)
    {
        //GetComponent<NetworkIdentity>().AssignClientAuthority(this.GetComponent<NetworkIdentity>().connectionToClient);
        m_Colour = color;
    }

    private void SetName(string oldName, string newName)
    {
        m_PlayerInfo.Name = newName;
    }

    private void SetColour(int oldColour, int newColour)
    {
        Material[] materiales = CarSkin.materials;
        switch (newColour)
        {
            case 1:
                materiales[1] = material_1;
                break;
            case 2:
                materiales[1] = material_2;
                break;
            case 3:
                materiales[1] = material_3;
                break;
            case 4:
                materiales[1] = material_4;
                break;
        }
        CarSkin.materials = materiales;
    }

    /// <summary>
    /// Called when the local player object has been set up.
    /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
    /// </summary>
    public override void OnStartLocalPlayer()
    {
        if (isLocalPlayer)
            m_PolePositionManager.SetLocalPlayer(this);
        m_PlayerInfo.ID = m_ID;

        if (isLocalPlayer)
        {
            _name = GameObject.Find("nombre").GetComponent<InputField>();
            m_Name = _name.text;
            if (m_Name == "")
                m_Name = "Player" + m_ID;
            _name.gameObject.SetActive(false);
            CmdChangeName(m_Name);
        }
        SetName("", m_Name);

        if (isLocalPlayer)
        {
            Dropdown Drop_color = GameObject.Find("Colour").GetComponent<Dropdown>();
            m_Colour = Drop_color.value;
            if (m_Colour == 0)
                m_Colour = 1;
            Drop_color.gameObject.SetActive(false);
            CmdChangeColour(m_Colour);
        }
        SetColour(0, m_Colour);
    }

    #endregion

    private void Awake()
    {
        m_PlayerInfo = GetComponent<PlayerInfo>();
        m_PlayerController = GetComponent<PlayerController>();
        m_NetworkManager = FindObjectOfType<NetworkManager>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();
        m_UIManager = FindObjectOfType<UIManager>();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (isLocalPlayer)
        {
            //m_PolePositionManager.barrier.SignalAndWait();
            if (m_PolePositionManager.countdownStarted)
                {
                    m_PlayerController.enabled = true;
                    m_PolePositionManager.gameStarted = true;
                }
            m_PlayerController.OnSpeedChangeEvent += OnSpeedChangeEventHandler;
            ConfigureCamera();
        }
    }

    public void StartGame()
    {
        m_PlayerController.enabled = true;
    }

    public void EndGame()
    {
        m_PlayerController.enabled = false;
        gameEnded = true;
    }
    
    void OnDestroy()
    {
        if (!gameEnded && isLocalPlayer)
        {
            m_PlayerController.enabled = false;
            m_UIManager.ServerCrashMessage();
        }
        if (NetworkManager.singleton.numPlayers <= 0)
        {
            NetworkManager.singleton.StopServer();
        }
    }

    void OnSpeedChangeEventHandler(float speed)
    {
        m_UIManager.UpdateSpeed((int) speed * 5); // 5 for visualization purpose (km/h)
    }

    void ConfigureCamera()
    {
        if (Camera.main != null) 
        {
            Camera.main.gameObject.GetComponent<CameraController>().m_Focus = this.gameObject;
            Camera.main.gameObject.GetComponent<CameraController>().playerInfo = m_PlayerInfo;
        }
    }

    /*public int GetLap()
    {
        return m_PlayerInfo.CurrentLap;
    }*/

    public PlayerInfo GetPlayerInfo()
    {
        return m_PlayerInfo;
    }

    public void SetCheckPoints(Transform[] checkpoints)
    {
        m_PlayerController.checkPoints = (Transform[])checkpoints.Clone();
    }
    [Command]
    public void CmdFinishGame()
    {
        m_PolePositionManager.RpcFinishGame();
        
    }
}
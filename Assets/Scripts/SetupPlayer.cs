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

/// <summary> Esta clase sirve para inicializar la información de cada jugador. </summary>
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
    [SerializeField] private Material[] material_5;
    [SerializeField] private Material[] material_6;
    [SerializeField] private Material carTire;
    [SerializeField] private Material glass;

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
        //No se protege m_ID, pues sólo se modifica al iniciar el servidor.
        m_ID = connectionToClient.connectionId;
        if (!isLocalPlayer)
            m_PlayerController.enabled = true;
            
        if (isServerOnly)
        {
            m_PlayerInfo.ID = m_ID;
            m_PlayerInfo.controller = m_PlayerController;

            m_PolePositionManager.AddPlayer(m_PlayerInfo);
            m_PlayerInfo.CheckPoint = 0;
            m_PlayerInfo.CanChangeLap = true;
        }
    }

    /// <summary>
    /// Called on every NetworkBehaviour when it is activated on a client.
    /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isServer)
            m_UIManager.ActivateClientLobbyHUD();

        m_PlayerInfo.ID = m_ID;
        m_PlayerInfo.controller = m_PlayerController;

        //Asignación del nombre del jugador
        SetName("", m_Name);

        SetColour(0, m_Colour);
        
        m_PolePositionManager.AddPlayer(m_PlayerInfo);
        m_PlayerInfo.CheckPoint = 0;
        m_PlayerInfo.CanChangeLap = true;
        if (isClientOnly)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;

        }
    }

    /// <summary> Comando que cambia el nombre del jugador. No se protege m_Name porque sólo se cambia al iniciar el cliente local. </summary>
    [Command]
    private void CmdChangeName(string name)
    {
        m_PolePositionManager.UpdateUINames();
        m_Name = name;
        SetName("",name);
    }
    
    /// <summary> Comando que cambia el color del coche del jugador. No se protege, porque sólo se ejecuta al seleccionar una opción desde la interfaz y es imposible seleccionar múltiples opciones simultáneamente. </summary>
    [Command]
    public void CmdChangeColour(int color)
    {
        m_Colour = color;
        SetColour(0,color);
    }

    /// <summary> Comando que asigna el valor true al booleano ready del jugador. No la protegemos porque siempre se le asigna el mismo valor. </summary>
    [Command]
    public void CmdPlayerReady()
    {
        m_PlayerInfo.controller.ready = true;
        m_PolePositionManager.UpdateUINames();
        RpcPlayerReady();
        if (m_PolePositionManager.m_Players.Count < m_PolePositionManager.Player_Count)
        {
            return;
        }
        bool start = true;
        foreach (PlayerInfo p in m_PolePositionManager.m_Players)
        {
            if (!p.controller.ready)
                start = false;
        }
        if (start)
        {
            m_PolePositionManager.lobbyEnded = true;
            RpcLobbyEnded();
            m_UIManager.ServerHideDropdowns();
            if (m_PolePositionManager.classLap)
            {
                for (int i = 0; i < m_PolePositionManager.m_Players.Count; i++)
                {
                    m_PolePositionManager.m_Players[i].controller.RecursiveChangeLayer(m_PolePositionManager.m_Players[i].gameObject, 9 + i);
                    if (!m_PolePositionManager.m_Players[i].controller.isLocalPlayer)
                    {
                        m_PolePositionManager.m_Players[i].gameObject.transform.position = m_PolePositionManager.startingPoints[0].transform.position;
                    }
                }
            }

        }
    }

    /// <summary> Asigna el valor true al booleano ready del jugador. </summary>
    [ClientRpc]
    private void RpcPlayerReady()
    {
        m_PlayerInfo.controller.ready = true;
        m_PolePositionManager.UpdateUINames();
    }

    /// <summary> Inicializa los jugadores cuando empieza la partida tras el lobby. </summary>
    [ClientRpc]
    private void RpcLobbyEnded()
    {
        m_PolePositionManager.lobbyEnded = true;
        m_UIManager.StartGameUI();
        m_PolePositionManager.m_LocalSetupPlayer.ConfigureCamera();
        if (m_PolePositionManager.classLap)
        {
            foreach (PlayerInfo p in m_PolePositionManager.m_Players)
            {
                if (!p.controller.isLocalPlayer)
                {
                    p.controller.SetRendererVisibility(false);
                }
            }
            m_PolePositionManager.m_LocalSetupPlayer.gameObject.transform.position = m_PolePositionManager.startingPoints[0].transform.position;
        }
        else
        {
            if (isLocalPlayer)
                m_PlayerController.CmdSetClassified();
            m_PlayerInfo.classified = true;
        }
        m_PolePositionManager.UpdateRaceProgress(true);
    }

    /// <summary> Hook que cambia el nombre del jugador en su PlayerInfo. </summary>
    private void SetName(string oldName, string newName)
    {
        m_PlayerInfo.Name = newName;
        m_PolePositionManager.UpdateUINames();
    }

    /// <summary> Hook que cambia el color del jugador en su PlayerInfo. </summary>
    private void SetColour(int oldColour, int newColour)
    {
        CarSkin.materials = GetCarMaterials(CarSkin.materials, newColour);
        
    }
    /// <summary> Función que devuelve un array de materiales según el color indicado, sólo funciona para coches. </summary>
    public Material[] GetCarMaterials(Material[] sourceMaterials, int color)
    {
        Material[] materiales = sourceMaterials;
        switch (color)
        {
            case 0:
                materiales[1] = material_1;
                break;
            case 1:
                materiales[1] = material_2;
                break;
            case 2:
                materiales[1] = material_3;
                break;
            case 3:
                materiales[1] = material_4;
                break;
            case 4:
                materiales = material_5;
                break;
            case 5:
                materiales = material_6;
                break;
        }
        if (color < 4)
        {
            materiales[0] = carTire;
            materiales[2] = glass;
        }
        return materiales;
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

    }

    #endregion

    private void Awake()
    {
        m_PlayerInfo = GetComponent<PlayerInfo>();
        m_PlayerController = GetComponent<PlayerController>();
        m_NetworkManager = FindObjectOfType<NetworkManager>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();
        m_UIManager = FindObjectOfType<UIManager>();

        SetCheckPoints(m_PolePositionManager.checkPoints);
    }

    /// <summary> En Start, si es el jugador local, se configura la cámara y se activa el PlayerController si ya ha terminado la cuenta atrás. </summary>
    void Start()
    {
        if (isLocalPlayer)
        {
            if (m_PolePositionManager.countdownStarted)
                {
                    m_PlayerController.enabled = true;
                    m_PolePositionManager.gameStarted = true;
                }
            m_PlayerController.OnSpeedChangeEvent += OnSpeedChangeEventHandler;
        }
    }

    /// <summary> Función que habilita el PlayerController del jugador. </summary>
    public void StartGame()
    {
        m_PlayerController.enabled = true;
        m_PlayerController.m_Rigidbody.constraints = RigidbodyConstraints.None;
        m_PlayerInfo.CanChangeLap = true;
        m_PlayerInfo.CheckPoint = 0;
        m_PlayerInfo.LastCheckPoint = 0;
    }

    /// <summary> Función que deshabilita el PlayerController del jugador e indica que la partida ha finalizado. </summary>
    public void EndGame()
    {
        m_PlayerController.enabled = false;
        gameEnded = true;
    }
    
    /// <summary> Cuando se destruye el objeto, si no ha terminado la partida, es porque se ha perdido la conexión con el servidor.
    /// En ese caso, se deshabilita su PlayerController y se muestra un mensaje de error por la interfaz. </summary>
    void OnDestroy()
    {
        if (!gameEnded && isLocalPlayer)
        {
            m_PlayerController.enabled = false;
            m_UIManager.ServerCrashMessage();
        }
    }

    /// <summary> Función llamada por el delegado de cambio de velocidad que actualiza la interfaz. </summary>
    void OnSpeedChangeEventHandler(float speed)
    {
        m_UIManager.UpdateSpeed((int) speed * 5); // 5 for visualization purpose (km/h)
    }

    /// <summary> Función que configura la cámara. </summary>
    void ConfigureCamera()
    {
        if (Camera.main != null) 
        {
            Camera.main.gameObject.GetComponent<CameraController>().m_Focus = this.gameObject;
            Camera.main.gameObject.GetComponent<CameraController>().playerInfo = m_PlayerInfo;
        }
    }

    /// <summary> Función que devuelve una referencia al PlayerInfo del jugador. </summary>
    public PlayerInfo GetPlayerInfo()
    {
        return m_PlayerInfo;
    }

    /// <summary> Función que sirve para asignar el array de checkpoints de PlayerController. </summary>
    public void SetCheckPoints(Transform[] checkpoints)
    {
        m_PlayerController.checkPoints = (Transform[])checkpoints.Clone();
    }
    
    /// <summary> Comando que se ocupa del final de la partida. </summary>
    [Command]
    public void CmdFinishGame()
    {
        if (isServerOnly)
        {
            m_PolePositionManager.RpcFinishGame();
            m_UIManager.FadeOut();
        }
        m_PolePositionManager.RpcFinishGame();
        
    }

    //Funciones importantes para el funcionamiento del chat
    public static event Action<SetupPlayer, string> OnMessage;

    /// <summary> Comando que envía mensajes del chat. </summary>
    [Command]
    public void CmdSend(string message)
    {
        if (message.Trim() != "")
            RpcReceive(message.Trim());
        if (isServerOnly)
            OnMessage?.Invoke(this, message);
    }

    /// <summary> Rpc que se ocupa de recibir los mensajes del chat. </summary>
    [ClientRpc]
    public void RpcReceive(string message)
    {
        OnMessage?.Invoke(this, message);
    }
}
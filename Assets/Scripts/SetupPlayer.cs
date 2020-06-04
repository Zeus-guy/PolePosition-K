﻿using System;
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
    [SyncVar] private string m_Name;
    [SyncVar] private int m_Colour;

    [SerializeField] private Renderer CarSkin;
    [SerializeField] private Material material_1;
    [SerializeField] private Material material_2;
    [SerializeField] private Material material_3;
    [SerializeField] private Material material_4;

    private UIManager m_UIManager;
    private NetworkManager m_NetworkManager;
    private PlayerController m_PlayerController;
    private PlayerInfo m_PlayerInfo;
    private PolePositionManager m_PolePositionManager;
    private InputField _name;

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

        //Asignación del nombre del jugador
        if (m_Name == null)
        {
            _name = GameObject.Find("nombre").GetComponent<InputField>();
            m_Name = _name.text;
            if (m_Name == "")
                m_Name = "Player" + m_ID;
            _name.gameObject.SetActive(false);
            CmdChangeName(m_Name);
        }
        m_PlayerInfo.Name = m_Name;

        if (m_Colour == 0)
        {
            Dropdown Drop_color = GameObject.Find("Colour").GetComponent<Dropdown>();
            m_Colour = Drop_color.value;
            if (m_Colour == 0)
                m_Colour = 1;
            Drop_color.gameObject.SetActive(false);
            CmdChangeColour(m_Colour);
        }
        Material[] materiales = CarSkin.materials;
        switch (m_Colour)
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
        m_PlayerInfo.CurrentLap = 0;
        m_PolePositionManager.AddPlayer(m_PlayerInfo);
    }

    //Manda el nombre al servidor
    [Command]
    private void CmdChangeName(string name)
    {
        GetComponent<NetworkIdentity>().AssignClientAuthority(this.GetComponent<NetworkIdentity>().connectionToClient);
        m_Name = name;
    }
    //Manda el color al servidor
    [Command]
    private void CmdChangeColour(int color)
    {
        GetComponent<NetworkIdentity>().AssignClientAuthority(this.GetComponent<NetworkIdentity>().connectionToClient);
        m_Colour = color;
    }
    /// <summary>
    /// Called when the local player object has been set up.
    /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
    /// </summary>
    public override void OnStartLocalPlayer()
    {
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
            m_PlayerController.enabled = true;
            m_PlayerController.OnSpeedChangeEvent += OnSpeedChangeEventHandler;
            ConfigureCamera();
        }
    }

    void OnSpeedChangeEventHandler(float speed)
    {
        m_UIManager.UpdateSpeed((int) speed * 5); // 5 for visualization purpose (km/h)
    }

    void ConfigureCamera()
    {
        if (Camera.main != null) Camera.main.gameObject.GetComponent<CameraController>().m_Focus = this.gameObject;
    }
}
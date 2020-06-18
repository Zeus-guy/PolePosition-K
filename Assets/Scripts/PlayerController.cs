using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

/// <summary> Clase que se ocupa de todo lo relacionado con el control del coche. </summary>
public class PlayerController : NetworkBehaviour
{
    #region Variables

    [Header("Movement")] public List<AxleInfo> axleInfos;
    public float forwardMotorTorque = 100000;
    public float backwardMotorTorque = 50000;
    public float maxSteeringAngle = 15;
    public float engineBrake = 1e+12f;
    public float footBrake = 1e+24f;
    public float topSpeed = 200f;
    public float downForce = 100f;
    public float slipLimit = 0.2f;

    private float CurrentRotation { get; set; }
    private float InputAcceleration { get; set; }
    private float InputSteering { get; set; }
    private float InputBrake { get; set; }

    private PlayerInfo m_PlayerInfo;
    private PolePositionManager m_PolePositionManager;
    private UIManager m_UIManager;

    public Rigidbody m_Rigidbody;
    private float m_SteerHelper = 0.8f;
    private float m_CurrentSpeed = 0;
    public const float maxDownTime = 3;
    public float currentDownTime = maxDownTime;
    public Transform[] checkPoints;
    [SyncVar] public float arcLength;
    [SyncVar(hook = nameof(ChangeLapHook))] public int CurrentLap;
    [SyncVar] public bool ready;

    private float Speed
    {
        get { return m_CurrentSpeed; }
        set
        {
            if (Math.Abs(m_CurrentSpeed - value) < float.Epsilon) return;
            m_CurrentSpeed = value;
            if (OnSpeedChangeEvent != null)
                OnSpeedChangeEvent(m_CurrentSpeed);
        }
    }


    public delegate void OnSpeedChangeDelegate(float newVal);

    public event OnSpeedChangeDelegate OnSpeedChangeEvent;

    #endregion Variables

    #region Unity Callbacks

    public void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        m_PlayerInfo = GetComponent<PlayerInfo>();
        m_UIManager = FindObjectOfType<UIManager>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();
    }

    /// <summary> En Update se toman los inputs del jugador y se guardan en las variables correspondientes. </summary>
    public void Update()
    {
        InputAcceleration = Input.GetAxis("Vertical");
        InputSteering = Input.GetAxis(("Horizontal"));
        InputBrake = Input.GetAxis("Jump");
        Speed = m_Rigidbody.velocity.magnitude;
    }

    /// <summary> En FixedUpdate se actualizan las físicas según los valores tomados anteriormente de los inputs. 
    /// <para> También se devuelve al jugador al último checkpoint por el que ha pasado si lleva demasiado tiempo sin poder moverse o tocando la hierba. </para></summary>
    public void FixedUpdate()
    {
        InputSteering = Mathf.Clamp(InputSteering, -1, 1);
        InputAcceleration = Mathf.Clamp(InputAcceleration, -1, 1);
        InputBrake = Mathf.Clamp(InputBrake, 0, 1);

        float steering = maxSteeringAngle * InputSteering;

        foreach (AxleInfo axleInfo in axleInfos)
        {
            if (axleInfo.steering)
            {
                axleInfo.leftWheel.steerAngle = steering;
                axleInfo.rightWheel.steerAngle = steering;
            }

            if (axleInfo.motor)
            {
                if (InputAcceleration > float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = forwardMotorTorque;
                    axleInfo.leftWheel.brakeTorque = 0;
                    axleInfo.rightWheel.motorTorque = forwardMotorTorque;
                    axleInfo.rightWheel.brakeTorque = 0;
                }

                if (InputAcceleration < -float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = -backwardMotorTorque;
                    axleInfo.leftWheel.brakeTorque = 0;
                    axleInfo.rightWheel.motorTorque = -backwardMotorTorque;
                    axleInfo.rightWheel.brakeTorque = 0;
                }

                if (Math.Abs(InputAcceleration) < float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = 0;
                    axleInfo.leftWheel.brakeTorque = engineBrake;
                    axleInfo.rightWheel.motorTorque = 0;
                    axleInfo.rightWheel.brakeTorque = engineBrake;
                }

                if (InputBrake > 0)
                {
                    axleInfo.leftWheel.brakeTorque = footBrake;
                    axleInfo.rightWheel.brakeTorque = footBrake;
                }
            }

            ApplyLocalPositionToVisuals(axleInfo.leftWheel);
            ApplyLocalPositionToVisuals(axleInfo.rightWheel);
        }

        SteerHelper();
        SpeedLimiter();
        AddDownForce();
        TractionControl();

        if (currentDownTime <= 0)
        {
            m_Rigidbody.velocity = Vector3.zero;
            this.gameObject.transform.position = checkPoints[m_PlayerInfo.LastCheckPoint].position;
            this.gameObject.transform.eulerAngles = checkPoints[m_PlayerInfo.LastCheckPoint].eulerAngles;
            currentDownTime = maxDownTime;
        }
    }

    #endregion

    #region Methods

    // crude traction control that reduces the power to wheel if the car is wheel spinning too much
    private void TractionControl()
    {
        foreach (var axleInfo in axleInfos)
        {
            WheelHit wheelHitLeft;
            WheelHit wheelHitRight;
            axleInfo.leftWheel.GetGroundHit(out wheelHitLeft);
            axleInfo.rightWheel.GetGroundHit(out wheelHitRight);

            if (wheelHitLeft.forwardSlip >= slipLimit)
            {
                var howMuchSlip = (wheelHitLeft.forwardSlip - slipLimit) / (1 - slipLimit);
                axleInfo.leftWheel.motorTorque -= axleInfo.leftWheel.motorTorque * howMuchSlip * slipLimit;
            }

            if (wheelHitRight.forwardSlip >= slipLimit)
            {
                var howMuchSlip = (wheelHitRight.forwardSlip - slipLimit) / (1 - slipLimit);
                axleInfo.rightWheel.motorTorque -= axleInfo.rightWheel.motorTorque * howMuchSlip * slipLimit;
            }
        }
    }

    // this is used to add more grip in relation to speed
    private void AddDownForce()
    {
        foreach (var axleInfo in axleInfos)
        {
            axleInfo.leftWheel.attachedRigidbody.AddForce(
                -transform.up * (downForce * axleInfo.leftWheel.attachedRigidbody.velocity.magnitude));
        }
    }

    private void SpeedLimiter()
    {
        float speed = m_Rigidbody.velocity.magnitude;
        if (speed > topSpeed)
            m_Rigidbody.velocity = topSpeed * m_Rigidbody.velocity.normalized;
    }

    // finds the corresponding visual wheel
    // correctly applies the transform
    public void ApplyLocalPositionToVisuals(WheelCollider col)
    {
        if (col.transform.childCount == 0)
        {
            return;
        }

        Transform visualWheel = col.transform.GetChild(0);
        Vector3 position;
        Quaternion rotation;
        col.GetWorldPose(out position, out rotation);
        var myTransform = visualWheel.transform;
        myTransform.position = position;
        myTransform.rotation = rotation;
    }

    /// <summary> En la función SteerHelper, se comprueba si o el coche está tumbado o no puede moverse, o si alguna rueda no está tocando el suelo o está tocando la hierba. 
    /// <para>Si se da alguna de esas situaciones, se decrementa un contador según el tiempo transcurrido desde la anterior llamada a la función. Si no, se resetea a su valor máximo. </para></summary>
    private void SteerHelper()
    {
        foreach (var axleInfo in axleInfos)
        {
            WheelHit[] wheelHit = new WheelHit[2];
            axleInfo.leftWheel.GetGroundHit(out wheelHit[0]);
            axleInfo.rightWheel.GetGroundHit(out wheelHit[1]);
            foreach (var wh in wheelHit)
            {
                if (wh.normal == Vector3.zero || wh.collider == null || (wh.collider != null && wh.collider.name == "grass"))
                {
                    currentDownTime -= Time.fixedDeltaTime;
                    return; // wheels arent on the ground so dont realign the rigidbody velocity
                }
            }
            currentDownTime = maxDownTime;
        }

        // this if is needed to avoid gimbal lock problems that will make the car suddenly shift direction
        if (Mathf.Abs(CurrentRotation - transform.eulerAngles.y) < 10f)
        {
            var turnAdjust = (transform.eulerAngles.y - CurrentRotation) * m_SteerHelper;
            Quaternion velRotation = Quaternion.AngleAxis(turnAdjust, Vector3.up);
            m_Rigidbody.velocity = velRotation * m_Rigidbody.velocity;
        }

        CurrentRotation = transform.eulerAngles.y;
    }

    /// <summary> Función que se ejecuta cuando el coche pasa por algún checkpoint.
    /// Si toca el siguiente checkpoint, se incrementa el contador de checkpoints y se guarda como el mayor checkpoint hasta el momento.
    ///Además, al pasar por el checkpoint 1, se modifica el valor de un booleano que indica que se puede cambiar de vuelta.
    /// <para>Si toca el anterior checkpoint, el contador interno que maneja el retorno al último checkpoint pasa a valer 0, 
    /// por lo que se teletransportará inmediatamente al jugador. Si el checkpoint en el que estaba antes no era el 1, se decrementa ese contador.</para>
    /// Esto sirve para que no puedas pasar del checkpoint 1 al 0, con lo que se evita que el jugador haga trampas.</summary>
    void OnTriggerEnter(Collider col)
    {
        int nextCheckPoint = (m_PlayerInfo.CheckPoint + 1) % 6;
        int previousCheckPoint = (m_PlayerInfo.CheckPoint - 1) % 6;

        if (int.Parse(col.name) == nextCheckPoint)
        {
            m_PlayerInfo.CheckPoint = nextCheckPoint;
            m_PlayerInfo.LastCheckPoint = nextCheckPoint;
            if (m_PlayerInfo.CheckPoint == 1)
                m_PlayerInfo.CanChangeLap = true;
        }
        else if (int.Parse(col.name) == previousCheckPoint)
        {
            currentDownTime = 0;
            if (m_PlayerInfo.CheckPoint != 1)
                m_PlayerInfo.CheckPoint = previousCheckPoint;
        }
    }

    /// <summary> Hook que se activará en los clientes cuando cambie el valor de la vuelta del jugador.
    /// <para> Sirve para asignar el valor correcto de la vuelta actual al PlayerInfo del jugador correspondiente, y para actualizar la interfaz. </para> </summary>
    public void ChangeLapHook(int oldLap, int newLap)
    {
        m_PlayerInfo.CurrentLap = newLap;
        m_PlayerInfo.CanChangeLap = false;
        if (isLocalPlayer)
            m_UIManager.SetLap(newLap);
    }

    #endregion

    #region Commands
    /// <summary> Comando que actualiza el valor de arcLength. </summary>
    [Command]
    public void CmdUpdateArcLength(float newArcL)
    {
        arcLength = newArcL;
    }

    /// <summary> Comando que incrementa el valor de CurrentLap. </summary>
    [Command]
    public void CmdIncreaseLap()
    {
        CurrentLap++;
    }
    /// <summary> Comando que resetea el valor de CurrentLap a 0. </summary>
    [Command]
    public void CmdResetLap()
    {
        m_PolePositionManager.gameStarted = false;
        m_PolePositionManager.timerStarted = false;
        m_PolePositionManager.countdown = m_PolePositionManager.MAXCOUNTDOWN;
        CurrentLap = 0;
        m_PlayerInfo.time1 = new TimeSpan();
    }
    /// <summary> Comando que asigna true a la variable classified del PlayerInfo a través de un Rpc. También lo hace directamente en el servidor. </summary>
    [Command]
    public void CmdSetClassified()
    {
        RpcSetClassified();
        m_PlayerInfo.classified = true;
    }
    /// <summary> Comando que guarda el tiempo de la vuelta indicada. </summary>
    [Command]
    public void CmdChangeTimes(int lap, long time)
    {
        if (isServer)
        {
            switch (lap)
            {
                case 0:
                    m_PlayerInfo.time1 = new TimeSpan(time);
                    break;
                case 1:
                    m_PlayerInfo.time2 = new TimeSpan(time);
                    break;
                case 2:
                    m_PlayerInfo.time3 = new TimeSpan(time);
                    break;
            }
        }
        RpcChangeTimes(lap, time);
    }
    #endregion

    #region RPCs

    /// <summary> Asigna el valor true al booleano classified del PlayerInfo correspondiente. </summary>
    [ClientRpc]
    private void RpcSetClassified()
    {
        m_PlayerInfo.classified = true;
    }

    /// <summary> Cambia los tiempos del jugador en todos los clientes. </summary>
    [ClientRpc]
    private void RpcChangeTimes(int lap, long time)
    {
        switch (lap)
        {
            case 0:
                m_PlayerInfo.time1 = new TimeSpan(time);
                break;
            case 1:
                m_PlayerInfo.time2 = new TimeSpan(time);
                break;
            case 2:
                m_PlayerInfo.time3 = new TimeSpan(time);
                break;
        }
    }

    #endregion
}
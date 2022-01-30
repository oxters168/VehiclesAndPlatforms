using UnityEngine;
using Rewired;
using UnityHelpers;
using EVP;

public class RewiredPlayerInput : MonoBehaviour
{
    public int playerId = 0;
    private Player player;

    public Vector2 orbitSensitivity = Vector2.one;
    public OrbitCameraController orbitCamera;

    public MovementController3D character;

    private Vector2 prevMousePosition = Vector2.zero;

    public bool inVehicle;
    [Tooltip("Frequency of vehicle sphere cast in seconds")]
    public float vehicleCastTime = 0.1f;
    public float vehicleCastRadius = 2;
    public LayerMask vehicleMask = ~0;
    public VehicleController targetVehicle;
    public VehicleController nearestVehicle;

    private bool prevEnterExit = false;
    private bool prevReset = false;
    private bool prevInVehicle = false;

    private float prevVehicleCastTime = float.MinValue;

    void Start()
    {
        player = ReInput.players.GetPlayer(playerId);        
    }

    void OnDrawGizmosSelected()
    {
        if (character)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(character.transform.position, vehicleCastRadius);
        }
    }

    void Update()
    {
        orbitCamera.SetLookHorizontal(player.GetAxis("CameraX"));
        orbitCamera.SetLookVertical(player.GetAxis("CameraY"));

        if (character && !inVehicle && (Time.time - prevVehicleCastTime) > vehicleCastTime)
        {
            prevVehicleCastTime = Time.time;
            nearestVehicle = GetNearestVehicle(character.transform.position, vehicleCastRadius, vehicleMask);
        }
    }
    void FixedUpdate()
    {
        EnterExitVehicle();
        CharacterInput();
        VehicleInput();

        prevInVehicle = inVehicle;
    }

    private void EnterExitVehicle()
    {
        bool enterExitVehicle = player.GetButton("EnterVehicle") || player.GetButton("ExitVehicle");
        if (enterExitVehicle && enterExitVehicle != prevEnterExit)
        {
            if (inVehicle)
            {
                character.transform.position = targetVehicle.transform.position + Vector3.up;
                character.gameObject.SetActive(true);
                targetVehicle = null;
                inVehicle = false;
            }
            else if (nearestVehicle)
            {
                targetVehicle = nearestVehicle;
                character.gameObject.SetActive(false);
                inVehicle = true;
            }
        }
        prevEnterExit = enterExitVehicle;
    }
    private void CharacterInput()
    {
        if (character && !inVehicle)
        {
            orbitCamera.target = character.transform;

            character.currentInput.horizontal = player.GetAxis("MoveX");
            character.currentInput.vertical = player.GetAxis("MoveY");
            character.currentInput.jump = player.GetButton("Jump");
            character.currentInput.sprint = player.GetButton("Sprint");
            character.currentInput.upAxisOrientation = orbitCamera.transform.rotation.GetYAxisRotation();
        }
    }
    private void VehicleInput()
    {
        if (targetVehicle && inVehicle)
        {
            orbitCamera.target = targetVehicle.transform;

            float steerInput = player.GetAxis("Steer");
            float handbrakeInput = Mathf.Clamp01(player.GetAxis("Handbrake"));
            float forwardInput = Mathf.Clamp01(player.GetAxis("Throttle"));
            float reverseInput = Mathf.Clamp01(-player.GetAxis("Throttle"));
            
            bool resetVehicle = false;
            bool resetDown = player.GetButton("ResetVehicle");
            if (resetDown && resetDown != prevReset)
                resetVehicle = true;
            prevReset = resetDown;

            DriveVehicle(targetVehicle, resetVehicle, steerInput, handbrakeInput, forwardInput, reverseInput);
        }
    }

	private static void DriveVehicle (VehicleController targetVehicle, bool m_doReset, float steerInput, float handbrakeInput, float forwardInput, float reverseInput, bool continuousForwardAndReverse = true, bool handbrakeOverridesThrottle = false)
    {
        if (targetVehicle == null) return;

        // Translate forward/reverse to vehicle input

        float throttleInput = 0.0f;
        float brakeInput = 0.0f;

        if (continuousForwardAndReverse)
        {
            float minSpeed = 0.1f;
            float minInput = 0.1f;

            if (targetVehicle.speed > minSpeed)
            {
                throttleInput = forwardInput;
                brakeInput = reverseInput;
            }
            else
            {
                if (reverseInput > minInput)
                {
                    throttleInput = -reverseInput;
                    brakeInput = 0.0f;
                }
                else if (forwardInput > minInput)
                {
                    if (targetVehicle.speed < -minSpeed)
                    {
                        throttleInput = 0.0f;
                        brakeInput = forwardInput;
                    }
                    else
                    {
                        throttleInput = forwardInput;
                        brakeInput = 0;
                    }
                }
            }
        }
        else
        {
            bool reverse = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (!reverse)
            {
                throttleInput = forwardInput;
                brakeInput = reverseInput;
            }
            else
            {
                throttleInput = -reverseInput;
                brakeInput = 0;
            }
        }

        // Override throttle if specified

        if (handbrakeOverridesThrottle)
        {
            throttleInput *= 1.0f-handbrakeInput;
        }

        // Apply input to vehicle

        targetVehicle.steerInput = steerInput;
        targetVehicle.throttleInput = throttleInput;
        targetVehicle.brakeInput = brakeInput;
        targetVehicle.handbrakeInput = handbrakeInput;

        // Do a vehicle reset

        if (m_doReset)
            targetVehicle.ResetVehicle();
    }

    private static VehicleController GetNearestVehicle(Vector3 point, float radius, LayerMask vehicleMask)
    {
        var vehiclesInVicinity = Physics.SphereCastAll(point, radius, Vector3.forward, 0.001f, vehicleMask);
        float nearestDist = float.MaxValue;
        VehicleController nearest = null;
        foreach (var castedObject in vehiclesInVicinity)
        {
            var vehicle = castedObject.rigidbody?.GetComponentInChildren<VehicleController>();
            if (vehicle)
            {
                float currentSqrDist = (castedObject.point - point).sqrMagnitude;
                if (currentSqrDist < nearestDist)
                {
                    nearestDist = currentSqrDist;
                    nearest = vehicle;
                }
            }
        }
        return nearest;
    }
}

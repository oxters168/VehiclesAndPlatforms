using UnityEngine;
using UnityHelpers;
using Rewired;

using System.Collections.Generic;
using System.Linq;

public class RigidbodyCharacterMovement : MonoBehaviour
{
    public int playerId = 0;
    private Player player;

    public Rigidbody characterBody;
    private Rigidbody[] allBodies;
    public float totalMass { get; private set; }
    // private Rigidbody CharacterBody { get { if (_characterBody == null) _characterBody = GetComponentInChildren<Rigidbody>(); return _characterBody; } }
    // private Rigidbody _characterBody;

    [Tooltip("m/s^2")]
    public float acceleration = 0.2f;
    [Tooltip("m/s")]
    public float maxSpeed = 5;

    [Space(10)]
    public Vector2 inputStrafe;

    void Start()
    {
        player = ReInput.players.GetPlayer(playerId);
        var tempAllBodies = new List<Rigidbody>(GetComponentsInChildren<Rigidbody>());
        // tempAllBodies.Remove(characterBody);
        totalMass = tempAllBodies.Select(body => body.mass).Aggregate((mass, otherMass) => mass + otherMass);
        allBodies = tempAllBodies.ToArray();
        Debug.Log("Total mass is " + totalMass + " of " + tempAllBodies.Count + " rigidbodies");
    }

    void FixedUpdate()
    {
        var currentPlanarVelocity = new Vector3(characterBody.velocity.x, 0, characterBody.velocity.z);
        var currentPlanarSpeed = currentPlanarVelocity.magnitude;
        var currentPlanarDir = currentPlanarVelocity.normalized;
        Debug.Log(currentPlanarSpeed);
        Debug.DrawRay(characterBody.position + Vector3.up, currentPlanarDir * (currentPlanarSpeed / maxSpeed), Color.blue, Time.fixedDeltaTime);

        inputStrafe = new Vector2(player.GetAxis("Horizontal"), player.GetAxis("Vertical"));
        inputStrafe = inputStrafe.ToCircle();
        Debug.DrawRay(characterBody.position + Vector3.up / 2, inputStrafe.ToXZVector3(), Color.yellow, Time.fixedDeltaTime);

        var currentAcc = acceleration;
        Vector3 planarInput = new Vector3(inputStrafe.x, 0, inputStrafe.y);
        var nextPlanarVelocity = currentPlanarVelocity + planarInput * currentAcc * Time.fixedDeltaTime;
        var nextPlanarSpeed = nextPlanarVelocity.magnitude;
        if (nextPlanarSpeed > maxSpeed)
            currentAcc = (maxSpeed - currentPlanarSpeed) / Time.fixedDeltaTime;
        float appliedForce = totalMass * currentAcc;
        characterBody.AddForce(planarInput * appliedForce, ForceMode.Force);
    }
}

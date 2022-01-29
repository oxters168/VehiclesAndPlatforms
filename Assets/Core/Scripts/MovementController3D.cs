using UnityEngine;
using UnityHelpers;

[System.Serializable]
public struct InputData
{
    public Quaternion upAxisOrientation;
    [Range(-1, 1)]
    public float horizontal;
    [Range(-1, 1)]
    public float vertical;
    public bool jump;
    public bool sprint;
}
public struct PhysicalData3D
{
    public Vector3 velocity;
    public Collider leftWall, rightWall, frontWall, backWall, topWall, botWall;
}
public class MovementController3D : MonoBehaviour
{
    public InputData currentInput;
    private InputData prevInput;


    [Space(10)]
    public float walkSpeed = 2.5f;
    public float runSpeed = 4;
    public float climbSpeed = 2;
    public float walkJumpSpeed = 5;
    public float runJumpSpeed = 4;
    public float addedFallAcceleration = 9.8f;
    public float wallDetectionDistance = 0.01f;
    public LayerMask groundMask = ~0;
    public LayerMask wallMask = ~0;
    public LayerMask ceilingMask = ~0;

    // [Space(10)]
    // public bool isIndoors;
    // public string outdoorCharacterLayer = "OutdoorCharacter";
    // public string indoorCharacterLayer = "IndoorCharacter";
    // public LayerMask outdoorPhysicalLayers = ~0;
    // public LayerMask indoorPhysicalLayers = ~0;

    public float deadzone = 0.1f;

    // [Space(10), Tooltip("Inverts the direction the player faces")]
    // public bool invertFlip;

    // public enum SpecificState { IdleLeft, IdleRight, WalkLeft, WalkRight, RunLeft, RunRight, JumpFaceLeft, JumpFaceRight, JumpMoveLeft, JumpMoveRight, FallFaceLeft, FallFaceRight, FallMoveLeft, FallMoveRight, ClimbLeftIdle, ClimbLeftUp, ClimbLeftDown, ClimbRightIdle, ClimbRightUp, ClimbRightDown, ClimbTopIdleLeft, ClimbTopIdleRight, ClimbTopMoveLeft, ClimbTopMoveRight }
    public enum SpecificState { Idle, Walk, Run, Jump, Fall, TopClimb, TopClimbIdle, SideClimbUp, SideClimbDown, SideClimbIdle }
    public enum AnimeState { Idle, Walk, Run, Jump, AirFall, Land, TopClimb, TopClimbIdle, SideClimb, SideClimbIdle }

    // [Space(10)]
    // public ValuesVault controlValues;
    // private SpriteRenderer[] Sprite7Up { get { if (_sprite7Up == null) _sprite7Up = GetComponentsInChildren<SpriteRenderer>(); return _sprite7Up; } }
    // private SpriteRenderer[] _sprite7Up;
    private Rigidbody AffectedBody { get { if (_affectedBody == null) _affectedBody = GetComponent<Rigidbody>(); return _affectedBody; } }
    private Rigidbody _affectedBody;
    private Animator[] SpriteAnim { get { if (_animator == null) _animator = GetComponentsInChildren<Animator>(); return _animator; } }
    private Animator[] _animator;

    private SpecificState prevState;
    public SpecificState currentState;
    private PhysicalData3D currentPhysicals = default;

    [Space(10)]
    public float leftDetectOffset = -0.01f;
    public float rightDetectOffset = -0.01f;
    public float frontDetectOffset = -0.01f;
    public float backDetectOffset = -0.01f;
    public float topDetectOffset = -0.01f;
    public float bottomDetectOffset = -0.01f;

    [Space(10)]
    public bool debugWallRays = true;
    private Bounds colliderBounds;

    private Vector3 otherObjectVelocity;
    private Vector3 otherObjectPrevVelocity;

    private Vector3 currentDirection = Vector3.forward;
    // private FixedJoint2D attachJoint;

    void Update()
    {
        currentPhysicals.velocity = AffectedBody.velocity;
        DetectWall();
        TickState();
        ApplyAnimation();
        // Debug.Log(currentState);

        // int currentLayer = isIndoors ? (LayerMask.NameToLayer(indoorCharacterLayer)) : (LayerMask.NameToLayer(outdoorCharacterLayer));
        // gameObject.layer = currentLayer;
        // foreach (Transform t in transform)
        //     t.gameObject.layer = currentLayer;
        // groundMask &= isIndoors ? ~outdoorPhysicalLayers : ~indoorPhysicalLayers; //Remove indoor/outdoor layers from gound mask
        // groundMask |= isIndoors ? indoorPhysicalLayers : outdoorPhysicalLayers; //Add indoor/outdoor layers to gound mask
        // wallMask &= isIndoors ? ~outdoorPhysicalLayers : ~indoorPhysicalLayers;
        // wallMask |= isIndoors ? indoorPhysicalLayers : outdoorPhysicalLayers;
        // ceilingMask &= isIndoors ? ~outdoorPhysicalLayers : ~indoorPhysicalLayers;
        // ceilingMask |= isIndoors ? indoorPhysicalLayers : outdoorPhysicalLayers;
    }
    void FixedUpdate()
    {
        RetrieveSurroundingVelocity();
        // AttachToBody();
        MoveCharacter();
        prevInput = currentInput;
    }
    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(colliderBounds.center, colliderBounds.size);
    }

    private void RetrieveSurroundingVelocity()
    {
        otherObjectPrevVelocity = otherObjectVelocity;
        //Get other object's velocity if climbing or standing on it to keep up with it
        if (currentPhysicals.frontWall && (currentState == SpecificState.SideClimbDown || currentState == SpecificState.SideClimbUp || currentState == SpecificState.SideClimbIdle))
        {
            var frontWallPhysics = currentPhysicals.frontWall.GetComponentInChildren<Rigidbody>();
            if (!frontWallPhysics)
                frontWallPhysics = currentPhysicals.frontWall.GetComponentInParent<Rigidbody>();

            if (frontWallPhysics)
            {
                // Debug.Log(frontWallPhysics.name);
                otherObjectVelocity = frontWallPhysics.velocity;
            }
        }
        else if (currentPhysicals.topWall && (currentState == SpecificState.TopClimb || currentState == SpecificState.TopClimbIdle))
        {
            var topWallPhysics = currentPhysicals.topWall.GetComponentInChildren<Rigidbody>();
            if (!topWallPhysics)
                topWallPhysics = currentPhysicals.topWall.GetComponentInParent<Rigidbody>();

            if (topWallPhysics)
            {
                // Debug.Log(topWallPhysics.name);
                otherObjectVelocity = topWallPhysics.velocity;
            }
        }
        else if (currentPhysicals.botWall)
        {
            var botWallPhysics = currentPhysicals.botWall.GetComponentInChildren<Rigidbody>();
            if (!botWallPhysics)
                botWallPhysics = currentPhysicals.botWall.GetComponentInParent<Rigidbody>();
            
            if (botWallPhysics)
            {
                // Debug.Log(botWallPhysics.name);
                otherObjectVelocity = botWallPhysics.velocity; //Might want to remove y value because object is below us and we are only standing on it, not grabbing on to it
            }
        }

        // Debug.Log(otherObjectVelocity);
        //Cancel out any velocities that can't be achieved;
        // if ((currentPhysicals.rightWall && otherObjectVelocity.x > float.Epsilon) || (currentPhysicals.leftWall && otherObjectVelocity.x < -float.Epsilon))
        //     otherObjectVelocity = new Vector2(0, otherObjectVelocity.y);
        // if ((currentPhysicals.topWall && otherObjectVelocity.y > float.Epsilon) || (currentPhysicals.botWall && otherObjectVelocity.y < -float.Epsilon))
        //     otherObjectVelocity = new Vector2(otherObjectVelocity.x, 0);
    }

    private void ApplyAnimation()
    {
        var prevAnimeState = GetAnimeFromState(prevState);
        var currentAnimeState = GetAnimeFromState(currentState);
        if (prevAnimeState != currentAnimeState)
            SetAnimTrigger(currentAnimeState.ToString());
    }
    private void SetAnimTrigger(string name)
    {
        foreach(var spriteAnim in SpriteAnim)
            spriteAnim.SetTrigger(name);
    }

    private void MoveCharacter()
    {
        float verticalForce = 0;
        Vector3 expectedVelocityDir = currentInput.upAxisOrientation * (new Vector2(currentInput.horizontal, currentInput.vertical).ToCircle()).ToXZVector3();

        if (!expectedVelocityDir.IsZero())
            currentDirection = expectedVelocityDir;

        Vector3 otherObjectPredictedVelocity = (otherObjectVelocity - otherObjectPrevVelocity);
        
        float planarSpeed = 0;
        if (currentState == SpecificState.Idle || currentState == SpecificState.Walk || currentState == SpecificState.Run || currentState == SpecificState.SideClimbUp || currentState == SpecificState.SideClimbDown || currentState == SpecificState.SideClimbIdle || currentState == SpecificState.TopClimb || currentState == SpecificState.TopClimbIdle || currentState == SpecificState.Fall || currentState == SpecificState.Jump)
        {
            if (currentState == SpecificState.TopClimb)
                planarSpeed = climbSpeed;
            else if (currentState != SpecificState.Idle && currentState != SpecificState.TopClimbIdle && currentState != SpecificState.SideClimbUp && currentState != SpecificState.SideClimbDown && currentState != SpecificState.SideClimbIdle)
            {
                if (!currentInput.sprint)
                    planarSpeed = walkSpeed;
                else
                    planarSpeed = runSpeed;
            }
        }
                
        Vector3 currentVelocity = AffectedBody.velocity.xz().ToXZVector3();
        // if (currentVelocity.magnitude > runSpeed)
        //     currentVelocity = currentVelocity.normalized * runSpeed; //To stop the character from having super human power
        // Vector3 planarForce = PhysicsHelpers.CalculateRequiredForceForSpeed(AffectedBody.mass, currentVelocity, expectedVelocityDir * planarSpeed, Time.fixedDeltaTime);
        // Vector3 addedPlanarForce = PhysicsHelpers.CalculateRequiredForceForSpeed(AffectedBody.mass, AffectedBody.velocity.xz().ToXZVector3(), (otherObjectVelocity.xz() + otherObjectPredictedVelocity.xz()).ToXZVector3(), Time.fixedDeltaTime);
        Vector3 planarForce = PhysicsHelpers.CalculateRequiredForceForSpeed(AffectedBody.mass, currentVelocity, expectedVelocityDir * planarSpeed + (otherObjectVelocity.xz() + otherObjectPredictedVelocity.xz()).ToXZVector3(), Time.fixedDeltaTime); 

        // Debug.Log(currentState + " from " + prevState);
        if (currentState == SpecificState.SideClimbUp || currentState == SpecificState.SideClimbDown || currentState == SpecificState.SideClimbIdle || currentState == SpecificState.TopClimb || currentState == SpecificState.TopClimbIdle || (currentState == SpecificState.Jump && prevState != SpecificState.Jump))
        {
            float verticalSpeed = 0;
            if (currentState == SpecificState.SideClimbUp)
                verticalSpeed = climbSpeed;
            else if (currentState == SpecificState.SideClimbDown)
                verticalSpeed = -climbSpeed;
            else if (currentState != SpecificState.SideClimbIdle && currentState != SpecificState.TopClimb && currentState != SpecificState.TopClimbIdle)
            {
                if (Mathf.Abs(currentInput.horizontal) > deadzone && currentInput.sprint)
                    verticalSpeed = runJumpSpeed;
                else
                    verticalSpeed = walkJumpSpeed;
            }

            // verticalForce = PhysicsHelpers.CalculateRequiredForceForSpeed(AffectedBody.mass, AffectedBody.velocity.y, verticalSpeed, Time.fixedDeltaTime, true);
            verticalForce = PhysicsHelpers.CalculateRequiredForceForSpeed(AffectedBody.mass, AffectedBody.velocity.y, (otherObjectVelocity.y + otherObjectPredictedVelocity.y) + verticalSpeed, Time.fixedDeltaTime, true);
            // verticalForce = PhysicsHelpers.CalculateRequiredForceForSpeed(AffectedBody.mass, AffectedBody.velocity.y, (otherObjectVelocity.y + otherObjectPredictedVelocity.y) + verticalSpeed, Time.fixedDeltaTime, true);
        }

        //Added weight when falling for better feel
        if (currentState == SpecificState.Fall && currentPhysicals.velocity.y < -float.Epsilon)
            verticalForce -= AffectedBody.mass * addedFallAcceleration;

        //When the jump button stops being held then stop ascending
        if (currentState == SpecificState.Jump && currentPhysicals.velocity.y > 0 && prevInput.jump && !currentInput.jump)
            verticalForce = PhysicsHelpers.CalculateRequiredForceForSpeed(AffectedBody.mass, AffectedBody.velocity.y, 0, Time.fixedDeltaTime);
        
        // if (Mathf.Abs(currentInput.horizontal) > float.Epsilon || (Mathf.Abs(currentInput.horizontal) < float.Epsilon && Mathf.Abs(prevInput.horizontal) > float.Epsilon))
        if (Mathf.Abs((planarForce).magnitude) > float.Epsilon)
            AffectedBody.AddForce(planarForce);
        if (Mathf.Abs(verticalForce) > float.Epsilon)
            AffectedBody.AddForce(Vector3.up * verticalForce);

        AffectedBody.MoveRotation(Quaternion.LookRotation(currentDirection));
    }

    private void TickState()
    {
        var nextState = GetNextState(currentState, prevState, currentInput, currentPhysicals, deadzone);
        prevState = currentState;
        currentState = nextState;
    }
    private static SpecificState GetNextState(SpecificState currentState, SpecificState prevState, InputData currentInput, PhysicalData3D currentPhysicals, float deadzone = float.Epsilon)
    {
        var nextState = currentState;

        bool isInputtingWalk = currentInput.horizontal > deadzone || currentInput.horizontal < -deadzone || currentInput.vertical > deadzone || currentInput.vertical < -deadzone;

        switch (currentState)
        {
            case SpecificState.Idle:
                if (isInputtingWalk && currentInput.sprint)
                    nextState = SpecificState.Run;
                else if (isInputtingWalk)
                    nextState = SpecificState.Walk;
                else if (!currentPhysicals.botWall)
                    nextState = SpecificState.Fall;
                else if (currentInput.jump)
                    nextState = SpecificState.Jump;
                else if (currentPhysicals.topWall && currentInput.jump)
                    nextState = SpecificState.TopClimbIdle;
                else if (!currentPhysicals.botWall)
                    nextState = SpecificState.Fall;
                break;
            case SpecificState.Walk:
                if (isInputtingWalk && currentInput.sprint)
                    nextState = SpecificState.Run;
                else if (!isInputtingWalk)
                    nextState = SpecificState.Idle;
                else if (!currentPhysicals.botWall)
                    nextState = SpecificState.Fall;
                else if (currentInput.jump)
                    nextState = SpecificState.Jump;
                else if (currentPhysicals.frontWall)
                    nextState = SpecificState.SideClimbIdle;
                else if (currentPhysicals.topWall && currentInput.jump)
                    nextState = SpecificState.TopClimb;
                break;
            case SpecificState.Run:
                if (!isInputtingWalk)
                    nextState = SpecificState.Idle;
                else if (currentInput.jump)
                    nextState = SpecificState.Jump;
                else if (!currentInput.sprint)
                    nextState = SpecificState.Walk;
                else if (!currentPhysicals.botWall)
                    nextState = SpecificState.Fall;
                else if (currentPhysicals.frontWall)
                    nextState = SpecificState.SideClimbIdle;
                else if (currentPhysicals.topWall && currentInput.jump)
                    nextState = SpecificState.TopClimb;
                break;
            case SpecificState.Jump:
                if (currentPhysicals.velocity.y < -deadzone)
                    nextState = SpecificState.Fall;
                else if (currentPhysicals.botWall)
                    nextState = SpecificState.Idle;
                else if (currentPhysicals.topWall && currentInput.jump)
                    nextState = SpecificState.TopClimbIdle;
                break;
            case SpecificState.Fall:
                if (currentPhysicals.botWall && isInputtingWalk && currentInput.sprint)
                    nextState = SpecificState.Run;
                else if (currentPhysicals.botWall && isInputtingWalk)
                    nextState = SpecificState.Walk;
                else if (currentPhysicals.botWall)
                    nextState = SpecificState.Idle;
                else if (currentPhysicals.frontWall && isInputtingWalk)
                    nextState = SpecificState.SideClimbIdle;
                break;
            case SpecificState.SideClimbIdle:
                if (!currentPhysicals.frontWall || !currentInput.jump)
                    nextState = SpecificState.Fall;
                else if (currentInput.vertical > deadzone)
                    nextState = SpecificState.SideClimbUp;
                else if (currentInput.vertical < -deadzone && !currentPhysicals.botWall)
                    nextState = SpecificState.SideClimbDown;
                break;
            case SpecificState.SideClimbUp:
                if (!currentPhysicals.frontWall || !currentInput.jump)
                    nextState = SpecificState.Fall;
                else if (currentInput.vertical > -deadzone && currentInput.vertical < deadzone)
                    nextState = SpecificState.SideClimbIdle;
                else if (currentInput.vertical < -deadzone)
                    nextState = SpecificState.SideClimbDown;
                else if (currentPhysicals.topWall)
                    nextState = SpecificState.TopClimbIdle;
                break;
            case SpecificState.SideClimbDown:
                if (!currentPhysicals.frontWall || !currentInput.jump)
                    nextState = SpecificState.Fall;
                else if (currentPhysicals.botWall || (currentInput.vertical > -deadzone && currentInput.vertical < deadzone))
                    nextState = SpecificState.SideClimbIdle;
                else if (currentInput.vertical > deadzone)
                    nextState = SpecificState.SideClimbUp;
                break;
            case SpecificState.TopClimbIdle:
                if (!currentInput.jump)
                    nextState = SpecificState.Fall;
                else if (isInputtingWalk)
                    nextState = SpecificState.TopClimb;
                else if (currentPhysicals.frontWall && currentInput.vertical < -deadzone)
                    nextState = SpecificState.SideClimbDown;
                else if (!currentPhysicals.topWall)
                    nextState = SpecificState.Fall;
                break;
            case SpecificState.TopClimb:
                if (!currentInput.jump)
                    nextState = SpecificState.Fall;
                else if (!isInputtingWalk)
                    nextState = SpecificState.TopClimbIdle;
                else if (currentPhysicals.frontWall)
                    nextState = SpecificState.SideClimbIdle;
                else if (!currentPhysicals.topWall)
                    nextState = SpecificState.Fall;
                break;
        }
        return nextState;
    }
    private static AnimeState GetAnimeFromState(SpecificState state)
    {
        var animeState = AnimeState.Idle;

        switch (state)
        {
            case SpecificState.Walk:
                animeState = AnimeState.Walk;
                break;
            case SpecificState.Idle:
                animeState = AnimeState.Idle;
                break;
            case SpecificState.Run:
                animeState = AnimeState.Run;
                break;
            case SpecificState.Jump:
                animeState = AnimeState.Jump;
                break;
            case SpecificState.Fall:
                animeState = AnimeState.AirFall;
                break;
            case SpecificState.SideClimbIdle:
                animeState = AnimeState.SideClimbIdle;
                break;
            case SpecificState.SideClimbUp:
            case SpecificState.SideClimbDown:
                animeState = AnimeState.SideClimb;
                break;
            case SpecificState.TopClimbIdle:
                animeState = AnimeState.TopClimbIdle;
                break;
            case SpecificState.TopClimb:
                animeState = AnimeState.TopClimb;
                break;
        }

        return animeState;
    }

    private Collider WallCast(bool debug, LayerMask mask, int colCount, int rowCount, Vector3 center, Vector3 normal, Vector3 ortho1, float ortho1Size, Vector3 ortho2, float ortho2Size)
    {
        Collider wallHit = null;
        Vector3 startCorner = center - (ortho1 * ortho1Size / 2) - (ortho2 * ortho2Size / 2);
        float ortho1Step = ortho1Size / (colCount - 1);
        float ortho2Step = ortho2Size / (rowCount - 1);
        for (int row = 0; row < rowCount; row++)
        {
            for (int col = 0; col < colCount; col++)
            {
                Vector3 currentPoint = startCorner + (ortho1 * ortho1Step * col) + (ortho2 * ortho2Step * row);
                Ray cast = new Ray(currentPoint, normal);

                RaycastHit hitInfo;
                var wallHasBeenHit = Physics.Raycast(cast.origin, cast.direction, out hitInfo, wallDetectionDistance, mask);
                if (debug)
                    Debug.DrawRay(cast.origin, cast.direction * wallDetectionDistance, hitInfo.transform != null ? Color.green : Color.red);
                if (wallHasBeenHit)
                {
                    wallHit = hitInfo.collider;
                    break;
                }
            }
            if (wallHit != null)
                break;
        }
        return wallHit;
    }
    private void DetectWall()
    {
        colliderBounds = transform.GetTotalBounds(Space.Self, true);

        Vector3 rightFaceCenter = transform.position + transform.up * colliderBounds.extents.y + transform.right * colliderBounds.extents.x;
        Vector3 leftFaceCenter = transform.position + transform.up * colliderBounds.extents.y - transform.right * colliderBounds.extents.x;
        Vector3 frontFaceCenter = transform.position + transform.up * colliderBounds.extents.y + transform.forward * colliderBounds.extents.z;
        Vector3 backFaceCenter = transform.position + transform.up * colliderBounds.extents.y - transform.forward * colliderBounds.extents.z;
        Vector3 topFaceCenter = transform.position + transform.up * colliderBounds.size.y;
        Vector3 botFaceCenter = transform.position;

        rightFaceCenter += transform.right * rightDetectOffset;
        leftFaceCenter -= transform.right * leftDetectOffset;
        frontFaceCenter += transform.forward * frontDetectOffset;
        backFaceCenter -= transform.forward * backDetectOffset;
        topFaceCenter += transform.up * topDetectOffset;
        botFaceCenter -= transform.up * bottomDetectOffset;

        currentPhysicals.rightWall = WallCast(debugWallRays, wallMask, 5, 5, rightFaceCenter, transform.right, transform.up, colliderBounds.size.y, transform.forward, colliderBounds.size.z);
        currentPhysicals.leftWall = WallCast(debugWallRays, wallMask, 5, 5, leftFaceCenter, -transform.right, transform.up, colliderBounds.size.y, transform.forward, colliderBounds.size.z);
        currentPhysicals.frontWall = WallCast(debugWallRays, wallMask, 5, 5, frontFaceCenter, transform.forward, transform.up, colliderBounds.size.y, transform.right, colliderBounds.size.x);
        currentPhysicals.backWall = WallCast(debugWallRays, wallMask, 5, 5, backFaceCenter, -transform.forward, transform.up, colliderBounds.size.y, transform.right, colliderBounds.size.x);
        currentPhysicals.topWall = WallCast(debugWallRays, wallMask, 5, 5, topFaceCenter, transform.up, transform.right, colliderBounds.size.x, transform.forward, colliderBounds.size.z);
        currentPhysicals.botWall = WallCast(debugWallRays, wallMask, 5, 5, botFaceCenter, -transform.up, transform.right, colliderBounds.size.x, transform.forward, colliderBounds.size.z);
    }
}

using UnityEngine;

namespace MORTIS.Players
{
    [RequireComponent(typeof(CharacterController))]
    public class LedgeClimber : MonoBehaviour
    {
        [Header("Camera")]
        public Transform viewForward;

        [Header("What can we climb?")]
        public LayerMask climbableLayers;

        [Header("Camera-based detection")]
        public float wallCheckDistance = 1.8f;
        public float minLedgeBelowCamera = 0.7f;
        public float maxLedgeAboveCamera = 2.5f;
        public float ledgeSearchExtraAbove = 0.5f;

        [Header("Hang & Climb feel")]
        public float hangSnapBack = 0.4f;

        [Tooltip("How far below the ledge top you hang. Increase to lower the camera.")]
        public float hangHeightBelowTop = 0.9f; // bumped default for more 'dangling' feel

        public float climbUpTime = 0.35f;
        public float climbForwardDistance = 0.7f;

        [Header("Grab Phase")]
        [Tooltip("Duration of the initial grab movement into the hanging pose.")]
        public float grabTime = 0.12f;

        CharacterController cc;

        private enum ClimbState { Normal, Grabbing, Hanging, Climbing }
        private ClimbState state = ClimbState.Normal;

        private Vector3 climbStartPos;
        private Vector3 climbTargetPos;
        private float climbTimer;

        // For grab tween
        private Vector3 grabStartPos;
        private Vector3 grabTargetPos;
        private float grabTimer;

        public bool IsBusy => state != ClimbState.Normal;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (viewForward == null && Camera.main != null)
                viewForward = Camera.main.transform;
        }

        public void Tick(float deltaTime, Vector2 moveInput, bool jumpPressed)
        {
            switch (state)
            {
                case ClimbState.Grabbing:
                    HandleGrabbing(deltaTime);
                    break;
                case ClimbState.Hanging:
                    HandleHanging(moveInput, jumpPressed);
                    break;
                case ClimbState.Climbing:
                    HandleClimbing(deltaTime);
                    break;
            }
        }

        public bool TryStartLedgeHang()
        {
            if (state != ClimbState.Normal)
                return false;
            if (viewForward == null)
                return false;

            Vector3 camPos = viewForward.position;

            Vector3 forwardFlat = viewForward.forward;
            forwardFlat.y = 0f;
            if (forwardFlat.sqrMagnitude < 0.0001f)
                forwardFlat = transform.forward;
            forwardFlat.Normalize();

            // 1) Wall in front of camera
            if (!Physics.Raycast(
                    camPos,
                    forwardFlat,
                    out RaycastHit wallHit,
                    wallCheckDistance,
                    climbableLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // 2) Find ledge top
            float camY = camPos.y;
            float searchTopY = camY + maxLedgeAboveCamera + ledgeSearchExtraAbove;

            Vector3 topSearchStart = new Vector3(
                wallHit.point.x,
                searchTopY,
                wallHit.point.z
            );

            float maxDown = maxLedgeAboveCamera + minLedgeBelowCamera + 1f;

            if (!Physics.Raycast(
                    topSearchStart,
                    Vector3.down,
                    out RaycastHit ledgeHit,
                    maxDown,
                    climbableLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            float ledgeY = ledgeHit.point.y;
            float relativeToCamera = ledgeY - camY;

            if (relativeToCamera < -minLedgeBelowCamera || relativeToCamera > maxLedgeAboveCamera)
                return false;

            // 3) Space to stand
            Vector3 up = Vector3.up;
            Vector3 standCenter = ledgeHit.point + up * (cc.height * 0.5f + 0.05f);

            Vector3 capsuleTop = standCenter + up * (cc.height * 0.5f - cc.radius);
            Vector3 capsuleBottom = standCenter - up * (cc.height * 0.5f - cc.radius);

            if (Physics.CheckCapsule(
                    capsuleTop,
                    capsuleBottom,
                    cc.radius,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // 4) Hanging position
            Vector3 wallNormal = wallHit.normal;

            Vector3 hangPos = ledgeHit.point
                              - wallNormal * hangSnapBack
                              - up * hangHeightBelowTop;

            // Start a short grab tween from current position to hangPos
            grabStartPos = transform.position;
            grabTargetPos = hangPos;
            grabTimer = 0f;

            // Face the wall immediately so the tween moves "into" the ledge
            Vector3 lookDir = -wallNormal;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDir);

            state = ClimbState.Grabbing;
            return true;
        }

        private void HandleGrabbing(float deltaTime)
        {
            if (grabTime <= 0f)
            {
                grabTime = 0.01f;
            }

            grabTimer += deltaTime;
            float t = Mathf.Clamp01(grabTimer / grabTime);

            // Slight ease-out so it slows as you reach the hang
            t = t * t * (3f - 2f * t);

            Vector3 newPos = Vector3.Lerp(grabStartPos, grabTargetPos, t);
            Vector3 delta = newPos - transform.position;
            cc.Move(delta);

            if (t >= 1f)
            {
                // Fully in hanging pose now
                state = ClimbState.Hanging;
            }
        }

        private void HandleHanging(Vector2 moveInput, bool jumpPressed)
        {
            if (jumpPressed || moveInput.y > 0.1f)
            {
                StartClimbUp();
                return;
            }

            if (moveInput.y < -0.1f)
            {
                state = ClimbState.Normal;
            }
        }

        private void StartClimbUp()
        {
            climbStartPos = transform.position;

            Vector3 forwardFlat = transform.forward;
            forwardFlat.y = 0f;
            forwardFlat.Normalize();
            Vector3 up = Vector3.up;

            Vector3 upOffset = up * (cc.height * 0.9f); // lower to 0.8f if final view feels too high
            Vector3 forwardOffset = forwardFlat * climbForwardDistance;

            climbTargetPos = transform.position + upOffset + forwardOffset;

            if (climbUpTime <= 0f)
                climbUpTime = 0.01f;

            climbTimer = 0f;
            state = ClimbState.Climbing;
        }

        private void HandleClimbing(float deltaTime)
        {
            climbTimer += deltaTime;
            float t = Mathf.Clamp01(climbTimer / climbUpTime);

            t = t * t * (3f - 2f * t);

            Vector3 newPos = Vector3.Lerp(climbStartPos, climbTargetPos, t);
            Vector3 delta = newPos - transform.position;
            cc.Move(delta);

            if (t >= 1f)
            {
                state = ClimbState.Normal;
            }
        }
    }
}

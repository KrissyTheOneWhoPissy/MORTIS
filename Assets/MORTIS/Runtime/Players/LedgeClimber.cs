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
        public float hangHeightBelowTop = 0.9f;

        public float climbUpTime = 0.35f;
        public float climbForwardDistance = 0.7f;

        [Header("Grab Phase")]
        [Tooltip("Duration of the initial grab movement into the hanging pose.")]
        public float grabTime = 0.12f;

        [Header("Procedural Hand Targets (no anchors needed)")]
        public float handSeparation = 0.45f;          // distance between hands (meters)
        public float handBelowTop = 0.08f;            // hands slightly below the top point
        public float handInFromEdge = 0.10f;          // push hands slightly back from the edge toward the player

        [Header("Stand Target Tuning")]
        public float standForwardFromEdge = 0.45f;    // how far onto the top we end up
        public float standExtraUp = 0.02f;            // small lift to avoid ground clipping

        [System.Serializable]
        public struct LedgeInfo
        {
            public RaycastHit wallHit;
            public RaycastHit topHit;

            public Vector3 ledgeTopPoint;
            public Vector3 wallNormal;
            public Vector3 forwardFlat;      // camera/player forward flattened
            public Vector3 edgeDir;          // left/right along the ledge

            public Quaternion faceWallRotation;

            public Vector3 hangRootPos;      // CharacterController center position when hanging
            public Vector3 standRootPos;     // CharacterController center position when standing

            public Vector3 leftHandTarget;   // world-space IK target (later)
            public Vector3 rightHandTarget;
        }

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

        // Stored ledge info for the active climb
        private LedgeInfo activeLedge;

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

        /// <summary>
        /// Non-destructive check: "If the player pressed Space right now, would we be able to start hanging?"
        /// Use this for UI prompts.
        /// </summary>
        public bool CanStartLedgeHangNow()
        {
            if (state != ClimbState.Normal) return false;
            if (viewForward == null) return false;

            return TryGetLedgeInfo(out _);
        }

        public bool TryStartLedgeHang()
        {
            if (state != ClimbState.Normal)
                return false;
            if (viewForward == null)
                return false;

            if (!TryGetLedgeInfo(out activeLedge))
                return false;

            Vector3 hangPos = activeLedge.hangRootPos;

            // Start a short grab tween from current position to hangPos
            grabStartPos = transform.position;
            grabTargetPos = hangPos;
            grabTimer = 0f;

            // Face the wall immediately so the tween moves "into" the ledge
            transform.rotation = activeLedge.faceWallRotation;

            state = ClimbState.Grabbing;
            return true;
        }

        /// <summary>
        /// Step 1: Ledge detection that returns a stable LedgeInfo including:
        /// - hangRootPos / standRootPos
        /// - left/right hand targets (procedurally computed, no anchors required)
        /// </summary>
        private bool TryGetLedgeInfo(out LedgeInfo info)
        {
            info = default;

            if (viewForward == null) return false;

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

            // 2) Find top surface by raycasting down from above the wall hit
            float camY = camPos.y;
            float searchTopY = camY + maxLedgeAboveCamera + ledgeSearchExtraAbove;

            Vector3 topSearchStart = new Vector3(wallHit.point.x, searchTopY, wallHit.point.z);
            float maxDown = maxLedgeAboveCamera + minLedgeBelowCamera + 1f;

            if (!Physics.Raycast(
                    topSearchStart,
                    Vector3.down,
                    out RaycastHit topHit,
                    maxDown,
                    climbableLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            float relativeToCamera = topHit.point.y - camY;
            if (relativeToCamera < -minLedgeBelowCamera || relativeToCamera > maxLedgeAboveCamera)
                return false;

            Vector3 wallNormal = wallHit.normal.normalized;

            // Direction along the ledge edge (left/right)
            Vector3 edgeDir = Vector3.Cross(Vector3.up, wallNormal);
            edgeDir.y = 0f;
            if (edgeDir.sqrMagnitude < 0.0001f)
                edgeDir = Vector3.right;
            edgeDir.Normalize();

            // Facing direction (toward the wall)
            Vector3 faceDir = -wallNormal;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude < 0.0001f)
                faceDir = forwardFlat;
            faceDir.Normalize();

            Quaternion faceWallRot = Quaternion.LookRotation(faceDir);

            Vector3 up = Vector3.up;
            Vector3 ledgeTop = topHit.point;

            // 3) Compute hang root target (CharacterController center position)
            Vector3 hangRoot = ledgeTop
                               - wallNormal * hangSnapBack
                               - up * hangHeightBelowTop;

            // 4) Compute stand root target (CharacterController center position)
            Vector3 standRoot = ledgeTop
                                + up * (cc.height * 0.5f + standExtraUp)
                                + (-wallNormal) * standForwardFromEdge;

            // 5) Validate space to stand (capsule check at standRoot)
            Vector3 standCenter = standRoot;
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

            // 6) Procedural hand targets (no anchors)
            Vector3 handBase = ledgeTop
                               - up * handBelowTop
                               - wallNormal * handInFromEdge;

            Vector3 rightHand = handBase + edgeDir * (handSeparation * 0.5f);
            Vector3 leftHand = handBase - edgeDir * (handSeparation * 0.5f);

            info = new LedgeInfo
            {
                wallHit = wallHit,
                topHit = topHit,
                ledgeTopPoint = ledgeTop,
                wallNormal = wallNormal,
                forwardFlat = forwardFlat,
                edgeDir = edgeDir,
                faceWallRotation = faceWallRot,
                hangRootPos = hangRoot,
                standRootPos = standRoot,
                leftHandTarget = leftHand,
                rightHandTarget = rightHand
            };

            return true;
        }

        private void HandleGrabbing(float deltaTime)
        {
            if (grabTime <= 0f) grabTime = 0.01f;

            grabTimer += deltaTime;
            float t = Mathf.Clamp01(grabTimer / grabTime);

            // ease-out
            t = t * t * (3f - 2f * t);

            Vector3 newPos = Vector3.Lerp(grabStartPos, grabTargetPos, t);
            Vector3 delta = newPos - transform.position;
            cc.Move(delta);

            if (t >= 1f)
            {
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

            // NOTE: For now we keep the old climb motion.
            // Later steps will replace this with a trajectory using activeLedge.standRootPos.
            Vector3 forwardFlat = transform.forward;
            forwardFlat.y = 0f;
            forwardFlat.Normalize();
            Vector3 up = Vector3.up;

            Vector3 upOffset = up * (cc.height * 0.9f);
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            if (state != ClimbState.Normal) return;

            if (TryGetLedgeInfo(out LedgeInfo l))
            {
                Gizmos.DrawSphere(l.ledgeTopPoint, 0.05f);

                Gizmos.DrawSphere(l.leftHandTarget, 0.05f);
                Gizmos.DrawSphere(l.rightHandTarget, 0.05f);

                Gizmos.DrawSphere(l.hangRootPos, 0.05f);
                Gizmos.DrawSphere(l.standRootPos, 0.05f);

                Gizmos.DrawLine(l.ledgeTopPoint, l.ledgeTopPoint + l.wallNormal * 0.5f);
                Gizmos.DrawLine(l.ledgeTopPoint, l.ledgeTopPoint + l.edgeDir * 0.5f);
            }
        }
#endif
    }
}

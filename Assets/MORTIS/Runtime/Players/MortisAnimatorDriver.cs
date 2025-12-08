using Unity.Netcode;
using UnityEngine;

namespace MORTIS.Players
{
    public class MortisAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private PlayerMover mover;
        [SerializeField] private Animator animator;

        [Header("Tuning")]
        [SerializeField] private float dampTime = 0.12f;
        [SerializeField] private float minMoveSpeed = 0.05f;

        private CharacterController cc;

        static readonly int SpeedHash  = Animator.StringToHash("Speed");
        static readonly int GroundHash = Animator.StringToHash("IsGrounded");
        static readonly int CrouchHash = Animator.StringToHash("IsCrouching");
        static readonly int JumpHash   = Animator.StringToHash("Jump");

        void Awake()
        {
            if (!mover) mover = GetComponent<PlayerMover>();
            if (!animator) animator = GetComponentInChildren<Animator>(true);

            cc = mover ? mover.GetComponent<CharacterController>() : null;
        }

        void Update()
        {
            if (!mover || !animator) return;

            // Only gate on ownership once the NetworkObject is actually spawned.
            if (mover is NetworkBehaviour nb && nb.NetworkObject != null && nb.NetworkObject.IsSpawned)
            {
                if (!nb.IsOwner) return;
            }

            float hs = 0f;
            if (cc != null)
            {
                Vector3 v = cc.velocity;
                v.y = 0f;
                hs = v.magnitude;
            }
            else
            {
                hs = mover.HorizontalSpeed;
            }

            float max = mover.MaxGroundSpeed > 0.001f ? mover.MaxGroundSpeed : 1f;
            float speed01 = Mathf.Clamp01(hs / max);
            if (hs < minMoveSpeed) speed01 = 0f;

            animator.SetFloat(SpeedHash, speed01, dampTime, Time.deltaTime);
            animator.SetBool(GroundHash, mover.IsGrounded);
        }

        public void TriggerJump() => animator.SetTrigger(JumpHash);
        public void SetCrouching(bool crouching) => animator.SetBool(CrouchHash, crouching);
    }
}

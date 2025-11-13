using UnityEngine;
using MORTIS.Players;

[RequireComponent(typeof(Collider))]
public class Trampoline : MonoBehaviour
{
    [Header("Bounce")]
    [SerializeField] float baseBounceStrength = 8f;    // minimal upward velocity
    [SerializeField] float impactScale       = 0.5f;   // how much fall speed adds to bounce
    [SerializeField] float maxBounceStrength = 18f;    // hard cap on upward speed

    [Header("Charge (Hold Jump)")]
    [SerializeField] float chargeRate         = 0.7f;  // how fast charge builds per second
    [SerializeField] float maxChargeMultiplier = 1.8f; // up to 1.8x stronger bounce

    PlayerMover currentPlayer;
    bool lastGroundedOnTrampoline;
    float charge;             // 0..1
    float lastVerticalVel;    // track previous vertical velocity

    void Reset()
    {
        // Make sure this is a trigger
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var mover = other.GetComponentInParent<PlayerMover>();
        if (mover != null && mover.IsOwner)  // only care about local owner
        {
            currentPlayer = mover;
            charge = 0f;
            lastGroundedOnTrampoline = mover.IsGrounded;
            lastVerticalVel = mover.VerticalVelocity;
        }
    }

    void OnTriggerExit(Collider other)
    {
        var mover = other.GetComponentInParent<PlayerMover>();
        if (mover != null && mover == currentPlayer)
        {
            currentPlayer = null;
            charge = 0f;
            lastGroundedOnTrampoline = false;
        }
    }

    void Update()
    {
        if (currentPlayer == null) return;

        bool grounded = currentPlayer.IsGrounded;
        float vVel    = currentPlayer.VerticalVelocity;

        // Build charge while standing on trampoline & holding jump
        if (grounded && currentPlayer.JumpHeld)
        {
            charge += chargeRate * Time.deltaTime;
            charge = Mathf.Clamp01(charge);
        }

        // Detect landing: in air last frame, grounded this frame
        if (!lastGroundedOnTrampoline && grounded)
        {
            // landing impact based on how fast we were falling
            float impact = Mathf.Abs(lastVerticalVel);

            // Base bounce + extra from impact
            float bounce = baseBounceStrength + impact * impactScale;

            // Apply charge multiplier (1..maxChargeMultiplier)
            float chargeMultiplier = Mathf.Lerp(1f, maxChargeMultiplier, charge);
            bounce *= chargeMultiplier;

            // Clamp final bounce
            bounce = Mathf.Min(bounce, maxBounceStrength);

            currentPlayer.ApplyVerticalImpulse(bounce);

            // Optional: keep some charge for chain bounces or reset it
            // Uncomment this to reset completely:
            // charge = 0f;
        }

        // Remember for next frame
        lastGroundedOnTrampoline = grounded;
        lastVerticalVel = vVel;
    }
}

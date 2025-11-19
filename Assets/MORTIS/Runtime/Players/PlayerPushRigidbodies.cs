using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerPushRigidbodies : MonoBehaviour
{
    [Tooltip("How strongly speed converts into push force.")]
    public float pushForceMultiplier = 1.5f;

    [Tooltip("Minimum player speed required before pushing starts.")]
    public float minSpeedToPush = 0.5f;

    [Tooltip("Maximum push force (to avoid yeeting planets into orbit).")]
    public float maxPushForce = 20f;

    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody rb = hit.rigidbody;
        if (rb == null || rb.isKinematic)
            return;

        // Don't push things mostly under our feet
        if (hit.moveDirection.y < -0.3f)
            return;

        // How fast is the player moving?
        float speed = controller.velocity.magnitude;

        // Ignore tiny movements so just standing next to the ball doesn't push it
        if (speed < minSpeedToPush)
            return;

        // Convert speed → force
        float force = Mathf.Clamp(speed * pushForceMultiplier, 0f, maxPushForce);

        // Push direction: horizontal, in the direction we are moving into the object
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z).normalized;

        rb.AddForce(pushDir * force, ForceMode.Impulse);
    }
}

using Unity.Netcode;
using UnityEngine;

namespace MORTIS.Players
{
    public class CameraHeadPositionFollow : NetworkBehaviour
    {
        [SerializeField] Transform headAnchor;   // under Visuals rig (HeadAnchor)
        [SerializeField] Transform cmTarget;     // CM_Target (under Player)
        [SerializeField] float positionLerp = 30f;

        void LateUpdate()
        {
            if (!IsOwner) return;
            if (!headAnchor || !cmTarget) return;

            // CM_Target should be a child of Player (yaw lives on Player).
            Transform player = cmTarget.parent;
            if (!player) return;

            // Convert head anchor world position into Player local space and apply as CM_Target localPosition.
            Vector3 targetLocal = player.InverseTransformPoint(headAnchor.position);

            if (positionLerp <= 0f)
                cmTarget.localPosition = targetLocal; // snap
            else
                cmTarget.localPosition = Vector3.Lerp(cmTarget.localPosition, targetLocal, Time.deltaTime * positionLerp);
        }
    }
}

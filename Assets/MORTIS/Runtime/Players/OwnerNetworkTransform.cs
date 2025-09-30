using Unity.Netcode;
using Unity.Netcode.Components; // <-- required for NetworkTransform

namespace MORTIS.Players
{
    // Owner-authoritative NetworkTransform (equivalent to NGO's sample)
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}

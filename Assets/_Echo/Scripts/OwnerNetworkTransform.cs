using Unity.Netcode.Components;

namespace Echo
{
    // Позицию героя двигает его владелец, остальные получают её по сети.
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}

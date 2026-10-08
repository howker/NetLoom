using System;

namespace NetLoom.Application.Topology
{
    public interface IDeviceConfirmationStore
    {
        void SetUnconfirmed(Guid deviceId, bool isUnconfirmed);
    }
}

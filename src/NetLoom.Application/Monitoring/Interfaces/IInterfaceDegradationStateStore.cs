using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public interface IInterfaceDegradationStateStore
    {
        InterfaceDegradationState Load(
            Guid deviceId,
            int ifIndex);

        InterfaceDegradationState ReplaceAndGetPrevious(
            InterfaceDegradationState current);
    }
}

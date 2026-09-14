using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public interface IInterfaceDegradationDeliveryAdapter
    {
        void Deliver(
            InterfaceDegradationOutboxEvent item);
    }
}

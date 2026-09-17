using System;

namespace NetLoom.Application.Topology
{
    public interface IManualTopologyService
    {
        ManualTopologyEditorSnapshot GetSnapshot();

        Guid CreateDevice(
            string name,
            ManualTopologyDeviceCategory category,
            string notes);

        void UpdateDevice(
            Guid deviceId,
            string name,
            ManualTopologyDeviceCategory category,
            string notes);

        void DeleteDevice(Guid deviceId);

        Guid CreatePort(
            Guid deviceId,
            string name,
            string mediaType);

        void UpdatePort(
            Guid interfaceId,
            string name,
            string mediaType);

        void DeletePort(Guid interfaceId);

        Guid CreateLink(
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId,
            string mediaType,
            string notes);

        void UpdateLink(
            Guid physicalLinkId,
            string mediaType,
            string notes);

        void DeleteLink(Guid physicalLinkId);
    }
}

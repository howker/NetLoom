using System;
using System.Windows;
using System.Windows.Controls;
using NetLoom.Application.Locations;
using NetLoom.Application.Topology;

namespace NetLoom.Wpf.Shell
{
    internal interface IShellTopologyEditor
    {
        bool HasChanges { get; }

        event EventHandler CloseRequested;
    }

    internal sealed class ManualTopologyEditorControl :
        UserControl,
        IShellTopologyEditor
    {
        private readonly ManualTopologyWindow
            _editor;

        public ManualTopologyEditorControl(
            IManualTopologyService service,
            Guid? initialDeviceId,
            Guid? initialLinkId,
            Window messageOwner)
        {
            _editor =
                new ManualTopologyWindow(
                    service,
                    initialDeviceId,
                    initialLinkId);

            _editor.CloseRequested +=
                OnEditorCloseRequested;

            Content =
                _editor.ExtractContentForEmbedding(
                    messageOwner);
        }

        public bool HasChanges =>
            _editor.HasChanges;

        public event EventHandler
            CloseRequested;

        private void OnEditorCloseRequested(
            object sender,
            EventArgs e)
        {
            CloseRequested?.Invoke(
                this,
                EventArgs.Empty);
        }
    }

    internal sealed class LocationTopologyEditorControl :
        UserControl,
        IShellTopologyEditor
    {
        private readonly LocationTopologyWindow
            _editor;

        public LocationTopologyEditorControl(
            ILocationTopologyService service,
            Guid? initialLocationId,
            Window messageOwner)
        {
            _editor =
                new LocationTopologyWindow(
                    service,
                    initialLocationId);

            _editor.CloseRequested +=
                OnEditorCloseRequested;

            Content =
                _editor.ExtractContentForEmbedding(
                    messageOwner);
        }

        public bool HasChanges =>
            _editor.HasChanges;

        public event EventHandler
            CloseRequested;

        private void OnEditorCloseRequested(
            object sender,
            EventArgs e)
        {
            CloseRequested?.Invoke(
                this,
                EventArgs.Empty);
        }
    }
}

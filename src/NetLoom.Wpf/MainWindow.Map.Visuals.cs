using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NetLoom.Application.Alerts;
using NetLoom.Application.Locations;
using NetLoom.Application.Lookup;
using NetLoom.Application.MapLayout;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyMap;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private sealed class MapLocationVisual
    {
        public MapLocationVisual(
            Border border,
            FrameworkElement header,
            TextBlock title,
            TextBlock description,
            Button collapseButton,
            TextBlock lockBadge,
            Thumb resizeThumb)
        {
            Border = border;
            Header = header;
            Title = title;
            Description = description;
            CollapseButton = collapseButton;
            LockBadge = lockBadge;
            ResizeThumb = resizeThumb;
        }

        public Border Border { get; }

        public FrameworkElement Header { get; }

        public TextBlock Title { get; }

        public TextBlock Description { get; }

        public Button CollapseButton { get; }

        public TextBlock LockBadge { get; }

        public Thumb ResizeThumb { get; }

        public Guid LocationId { get; set; }

        public Guid? ParentLocationId { get; set; }

        public bool IsCollapsed { get; set; }

        public bool IsLocked { get; set; }

        public double ExpandedWidth { get; set; }

        public double ExpandedHeight { get; set; }
    }

    private sealed class MapNodeVisual
    {
        public MapNodeVisual(
            Border border,
            Border stateStripe,
            TextBlock title,
            TextBlock secondary,
            Path categoryIcon,
            TextBlock lockBadge)
        {
            Border = border;
            StateStripe = stateStripe;
            Title = title;
            Secondary = secondary;
            CategoryIcon = categoryIcon;
            LockBadge = lockBadge;
        }

        public Border Border { get; }

        public Border StateStripe { get; }

        public TextBlock Title { get; }

        public TextBlock Secondary { get; }

        public Path CategoryIcon { get; }

        public TextBlock LockBadge { get; }

        public Guid? DeviceId { get; set; }

        public Guid? LocationId { get; set; }

        public bool IsManual { get; set; }

        public bool IsLocked { get; set; }
    }

    private sealed class MapLinkVisual
    {
        public MapLinkVisual(
            Line line,
            TextBlock label)
        {
            Line = line;
            Label = label;
        }

        public Line Line { get; }

        public TextBlock Label { get; }

        public MapFreshness? LastFreshness { get; set; }
    }

}

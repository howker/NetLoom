using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private static string NodeCategoryIconGeometryKey(
        MapNodeCategory category)
    {
        switch (category)
        {
            case MapNodeCategory.MediaConverter:
                return "NetLoom.Icon.MediaConverter";

            case MapNodeCategory.UnmanagedSwitch:
                return "NetLoom.Icon.UnmanagedSwitch";

            case MapNodeCategory.OpticalConverter:
                return "NetLoom.Icon.OpticalConverter";

            case MapNodeCategory.PassiveNetworkEquipment:
                return "NetLoom.Icon.PassiveNetworkEquipment";

            default:
                return "NetLoom.Icon.Unknown";
        }
    }

    private static string NodeCategoryIconBrushKey(
        MapNodeCategory category)
    {
        return category == MapNodeCategory.Unknown
            ? "NetLoom.Brush.TextDisabled"
            : "NetLoom.Brush.Accent";
    }

    private static string NodeCategoryIconToolTip(
        MapNodeCategory category)
    {
        switch (category)
        {
            case MapNodeCategory.MediaConverter:
                return UiText.Get(
                    "CategoryMediaConverter");

            case MapNodeCategory.UnmanagedSwitch:
                return UiText.Get(
                    "CategoryUnmanagedSwitch");

            case MapNodeCategory.OpticalConverter:
                return UiText.Get(
                    "CategoryOpticalConverter");

            case MapNodeCategory.PassiveNetworkEquipment:
                return UiText.Get(
                    "CategoryPassiveNetworkEquipment");

            default:
                return UiText.Get(
                    "CategoryUnknown");
        }
    }
}

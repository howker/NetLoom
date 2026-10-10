using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace NetLoom.Wpf.MapInteraction
{
    // Адреса машины, на которой работают Desktop и Engine (ADR-085).
    // Вынесено за интерфейс, чтобы тесты и галерея подставляли свой MAC.
    public interface IEngineHostAddresses
    {
        // MAC-адреса в формате с двоеточиями («AA:BB:CC:DD:EE:FF»), который принимает поиск по MAC.
        IReadOnlyList<string> GetMacAddresses();
    }

    // Реализация по умолчанию: работающие физические адаптеры Ethernet и Wi-Fi.
    public sealed class SystemEngineHostAddresses : IEngineHostAddresses
    {
        public IReadOnlyList<string> GetMacAddresses()
        {
            var result = new List<string>();
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                    if (adapter.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                        adapter.NetworkInterfaceType != NetworkInterfaceType.GigabitEthernet &&
                        adapter.NetworkInterfaceType != NetworkInterfaceType.Wireless80211) continue;
                    var bytes = adapter.GetPhysicalAddress().GetAddressBytes();
                    if (bytes.Length != 6 || bytes.All(item => item == 0)) continue;
                    var mac = string.Join(":", bytes.Select(item => item.ToString("X2")));
                    if (!result.Contains(mac, StringComparer.OrdinalIgnoreCase)) result.Add(mac);
                }
            }
            catch (NetworkInformationException)
            {
                // Адаптеры недоступны: точка опроса останется неопределённой, это не ошибка окна.
            }
            return result;
        }
    }
}

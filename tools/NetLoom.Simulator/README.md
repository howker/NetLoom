# NetLoom.Simulator

`NetLoom.Simulator` воспроизводит сохранённые raw SNMP varbind snapshots через production parsers NetLoom.

## Replay

```text
NetLoom.Simulator.exe replay <snapshot.json>
NetLoom.Simulator.exe replay <directory>
```

Для директории читаются `*.json` верхнего уровня в детерминированном порядке.

Поддерживаемые parser-backed kinds:
- `Lldp`;
- `Cdp`;
- `Fdb`;
- `Arp`.

`SnmpInventory` пока не поддерживается replay-командой: текущая inventory-реализация является collector-ом с `ISnmpTransport` и не имеет отдельного production raw parser. Simulator не подменяет её искусственным parser-ом.

## Snapshot schema v1

```json
{
  "schemaVersion": 1,
  "observationId": "11111111-1111-1111-1111-111111111111",
  "kind": "Lldp",
  "sourceAddress": "192.0.2.10",
  "capturedUtc": "2026-01-01T10:00:00.0000000Z",
  "variables": [
    {
      "oid": "1.0.8802.1.1.2.1.3.7.1.3.17",
      "typeCode": 4,
      "displayValue": "Gi1/0/17",
      "encodedValueBase64": ""
    }
  ]
}
```

`encodedValueBase64` хранит raw encoded SNMP value в Base64. Это важно для parser-ов, которым недостаточно `displayValue` — например, ARP physical address.

Fixtures не должны содержать реальные credentials или production identifiers.

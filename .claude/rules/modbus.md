---
paths:
  - "Services/ModbusTcpEndpointClient.cs"
  - "Services/ModbusTcpGatewayService.cs"
  - "Services/ModbusTcpMonitoringServer.cs"
  - "Services/IModbusGatewayService.cs"
  - "Models/ModbusProtocolDefinitions.cs"
  - "Models/ModbusMonitoringClientModels.cs"
  - "Models/OvrEndpointSettingsModels.cs"
---
# Modbus TCP communication

Uses **NModbus 3.0.81** over TCP. Register/tag layout source of truth:
`Models/ModbusProtocolDefinitions.cs` and the spec workbook
`Document/Protocol/LineSimulator_Modbus_Protocol.xlsx` (see `.claude/docs/README.md`).

- Go through `IModbusGatewayService`; don't open raw sockets from ViewModels.
- `ModbusTcpMonitoringServer` is a **read-only** monitoring endpoint (see the
  `configure-read-only-monitoring-server-endpoint` harness task) — keep it read-only.
- Word/byte order matters: recent history moved bus-out to **little-endian**
  (`nbus out => little endian`). Preserve endianness when touching register packing.
- Register counts and addresses are hardware contracts — verify against the protocol
  workbook before changing, and don't silently shift addresses.
- Device metering (EOCR-iSEM2, GIMAC1000, ZH194F power meters) register maps are in
  `Document/RLC부하장치_.../통신자료/`.

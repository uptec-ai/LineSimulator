namespace TestMcAlgorithm.Models;

public enum ModbusPointArea
{
    Coil,
    DiscreteInput,
    InputRegister,
    HoldingRegister,
    Status,
}

public enum ModbusAccess
{
    ReadOnly,
    ReadWrite,
}

public sealed record ModbusProtocolPoint(
    ModbusPointArea Area,
    ushort Address,
    ushort Length,
    string Name,
    string DataType,
    ModbusAccess Access,
    string Scale,
    string Description);

public sealed record ModbusMonitoringSnapshot(
    bool IsLineSimulatorConnected,
    bool IsManualMode,
    bool IsBus1Enabled,
    bool IsBus2Enabled,
    bool IsBus3Enabled,
    bool IsBus1Applied,
    bool IsBus2Applied,
    bool IsBus3Applied,
    float NBusOut1,
    float NBusOut2,
    float NBusOut3,
    IReadOnlyDictionary<string, bool> KFeedbackStates);

public static class ModbusProtocolDefinitions
{
    public const string ServerHost = "127.0.0.1";
    public const int ServerPort = 7000;
    public const int MaxMonitoringClients = 16;
    public const byte UnitId = 1;
    public const ushort ProtocolMajorVersion = 1;
    public const ushort ProtocolMinorVersion = 0;

    public const ushort CoilRemoteStartRequest = 0;
    public const ushort CoilRemoteStopRequest = 1;
    public const ushort CoilAlarmResetRequest = 2;
    public const ushort CoilAlarmOutput = 100;

    public const ushort DiscreteInputLineConnected = 100;
    public const ushort DiscreteInputManualMode = 101;
    public const ushort DiscreteInputBus1Enabled = 110;
    public const ushort DiscreteInputBus2Enabled = 111;
    public const ushort DiscreteInputBus3Enabled = 112;
    public const ushort DiscreteInputBus1Applied = 120;
    public const ushort DiscreteInputBus2Applied = 121;
    public const ushort DiscreteInputBus3Applied = 122;

    public const ushort InputRegisterNBusOut1 = 0;
    public const ushort InputRegisterNBusOut2 = 2;
    public const ushort InputRegisterNBusOut3 = 4;

    public const ushort HoldingRegisterProtocolMajor = 0;
    public const ushort HoldingRegisterProtocolMinor = 1;
    public const ushort HoldingRegisterStatusCode = 2;
    public const ushort HoldingRegisterUnitId = 3;
    public const ushort HoldingRegisterRemoteCommand = 100;
    public const ushort HoldingRegisterRemoteCommandSequence = 101;

    public static IReadOnlyList<ModbusProtocolPoint> Points { get; } = BuildPoints();

    public static IReadOnlyList<ModbusProtocolPoint> StatusCodes { get; } =
    [
        new(ModbusPointArea.Status, 0, 1, "Idle", "UInt16", ModbusAccess.ReadOnly, "1", "Line simulator is not connected."),
        new(ModbusPointArea.Status, 1, 1, "Connected", "UInt16", ModbusAccess.ReadOnly, "1", "Line simulator is connected in auto mode."),
        new(ModbusPointArea.Status, 2, 1, "ManualMode", "UInt16", ModbusAccess.ReadOnly, "1", "Application is in manual mode."),
        new(ModbusPointArea.Status, 3, 1, "AlarmOrFault", "UInt16", ModbusAccess.ReadOnly, "1", "Reserved for alarm/fault state."),
    ];

    public static ushort ResolveStatusCode(ModbusMonitoringSnapshot snapshot)
    {
        if (!snapshot.IsLineSimulatorConnected)
        {
            return 0;
        }

        return snapshot.IsManualMode ? (ushort)2 : (ushort)1;
    }

    private static IReadOnlyList<ModbusProtocolPoint> BuildPoints()
    {
        var points = new List<ModbusProtocolPoint>
        {
            new(ModbusPointArea.Coil, CoilRemoteStartRequest, 1, "RemoteStartRequest", "Bool", ModbusAccess.ReadWrite, "1", "Reserved writable command coil for future monitoring-system start request."),
            new(ModbusPointArea.Coil, CoilRemoteStopRequest, 1, "RemoteStopRequest", "Bool", ModbusAccess.ReadWrite, "1", "Reserved writable command coil for future monitoring-system stop request."),
            new(ModbusPointArea.Coil, CoilAlarmResetRequest, 1, "AlarmResetRequest", "Bool", ModbusAccess.ReadWrite, "1", "Reserved writable command coil for future alarm reset request."),
            new(ModbusPointArea.Coil, CoilAlarmOutput, 1, "AlarmOutput", "Bool", ModbusAccess.ReadWrite, "1", "Application alarm coil mirror. Currently reserved.")
        };

        points.AddRange(KCatalog.All.Select(item =>
            new ModbusProtocolPoint(
                ModbusPointArea.DiscreteInput,
                item.FeedbackAddress,
                1,
                $"{item.Code}_Feedback",
                "Bool",
                ModbusAccess.ReadOnly,
                "1",
                $"{item.Code} / MC{item.SourceMcNumber} / {item.TargetBus} feedback state.")));

        points.AddRange(
        [
            new(ModbusPointArea.DiscreteInput, DiscreteInputLineConnected, 1, "LineSimulatorConnected", "Bool", ModbusAccess.ReadOnly, "1", "True when this app is connected to the line simulator PLC/client target."),
            new(ModbusPointArea.DiscreteInput, DiscreteInputManualMode, 1, "ManualMode", "Bool", ModbusAccess.ReadOnly, "1", "True when operation mode is manual."),
            new(ModbusPointArea.DiscreteInput, DiscreteInputBus1Enabled, 1, "BUS1Enabled", "Bool", ModbusAccess.ReadOnly, "1", "BUS1 use setting."),
            new(ModbusPointArea.DiscreteInput, DiscreteInputBus2Enabled, 1, "BUS2Enabled", "Bool", ModbusAccess.ReadOnly, "1", "BUS2 use setting."),
            new(ModbusPointArea.DiscreteInput, DiscreteInputBus3Enabled, 1, "BUS3Enabled", "Bool", ModbusAccess.ReadOnly, "1", "BUS3 use setting."),
            new(ModbusPointArea.DiscreteInput, DiscreteInputBus1Applied, 1, "BUS1Applied", "Bool", ModbusAccess.ReadOnly, "1", "BUS OUT1 applied state."),
            new(ModbusPointArea.DiscreteInput, DiscreteInputBus2Applied, 1, "BUS2Applied", "Bool", ModbusAccess.ReadOnly, "1", "BUS OUT2 applied state."),
            new(ModbusPointArea.DiscreteInput, DiscreteInputBus3Applied, 1, "BUS3Applied", "Bool", ModbusAccess.ReadOnly, "1", "BUS OUT3 applied state."),
            new(ModbusPointArea.InputRegister, InputRegisterNBusOut1, 2, "NBusOut1", "Float32-BigEndian", ModbusAccess.ReadOnly, "1", "NBUS OUT #1 current/output value. 2 registers, high word first."),
            new(ModbusPointArea.InputRegister, InputRegisterNBusOut2, 2, "NBusOut2", "Float32-BigEndian", ModbusAccess.ReadOnly, "1", "NBUS OUT #2 current/output value. 2 registers, high word first."),
            new(ModbusPointArea.InputRegister, InputRegisterNBusOut3, 2, "NBusOut3", "Float32-BigEndian", ModbusAccess.ReadOnly, "1", "NBUS OUT #3 current/output value. 2 registers, high word first."),
            new(ModbusPointArea.HoldingRegister, HoldingRegisterProtocolMajor, 1, "ProtocolMajorVersion", "UInt16", ModbusAccess.ReadOnly, "1", "Protocol major version."),
            new(ModbusPointArea.HoldingRegister, HoldingRegisterProtocolMinor, 1, "ProtocolMinorVersion", "UInt16", ModbusAccess.ReadOnly, "1", "Protocol minor version."),
            new(ModbusPointArea.HoldingRegister, HoldingRegisterStatusCode, 1, "StatusCode", "UInt16", ModbusAccess.ReadOnly, "1", "0=Idle, 1=Connected, 2=ManualMode, 3=AlarmOrFault."),
            new(ModbusPointArea.HoldingRegister, HoldingRegisterUnitId, 1, "UnitId", "UInt16", ModbusAccess.ReadOnly, "1", "Modbus unit id served by this app."),
            new(ModbusPointArea.HoldingRegister, HoldingRegisterRemoteCommand, 1, "RemoteCommand", "UInt16", ModbusAccess.ReadWrite, "1", "Reserved writable command register. 0=None, 1=Start, 2=Stop, 3=AlarmReset."),
            new(ModbusPointArea.HoldingRegister, HoldingRegisterRemoteCommandSequence, 1, "RemoteCommandSequence", "UInt16", ModbusAccess.ReadWrite, "1", "Reserved writable command sequence number.")
        ]);

        return points
            .OrderBy(item => item.Area)
            .ThenBy(item => item.Address)
            .ToArray();
    }
}

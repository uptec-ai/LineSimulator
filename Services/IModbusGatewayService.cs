namespace TestMcAlgorithm.Services;

public interface IModbusGatewayService : IAsyncDisposable
{ 
    // 통신 인터페이스 약속
    bool IsConnected { get; }
    Task ConnectAsync(string host, int port, CancellationToken cancellationToken);
    Task DisconnectAsync();
    Task WriteSingleCoilAsync(byte unitId, ushort coilAddress, bool value, CancellationToken cancellationToken);
    Task<bool[]> ReadDiscreteInputsAsync(byte unitId, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken);
    Task<ushort[]> ReadInputRegistersAsync(byte unitId, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken);
    Task<ushort[]> ReadHoldingRegistersAsync(byte unitId, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken);
}

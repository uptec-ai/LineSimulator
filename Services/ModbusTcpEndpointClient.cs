using System.Net.Sockets;
using NModbus;

namespace TestMcAlgorithm.Services;

public sealed class ModbusTcpEndpointClient : IAsyncDisposable
{
    private readonly ModbusFactory _modbusFactory = new();

    private TcpClient? _client;
    private IModbusMaster? _master;

    public bool IsConnected => IsSocketHealthy(_client, _master);

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        await DisconnectAsync();

        var client = new TcpClient
        {
            NoDelay = true,
            ReceiveTimeout = 3000,
            SendTimeout = 3000,
        };

        await client.ConnectAsync(host, port, cancellationToken);

        _client = client;
        _master = _modbusFactory.CreateMaster(client);
    }

    public async Task EnsureConnectedAsync(string host, int port, CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return;
        }

        await ConnectAsync(host, port, cancellationToken);
    }

    public Task DisconnectAsync()
    {
        if (_client is not null)
        {
            try
            {
                _client.Close();
            }
            catch
            {
                // ignore shutdown race
            }
        }

        _master = null;
        _client?.Dispose();
        _client = null;

        return Task.CompletedTask;
    }

    public async Task<ushort[]> ReadHoldingRegistersAsync(byte unitId, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken)
    {
        if (!IsConnected || _master is null)
        {
            throw new InvalidOperationException("Modbus endpoint is not connected.");
        }

        return await _master.ReadHoldingRegistersAsync(unitId, startAddress, numberOfPoints).WaitAsync(cancellationToken);
    }

    public async Task<ushort[]> ReadInputRegistersAsync(byte unitId, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken)
    {
        if (!IsConnected || _master is null)
        {
            throw new InvalidOperationException("Modbus endpoint is not connected.");
        }

        return await _master.ReadInputRegistersAsync(unitId, startAddress, numberOfPoints).WaitAsync(cancellationToken);
    }

    public async Task WriteSingleRegisterAsync(byte unitId, ushort registerAddress, ushort value, CancellationToken cancellationToken)
    {
        if (!IsConnected || _master is null)
        {
            throw new InvalidOperationException("Modbus endpoint is not connected.");
        }

        await _master.WriteSingleRegisterAsync(unitId, registerAddress, value).WaitAsync(cancellationToken);
    }

    public async Task WriteMultipleRegistersAsync(byte unitId, ushort startAddress, ushort[] values, CancellationToken cancellationToken)
    {
        if (!IsConnected || _master is null)
        {
            throw new InvalidOperationException("Modbus endpoint is not connected.");
        }

        await _master.WriteMultipleRegistersAsync(unitId, startAddress, values).WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private static bool IsSocketHealthy(TcpClient? client, IModbusMaster? master)
    {
        if (client?.Client is not Socket socket || master is null)
        {
            return false;
        }

        try
        {
            if (!socket.Connected)
            {
                return false;
            }

            return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}

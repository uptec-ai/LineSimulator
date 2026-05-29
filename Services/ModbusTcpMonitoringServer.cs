using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using TestMcAlgorithm.Models;

namespace TestMcAlgorithm.Services;

public sealed class ModbusTcpMonitoringServer : IAsyncDisposable
{
    private const int PointCapacity = 1024;
    private const byte IllegalFunction = 1;
    private const byte IllegalDataAddress = 2;
    private const byte IllegalDataValue = 3;
    private const byte ServerDeviceFailure = 4;
    private static readonly TimeSpan ShutdownWaitTimeout = TimeSpan.FromMilliseconds(500);

    private readonly Func<ModbusMonitoringSnapshot> _snapshotFactory;
    private readonly MonitoringDataStore _dataStore = new(PointCapacity);
    private readonly ConcurrentDictionary<string, ClientSession> _clients = new();
    private readonly ConcurrentDictionary<string, Task> _clientTasks = new();
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(250);

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;  // single accept loop for all clients since listener is shared
    private Task? _refreshTask; // single refresh task for all clients since data store is shared

    public ModbusTcpMonitoringServer(Func<ModbusMonitoringSnapshot> snapshotFactory)
    {
        _snapshotFactory = snapshotFactory;
        WriteStaticRegisters();
    }

    public event Action<ModbusMonitoringClientStatus>? ClientStatusChanged;

    public bool IsRunning => _acceptTask is { IsCompleted: false };

    public int ActiveClientCount => _clients.Values.Count(client => client.IsConnected);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Parse(ModbusProtocolDefinitions.ServerHost), ModbusProtocolDefinitions.ServerPort);
        _listener.Start(ModbusProtocolDefinitions.MaxMonitoringClients);

        _refreshTask = RefreshLoopAsync(_cts.Token);
        _acceptTask = AcceptLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();

        try
        {
            _listener?.Stop();
        }
        catch
        {
            // shutdown path
        }

        foreach (var client in _clients.Values)
        {
            client.TcpClient.Close();
        }

        await WaitForTaskAsync(_refreshTask, ShutdownWaitTimeout);
        await WaitForTaskAsync(_acceptTask, ShutdownWaitTimeout);
        await WaitForClientTasksAsync();

        _listener = null;
        _acceptTask = null;
        _refreshTask = null;
        _clients.Clear();
        _clientTasks.Clear();
        _cts.Dispose();
        _cts = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient? tcpClient = null;
            try
            {
                tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken);
                tcpClient.NoDelay = true;

                if (ActiveClientCount >= ModbusProtocolDefinitions.MaxMonitoringClients)
                {
                    tcpClient.Close();
                    continue;
                }

                var session = ClientSession.Create(tcpClient);
                _clients[session.ClientId] = session;
                PublishClientStatus(session);
                var clientTask = HandleClientAsync(session, cancellationToken);
                _clientTasks[session.ClientId] = clientTask;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                tcpClient?.Close();
            }
        }
    }

    private async Task HandleClientAsync(ClientSession session, CancellationToken cancellationToken)
    {
        try
        {
            using var client = session.TcpClient;
            await using var stream = client.GetStream();

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var header = new byte[7];
                await stream.ReadExactlyAsync(header, cancellationToken);

                var transactionId = ReadUInt16(header, 0);
                var protocolId = ReadUInt16(header, 2);
                var length = ReadUInt16(header, 4);
                var unitId = header[6];

                if (protocolId != 0 || length < 2)
                {
                    break;
                }

                var pdu = new byte[length - 1];
                await stream.ReadExactlyAsync(pdu, cancellationToken);

                var responsePdu = HandleRequest(unitId, pdu, session);
                var response = BuildResponse(transactionId, unitId, responsePdu);
                await stream.WriteAsync(response, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (EndOfStreamException)
        {
            // client disconnected
        }
        catch (IOException)
        {
            // client disconnected
        }
        catch (SocketException)
        {
            // client disconnected
        }
        catch (ObjectDisposedException)
        {
            // client disconnected during shutdown
        }
        catch
        {
            // keep client handler failures from escaping as background task faults
        }
        finally
        {
            session.IsConnected = false;
            session.LastSeenAt = DateTime.Now;
            PublishClientStatus(session);
            _clients.TryRemove(session.ClientId, out _);
            _clientTasks.TryRemove(session.ClientId, out _);
        }
    }

    private byte[] HandleRequest(byte unitId, byte[] pdu, ClientSession session)
    {
        if (pdu.Length == 0)
        {
            return BuildExceptionResponse(0, IllegalFunction);
        }

        var functionCode = pdu[0];
        ushort? startAddress = pdu.Length >= 3 ? ReadUInt16(pdu, 1) : null;
        ushort? pointCount = pdu.Length >= 5 ? ReadUInt16(pdu, 3) : null;
        session.RecordRequest(functionCode, startAddress, pointCount);
        PublishClientStatus(session);

        if (unitId != ModbusProtocolDefinitions.UnitId)
        {
            return BuildExceptionResponse(functionCode, IllegalDataValue);
        }

        try
        {
            return functionCode switch
            {
                1 => ReadBooleanPoints(functionCode, _dataStore.Coils, pdu),
                2 => ReadBooleanPoints(functionCode, _dataStore.DiscreteInputs, pdu),
                3 => ReadRegisters(functionCode, _dataStore.HoldingRegisters, pdu),
                4 => ReadRegisters(functionCode, _dataStore.InputRegisters, pdu),
                5 => WriteSingleCoil(pdu),
                6 => WriteSingleRegister(pdu),
                15 => WriteMultipleCoils(pdu),
                16 => WriteMultipleRegisters(pdu),
                _ => BuildExceptionResponse(functionCode, IllegalFunction)
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return BuildExceptionResponse(functionCode, IllegalDataAddress);
        }
        catch (ArgumentException)
        {
            return BuildExceptionResponse(functionCode, IllegalDataValue);
        }
        catch
        {
            return BuildExceptionResponse(functionCode, ServerDeviceFailure);
        }
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            RefreshDataStore();

            using var timer = new PeriodicTimer(_refreshInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                RefreshDataStore();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 정상 종료
        }
        catch (Exception ex)
        {
            File.AppendAllText(
                "shutdown_error.log",
                DateTime.Now + Environment.NewLine + ex + Environment.NewLine);
        }
    }

    private void RefreshDataStore()
    {
        var snapshot = _snapshotFactory();

        _dataStore.WriteDiscreteInput(ModbusProtocolDefinitions.DiscreteInputLineConnected, snapshot.IsLineSimulatorConnected);
        _dataStore.WriteDiscreteInput(ModbusProtocolDefinitions.DiscreteInputManualMode, snapshot.IsManualMode);
        _dataStore.WriteDiscreteInput(ModbusProtocolDefinitions.DiscreteInputBus1Enabled, snapshot.IsBus1Enabled);
        _dataStore.WriteDiscreteInput(ModbusProtocolDefinitions.DiscreteInputBus2Enabled, snapshot.IsBus2Enabled);
        _dataStore.WriteDiscreteInput(ModbusProtocolDefinitions.DiscreteInputBus3Enabled, snapshot.IsBus3Enabled);
        _dataStore.WriteDiscreteInput(ModbusProtocolDefinitions.DiscreteInputBus1Applied, snapshot.IsBus1Applied);
        _dataStore.WriteDiscreteInput(ModbusProtocolDefinitions.DiscreteInputBus2Applied, snapshot.IsBus2Applied);
        _dataStore.WriteDiscreteInput(ModbusProtocolDefinitions.DiscreteInputBus3Applied, snapshot.IsBus3Applied);

        foreach (var definition in KCatalog.All)
        {
            var value = snapshot.KFeedbackStates.TryGetValue(definition.Code, out var isOn) && isOn;
            _dataStore.WriteDiscreteInput(definition.FeedbackAddress, value);
        }

        WriteFloat32(ModbusProtocolDefinitions.InputRegisterNBusOut1, snapshot.NBusOut1);
        WriteFloat32(ModbusProtocolDefinitions.InputRegisterNBusOut2, snapshot.NBusOut2);
        WriteFloat32(ModbusProtocolDefinitions.InputRegisterNBusOut3, snapshot.NBusOut3);

        _dataStore.WriteHoldingRegister(
            ModbusProtocolDefinitions.HoldingRegisterStatusCode,
            ModbusProtocolDefinitions.ResolveStatusCode(snapshot));
    }

    private byte[] ReadBooleanPoints(byte functionCode, bool[] source, byte[] pdu)
    {
        if (pdu.Length != 5)
        {
            throw new ArgumentException("Invalid boolean read request length.");
        }

        var startAddress = ReadUInt16(pdu, 1);
        var quantity = ReadUInt16(pdu, 3);
        if (quantity is < 1 or > 2000)
        {
            throw new ArgumentException("Invalid boolean point quantity.");
        }

        ValidateRange(source.Length, startAddress, quantity);

        var byteCount = (byte)((quantity + 7) / 8);
        var response = new byte[2 + byteCount];
        response[0] = functionCode;
        response[1] = byteCount;

        lock (_dataStore.SyncRoot)
        {
            for (var i = 0; i < quantity; i++)
            {
                if (source[startAddress + i])
                {
                    response[2 + (i / 8)] |= (byte)(1 << (i % 8));
                }
            }
        }

        return response;
    }

    private byte[] ReadRegisters(byte functionCode, ushort[] source, byte[] pdu)
    {
        if (pdu.Length != 5)
        {
            throw new ArgumentException("Invalid register read request length.");
        }

        var startAddress = ReadUInt16(pdu, 1);
        var quantity = ReadUInt16(pdu, 3);
        if (quantity is < 1 or > 125)
        {
            throw new ArgumentException("Invalid register quantity.");
        }

        ValidateRange(source.Length, startAddress, quantity);

        var response = new byte[2 + quantity * 2];
        response[0] = functionCode;
        response[1] = (byte)(quantity * 2);

        lock (_dataStore.SyncRoot)
        {
            for (var i = 0; i < quantity; i++)
            {
                WriteUInt16(response, 2 + i * 2, source[startAddress + i]);
            }
        }

        return response;
    }

    private byte[] WriteSingleCoil(byte[] pdu)
    {
        if (pdu.Length != 5)
        {
            throw new ArgumentException("Invalid single coil write request length.");
        }

        var address = ReadUInt16(pdu, 1);
        var rawValue = ReadUInt16(pdu, 3);
        if (rawValue is not (0x0000 or 0xFF00))
        {
            throw new ArgumentException("Invalid coil write value.");
        }

        ValidateRange(_dataStore.Coils.Length, address, 1);
        _dataStore.WriteCoil(address, rawValue == 0xFF00);

        return pdu.ToArray();
    }

    private byte[] WriteSingleRegister(byte[] pdu)
    {
        if (pdu.Length != 5)
        {
            throw new ArgumentException("Invalid single register write request length.");
        }

        var address = ReadUInt16(pdu, 1);
        var value = ReadUInt16(pdu, 3);
        ValidateRange(_dataStore.HoldingRegisters.Length, address, 1);
        _dataStore.WriteHoldingRegister(address, value);

        return pdu.ToArray();
    }

    private byte[] WriteMultipleCoils(byte[] pdu)
    {
        if (pdu.Length < 6)
        {
            throw new ArgumentException("Invalid multiple coil write request length.");
        }

        var startAddress = ReadUInt16(pdu, 1);
        var quantity = ReadUInt16(pdu, 3);
        var byteCount = pdu[5];
        if (quantity is < 1 or > 1968 || byteCount != (quantity + 7) / 8 || pdu.Length != 6 + byteCount)
        {
            throw new ArgumentException("Invalid multiple coil write payload.");
        }

        ValidateRange(_dataStore.Coils.Length, startAddress, quantity);
        lock (_dataStore.SyncRoot)
        {
            for (var i = 0; i < quantity; i++)
            {
                _dataStore.Coils[startAddress + i] = (pdu[6 + (i / 8)] & (1 << (i % 8))) != 0;
            }
        }

        return [15, pdu[1], pdu[2], pdu[3], pdu[4]];
    }

    private byte[] WriteMultipleRegisters(byte[] pdu)
    {
        if (pdu.Length < 6)
        {
            throw new ArgumentException("Invalid multiple register write request length.");
        }

        var startAddress = ReadUInt16(pdu, 1);
        var quantity = ReadUInt16(pdu, 3);
        var byteCount = pdu[5];
        if (quantity is < 1 or > 123 || byteCount != quantity * 2 || pdu.Length != 6 + byteCount)
        {
            throw new ArgumentException("Invalid multiple register write payload.");
        }

        ValidateRange(_dataStore.HoldingRegisters.Length, startAddress, quantity);
        lock (_dataStore.SyncRoot)
        {
            for (var i = 0; i < quantity; i++)
            {
                _dataStore.HoldingRegisters[startAddress + i] = ReadUInt16(pdu, 6 + i * 2);
            }
        }

        return [16, pdu[1], pdu[2], pdu[3], pdu[4]];
    }

    private void WriteStaticRegisters()
    {
        _dataStore.WriteHoldingRegister(ModbusProtocolDefinitions.HoldingRegisterProtocolMajor, ModbusProtocolDefinitions.ProtocolMajorVersion);
        _dataStore.WriteHoldingRegister(ModbusProtocolDefinitions.HoldingRegisterProtocolMinor, ModbusProtocolDefinitions.ProtocolMinorVersion);
        _dataStore.WriteHoldingRegister(ModbusProtocolDefinitions.HoldingRegisterUnitId, ModbusProtocolDefinitions.UnitId);
    }

    private void WriteFloat32(ushort startAddress, float value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        _dataStore.WriteInputRegister(startAddress, (ushort)((bytes[0] << 8) | bytes[1]));
        _dataStore.WriteInputRegister((ushort)(startAddress + 1), (ushort)((bytes[2] << 8) | bytes[3]));
    }

    private void PublishClientStatus(ClientSession session)
    {
        ClientStatusChanged?.Invoke(session.ToStatus());
    }

    private static byte[] BuildResponse(ushort transactionId, byte unitId, byte[] pdu)
    {
        var response = new byte[7 + pdu.Length];
        WriteUInt16(response, 0, transactionId);
        WriteUInt16(response, 2, 0);
        WriteUInt16(response, 4, (ushort)(pdu.Length + 1));
        response[6] = unitId;
        Array.Copy(pdu, 0, response, 7, pdu.Length);
        return response;
    }

    private static byte[] BuildExceptionResponse(byte functionCode, byte exceptionCode)
    {
        return [(byte)(functionCode | 0x80), exceptionCode];
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)(value >> 8);
        bytes[offset + 1] = (byte)value;
    }

    private static void ValidateRange(int capacity, ushort startAddress, ushort count)
    {
        if (startAddress >= capacity || startAddress + count > capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(startAddress));
        }
    }

    private static async Task WaitForTaskAsync(Task? task, TimeSpan timeout)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.WaitAsync(timeout); // do not block indefinitely on tasks that did not unwind promptly during shutdown
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (TimeoutException)
        {
            // do not block application shutdown on a socket wait that did not unwind promptly
        }
        catch (ObjectDisposedException)
        {
            // normal shutdown
        }
        catch (SocketException)
        {
            // listener can throw during normal shutdown
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async Task WaitForClientTasksAsync()
    {
        var tasks = _clientTasks.Values.ToArray();
        if (tasks.Length == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(tasks).WaitAsync(ShutdownWaitTimeout);
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (TimeoutException)
        {
            // do not block application shutdown on client handlers that did not unwind promptly
        }
        catch (ObjectDisposedException)
        {
            // normal shutdown
        }
        catch (SocketException)
        {
            // sockets are closed during normal shutdown
        }
        catch (IOException)
        {
            // streams are closed during normal shutdown
        }
        catch
        {
            // client handlers already publish disconnect state in their finally blocks
        }
    }

    private sealed class MonitoringDataStore
    {
        public MonitoringDataStore(int capacity)
        {
            Coils = new bool[capacity];
            DiscreteInputs = new bool[capacity];
            HoldingRegisters = new ushort[capacity];
            InputRegisters = new ushort[capacity];
        }

        public object SyncRoot { get; } = new();

        public bool[] Coils { get; }

        public bool[] DiscreteInputs { get; }

        public ushort[] HoldingRegisters { get; }

        public ushort[] InputRegisters { get; }

        public void WriteCoil(ushort address, bool value)
        {
            lock (SyncRoot)
            {
                Coils[address] = value;
            }
        }

        public void WriteDiscreteInput(ushort address, bool value)
        {
            lock (SyncRoot)
            {
                DiscreteInputs[address] = value;
            }
        }

        public void WriteHoldingRegister(ushort address, ushort value)
        {
            lock (SyncRoot)
            {
                HoldingRegisters[address] = value;
            }
        }

        public void WriteInputRegister(ushort address, ushort value)
        {
            lock (SyncRoot)
            {
                InputRegisters[address] = value;
            }
        }
    }

    private sealed class ClientSession
    {
        private ClientSession(TcpClient tcpClient, string clientId, string remoteEndPoint)
        {
            TcpClient = tcpClient;
            ClientId = clientId;
            RemoteEndPoint = remoteEndPoint;
            ConnectedAt = DateTime.Now;
            LastSeenAt = ConnectedAt;
            IsConnected = true;
        }

        public TcpClient TcpClient { get; }

        public string ClientId { get; }

        public string RemoteEndPoint { get; }

        public DateTime ConnectedAt { get; }

        public DateTime LastSeenAt { get; set; }

        public byte? LastFunctionCode { get; private set; }

        public ushort? LastStartAddress { get; private set; }

        public ushort? LastPointCount { get; private set; }

        public int RequestCount { get; private set; }

        public bool IsConnected { get; set; }

        public static ClientSession Create(TcpClient tcpClient)
        {
            var remoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
            return new ClientSession(tcpClient, Guid.NewGuid().ToString("N"), remoteEndPoint);
        }

        public void RecordRequest(byte functionCode, ushort? startAddress, ushort? pointCount)
        {
            LastFunctionCode = functionCode;
            LastStartAddress = startAddress;
            LastPointCount = pointCount;
            LastSeenAt = DateTime.Now;
            RequestCount++;
        }

        public ModbusMonitoringClientStatus ToStatus()
        {
            return new ModbusMonitoringClientStatus(
                ClientId,
                RemoteEndPoint,
                ConnectedAt,
                LastSeenAt,
                LastFunctionCode,
                LastStartAddress,
                LastPointCount,
                RequestCount,
                IsConnected);
        }
    }
}

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

// Force stdout to flush on every write so Node.js receives lines immediately
var stdout = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };
Console.SetOut(stdout);
Console.InputEncoding  = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--check"))
{
    await CheckAsync();
    return;
}


var serviceUuid = GetArgValue(args, "--service");
var charUuid = GetArgValue(args, "--char");

if (serviceUuid == null || charUuid == null)
{
    Emit(new { @event = "error", reason = "Missing parameters" });
    return;
}


await RunAsync(serviceUuid, charUuid);

// ---------------------------------------------------------------------------

static async Task CheckAsync()
{
    try
    {
        var adapter = await BluetoothAdapter.GetDefaultAsync();

        if (adapter == null)
        {
            Emit(new { success = false, reason = "No Bluetooth adapter found" });
            return;
        }
        if (!adapter.IsLowEnergySupported)
        {
            Emit(new { success = false, reason = "Adapter does not support Bluetooth Low Energy" });
            return;
        }
        if (!adapter.IsPeripheralRoleSupported)
        {
            Emit(new { success = false, reason = "Adapter does not support BLE Peripheral role" });
            return;
        }

        Emit(new { success = true, reason = "BLE peripheral supported" });
    }
    catch (Exception ex)
    {
        Emit(new { success = false, reason = ex.Message });
    }
}

// ---------------------------------------------------------------------------

static async Task RunAsync(string SERVICE_UUID, string CHAR_UUID)
{
    var sessions = new ConcurrentDictionary<string, GattSession>();
    var tcs      = new TaskCompletionSource();

    // -- Create GATT Service --
    if (!Guid.TryParse(SERVICE_UUID, out Guid serviceGuid))
    {
        Emit(new { @event = "error", message = $"Invalid service UUID: {SERVICE_UUID}" });
        return;
    }

    var serviceResult = await GattServiceProvider.CreateAsync(serviceGuid);
    if (serviceResult.Error != BluetoothError.Success)
    {
        Emit(new { @event = "error", message = $"Failed to create GATT service: {serviceResult.Error}" });
        return;
    }
    var serviceProvider = serviceResult.ServiceProvider;

    // -- Create Characteristic --
    var charParams = new GattLocalCharacteristicParameters
    {
        CharacteristicProperties = GattCharacteristicProperties.Write |
                                   GattCharacteristicProperties.WriteWithoutResponse,
        ReadProtectionLevel  = GattProtectionLevel.Plain,
        WriteProtectionLevel = GattProtectionLevel.Plain,
    };


    if (!Guid.TryParse(CHAR_UUID, out Guid charGuid))
    {
        Emit(new { @event = "error", message = $"Invalid char UUID: {CHAR_UUID}" });
        return;
    }

    var charResult = await serviceProvider.Service.CreateCharacteristicAsync(
        charGuid, charParams);

    if (charResult.Error != BluetoothError.Success)
    {
        Emit(new { @event = "error", message = $"Failed to create characteristic: {charResult.Error}" });
        return;
    }

    var characteristic = charResult.Characteristic;

    // -- Handle incoming writes --
    characteristic.WriteRequested += async (_, args) =>
    {
        using var deferral = args.GetDeferral();

        GattWriteRequest request;
        try { request = await args.GetRequestAsync(); }
        catch { return; }

        var session  = args.Session;
        var deviceId = session.DeviceId.Id;

        // Detect new connections
        if (sessions.TryAdd(deviceId, session))
        {
            session.SessionStatusChanged += (_, statusArgs) =>
            {
                if (statusArgs.Status == GattSessionStatus.Closed)
                {
                    sessions.TryRemove(deviceId, out _);
                    Emit(new { @event = "disconnection", deviceId });
                }
            };
            Emit(new { @event = "connection", deviceId });
        }

        // Read data
        var reader = DataReader.FromBuffer(request.Value);
        var bytes  = new byte[request.Value.Length];
        reader.ReadBytes(bytes);
        var data = Encoding.UTF8.GetString(bytes).Trim();

        Emit(new { @event = "data", deviceId, data });

        if (request.Option == GattWriteOption.WriteWithResponse)
            request.Respond();
    };

    // -- Advertise --
    serviceProvider.StartAdvertising(new GattServiceProviderAdvertisingParameters
    {
        IsDiscoverable = true,
        IsConnectable  = true,
    });

    Emit(new { @event = "ready" });

    // -- Listen for stop command on stdin --
    _ = Task.Run(async () =>
    {
        try
        {
            string? line;
            while ((line = await Console.In.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.GetProperty("cmd").GetString() == "stop")
                        break;
                }
                catch { /* ignore malformed commands */ }
            }
        }
        catch { /* stdin closed */ }
        tcs.TrySetResult();
    });

    await tcs.Task;

    serviceProvider.StopAdvertising();
}

// ---------------------------------------------------------------------------

static void Emit(object payload)
{
    Console.WriteLine(JsonSerializer.Serialize(payload));
}

static string? GetArgValue(string[] args, string name)
{
    var prefix = name + "=";

    // Supports: --name=value
    var inline = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    if (inline is not null)
        return inline[prefix.Length..];

    // Supports: --name value
    var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    if (index >= 0 && index < args.Length - 1)
        return args[index + 1];

    return null;
}
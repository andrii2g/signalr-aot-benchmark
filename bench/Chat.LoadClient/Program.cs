using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Chat.Shared;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

var options = ParseOptions(args);
if (options is null)
{
    return 2;
}

var startedAtUtc = DateTimeOffset.UtcNow;
var latencies = new ConcurrentBag<double>();
var connectionsList = new List<HubConnection>(options.Connections);
var sendWatch = new Stopwatch();
var totalWatch = Stopwatch.StartNew();
var actualReceived = 0L;
var actualSent = 0L;
var sendErrors = 0L;
var connected = 0;

try
{
    for (var i = 0; i < options.Connections; i++)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(options.Url, hubOptions =>
            {
                hubOptions.Transports = HttpTransportType.WebSockets;
            })
            .AddJsonProtocol(jsonOptions =>
            {
                jsonOptions.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, ChatJsonContext.Default);
            })
            .Build();

        connection.On<ChatMessage>("ReceiveMessage", message =>
        {
            var now = Stopwatch.GetTimestamp();
            var latencyMs = (now - message.SentAtTimestamp) * 1000.0 / Stopwatch.Frequency;

            Interlocked.Increment(ref actualReceived);
            latencies.Add(latencyMs);
        });

        connectionsList.Add(connection);
    }

    foreach (var connection in connectionsList)
    {
        await connection.StartAsync();
        connected++;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to start SignalR clients after {connected} successful connections: {ex.Message}");
    await StopConnectionsAsync(connectionsList);
    return 1;
}

await Task.Delay(1000);

var payloadText = CreatePayload(options.MessageSize);
sendWatch.Start();

var sendTasks = connectionsList.Select((connection, clientIndex) => Task.Run(async () =>
{
    for (var sequence = 0; sequence < options.MessagesPerConnection; sequence++)
    {
        var message = new ChatMessage
        {
            SenderId = clientIndex,
            Sequence = sequence,
            User = $"client-{clientIndex}",
            Text = payloadText,
            SentAtTimestamp = Stopwatch.GetTimestamp()
        };

        try
        {
            await connection.InvokeAsync("SendMessage", message);
            Interlocked.Increment(ref actualSent);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref sendErrors);
            Console.Error.WriteLine($"Send failed for client {clientIndex}, sequence {sequence}: {ex.Message}");
        }
    }
}));

await Task.WhenAll(sendTasks);
sendWatch.Stop();

var targetMessagesSent = options.Connections * options.MessagesPerConnection;
var expectedReceived = targetMessagesSent * options.Connections;
var receiveDeadline = DateTimeOffset.UtcNow.AddSeconds(options.ReceiveTimeoutSeconds);

while (Volatile.Read(ref actualReceived) < expectedReceived && DateTimeOffset.UtcNow < receiveDeadline)
{
    await Task.Delay(100);
}

totalWatch.Stop();
await StopConnectionsAsync(connectionsList);

var latencyValues = latencies.ToArray();
Array.Sort(latencyValues);

var received = Volatile.Read(ref actualReceived);
var sent = Volatile.Read(ref actualSent);
var errors = Volatile.Read(ref sendErrors);
var completeness = expectedReceived == 0 ? 0.0 : received * 100.0 / expectedReceived;
var sendDurationMs = sendWatch.Elapsed.TotalMilliseconds;
var totalDurationMs = totalWatch.Elapsed.TotalMilliseconds;

var result = new
{
    name = options.Name,
    url = options.Url,
    startedAtUtc = startedAtUtc.ToString("O"),
    connections = options.Connections,
    messagesPerConnection = options.MessagesPerConnection,
    messageSize = options.MessageSize,
    targetMessagesSent,
    actualMessagesSent = sent,
    expectedMessagesReceived = expectedReceived,
    actualMessagesReceived = received,
    receiveCompletenessPercent = completeness,
    sendDurationMs,
    totalDurationMs,
    sentMessagesPerSecond = sendDurationMs <= 0 ? 0.0 : sent / (sendDurationMs / 1000.0),
    receivedMessagesPerSecond = totalDurationMs <= 0 ? 0.0 : received / (totalDurationMs / 1000.0),
    averageLatencyMs = latencyValues.Length == 0 ? 0.0 : latencyValues.Average(),
    p50LatencyMs = Percentile(latencyValues, 50),
    p95LatencyMs = Percentile(latencyValues, 95),
    p99LatencyMs = Percentile(latencyValues, 99),
    sendErrors = errors
};

var outputPath = Path.GetFullPath(options.Output);
var outputDirectory = Path.GetDirectoryName(outputPath);
if (!string.IsNullOrEmpty(outputDirectory))
{
    Directory.CreateDirectory(outputDirectory);
}

await File.WriteAllTextAsync(
    outputPath,
    JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

Console.WriteLine($"Wrote benchmark results: {outputPath}");
Console.WriteLine($"Received completeness: {completeness:F2}%");

return sent > 0 && completeness >= 99.5 ? 0 : 1;

static async Task StopConnectionsAsync(IEnumerable<HubConnection> connections)
{
    foreach (var connection in connections)
    {
        try
        {
            await connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to dispose connection: {ex.Message}");
        }
    }
}

static string CreatePayload(int messageSize)
{
    return new string('x', Math.Max(0, messageSize));
}

static double Percentile(double[] sortedValues, int percentile)
{
    if (sortedValues.Length == 0)
    {
        return 0.0;
    }

    var rank = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Length);
    var index = Math.Clamp(rank - 1, 0, sortedValues.Length - 1);
    return sortedValues[index];
}

static BenchmarkOptions? ParseOptions(string[] args)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < args.Length; i++)
    {
        var key = args[i];
        if (!key.StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
        {
            return Usage($"Invalid option syntax near '{key}'.");
        }

        values[key[2..]] = args[++i];
    }

    if (!TryGet(values, "name", out var name) ||
        !TryGet(values, "url", out var url) ||
        !TryGetInt(values, "connections", out var connections) ||
        !TryGetInt(values, "messages-per-connection", out var messagesPerConnection) ||
        !TryGetInt(values, "message-size", out var messageSize) ||
        !TryGetInt(values, "receive-timeout-seconds", out var receiveTimeoutSeconds) ||
        !TryGet(values, "output", out var output))
    {
        return Usage("Missing or invalid required option.");
    }

    if (!Uri.TryCreate(url, UriKind.Absolute, out _))
    {
        return Usage("--url must be an absolute URL.");
    }

    if (connections <= 0 || messagesPerConnection <= 0 || messageSize < 0 || receiveTimeoutSeconds <= 0)
    {
        return Usage("Numeric options must be positive, except --message-size which may be zero.");
    }

    return new BenchmarkOptions(name, url, connections, messagesPerConnection, messageSize, receiveTimeoutSeconds, output);
}

static bool TryGet(Dictionary<string, string> values, string name, out string value)
{
    return values.TryGetValue(name, out value!) && !string.IsNullOrWhiteSpace(value);
}

static bool TryGetInt(Dictionary<string, string> values, string name, out int value)
{
    value = 0;
    return values.TryGetValue(name, out var text) && int.TryParse(text, out value);
}

static BenchmarkOptions? Usage(string message)
{
    Console.Error.WriteLine(message);
    Console.Error.WriteLine("""
Usage:
  Chat.LoadClient --name <name> --url <hub-url> --connections <count> --messages-per-connection <count> --message-size <chars> --receive-timeout-seconds <seconds> --output <path>
""");
    return null;
}

internal sealed record BenchmarkOptions(
    string Name,
    string Url,
    int Connections,
    int MessagesPerConnection,
    int MessageSize,
    int ReceiveTimeoutSeconds,
    string Output);

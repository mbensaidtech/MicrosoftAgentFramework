using System.Net.Sockets;
using CommonUtilities;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AIAgentWithCustomHttpTransport;

/// <summary>
/// BONUS (optional, needs Docker): sends the spans of the lab to a local Grafana (see the Grafana folder).
/// <para>
/// It does nothing unless the <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> environment variable is set: without Docker, the lab runs
/// exactly the same. When it is set, this class starts its own tracer provider, with an OTLP exporter. It is independent
/// from the tracer provider of scenario 3: several providers can listen to the same activity source, each one with its own
/// exporters (here: in memory for the console, OTLP for Grafana).
/// </para>
/// <para>
/// Only the spans are exported. The metrics of UseOpenTelemetry (gen_ai.client.token.usage...) would count each model call
/// twice in this lab, because the agent and its chat client are both instrumented; the spans hold the same numbers, once.
/// </para>
/// </summary>
internal sealed class GrafanaExport : IDisposable
{
    /// <summary>Standard OpenTelemetry variable, read by the OTLP exporter itself (gRPC by default: http://localhost:4317).</summary>
    public const string EndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>Service name of the spans in Grafana (the dashboard filters on it).</summary>
    public const string ServiceName = "lab11-observe-an-agent";

    /// <summary>The source name given to UseOpenTelemetry in scenario 3.</summary>
    private const string AgentTelemetrySource = "AIAgentWithCustomHttpTransport";

    /// <summary>Activity source of HttpClient itself (.NET 9 and later): one span per HTTP request, under its "chat" span.</summary>
    private const string HttpClientTelemetrySource = "System.Net.Http";

    private readonly TracerProvider _tracerProvider;

    private GrafanaExport(TracerProvider tracerProvider) => _tracerProvider = tracerProvider;

    /// <summary>Starts the export when OTEL_EXPORTER_OTLP_ENDPOINT is set and a collector answers on it; otherwise returns null.</summary>
    public static GrafanaExport? StartIfConfigured()
    {
        string? endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
        {
            ColoredConsole.WriteWarningLine($"Grafana bonus: {EndpointVariable}='{endpoint}' is not a URL (expected: http://localhost:4317). Export disabled.");
            return null;
        }

        // Without this check, the exporter would retry in the background and delay the end of the program.
        if (!AcceptsConnections(uri))
        {
            ColoredConsole.WriteWarningLine($"Grafana bonus: nothing listens on {uri.Host}:{uri.Port}. Start Grafana (docker compose up -d, in the Grafana folder) or remove {EndpointVariable}. Export disabled.");
            return null;
        }

        TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .AddSource(AgentTelemetrySource, HttpClientTelemetrySource)
            .AddOtlpExporter()
            .Build();

        ColoredConsole.WriteInfoLine($"Grafana bonus: the spans are also sent to {uri} (dashboard: http://localhost:3000).");
        return new GrafanaExport(tracerProvider);
    }

    /// <summary>Sends the spans still buffered, then stops the provider.</summary>
    public void Dispose() => _tracerProvider.Dispose();

    private static bool AcceptsConnections(Uri uri)
    {
        try
        {
            using TcpClient client = new();
            return client.ConnectAsync(uri.Host, uri.Port).Wait(TimeSpan.FromSeconds(1)) && client.Connected;
        }
        catch (Exception ex) when (ex is SocketException or AggregateException)
        {
            return false;
        }
    }
}

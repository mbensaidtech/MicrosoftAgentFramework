using System.Security.Cryptography;
using System.Text;
using LabDashboard.Settings;

namespace LabDashboard.TrainerModels;

/// <summary>
/// Simulated trainer source, overview and decryptor (<c>Dashboard:TrainerModels:Mock</c>), for development and tests only, so that the whole flow
/// can be built without the Admin Dashboard. It never calls the network and protects nothing: its "sealed" payload is a plain
/// marker, not encrypted, so that it cannot be mistaken for the real protocol. Its profiles are complete Azure OpenAI profiles with obviously
/// fake values (<c>*.trainer-mock.invalid</c> hosts, keys starting with <c>mock-</c>). It reads its options at every call, so a test can
/// change the error to simulate between two calls; the code it expects is configuration, not the candidate's code.
/// Its overview describes the same configurations as the contract would (default per kind, Azure OpenAI, key provided, 1536 dimensions);
/// <see cref="TrainerModelsMockOptions.RetrieveError"/> applies to the overview too.
/// </summary>
internal sealed class MockTrainerConfiguration : ITrainerConfigurationSource, ITrainerConfigurationDecryptor, ITrainerModelsOverviewSource
{
    /// <summary>The code that unlocks the simulated configuration unless <see cref="TrainerModelsMockOptions.Code"/> sets another one.</summary>
    public const string DefaultCode = "K7QX9";

    public const string ChatEndpoint = "https://trainer-mock.invalid/";
    public const string ChatDeployment = "mock-gpt-4o-mini";
    public const string ChatApiKey = "mock-llm-key-not-a-real-secret-1111";

    public const string EmbeddingEndpoint = "https://embedding.trainer-mock.invalid/";
    public const string EmbeddingDeployment = "mock-text-embedding-3-small";
    public const string EmbeddingApiKey = "mock-embedding-key-not-a-real-secret-2222";

    /// <summary>Ids and date of the simulated configurations, as the contract would send them (fixed: "updated since your last unlock" stays quiet).</summary>
    public const string ChatConfigId = "00000000-0000-4000-8000-00000000c4a7";
    public const string EmbeddingConfigId = "00000000-0000-4000-8000-0000000e3b0d";
    public const string ConfigUpdatedAt = "2026-10-25T00:00:00.000Z";
    public const string ChatConfigName = "Simulated LLM (mock)";
    public const string EmbeddingConfigName = "Simulated embedding model (mock)";

    private const string PayloadPrefix = "labbench-mock-trainer-configuration:";
    private const string ChatValue = "chat";
    private const string EmbeddingValue = "embedding";

    private readonly TrainerModelsMockOptions _options;

    public MockTrainerConfiguration(TrainerModelsMockOptions options, ILogger logger)
    {
        _options = options;
        if (!TrainerConfigurationService.IsValidCode(options.Code))
        {
            // The value is not logged: whatever it is, a code never reaches the log.
            logger.LogWarning("Dashboard:TrainerModels:Mock:Code is not a 5-character code of the alphabet: the simulated configuration is unlocked with the default code instead.");
        }
    }

    private string ExpectedCode => TrainerCodeFormat.Normalize(_options.Code) ?? DefaultCode;

    public Task<TrainerOutcome<SealedTrainerPayload>> RetrieveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_options.RetrieveError is { } error)
        {
            return Task.FromResult(TrainerOutcome<SealedTrainerPayload>.Failure(Simulate(error)));
        }

        // What the payload "contains" is fixed at retrieval, as a real sealed configuration would be.
        string kinds = _options.ProvideEmbedding ? $"{ChatValue},{EmbeddingValue}" : ChatValue;
        SealedTrainerPayload payload = new(Encoding.UTF8.GetBytes(PayloadPrefix + kinds)) { Overview = SimulatedOverview(_options.ProvideEmbedding) };
        return Task.FromResult(TrainerOutcome<SealedTrainerPayload>.Success(payload));
    }

    public Task<TrainerOutcome<TrainerModelsOverview>> GetOverviewAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_options.RetrieveError is { } error
            ? TrainerOutcome<TrainerModelsOverview>.Failure(Simulate(error))
            : TrainerOutcome<TrainerModelsOverview>.Success(SimulatedOverview(_options.ProvideEmbedding)));
    }

    /// <summary>The simulated configurations, built from a document shaped like the contract's so that they go through the same rules (Rule 17).</summary>
    internal static TrainerModelsOverview SimulatedOverview(bool provideEmbedding)
    {
        List<AdminModelConfig> configs =
        [
            new(ChatConfigId, ChatConfigName, ModelKind.Chat, AzureOpenAISettingsStore.AzureOpenAIProvider,
                new Dictionary<string, string> { ["endpoint"] = ChatEndpoint, ["deployment"] = ChatDeployment, ["model"] = "gpt-4o-mini" },
                new Dictionary<string, double>(), [AdminModelConfigsContract.ApiKeySecretField], IsDefault: true, Embedding: null, ConfigUpdatedAt),
        ];
        if (provideEmbedding)
        {
            configs.Add(new(EmbeddingConfigId, EmbeddingConfigName, ModelKind.Embedding, AzureOpenAISettingsStore.AzureOpenAIProvider,
                new Dictionary<string, string> { ["endpoint"] = EmbeddingEndpoint, ["deployment"] = EmbeddingDeployment, ["model"] = "text-embedding-3-small" },
                new Dictionary<string, double> { ["dimensions"] = TrainerModelsOverview.Lab07EmbeddingDimensions }, [AdminModelConfigsContract.ApiKeySecretField],
                IsDefault: true, new AdminEmbeddingInfo("text-embedding-3-small", TrainerModelsOverview.Lab07EmbeddingDimensions, "cosine"), ConfigUpdatedAt));
        }

        AdminModelConfigs document = new(configs, ChatConfigId, provideEmbedding ? EmbeddingConfigId : null, Sealed: null, [], [],
            SealedUnavailableReason: null, ServerTime: null, new Dictionary<string, ModelKind>());
        return TrainerModelsOverview.From(document, simulated: true);
    }

    public ValueTask<TrainerUnsealResult> UnsealAsync(SealedTrainerPayload payload, string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_options.UnlockError is { } error)
        {
            return Result(TrainerUnsealResult.Failure(Simulate(error)));
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(code), Encoding.ASCII.GetBytes(ExpectedCode)))
        {
            return Result(TrainerUnsealResult.Failure(TrainerConfigurationErrorKind.InvalidCode));
        }

        string content = Encoding.UTF8.GetString(payload.Content);
        if (!content.StartsWith(PayloadPrefix, StringComparison.Ordinal))
        {
            return Result(TrainerUnsealResult.Failure(TrainerConfigurationErrorKind.InvalidResponse));
        }

        List<TrainerModelProfile> profiles = [];
        foreach (string kind in content[PayloadPrefix.Length..].Split(','))
        {
            switch (kind)
            {
                case ChatValue:
                    profiles.Add(new TrainerModelProfile(ModelKind.Chat, AzureOpenAISettingsStore.AzureOpenAIProvider, ChatEndpoint, ChatDeployment, ChatApiKey)
                    {
                        ConfigId = ChatConfigId,
                        ConfigName = ChatConfigName,
                        ConfigUpdatedAt = ConfigUpdatedAt,
                    });
                    break;
                case EmbeddingValue:
                    profiles.Add(new TrainerModelProfile(ModelKind.Embedding, AzureOpenAISettingsStore.AzureOpenAIProvider, EmbeddingEndpoint, EmbeddingDeployment, EmbeddingApiKey)
                    {
                        ConfigId = EmbeddingConfigId,
                        ConfigName = EmbeddingConfigName,
                        ConfigUpdatedAt = ConfigUpdatedAt,
                    });
                    break;
                default:
                    return Result(TrainerUnsealResult.Failure(TrainerConfigurationErrorKind.InvalidResponse));
            }
        }

        return Result(TrainerUnsealResult.Success(profiles));
    }

    /// <summary>A simulated <c>notConfigured</c> names both settings, so that the UI shows its longest message.</summary>
    private static TrainerConfigurationError Simulate(TrainerConfigurationErrorKind kind) =>
        kind == TrainerConfigurationErrorKind.NotConfigured
            ? new TrainerConfigurationError(kind, [TrainerConfigurationService.ServerUrlSetting, TrainerConfigurationService.ApiKeySetting])
            : new TrainerConfigurationError(kind);

    private static ValueTask<T> Result<T>(T value) => ValueTask.FromResult(value);
}

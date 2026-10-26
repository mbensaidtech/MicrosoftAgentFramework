namespace LabDashboard.TrainerModels;

/// <summary>
/// The Admin Dashboard adapter of the trainer flow (contract v1, Business Rule 15): the overview and the sealed retrieval, both through
/// <see cref="AdminModelConfigsClient"/>. Used whenever the mock is off and the admin URL, the admin API key and the identity are known.
/// </summary>
internal sealed class AdminTrainerConfigurationSource(AdminModelConfigsClient client) : ITrainerConfigurationSource, ITrainerModelsOverviewSource
{
    public async Task<TrainerOutcome<TrainerModelsOverview>> GetOverviewAsync(CancellationToken cancellationToken)
    {
        TrainerOutcome<AdminModelConfigs> overview = await client.GetOverviewAsync(cancellationToken);
        return overview.Succeeded
            ? TrainerOutcome<TrainerModelsOverview>.Success(TrainerModelsOverview.From(overview.Value, simulated: false))
            : TrainerOutcome<TrainerModelsOverview>.Failure(overview.Error);
    }

    public async Task<TrainerOutcome<SealedTrainerPayload>> RetrieveAsync(CancellationToken cancellationToken)
    {
        TrainerOutcome<AdminSealedConfiguration> sealedConfiguration = await client.GetSealedAsync(cancellationToken);
        return sealedConfiguration.Succeeded
            ? TrainerOutcome<SealedTrainerPayload>.Success(new SealedTrainerPayload(sealedConfiguration.Value))
            : TrainerOutcome<SealedTrainerPayload>.Failure(sealedConfiguration.Error);
    }
}

/// <summary>
/// The adapter used when the admin URL or the admin API key is missing: the service answers <see cref="TrainerConfigurationErrorKind.NotConfigured"/>
/// before reaching it, so it is a safety net that never calls the network.
/// </summary>
internal sealed class TrainerConfigurationNotConfigured(IReadOnlyList<string> missing)
    : ITrainerConfigurationSource, ITrainerConfigurationDecryptor, ITrainerModelsOverviewSource
{
    private TrainerConfigurationError Error => new(TrainerConfigurationErrorKind.NotConfigured, missing);

    public Task<TrainerOutcome<TrainerModelsOverview>> GetOverviewAsync(CancellationToken cancellationToken) =>
        Task.FromResult(TrainerOutcome<TrainerModelsOverview>.Failure(Error));

    public Task<TrainerOutcome<SealedTrainerPayload>> RetrieveAsync(CancellationToken cancellationToken) =>
        Task.FromResult(TrainerOutcome<SealedTrainerPayload>.Failure(Error));

    public ValueTask<TrainerUnsealResult> UnsealAsync(SealedTrainerPayload payload, string code, CancellationToken cancellationToken) =>
        ValueTask.FromResult(TrainerUnsealResult.Failure(Error));
}

namespace CrmFantasy.Services.CrmIntegrations;

/// <summary>Tentativa registrada na integração.</summary>
public sealed record CrmIntegrationAttempt(
    int Attempt,
    string Status);

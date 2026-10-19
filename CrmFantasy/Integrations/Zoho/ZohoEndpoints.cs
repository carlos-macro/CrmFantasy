using System.Diagnostics.CodeAnalysis;
using CrmFantasy.Options;

namespace CrmFantasy.Integrations.Zoho;

/// <summary>URLs do Zoho para o data center e o ambiente configurados.</summary>
/// <param name="Accounts">Servidor OAuth (renovação do access token).</param>
/// <param name="CrmApi">Base da API REST do CRM, já com a versão (<c>/crm/v8/</c>).</param>
public sealed record ZohoEndpoints(Uri Accounts, Uri CrmApi)
{
    private static readonly Dictionary<string, (string Accounts, string Api)> Domains =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["US"] = ("accounts.zoho.com", "zohoapis.com"),
            ["EU"] = ("accounts.zoho.eu", "zohoapis.eu"),
            ["IN"] = ("accounts.zoho.in", "zohoapis.in"),
            ["AU"] = ("accounts.zoho.com.au", "zohoapis.com.au"),
            ["JP"] = ("accounts.zoho.jp", "zohoapis.jp"),
            ["CA"] = ("accounts.zohocloud.ca", "zohoapis.ca"),
        };

    private static readonly Dictionary<string, string> EnvironmentPrefixes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Production"] = "www",
            ["Sandbox"] = "sandbox",
            ["Developer"] = "developer",
        };

    public static bool TryCreate(ZohoOptions options, [NotNullWhen(true)] out ZohoEndpoints? endpoints)
    {
        endpoints = null;

        if (!Domains.TryGetValue(options.DataCenter, out var domains)) return false;

        if (!EnvironmentPrefixes.TryGetValue(options.Environment, out var prefix)) return false;

        endpoints = new ZohoEndpoints(
            new Uri($"https://{domains.Accounts}/"),
            new Uri($"https://{prefix}.{domains.Api}/crm/v8/"));

        return true;
    }

    public static ZohoEndpoints From(ZohoOptions options)
    {
        if (TryCreate(options, out var endpoints)) return endpoints;

        throw new ZohoCrmException(
            $"Data center ou ambiente do Zoho inválido: {options.DataCenter}/{options.Environment}.");
    }
}

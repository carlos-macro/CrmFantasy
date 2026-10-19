using CrmFantasy.Http;
using CrmFantasy.Integrations.Blip;
using CrmFantasy.Integrations.Blip.Interfaces;
using CrmFantasy.Integrations.Zoho;
using CrmFantasy.Integrations.Zoho.Interfaces;
using CrmFantasy.Options;
using CrmFantasy.Services.ActiveMessages;
using CrmFantasy.Services.CrmIntegrations;
using CrmFantasy.Services.Notes;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ZohoCrmExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services
    .AddOptions<ApiAccessOptions>()
    .BindConfiguration(ApiAccessOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ZohoOptions>()
    .BindConfiguration(ZohoOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(zoho => ZohoEndpoints.TryCreate(zoho, out _), "Zoho:DataCenter ou Zoho:Environment inválido.")
    .ValidateOnStart();

builder.Services
    .AddOptions<BlipHubOptions>()
    .BindConfiguration(BlipHubOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<CrmNoteOptions>()
    .BindConfiguration(CrmNoteOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<CrmIntegrationOptions>()
    .BindConfiguration(CrmIntegrationOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient(ZohoTokenProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<ZohoTokenProvider>();

builder.Services.AddHttpClient<IZohoCrmClient, ZohoCrmClient>((services, client) =>
{
    var zoho = services.GetRequiredService<IOptions<ZohoOptions>>().Value;

    client.BaseAddress = ZohoEndpoints.From(zoho).CrmApi;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IBlipHubClient, BlipHubClient>(
    client => client.Timeout = TimeSpan.FromSeconds(30));

builder.Services.AddScoped<CrmNoteService>();
builder.Services.AddScoped<CrmIntegrationService>();
builder.Services.AddScoped<ActiveMessageService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();

app.MapControllers();

app.Run();

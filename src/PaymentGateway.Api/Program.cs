using System.Diagnostics;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using PaymentGateway.Api.Options;
using PaymentGateway.Api.Services;

using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("supportedCurrencies.json", false, true)
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true);

builder.Services
    .Configure<BankOptions>(builder.Configuration.GetSection(BankOptions.Name))
    .Configure<CurrencyCodes>(builder.Configuration.GetSection(CurrencyCodes.Name));

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var serviceName = "payment-gateway-challenge-dotnet";
builder.Services.AddSingleton(new ActivitySource(serviceName));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => 
        resource
            .AddService(serviceName)
            .AddAttributes(new Dictionary<string, object>
            {
                { "service.version", "1.0.0" },
                { "deployment.environment", builder.Environment.EnvironmentName }
            }))
    .WithTracing(tracing => tracing
        .AddSource(serviceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

builder.Services
    .AddSingleton<IPaymentsRepository, PaymentsRepository>();

builder.Services.AddHttpClient<IPaymentProcessor, PaymentProcessor>(client =>
    {
        client.BaseAddress =
            new Uri(builder.Configuration.GetRequiredSection(BankOptions.Name).Get<BankOptions>()!.BaseUrl);
    })
    .AddPolicyHandler(_ =>
            HttpPolicyExtensions.HandleTransientHttpError()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();
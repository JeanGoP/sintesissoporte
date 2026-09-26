using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "Sidecil Tickets Mail");
var connection = builder.Configuration.GetConnectionString("Tickets") ?? throw new InvalidOperationException("Configura ConnectionStrings__Tickets.");
builder.Services.AddDbContext<TicketsDbContext>(o => o.UseSqlServer(connection));
builder.Services.Configure<MailOptions>(builder.Configuration.GetSection("Mail"));
builder.Services.AddScoped<EmailComposer>();
builder.Services.AddScoped<InboundProcessor>();
builder.Services.AddScoped<OutboundDispatcher>();
builder.Services.AddScoped<InboxPoller>();
var mail = builder.Configuration.GetSection("Mail").Get<MailOptions>() ?? new();
if (mail.Mode is not ("Disabled" or "Pickup" or "Smtp")) throw new InvalidOperationException("Modo de correo inválido.");
if (mail.Mode == "Pickup" && (!builder.Environment.IsDevelopment() || string.IsNullOrWhiteSpace(mail.PickupDirectory) || string.IsNullOrWhiteSpace(mail.InboxDirectory)))
    throw new InvalidOperationException("Pickup exige Development y directorios privados configurados.");
if (mail.Mode == "Smtp" && (string.IsNullOrWhiteSpace(mail.SmtpHost) || string.IsNullOrWhiteSpace(mail.SmtpUser) ||
    mail.FromAddress.EndsWith(".invalid") || !Uri.TryCreate(mail.PublicBaseUrl, UriKind.Absolute, out var publicUrl) || publicUrl.Scheme != "https"))
    throw new InvalidOperationException("Configura SMTP, remitente real y URL pública HTTPS.");
if (mail.Mode == "Smtp" && !string.IsNullOrEmpty(mail.ImapHost) && string.IsNullOrWhiteSpace(mail.TrustedAuthenticationService))
    throw new InvalidOperationException("Configura el Authentication-Results de confianza del proveedor IMAP.");
if (!args.Contains("--once")) builder.Services.AddHostedService<MailWorker>();
using var host = builder.Build();
if (args.Contains("--once")) {
    using var scope = host.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<OutboundDispatcher>().DispatchAsync();
    scope.ServiceProvider.GetRequiredService<TicketsDbContext>().ChangeTracker.Clear();
    await scope.ServiceProvider.GetRequiredService<InboxPoller>().PollAsync(CancellationToken.None);
} else await host.RunAsync();

public sealed class MailWorker(IServiceScopeFactory scopes, ILogger<MailWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(RunAsync(true, stoppingToken), RunAsync(false, stoppingToken));

    private async Task RunAsync(bool outbound, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = scopes.CreateScope();
                if (outbound) await scope.ServiceProvider.GetRequiredService<OutboundDispatcher>().DispatchAsync(stoppingToken);
                else await scope.ServiceProvider.GetRequiredService<InboxPoller>().PollAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) {
                logger.LogError("Falló el ciclo de correo {Direction} ({ErrorType}); se reintentará.",
                    outbound ? "saliente" : "entrante", ex.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}

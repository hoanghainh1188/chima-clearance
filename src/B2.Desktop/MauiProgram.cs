using System.Text.Json;
using System.Text.Json.Serialization;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Desktop.Services.Logging;
using B2.Desktop.Services.UserSettings;
using B2.Desktop.ViewModels;
using B2.Desktop.ViewModels.Admin;
using B2.Desktop.Views;
using B2.Desktop.Views.Admin;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Refit;

namespace B2.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit();

        // The exit-gate PC is a LAN client of the entry-gate PC, so the server address is
        // deployment data (b2-client.json / B2_SERVER_URL), not a constant. See ApiEndpointResolver.
        var endpoint = ApiEndpointResolver.Resolve(
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable(ApiEndpointResolver.EnvironmentVariableName));
        var apiUri = endpoint.BaseUri;
        builder.Services.AddSingleton(new DesktopRuntimeOptions(apiUri, endpoint.Source));
        builder.Services.AddSingleton<ConnectivityService>();
        builder.Services.AddSingleton<IToastService, ToastService>();
        builder.Services.AddSingleton<IModalService, ModalService>();
        builder.Services.AddSingleton<IUserFormDialog, UserFormDialog>();
        builder.Services.AddSingleton<ISessionCorrelationService, SessionCorrelationService>();
        builder.Services.AddSingleton<ISessionContext, SessionContext>();
        builder.Services.AddSingleton<IAppNavigator, AppNavigator>();
        builder.Services.AddSingleton<IUserSettingsService, UserSettingsService>();
        builder.Services.AddTransient<CorrelationCaptureHandler>();

        var refitSettings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() },
                }),
        };

        // Token STATE lives in a singleton store; the DelegatingHandler that
        // reads it MUST be transient (HttpClientFactory rebuilds the handler
        // chain and rejects a reused/cached handler instance).
        builder.Services.AddSingleton<IAuthTokenStore, AuthTokenStore>();
        builder.Services.AddTransient<AuthTokenHandler>();
        builder.Services.AddRefitClient<IB2Api>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = apiUri)
            .AddHttpMessageHandler<AuthTokenHandler>()
            .AddHttpMessageHandler<CorrelationCaptureHandler>();

        builder.Services.AddSingleton<IMainShellFactory, MainShellFactory>();

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ChangePasswordViewModel>();
        builder.Services.AddTransient<ChangePasswordPage>();
        builder.Services.AddTransient<UsersViewModel>();
        builder.Services.AddTransient<UsersPage>();
        builder.Services.AddTransient<EntryViewModel>();
        builder.Services.AddTransient<EntryPage>();
        builder.Services.AddTransient<ExitViewModel>();
        builder.Services.AddTransient<ExitPage>();
        builder.Services.AddTransient<MasterDataViewModel>();
        builder.Services.AddTransient<MasterDataPage>();
        builder.Services.AddTransient<LedgerViewModel>();
        builder.Services.AddTransient<LedgerPage>();
        builder.Services.AddTransient<ReportsViewModel>();
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<ExportViewModel>();
        builder.Services.AddTransient<ExportPage>();
        builder.Services.AddTransient<AuditViewModel>();
        builder.Services.AddTransient<AuditPage>();
        builder.Services.AddTransient<HealthViewModel>();
        builder.Services.AddTransient<HealthPage>();
        builder.Services.AddTransient<BackupViewModel>();
        builder.Services.AddTransient<BackupPage>();
        builder.Services.AddTransient<KvgsViewModel>();
        builder.Services.AddTransient<KvgsPage>();
        builder.Services.AddTransient<AppShell>();

        // A shipped build had no logging provider at all (AddDebug was inside #if DEBUG — and
        // did not even compile, since Microsoft.Extensions.Logging.Debug is not referenced), so
        // ILogger calls went nowhere on a gate PC. The runbook points operators at this file.
        builder.Logging.AddProvider(new FileLoggerProvider(FileLoggerProvider.DefaultDirectory));

        var app = builder.Build();

        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(MauiProgram));
        if (endpoint.Warning is not null)
        {
            log.LogWarning("Server address: {Warning}", endpoint.Warning);
        }
        else
        {
            log.LogInformation("Server address {Uri} (from {Source}).", apiUri, endpoint.Source);
        }

        return app;
    }
}

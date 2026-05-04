using System.Text.Json;
using System.Text.Json.Serialization;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
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

        const string apiBase = "http://127.0.0.1:5000/";
        var apiUri = new Uri(apiBase);
        builder.Services.AddSingleton(new DesktopRuntimeOptions(apiUri));
        builder.Services.AddSingleton<ConnectivityService>();
        builder.Services.AddSingleton<IToastService, ToastService>();
        builder.Services.AddSingleton<IModalService, ModalService>();
        builder.Services.AddSingleton<ISessionCorrelationService, SessionCorrelationService>();
        builder.Services.AddSingleton<ISessionContext, SessionContext>();
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

        builder.Services.AddSingleton<AuthTokenHandler>();
        builder.Services.AddRefitClient<IB2Api>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBase))
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
        builder.Services.AddTransient<KvgsViewModel>();
        builder.Services.AddTransient<KvgsPage>();
        builder.Services.AddTransient<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

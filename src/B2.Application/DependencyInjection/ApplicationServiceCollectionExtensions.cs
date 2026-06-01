using B2.Application.Admin.Audit;
using B2.Application.Admin.Health;
using B2.Application.Admin.LogViewer;
using B2.Application.Admin.Restore;
using B2.Application.Auth;
using B2.Application.Declarations;
using B2.Application.Declarations.Cancel;
using B2.Application.Declarations.ConfirmKvgs;
using B2.Application.Declarations.Exit;
using B2.Application.Declarations.List;
using B2.Application.DailyHeaders;
using B2.Application.Export;
using B2.Application.MasterData;
using B2.Application.Reports;
using B2.Application.Users;
using B2.Application.Vehicles;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddB2Application(this IServiceCollection services)
    {
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<RefreshCommandHandler>();
        services.AddScoped<ChangePasswordCommandHandler>();
        services.AddScoped<UserCommandHandler>();
        services.AddScoped<MasterDataCommandHandler>();
        services.AddScoped<CreateDeclarationCommandHandler>();
        services.AddScoped<AddVehicleCommandHandler>();
        services.AddScoped<MarkVehicleExitCommandHandler>();
        services.AddScoped<UnExitVehicleCommandHandler>();
        services.AddScoped<ConfirmKvgsCommandHandler>();
        services.AddScoped<CancelDeclarationCommandHandler>();
        services.AddScoped<ListDeclarationsQueryHandler>();
        services.AddScoped<GetDeclarationDetailQueryHandler>();
        services.AddScoped<SearchVehiclesBySuffixQueryHandler>();
        services.AddScoped<EnsureTodayHeaderCommandHandler>();
        services.AddScoped<RecomputeDailySummaryCommandHandler>();
        services.AddScoped<UpdateOfficerNameCommandHandler>();
        services.AddScoped<ListDailyHeadersQueryHandler>();
        services.AddScoped<ByProcessorReportQueryHandler>();
        services.AddScoped<ByYardReportQueryHandler>();
        services.AddScoped<ByHourReportQueryHandler>();
        services.AddScoped<ByOfficerReportQueryHandler>();
        services.AddScoped<PendingReportQueryHandler>();
        services.AddScoped<ExportSoB2QueryHandler>();
        services.AddScoped<ListAuditQueryHandler>();
        services.AddScoped<LogsQueryHandler>();
        services.AddScoped<HealthQueryHandler>();
        services.AddScoped<RestoreCommandHandler>();
        services.AddValidatorsFromAssemblyContaining<Auth.Validators.LoginRequestValidator>();
        return services;
    }
}

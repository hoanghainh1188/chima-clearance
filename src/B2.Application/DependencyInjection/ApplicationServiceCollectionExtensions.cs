using B2.Application.Auth;
using B2.Application.Declarations;
using B2.Application.Declarations.Exit;
using B2.Application.MasterData;
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
        services.AddScoped<SearchVehiclesBySuffixQueryHandler>();
        services.AddValidatorsFromAssemblyContaining<Auth.Validators.LoginRequestValidator>();
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;

using PromptNest.Core.Abstractions;
using PromptNest.Data.Db;
using PromptNest.Data.Migrations;
using PromptNest.Data.Repositories;

namespace PromptNest.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddPromptNestData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IMigrationRunner, SqliteMigrationRunner>();
        services.AddSingleton<IPromptRepository, PromptRepository>();
        services.AddSingleton<IFolderRepository, FolderRepository>();
        services.AddSingleton<ITagRepository, TagRepository>();
        services.AddSingleton<IVariableValueRepository, VariableValueRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();

        return services;
    }
}
using Microsoft.Extensions.DependencyInjection;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Services;
using PromptNest.Core.Variables;

namespace PromptNest.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddPromptNestCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IVariableParser, VariableParser>();
        services.AddSingleton<IVariableResolver, VariableResolver>();
        services.AddSingleton<IPromptService, PromptService>();
        services.AddSingleton<IFolderService, FolderService>();
        services.AddSingleton<ITagService, TagService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IPromptCopyService, PromptCopyService>();
        services.AddSingleton<IImportExportService, ImportExportService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IRepositoryPromptScanner, RepositoryPromptScanner>();
        services.AddSingleton<IRepositoryPromptImportNormalizer, RepositoryPromptImportNormalizer>();
        services.AddSingleton<ILinearBatchReportFormatter, LinearBatchReportFormatter>();

        return services;
    }
}

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Services;

public sealed class FolderService : IFolderService
{
    private readonly IFolderRepository _folderRepository;

    public FolderService(IFolderRepository folderRepository)
    {
        _folderRepository = folderRepository;
    }

    public Task<IReadOnlyList<Folder>> ListAsync(CancellationToken cancellationToken) =>
        _folderRepository.ListAsync(cancellationToken);
}
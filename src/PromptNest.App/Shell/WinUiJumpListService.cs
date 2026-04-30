using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

using Windows.UI.StartScreen;

namespace PromptNest.App.Shell;

public sealed class WinUiJumpListService : IJumpListService
{
    public async Task RefreshFavoritesAsync(IReadOnlyList<Prompt> favoritePrompts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(favoritePrompts);
        cancellationToken.ThrowIfCancellationRequested();

        if (!JumpList.IsSupported())
        {
            return;
        }

        try
        {
            JumpList jumpList = await JumpList.LoadCurrentAsync();
            jumpList.SystemGroupKind = JumpListSystemGroupKind.None;
            jumpList.Items.Clear();

            foreach (Prompt prompt in favoritePrompts.Take(5))
            {
                JumpListItem item = JumpListItem.CreateWithArguments(
                    $"promptnest://prompt/{Uri.EscapeDataString(prompt.Id)}",
                    prompt.Title);
                item.Description = "Open prompt in PromptNest";
                jumpList.Items.Add(item);
            }

            await jumpList.SaveAsync();
        }
        catch (Exception)
        {
            // Jump Lists can be unavailable for unpackaged/debug runs. Startup should remain healthy.
        }
    }
}
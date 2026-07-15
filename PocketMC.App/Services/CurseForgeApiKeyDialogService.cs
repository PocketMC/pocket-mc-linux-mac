using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PocketMC.Core.Services;
using PocketMC.App.Views;
using System.Threading.Tasks;

namespace PocketMC.App.Services
{
    public class CurseForgeApiKeyDialogService : ICurseForgeApiKeyDialogService
    {
        public Task<string?> PromptForApiKeyAsync()
        {
            var tcs = new TaskCompletionSource<string?>();

            Dispatcher.UIThread.Post(async () =>
            {
                var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var owner = lifetime?.MainWindow;

                var dialog = new CurseForgeApiKeyDialogWindow();
                
                bool result = false;
                if (owner != null)
                {
                    var showRes = await dialog.ShowDialog<bool>(owner);
                    result = showRes;
                }
                else
                {
                    // Fallback if no main window owner
                    var showRes = await dialog.ShowDialog<bool>(dialog);
                    result = showRes;
                }

                if (result)
                {
                    tcs.SetResult(dialog.ApiKey);
                }
                else
                {
                    tcs.SetResult(null);
                }
            });

            return tcs.Task;
        }
    }
}

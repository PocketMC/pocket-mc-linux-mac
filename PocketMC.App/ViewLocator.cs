using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace PocketMC.App
{
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? data)
        {
            if (data is null) return null;

            var vmName = data.GetType().FullName!;
            var viewName = vmName.Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);

            var name = viewName.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);
            if (type == null)
            {
                name = viewName.Replace("ViewModel", "Page", StringComparison.Ordinal);
                type = Type.GetType(name);
            }

            if (type != null)
            {
                var resolvedView = App.Services.GetService(type);
                if (resolvedView is Control control)
                {
                    return control;
                }
                
                return (Control)Activator.CreateInstance(type)!;
            }

            return new TextBlock { Text = "View Not Found: " + name };
        }

        public bool Match(object? data)
        {
            return data is CommunityToolkit.Mvvm.ComponentModel.ObservableObject;
        }
    }
}

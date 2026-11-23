using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace PocketMC.App
{
    public class ViewLocator : IDataTemplate
    {
        public static Type? ResolveViewType(Type vmType)
        {
            var vmName = vmType.FullName!;
            var viewName = vmName.Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);

            var name = viewName.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = vmType.Assembly.GetType(name) ?? Type.GetType(name);
            if (type == null)
            {
                name = viewName.Replace("ViewModel", "Page", StringComparison.Ordinal);
                type = vmType.Assembly.GetType(name) ?? Type.GetType(name);
            }

            return type;
        }

        public Control? Build(object? data)
        {
            if (data is null) return null;

            var type = ResolveViewType(data.GetType());
            if (type != null)
            {
                Control control;
                var resolvedView = App.Services?.GetService(type);
                if (resolvedView is Control c)
                {
                    control = c;
                }
                else
                {
                    control = (Control)Activator.CreateInstance(type)!;
                }

                control.DataContext = data;
                return control;
            }

            return new TextBlock { Text = "View Not Found: " + data.GetType().FullName };
        }

        public bool Match(object? data)
        {
            return data is CommunityToolkit.Mvvm.ComponentModel.ObservableObject;
        }
    }
}

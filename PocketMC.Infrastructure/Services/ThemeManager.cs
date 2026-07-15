using System;
using Avalonia;
using Avalonia.Styling;

namespace PocketMC.Infrastructure.Services
{
    public enum ThemeMode { System, Light, Dark }

    public class ThemeManager
    {
        public void ApplyTheme(ThemeMode mode)
        {
            var app = Application.Current;
            if (app == null) return;

            app.RequestedThemeVariant = mode switch
            {
                ThemeMode.Light => ThemeVariant.Light,
                ThemeMode.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default // Tracks OS theme automatically
            };
        }
    }
}

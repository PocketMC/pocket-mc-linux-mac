using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PocketMC.Core.Models;

namespace PocketMC.App.Views.Dialogs;

public partial class OllamaModelManagerDialog : Window
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public string? SelectedModelName { get; private set; }

    public OllamaModelManagerDialog()
    {
        InitializeComponent();

        BtnClose.Click += (s, e) => Close();
        BtnPull.Click += BtnPull_Click;

        _ = CheckDaemonAndLoadModelsAsync();
    }

    private async Task CheckDaemonAndLoadModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("http://localhost:11434/api/tags");
            if (response.IsSuccessStatusCode)
            {
                TxtStatus.Text = "Local Ollama daemon is online.";
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var list = new List<OllamaModelInfo>();

                if (doc.RootElement.TryGetProperty("models", out var modelsElem) && modelsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in modelsElem.EnumerateArray())
                    {
                        string name = m.GetProperty("name").GetString() ?? "";
                        long size = m.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                        list.Add(new OllamaModelInfo
                        {
                            Name = name,
                            SizeBytes = size
                        });
                    }
                }

                InstalledModelsList.ItemsSource = list;
            }
            else
            {
                TxtStatus.Text = $"Daemon returned status: {response.StatusCode}";
            }
        }
        catch
        {
            TxtStatus.Text = "Ollama daemon is offline. See Setup Guide tab.";
        }
    }

    private async void BtnPull_Click(object? sender, RoutedEventArgs e)
    {
        string model = TxtPullModelTag.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(model)) return;

        BtnPull.IsEnabled = false;
        PullProgressBar.IsVisible = true;
        TxtPullStatus.IsVisible = true;
        TxtPullStatus.Text = $"Requesting download for {model}...";

        try
        {
            var payload = JsonSerializer.Serialize(new { name = model, stream = false });
            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync("http://localhost:11434/api/pull", content);

            if (res.IsSuccessStatusCode)
            {
                TxtPullStatus.Text = $"Successfully pulled model '{model}'!";
                PullProgressBar.Value = 100;
                await CheckDaemonAndLoadModelsAsync();
            }
            else
            {
                TxtPullStatus.Text = $"Failed to pull: {res.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            TxtPullStatus.Text = $"Error: {ex.Message}";
        }
        finally
        {
            BtnPull.IsEnabled = true;
        }
    }

    private void SelectModel_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
        {
            SelectedModelName = name;
            Close();
        }
    }
}

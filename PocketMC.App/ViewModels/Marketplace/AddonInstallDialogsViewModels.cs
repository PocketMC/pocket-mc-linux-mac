using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels.Marketplace;

public partial class AddonInstallRowViewModel : ObservableObject
{
    public ResolvedDependency ResolvedItem { get; init; } = null!;

    public string ProjectTitle => ResolvedItem.ProjectTitle;
    public string FileName => ResolvedItem.FileName;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private bool _isDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusText = "Waiting...";

    [ObservableProperty]
    private string _statusForeground = "#A6ADC8";

    public bool HasStatus => !string.IsNullOrEmpty(StatusText) || IsDownloading;
}

public partial class DependencyConfirmationViewModel : ObservableObject
{
    public ObservableCollection<ResolvedDependency> Dependencies { get; }

    public bool HasIncompatible { get; }
    public bool HasErrors { get; }
    public bool HasFailedRequiredDependency => Dependencies.Any(d => d.Type == DependencyType.Required && !string.IsNullOrEmpty(d.Error));
    public bool CanInstall => !HasIncompatible && !HasFailedRequiredDependency && Dependencies.Any(d => d.IsSelected);

    public IRelayCommand ConfirmCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public bool? Result { get; private set; }
    public event Action? CloseRequested;

    public DependencyConfirmationViewModel(IEnumerable<ResolvedDependency> dependencies)
    {
        Dependencies = new ObservableCollection<ResolvedDependency>(dependencies);
        foreach (var dep in Dependencies)
        {
            dep.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ResolvedDependency.IsSelected))
                {
                    OnPropertyChanged(nameof(CanInstall));
                }
            };
        }

        HasIncompatible = Dependencies.Any(d => d.Type == DependencyType.Incompatible);
        HasErrors = Dependencies.Any(d => !string.IsNullOrEmpty(d.Error) || !string.IsNullOrEmpty(d.Warning));

        ConfirmCommand = new RelayCommand(() =>
        {
            Result = true;
            CloseRequested?.Invoke();
        });
        CancelCommand = new RelayCommand(() =>
        {
            Result = false;
            CloseRequested?.Invoke();
        });
    }
}

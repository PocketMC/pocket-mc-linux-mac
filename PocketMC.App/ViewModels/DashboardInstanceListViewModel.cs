using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels
{
    public partial class DashboardInstanceListViewModel : ObservableObject
    {
        private readonly IInstanceService _instanceService;

        [ObservableProperty]
        private bool _isLoading = true;

        public ObservableCollection<InstanceCardViewModel> Instances { get; } = new();

        public DashboardInstanceListViewModel(IInstanceService instanceService)
        {
            _instanceService = instanceService;
        }

        public async Task LoadInstancesAsync()
        {
            try
            {
                if (Instances.Count == 0)
                {
                    IsLoading = true;
                }

                var list = await _instanceService.ListInstancesAsync();

                var sorted = list
                    .OrderByDescending(m => m.PinnedAt.HasValue)
                    .ThenBy(m => m.PinnedAt)
                    .ThenByDescending(m => m.CreatedAt)
                    .ToList();

                // Synchronize collection
                var currentMap = Instances.ToDictionary(c => c.Id);
                var newCards = new ObservableCollection<InstanceCardViewModel>();

                foreach (var item in sorted)
                {
                    if (currentMap.TryGetValue(item.Id, out var existingCard))
                    {
                        newCards.Add(existingCard);
                    }
                    else
                    {
                        newCards.Add(new InstanceCardViewModel(item));
                    }
                }

                Instances.Clear();
                foreach (var card in newCards)
                {
                    Instances.Add(card);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

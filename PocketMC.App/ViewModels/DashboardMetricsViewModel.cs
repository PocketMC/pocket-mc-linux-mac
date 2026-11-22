using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels
{
    public partial class DashboardMetricsViewModel : ObservableObject
    {
        private readonly INativeMetricsProvider? _metricsProvider;
        private readonly Dictionary<Guid, CircularBuffer<double>> _cpuHistories = new();
        private readonly Dictionary<Guid, CircularBuffer<double>> _ramHistories = new();

        public DashboardMetricsViewModel(INativeMetricsProvider? metricsProvider = null)
        {
            _metricsProvider = metricsProvider;
        }

        public void ApplyMetrics(InstanceCardViewModel vm, double cpuUsagePercent, double ramUsageMb, int playerCount)
        {
            if (!vm.IsRunning)
            {
                vm.CpuText = "· · ·";
                vm.RamText = "· · ·";
                vm.PlayerStatus = "· · ·";
                vm.NotifyUptimeChanged();
                return;
            }

            vm.UpdateMetrics(cpuUsagePercent, ramUsageMb, playerCount);
            RecordHistory(vm.Id, cpuUsagePercent, ramUsageMb);
        }

        public void ApplyLiveMetrics(InstanceCardViewModel vm, int pid, int playerCount = 0)
        {
            if (!vm.IsRunning || _metricsProvider == null || pid <= 0)
            {
                vm.CpuText = "· · ·";
                vm.RamText = "· · ·";
                vm.PlayerStatus = "· · ·";
                vm.NotifyUptimeChanged();
                return;
            }

            var snapshot = _metricsProvider.GetProcessMetrics(pid);
            double ramMb = snapshot.MemoryBytes / (1024.0 * 1024.0);
            ApplyMetrics(vm, snapshot.CpuPercent, ramMb, playerCount);
        }

        public IEnumerable<double> GetCpuHistory(Guid id)
        {
            if (_cpuHistories.TryGetValue(id, out var buffer))
            {
                return buffer.ToList();
            }
            return Array.Empty<double>();
        }

        public IEnumerable<double> GetRamHistory(Guid id)
        {
            if (_ramHistories.TryGetValue(id, out var buffer))
            {
                return buffer.ToList();
            }
            return Array.Empty<double>();
        }

        private void RecordHistory(Guid id, double cpu, double ram)
        {
            if (!_cpuHistories.TryGetValue(id, out var cpuBuf))
            {
                cpuBuf = new CircularBuffer<double>(30);
                _cpuHistories[id] = cpuBuf;
            }
            cpuBuf.Add(cpu);

            if (!_ramHistories.TryGetValue(id, out var ramBuf))
            {
                ramBuf = new CircularBuffer<double>(30);
                _ramHistories[id] = ramBuf;
            }
            ramBuf.Add(ram);
        }
    }
}

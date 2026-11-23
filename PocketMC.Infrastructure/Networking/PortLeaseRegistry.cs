using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using PocketMC.Core.Models.Networking;

namespace PocketMC.Infrastructure.Networking;

public sealed class PortLeaseRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<PortLeaseKey, PortLease> _leasesByKey = new();
    private readonly Dictionary<Guid, HashSet<PortLeaseKey>> _leaseKeysByInstance = new();

    public bool TryReserve(PortLease lease, out PortLease? conflictingLease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        if (!lease.InstanceId.HasValue)
        {
            throw new ArgumentException("Port leases stored in the registry must have an owning instance id.", nameof(lease));
        }

        PortLeaseKey key = PortLeaseKey.FromLease(lease);

        lock (_sync)
        {
            if (_leasesByKey.TryGetValue(key, out PortLease? existingExactLease))
            {
                if (existingExactLease.InstanceId == lease.InstanceId)
                {
                    conflictingLease = null;
                    return true;
                }

                conflictingLease = existingExactLease;
                return false;
            }

            PortLease? overlap = FindOverlappingLeaseUnsafe(lease);
            if (overlap != null)
            {
                conflictingLease = overlap;
                return false;
            }

            _leasesByKey[key] = lease;

            Guid instanceId = lease.InstanceId.Value;
            if (!_leaseKeysByInstance.TryGetValue(instanceId, out HashSet<PortLeaseKey>? keys))
            {
                keys = new HashSet<PortLeaseKey>();
                _leaseKeysByInstance[instanceId] = keys;
            }

            keys.Add(key);
            conflictingLease = null;
            return true;
        }
    }

    public bool Release(PortLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        if (!lease.InstanceId.HasValue)
        {
            return false;
        }

        return Release(
            lease.InstanceId.Value,
            lease.Port,
            lease.Protocol,
            lease.IpMode,
            lease.BindAddress);
    }

    public bool Release(Guid instanceId, int port, PortProtocol protocol = PortProtocol.Tcp, PortIpMode ipMode = PortIpMode.IPv4, string? bindAddress = null)
    {
        PortLeaseKey key = new(port, protocol, ipMode, NormalizeBindAddress(bindAddress));

        lock (_sync)
        {
            if (!_leasesByKey.TryGetValue(key, out PortLease? existing) || existing.InstanceId != instanceId)
            {
                return false;
            }

            _leasesByKey.Remove(key);

            if (_leaseKeysByInstance.TryGetValue(instanceId, out HashSet<PortLeaseKey>? keys))
            {
                keys.Remove(key);
                if (keys.Count == 0)
                {
                    _leaseKeysByInstance.Remove(instanceId);
                }
            }

            return true;
        }
    }

    public int ReleaseAll(Guid instanceId)
    {
        lock (_sync)
        {
            if (!_leaseKeysByInstance.TryGetValue(instanceId, out HashSet<PortLeaseKey>? keys) || keys.Count == 0)
            {
                return 0;
            }

            int count = 0;
            foreach (PortLeaseKey key in keys)
            {
                if (_leasesByKey.Remove(key))
                {
                    count++;
                }
            }

            _leaseKeysByInstance.Remove(instanceId);
            return count;
        }
    }

    public IReadOnlyList<PortLease> GetLeases()
    {
        lock (_sync)
        {
            return _leasesByKey.Values.ToArray();
        }
    }

    public IReadOnlyList<PortLease> GetLeasesForInstance(Guid instanceId)
    {
        lock (_sync)
        {
            if (!_leaseKeysByInstance.TryGetValue(instanceId, out HashSet<PortLeaseKey>? keys) || keys.Count == 0)
            {
                return Array.Empty<PortLease>();
            }

            return keys
                .Select(key => _leasesByKey.TryGetValue(key, out PortLease? lease) ? lease : null)
                .Where(lease => lease != null)
                .Select(lease => lease!)
                .ToArray();
        }
    }

    public bool IsPortLeased(int port, PortProtocol protocol = PortProtocol.Tcp, PortIpMode ipMode = PortIpMode.IPv4, string? bindAddress = null, Guid? ignoringInstanceId = null)
    {
        var probeLease = new PortLease(port, protocol, ipMode, bindAddress: bindAddress);

        lock (_sync)
        {
            return _leasesByKey.Values.Any(existing =>
                (!ignoringInstanceId.HasValue || existing.InstanceId != ignoringInstanceId) &&
                LeasesOverlap(existing, probeLease));
        }
    }

    private PortLease? FindOverlappingLeaseUnsafe(PortLease candidate)
    {
        foreach (PortLease existing in _leasesByKey.Values)
        {
            if (existing.InstanceId == candidate.InstanceId)
            {
                continue;
            }

            if (LeasesOverlap(existing, candidate))
            {
                return existing;
            }
        }

        return null;
    }

    private static bool LeasesOverlap(PortLease left, PortLease right)
    {
        if (left.Port != right.Port)
        {
            return false;
        }

        if (!ProtocolsOverlap(left.Protocol, right.Protocol))
        {
            return false;
        }

        if (!IpModesOverlap(left.IpMode, right.IpMode))
        {
            return false;
        }

        return BindAddressesOverlap(left.BindAddress, right.BindAddress);
    }

    private static bool ProtocolsOverlap(PortProtocol left, PortProtocol right)
    {
        if (left == PortProtocol.TcpAndUdp || right == PortProtocol.TcpAndUdp)
        {
            return true;
        }

        return left == right;
    }

    private static bool IpModesOverlap(PortIpMode left, PortIpMode right)
    {
        if (left == PortIpMode.DualStack || right == PortIpMode.DualStack)
        {
            return true;
        }

        return left == right;
    }

    private static bool BindAddressesOverlap(string? left, string? right)
    {
        string normalizedLeft = NormalizeBindAddress(left);
        string normalizedRight = NormalizeBindAddress(right);

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        bool leftIsWildcard = IsWildcardAddress(normalizedLeft);
        bool rightIsWildcard = IsWildcardAddress(normalizedRight);

        return leftIsWildcard || rightIsWildcard;
    }

    private static bool IsWildcardAddress(string normalizedAddress)
    {
        return string.IsNullOrWhiteSpace(normalizedAddress) ||
               normalizedAddress == "0.0.0.0" ||
               normalizedAddress == "::";
    }

    private static string NormalizeBindAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return string.Empty;
        }

        address = address.Trim();
        if (IPAddress.TryParse(address, out IPAddress? parsed))
        {
            return parsed.ToString();
        }

        return address;
    }

    private readonly record struct PortLeaseKey(
        int Port,
        PortProtocol Protocol,
        PortIpMode IpMode,
        string BindAddress)
    {
        public static PortLeaseKey FromLease(PortLease lease)
        {
            return new PortLeaseKey(
                lease.Port,
                lease.Protocol,
                lease.IpMode,
                NormalizeBindAddress(lease.BindAddress));
        }
    }
}

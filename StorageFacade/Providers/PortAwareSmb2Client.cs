// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using SMBLibrary;
    using SMBLibrary.Client;

    /// <summary>
    /// <see cref="SMB2Client"/> with an explicit TCP port. Public
    /// <see cref="SMB2Client.Connect(string, SMBTransportType)"/> always uses 445;
    /// the library exposes a <c>protected internal</c> overload that accepts a port.
    /// </summary>
    internal sealed class PortAwareSmb2Client : SMB2Client
    {
        private readonly Func<string, IPAddress[]> _resolveHost;
        private readonly Func<IPAddress, int, bool> _connectToAddress;

        internal PortAwareSmb2Client()
            : this(Dns.GetHostAddresses, connectToAddress: null)
        {
        }

        /// <summary>Test seam for DNS resolution and TCP connect.</summary>
        internal PortAwareSmb2Client(
            Func<string, IPAddress[]> resolveHost,
            Func<IPAddress, int, bool> connectToAddress = null)
        {
            _resolveHost = resolveHost ?? throw new ArgumentNullException(nameof(resolveHost));
            _connectToAddress = connectToAddress;
        }

        /// <summary>Connects with Direct TCP transport on <paramref name="port"/> (default SMB is 445).</summary>
        public bool Connect(string serverName, int port)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
            if (port is < 1 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be 1–65535.");

            IPAddress[] hostAddresses = _resolveHost(serverName);
            IPAddress serverAddress = SelectServerAddress(serverName, hostAddresses);
            return ConnectTo(serverAddress, port);
        }

        internal static IPAddress SelectServerAddress(string serverName, IPAddress[] hostAddresses)
        {
            if (hostAddresses == null || hostAddresses.Length == 0)
                throw new StorageFacadeException($"Cannot resolve host name {serverName} to an IP address");

            return hostAddresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? hostAddresses[0];
        }

        private bool ConnectTo(IPAddress serverAddress, int port)
        {
            if (_connectToAddress != null)
                return _connectToAddress(serverAddress, port);

            return Connect(serverAddress, SMBTransportType.DirectTCPTransport, port);
        }
    }
}

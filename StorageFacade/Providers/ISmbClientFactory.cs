// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using SMBLibrary;
    using SMBLibrary.Client;

    /// <summary>
    /// Factory for authenticated SMB sessions used by <see cref="SMBNetworkStorageProvider"/>.
    /// </summary>
    internal interface ISmbClientFactory
    {
        ISMBClient CreateConnectedClient();
    }

    internal sealed class SmbClientFactory : ISmbClientFactory
    {
        private readonly SMBNetworkStorageProviderConfig _config;

        internal SmbClientFactory(SMBNetworkStorageProviderConfig config)
        {
            _config = config;
        }

        public ISMBClient CreateConnectedClient()
        {
            var smbClient = new PortAwareSmb2Client();
            bool isConnected = smbClient.Connect(_config.PhysicalServer, _config.Port);
            if (!isConnected)
                throw new StorageFacadeException(
                    $"Unable to connect to '{_config.LogicalServer}' on TCP {_config.Port}");

            NTStatus status = smbClient.Login(_config.Domain, _config.Username, _config.Password, AuthenticationMethod.NTLMv2);
            if (status != NTStatus.STATUS_SUCCESS)
                throw new StorageFacadeException(
                    $"Unable to authenticate as '{_config.Username}' in domain '{_config.Domain}'");

            return smbClient;
        }
    }
}

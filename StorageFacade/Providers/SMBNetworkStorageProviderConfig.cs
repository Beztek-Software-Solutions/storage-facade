// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    /// <summary>
    /// Configuration for the SMB network share provider, including optional manual DFS mapping
    /// via a physical server name when the logical (DFS) server differs.
    /// </summary>
    public class SMBNetworkStorageProviderConfig : IStorageProviderConfig
    {
        /// <inheritdoc/>
        public StorageFacadeType StorageFacadeType { get; } = StorageFacadeType.SMBNetworkStore;

        /// <inheritdoc/>
        public string Name { get; }

        internal string PhysicalServer { get; }
        internal string LogicalServer { get; }
        internal string ShareName { get; }
        internal string Domain { get; }
        internal string Username { get; }
        internal string Password { get; }
        internal int SmbIdleTimeoutSeconds { get; }

        /// <summary>
        /// Creates an SMB share configuration. Supports a manual DFS workaround: although SMBLibrary
        /// does not resolve DFS automatically, you can map a logical DFS server name to a physical
        /// server. For example, if DFS path <c>\\server1\path1\path2</c> maps to
        /// <c>\\physical-server\path2</c>, set <paramref name="logicalServer"/> to <c>server1</c>,
        /// <paramref name="shareName"/> to <c>path2</c>, and <paramref name="physicalServer"/> to
        /// the physical host. The mapping must remain stable for the lifetime of this instance.
        /// </summary>
        /// <param name="logicalServer">Logical SMB or DFS server name.</param>
        /// <param name="shareName">Share name on the server.</param>
        /// <param name="domain">Authentication domain.</param>
        /// <param name="username">Authentication username.</param>
        /// <param name="password">Authentication password.</param>
        /// <param name="physicalServer">Optional physical host for DFS mapping; defaults to <paramref name="logicalServer"/>.</param>
        /// <param name="smbIdleTimeoutSeconds">Idle seconds before the SMB client is refreshed (default 899).</param>
        public SMBNetworkStorageProviderConfig(string logicalServer, string shareName, string domain, string username, string password, string physicalServer = null, int smbIdleTimeoutSeconds = 899)
        {
            this.LogicalServer = logicalServer.ToLower();
            this.PhysicalServer = physicalServer == null ? logicalServer : physicalServer.ToLower();
            this.ShareName = shareName;
            this.Name = @$"\\{LogicalServer}\{ShareName.ToLower()}";
            this.Domain = domain.ToLower();
            this.Username = username;
            this.Password = password;
            this.SmbIdleTimeoutSeconds = smbIdleTimeoutSeconds;
        }
    }
}

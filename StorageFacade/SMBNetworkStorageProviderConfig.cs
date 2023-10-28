// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    public class SMBNetworkStorageProviderConfig : IStorageProviderConfig
    {
        public StorageFacadeType StorageFacadeType { get; } = StorageFacadeType.SMBNetworkStore;
        public string Name { get; }
        internal string PhysicalServer { get; }
        internal string LogicalServer { get; }
        internal string ShareName { get; }
        internal string Domain { get; }
        internal string Username { get; }
        internal string Password { get; }

        /// <summary>
        /// Constructor for SMB Network storage. This has a "poor-man's" implementation for DFS. Although the underlying SMBLibrary does not provide
        /// DFS Support, this object can configure an SMB Share with an optional physical server that maps the DFS name. This mapping is handled transparentyly
        /// by the library. For example, if the DFS name is \\<server1>\<path1>/<path2> which is the DFS share for \\<physical server 1>\<path2>, if the DFS name
        /// \\<server1>\<path1> is mapped to the physical server \\<physical server 1> (where <path2> is the share name), this library will correctly resolve
        /// all the DFS files transparently. So while this library cannot automatically resolve DFS names, this provides a work-around, as long as the destination
        /// physical server does not change while this instance is up.
        /// </summary>
        /// <param name="name ">is the name of the facade</param>
        /// <param name="logicalServer"> is the SMB server of VFS server name</param>
        /// <param name="shareName">is the name of the share</param>
        /// <param name="domain">is the domain for the logical server</param>
        /// <param name="username">is the username for auth</param>
        /// <param name="password">is the password for auth</param>
        /// <param name="physicalServer">provides the mapping behind DFS shares if relevant</param>
        public SMBNetworkStorageProviderConfig(string logicalServer, string shareName, string domain, string username, string password, string physicalServer = null)
        {
            this.LogicalServer = logicalServer.ToLower();
            this.PhysicalServer = physicalServer == null ? logicalServer : physicalServer.ToLower();
            this.ShareName = shareName;
            this.Name = @$"\\{LogicalServer}\{ShareName.ToLower()}";
            this.Domain = domain.ToLower();
            this.Username = username;
            this.Password = password;
        }
    }
}

// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    public class SMBNetworkStorageProviderConfig : IStorageProviderConfig
    {
        public StorageProviderType StorageProviderType { get; } = StorageProviderType.SMBNetworkStore;
        public string Name { get; }
        internal string Server { get; }
        internal string ShareName { get; }
        internal string Domain { get; }
        internal string Username { get; }
        internal string Password { get; }

        public SMBNetworkStorageProviderConfig(string name, string server, string shareName, string domain, string username, string password)
        {
            this.Name = name;
            this.Server = server;
            this.ShareName = shareName;
            this.Domain = domain;
            this.Username = username;
            this.Password = password;
        }
    }
}

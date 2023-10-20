// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    public class FileStorageProviderConfig : IStorageProviderConfig
    {
        public string Name { get; }

        public StorageProviderType StorageProviderType { get; } = StorageProviderType.LocalFileStore;

        public FileStorageProviderConfig(string name)
        {
            this.Name = name;
        }
    }
}

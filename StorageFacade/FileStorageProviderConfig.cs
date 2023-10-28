// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    public class FileStorageProviderConfig : IStorageProviderConfig
    {
        public string Name { get; }

        public StorageFacadeType StorageFacadeType { get; } = StorageFacadeType.LocalFileStore;

        public FileStorageProviderConfig()
        {
            this.Name = "Local Store";
        }
    }
}

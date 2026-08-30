// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    /// <summary>Configuration for the local filesystem provider.</summary>
    public class FileStorageProviderConfig : IStorageProviderConfig
    {
        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public StorageFacadeType StorageFacadeType { get; } = StorageFacadeType.LocalFileStore;

        /// <summary>Creates the default local store configuration (`Local Store`).</summary>
        public FileStorageProviderConfig()
        {
            this.Name = "Local Store";
        }
    }
}

// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    /// <summary>Configuration marker for a storage provider; passed to <see cref="StorageFacadeFactory.GetStorageFacade"/>.</summary>
    public interface IStorageProviderConfig
    {
        /// <summary>Logical name / URI prefix for the store.</summary>
        string Name { get; }

        /// <summary>Provider kind.</summary>
        StorageFacadeType StorageFacadeType { get; }
    }
}

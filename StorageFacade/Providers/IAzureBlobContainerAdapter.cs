// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs.Models;

    /// <summary>
    /// Testable abstraction over Azure Blob container operations used by <see cref="AzureBlobStorageProvider"/>.
    /// </summary>
    internal interface IAzureBlobContainerAdapter
    {
        IEnumerable<BlobHierarchyItem> GetBlobsByHierarchy(string prefix);

        IEnumerable<BlobItem> GetBlobs(string prefix);

        BlobProperties GetBlobProperties(string blobName);

        Task<bool> BlobExistsAsync(string blobName);

        Task<Stream> OpenReadAsync(string blobName);

        Task UploadAsync(string blobName, Stream inputStream);

        Task DeleteAsync(string blobName);
    }
}

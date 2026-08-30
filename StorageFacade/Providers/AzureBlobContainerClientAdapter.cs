// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;

    internal sealed class AzureBlobContainerClientAdapter : IAzureBlobContainerAdapter
    {
        private readonly BlobContainerClient _containerClient;

        internal AzureBlobContainerClientAdapter(BlobContainerClient containerClient)
        {
            _containerClient = containerClient;
        }

        public IEnumerable<BlobHierarchyItem> GetBlobsByHierarchy(string prefix)
        {
            return _containerClient.GetBlobsByHierarchy(BlobTraits.None, BlobStates.None, "/", prefix).AsEnumerable();
        }

        public IEnumerable<BlobItem> GetBlobs(string prefix)
        {
            return _containerClient.GetBlobs(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None).AsEnumerable();
        }

        public BlobProperties GetBlobProperties(string blobName)
        {
            return _containerClient.GetBlobClient(blobName).GetProperties();
        }

        public async Task<bool> BlobExistsAsync(string blobName)
        {
            return await _containerClient.GetBlobClient(blobName).ExistsAsync();
        }

        public async Task<Stream> OpenReadAsync(string blobName)
        {
            Azure.Response<BlobDownloadStreamingResult> response = await _containerClient.GetBlobClient(blobName).DownloadStreamingAsync();
            return response.Value.Content;
        }

        public Task UploadAsync(string blobName, Stream inputStream)
        {
            return _containerClient.GetBlobClient(blobName).UploadAsync(inputStream, overwrite: true);
        }

        public Task DeleteAsync(string blobName)
        {
            return _containerClient.GetBlobClient(blobName).DeleteAsync();
        }
    }
}

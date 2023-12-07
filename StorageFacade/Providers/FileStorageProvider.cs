// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using SMBLibrary.Services;

    /// <summary>
    /// Implements the storage provider for the local filesystem
    /// </summary>
    internal class FileStorageProvider : IStorageProvider
    {
        private FileStorageProviderConfig localFileStorageProviderConfig;

        internal FileStorageProvider(FileStorageProviderConfig fileStorageProviderConfig)
        {
            this.localFileStorageProviderConfig = fileStorageProviderConfig;
        }

        public string GetName()
        {
            return localFileStorageProviderConfig.Name;
        }

        public new StorageFacadeType GetType()
        {
            return localFileStorageProviderConfig.StorageFacadeType;
        }

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string logicalPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            IEnumerable<FileInfo> fileInfoEnumerable = new DirectoryInfo(logicalPath).EnumerateFiles("*");

            foreach (FileInfo fileInfo in fileInfoEnumerable)
            {
                StorageInfo storageInfo = GetStorageInfo(fileInfo);
                if (StorageFilter.IsMatch(storageFilter, storageInfo))
                    yield return storageInfo;
            }
            if (isRecursive)
            {
                foreach (string subDirectory in Directory.GetDirectories(logicalPath))
                {
                    // Do not recurse symbolic links
                    if (new DirectoryInfo(logicalPath).LinkTarget == null)
                    {
                        foreach (StorageInfo storageInfo in EnumerateStorageInfo(subDirectory, true, storageFilter))
                        {
                            yield return storageInfo;
                        }
                    }
                }
            }
            yield break;
        }

        public StorageInfo GetStorageInfo(string logicalPath)
        {
            return GetStorageInfo(new FileInfo(logicalPath));
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            FileInfo fileInfo = new FileInfo(storageInfo.LogicalPath);
            return await Task.FromResult(File.OpenRead(storageInfo.LogicalPath)).ConfigureAwait(false);
        }

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories = false)
        {
            // Create the parent directory hierarchy if it does not exist
            if (createParentDirectories)
            {
                string parent = Directory.GetParent(logicalPath).FullName;
                Directory.CreateDirectory(parent);
            }

            using (FileStream fileStream = File.Create(logicalPath))
            {
                inputStream.Seek(0, SeekOrigin.Begin);
                await inputStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }
        }

        public async Task DeleteStorageAsync(string logicalPath)
        {
            await Task.Run(() =>
            {
                File.Delete(logicalPath);
            });
        }

        // Internal

        private StorageInfo GetStorageInfo(FileInfo fileInfo)
        {
            StorageInfo currStorageInfo = new StorageInfo();
            currStorageInfo.IsFile = true;
            currStorageInfo.Name = fileInfo.Name;
            currStorageInfo.LogicalPath = fileInfo.FullName;
            currStorageInfo.Timestamp = fileInfo.LastWriteTime;
            currStorageInfo.SizeBytes = fileInfo.Length;

            return currStorageInfo;
        }
    }
}

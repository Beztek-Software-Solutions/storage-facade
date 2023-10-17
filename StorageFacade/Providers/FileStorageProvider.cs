// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements the storage provider for the local filesystem
    /// </summary>
    public class FileStorageProvider : IStorageProvider
    {
        FileStorageProviderConfig LocalFileStorageProviderConfig { get; }

        public FileStorageProvider(FileStorageProviderConfig fileStorageProviderConfig)
        {
            this.LocalFileStorageProviderConfig = fileStorageProviderConfig;
        }

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string rootPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            IEnumerable<FileInfo> fileInfoEnumerable = new DirectoryInfo(rootPath).EnumerateFiles("*");

            foreach (FileInfo fileInfo in fileInfoEnumerable)
            {
                StorageInfo storageInfo = GetStorageInfo(fileInfo);
                if (StorageFilter.IsMatch(storageFilter, storageInfo))
                    yield return storageInfo;
            }
            if (isRecursive)
            {
                foreach (string subDirectory in Directory.GetDirectories(rootPath))
                {
                    // Do not recurse symbolic links
                    if (new DirectoryInfo(rootPath).LinkTarget == null)
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

        public StorageInfo GetStorageInfo(string storagePath)
        {
            return GetStorageInfo(new FileInfo(storagePath));
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            FileInfo fileInfo = new FileInfo(storageInfo.Path);
            return await Task.FromResult(File.OpenRead(storageInfo.Path)).ConfigureAwait(false);
        }

        public async Task WriteStorageAsync(string storagePath, Stream inputStream)
        {
            using (FileStream fileStream = File.Create(storagePath))
            {
                inputStream.Seek(0, SeekOrigin.Begin);
                await inputStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }
        }

        public async Task DeleteStorageAsync(string storagePath)
        {
            await Task.Run(() => {
                File.Delete(storagePath);
            });
        }

        // Internal

        private StorageInfo GetStorageInfo(FileInfo fileInfo)
        {
            StorageInfo currStorageInfo = new StorageInfo();
            currStorageInfo.IsFile = true;
            currStorageInfo.Name = fileInfo.Name;
            currStorageInfo.Path = fileInfo.FullName;
            currStorageInfo.Timestamp = fileInfo.LastWriteTime;
            currStorageInfo.SizeBytes = fileInfo.Length;

            return currStorageInfo;
        }
    }
}

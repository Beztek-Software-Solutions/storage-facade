// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Moq;

    /// <summary>
    /// Builds Moq <see cref="IStorageProvider"/> instances that simulate remote stores (S3, Azure, SMB, GCS, OSS)
    /// with in-memory backing so tests do not require cloud credentials or network shares.
    /// </summary>
    internal static class MockStorageProviderBuilder
    {
        internal static (Mock<IStorageProvider> Mock, Dictionary<string, byte[]> Store) Create(
            string name,
            StorageFacadeType facadeType)
        {
            var store = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var mock = new Mock<IStorageProvider>();

            mock.Setup(p => p.GetName()).Returns(name);
            mock.Setup(p => p.GetType()).Returns(facadeType);

            mock.Setup(p => p.EnumerateStorageInfo(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<StorageFilter>()))
                .Returns((string rootPath, bool isRecursive, StorageFilter filter) =>
                    Enumerate(store, name, rootPath, isRecursive, filter));

            mock.Setup(p => p.GetStorageInfo(It.IsAny<string>()))
                .Returns((string storagePath) => GetStorageInfo(store, storagePath));

            mock.Setup(p => p.ReadStorageAsync(It.IsAny<StorageInfo>()))
                .Returns((StorageInfo info) => ReadStorageAsync(store, info));

            mock.Setup(p => p.WriteStorageAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                .Returns((string storagePath, Stream inputStream, bool createParentDirectories) =>
                    WriteStorageAsync(store, storagePath, inputStream));

            mock.Setup(p => p.DeleteStorageAsync(It.IsAny<string>()))
                .Returns((string storagePath) => DeleteStorageAsync(store, storagePath));

            mock.Setup(p => p.ComputeMD5Checksum(It.IsAny<string>()))
                .Returns((string storagePath) => ComputeMd5Checksum(store, storagePath));

            return (mock, store);
        }

        internal static IStorageFacade CreateFacade(string name, StorageFacadeType facadeType)
        {
            var (mock, _) = Create(name, facadeType);
            return new StorageFacade(mock.Object);
        }

        internal static IStorageFacade CreateS3Facade(string bucket = "test-bucket") =>
            CreateFacade($"s3://{bucket}", StorageFacadeType.AmazonS3Store);

        internal static IStorageFacade CreateAzureFacade(string account = "acct", string container = "files") =>
            CreateFacade($"https://{account}.blob.core.windows.net/{container}", StorageFacadeType.AzureBlobStore);

        internal static IStorageFacade CreateSmbFacade(string server = "fileserver", string share = "data") =>
            CreateFacade($@"\\{server}\{share}", StorageFacadeType.SMBNetworkStore);

        internal static IStorageFacade CreateGcsFacade(string bucket = "media") =>
            CreateFacade($"gs://{bucket}", StorageFacadeType.GoogleCloudStorageStore);

        internal static IStorageFacade CreateOssFacade(string bucket = "archive") =>
            CreateFacade($"oss://{bucket}", StorageFacadeType.AlibabaOssStore);

        private static IEnumerable<StorageInfo> Enumerate(
            Dictionary<string, byte[]> store,
            string providerName,
            string rootPath,
            bool isRecursive,
            StorageFilter filter)
        {
            string prefix = NormalizeRoot(rootPath, providerName);
            foreach (string key in store.Keys.OrderBy(k => k))
            {
                if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string relative = key[prefix.Length..].TrimStart('/', '\\');
                if (!isRecursive && (relative.Contains('/') || relative.Contains('\\')))
                    continue;

                StorageInfo info = BuildStorageInfo(key, store[key]);
                if (StorageFilter.IsMatch(filter, info))
                    yield return info;
            }
        }

        private static StorageInfo GetStorageInfo(Dictionary<string, byte[]> store, string storagePath)
        {
            if (!store.TryGetValue(storagePath, out byte[] data))
                throw new StorageNotFoundException(storagePath);

            return BuildStorageInfo(storagePath, data);
        }

        private static Task<Stream> ReadStorageAsync(Dictionary<string, byte[]> store, StorageInfo info)
        {
            if (!store.TryGetValue(info.LogicalPath, out byte[] data))
                throw new StorageNotFoundException(info.LogicalPath);

            return Task.FromResult<Stream>(new MemoryStream(data, writable: false));
        }

        private static Task WriteStorageAsync(Dictionary<string, byte[]> store, string storagePath, Stream inputStream)
        {
            using var ms = new MemoryStream();
            inputStream.CopyTo(ms);
            store[storagePath] = ms.ToArray();
            return Task.CompletedTask;
        }

        private static Task DeleteStorageAsync(Dictionary<string, byte[]> store, string storagePath)
        {
            if (!store.Remove(storagePath))
                throw new StorageNotFoundException(storagePath);

            return Task.CompletedTask;
        }

        private static Task<string> ComputeMd5Checksum(Dictionary<string, byte[]> store, string storagePath)
        {
            if (!store.TryGetValue(storagePath, out byte[] data))
                throw new StorageNotFoundException(storagePath);

            using var md5 = MD5.Create();
            return Task.FromResult(Convert.ToBase64String(md5.ComputeHash(data)));
        }

        private static StorageInfo BuildStorageInfo(string logicalPath, byte[] data)
        {
            string fileName = logicalPath.Split('/', '\\').Last();
            return new StorageInfo
            {
                IsFile = true,
                Name = fileName,
                LogicalPath = logicalPath,
                Timestamp = DateTime.UtcNow,
                SizeBytes = data.Length
            };
        }

        private static string NormalizeRoot(string rootPath, string providerName)
        {
            if (!rootPath.StartsWith(providerName, StringComparison.OrdinalIgnoreCase))
                return rootPath.TrimEnd('/', '\\') + "/";

            string suffix = rootPath[providerName.Length..].TrimStart('/', '\\');
            return string.IsNullOrEmpty(suffix)
                ? providerName.TrimEnd('/', '\\') + "/"
                : rootPath.TrimEnd('/', '\\') + "/";
        }
    }
}

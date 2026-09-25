// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class ComboStorageFacadeTests
    {
        [Test]
        public void GetName_ReturnsComboProvider()
        {
            var combo = new ComboStorageFacade(new List<IStorageFacade>());
            Assert.That(combo.GetName(), Is.EqualTo("ComboProvider"));
        }

        [Test]
        public void GetType_ReturnsComboStore()
        {
            var combo = new ComboStorageFacade(new List<IStorageFacade>());
            Assert.That(combo.GetType(), Is.EqualTo(StorageFacadeType.ComboStore));
        }

        [Test]
        public async Task RoutesWriteToMatchingMockProvider_ByPathPrefix()
        {
            IStorageFacade s3 = MockStorageProviderBuilder.CreateS3Facade("orders");
            IStorageFacade azure = MockStorageProviderBuilder.CreateAzureFacade("acct", "archive");

            var combo = new ComboStorageFacade(new List<IStorageFacade> { s3, azure });

            string s3Path = "s3://orders/invoices/a.pdf";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("s3-data")))
                await combo.WriteStorageAsync(s3Path, stream, createParentDirectories: true);

            string azurePath = "https://acct.blob.core.windows.net/archive/backup/b.pdf";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("azure-data")))
                await combo.WriteStorageAsync(azurePath, stream, createParentDirectories: true);

            StorageInfo s3Info = combo.GetStorageInfo(s3Path);
            StorageInfo azureInfo = combo.GetStorageInfo(azurePath);

            using (var reader = new StreamReader(await combo.ReadStorageAsync(s3Info)))
                Assert.That(reader.ReadToEnd(), Is.EqualTo("s3-data"));

            using (var reader = new StreamReader(await combo.ReadStorageAsync(azureInfo)))
                Assert.That(reader.ReadToEnd(), Is.EqualTo("azure-data"));
        }

        [Test]
        public async Task RoutesToDefaultLocalFileStore_WhenNoPrefixMatches()
        {
            var mockS3 = new Mock<IStorageFacade>();
            mockS3.Setup(f => f.GetName()).Returns("s3://only-bucket");

            var combo = new ComboStorageFacade(new List<IStorageFacade> { mockS3.Object });

            string tempRoot = Path.Combine(Path.GetTempPath(), "storage-facade-combo-" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);
            string localPath = Path.Combine(tempRoot, "local.txt");

            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("local")))
                    await combo.WriteStorageAsync(localPath, stream, createParentDirectories: true);

                StorageInfo info = combo.GetStorageInfo(localPath);
                using var reader = new StreamReader(await combo.ReadStorageAsync(info));
                Assert.That(reader.ReadToEnd(), Is.EqualTo("local"));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public async Task RoutesWrite_WhenPathCasingDiffersFromRegisteredPrefix()
        {
            IStorageFacade s3 = MockStorageProviderBuilder.CreateS3Facade("orders");
            using var combo = new ComboStorageFacade(new List<IStorageFacade> { s3 });

            // Config Name is typically lowercased; callers may use mixed case.
            string path = "S3://ORDERS/invoices/mixed.pdf";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("mixed")))
                await combo.WriteStorageAsync(path, stream, createParentDirectories: true);

            StorageInfo info = combo.GetStorageInfo(path);
            using var reader = new StreamReader(await combo.ReadStorageAsync(info));
            Assert.That(reader.ReadToEnd(), Is.EqualTo("mixed"));
        }

        [Test]
        public async Task ComputeMD5Checksum_DelegatesToRoutedFacade()
        {
            var mockS3 = new Mock<IStorageFacade>();
            mockS3.Setup(f => f.GetName()).Returns("s3://bucket");
            mockS3.Setup(f => f.ComputeMD5Checksum("s3://bucket/a.txt")).ReturnsAsync("checksum");

            var combo = new ComboStorageFacade(new List<IStorageFacade> { mockS3.Object });
            Assert.That(await combo.ComputeMD5Checksum("s3://bucket/a.txt"), Is.EqualTo("checksum"));
        }

        [Test]
        public void EnumerateStorageInfo_DelegatesToRoutedFacade()
        {
            var mock = new Mock<IStorageFacade>();
            mock.Setup(f => f.GetName()).Returns("s3://bucket");
            var expected = new List<StorageInfo> { new() { LogicalPath = "s3://bucket/a.txt", Name = "a.txt", IsFile = true } };
            mock.Setup(f => f.EnumerateStorageInfo("s3://bucket/prefix", false, null)).Returns(expected);

            var combo = new ComboStorageFacade(new List<IStorageFacade> { mock.Object });
            var results = new List<StorageInfo>(combo.EnumerateStorageInfo("s3://bucket/prefix", false));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].LogicalPath, Is.EqualTo("s3://bucket/a.txt"));
        }
    }
}

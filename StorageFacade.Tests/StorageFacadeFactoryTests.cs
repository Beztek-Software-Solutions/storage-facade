// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class StorageFacadeFactoryTests
    {
        [Test]
        public void GetStorageFacade_LocalFileStore_ReturnsLocalFacade()
        {
            IStorageFacade facade = StorageFacadeFactory.GetStorageFacade(new FileStorageProviderConfig());

            Assert.That(facade, Is.Not.Null);
            Assert.That(facade.GetType(), Is.EqualTo(StorageFacadeType.LocalFileStore));
            Assert.That(facade.GetName(), Is.EqualTo("Local Store"));
        }

        [Test]
        public void GetStorageFacade_S3_ReturnsS3Facade()
        {
            var config = new AwsS3StorageProviderConfig("key", "secret", "us-east-1", "my-bucket");
            IStorageFacade facade = StorageFacadeFactory.GetStorageFacade(config);

            Assert.That(facade, Is.Not.Null);
            Assert.That(facade.GetType(), Is.EqualTo(StorageFacadeType.AmazonS3Store));
            Assert.That(facade.GetName(), Is.EqualTo("s3://my-bucket"));
        }

        [Test]
        public void GetStorageFacade_AzureAccountKey_ReturnsAzureFacade()
        {
            string accountKey = Convert.ToBase64String(new byte[32]);
            var config = new AzureBlobStorageProviderConfig("myaccount.blob.core.windows.net", accountKey, "container");
            IStorageFacade facade = StorageFacadeFactory.GetStorageFacade(config);

            Assert.That(facade, Is.Not.Null);
            Assert.That(facade.GetType(), Is.EqualTo(StorageFacadeType.AzureBlobStore));
            Assert.That(facade.GetName(), Is.EqualTo("https://myaccount.blob.core.windows.net/container"));
        }

        [Test]
        public void GetStorageFacade_AzureSasUri_ReturnsAzureFacade()
        {
            var uri = new Uri("https://myaccount.blob.core.windows.net/container?sv=2021-06-08&sig=abc");
            var config = new AzureBlobStorageProviderConfig(uri);
            IStorageFacade facade = StorageFacadeFactory.GetStorageFacade(config);

            Assert.That(facade, Is.Not.Null);
            Assert.That(facade.GetType(), Is.EqualTo(StorageFacadeType.AzureBlobStore));
            Assert.That(facade.GetName(), Is.EqualTo("https://myaccount.blob.core.windows.net/container"));
        }

        [Test]
        public void GetStorageFacade_Smb_ReturnsSmbFacade()
        {
            var config = new SMBNetworkStorageProviderConfig(
                logicalServer: "dfs-server",
                shareName: "Share",
                domain: "CORP",
                username: "user",
                password: "pass",
                physicalServer: "physical-server");

            IStorageFacade facade = StorageFacadeFactory.GetStorageFacade(config);

            Assert.That(facade, Is.Not.Null);
            Assert.That(facade.GetType(), Is.EqualTo(StorageFacadeType.SMBNetworkStore));
            Assert.That(facade.GetName(), Is.EqualTo(@"\\dfs-server\share"));
        }

        [Test]
        public void GetStorageFacade_GoogleCloud_ReturnsGcsFacade()
        {
            var config = new GoogleCloudStorageProviderConfig("My-Bucket", serviceUri: "http://127.0.0.1:4443/storage/v1/");
            using IDisposable disposable = (IDisposable)StorageFacadeFactory.GetStorageFacade(config);
            var facade = (IStorageFacade)disposable;

            Assert.That(facade.GetType(), Is.EqualTo(StorageFacadeType.GoogleCloudStorageStore));
            Assert.That(facade.GetName(), Is.EqualTo("gs://my-bucket"));
        }

        [Test]
        public void GetStorageFacade_AlibabaOss_WithExplicitKeys_ReturnsOssFacade()
        {
            var config = new AlibabaOssStorageProviderConfig(
                "oss-us-west-1.aliyuncs.com",
                "ak",
                "secret",
                "My-Bucket");
            using IDisposable disposable = (IDisposable)StorageFacadeFactory.GetStorageFacade(config);
            var facade = (IStorageFacade)disposable;

            Assert.That(facade.GetType(), Is.EqualTo(StorageFacadeType.AlibabaOssStore));
            Assert.That(facade.GetName(), Is.EqualTo("oss://my-bucket"));
        }

        [Test]
        public void StorageFacade_Dispose_IsIdempotent()
        {
            var facade = new StorageFacade(Mock.Of<IStorageProvider>());
            facade.Dispose();
            facade.Dispose();
        }
    }
}

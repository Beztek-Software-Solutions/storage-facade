// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class ProviderConfigurationTests
    {
        [Test]
        public void FileStorageProviderConfig_HasExpectedDefaults()
        {
            var config = new FileStorageProviderConfig();
            Assert.That(config.Name, Is.EqualTo("Local Store"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.LocalFileStore));
        }

        [Test]
        public void AwsS3StorageProviderConfig_NormalizesName()
        {
            var config = new AwsS3StorageProviderConfig("key", "secret", "us-west-2", "My-Bucket");
            Assert.That(config.Name, Is.EqualTo("s3://my-bucket"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.AmazonS3Store));
        }

        [Test]
        public void AzureBlobStorageProviderConfig_AccountKeyConstructor_ParsesAccountName()
        {
            var config = new AzureBlobStorageProviderConfig(
                "MyAccount.blob.core.windows.net",
                "key",
                "uploads",
                isHierarchicalNamespace: true);

            Assert.That(config.Name, Is.EqualTo("https://myaccount.blob.core.windows.net/uploads"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.AzureBlobStore));
        }

        [Test]
        public void AzureBlobStorageProviderConfig_SasUriConstructor_ParsesContainer()
        {
            var uri = new Uri("https://acct.blob.core.windows.net/data?sp=r&sig=x");
            var config = new AzureBlobStorageProviderConfig(uri, isHierarchicalNamespace: false);

            Assert.That(config.Name, Is.EqualTo("https://acct.blob.core.windows.net/data"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.AzureBlobStore));
        }

        [Test]
        public void SmbNetworkStorageProviderConfig_MapsDfsPhysicalServer()
        {
            var config = new SMBNetworkStorageProviderConfig(
                logicalServer: "DFS-HOST",
                shareName: "Docs",
                domain: "CORP",
                username: "svc",
                password: "secret",
                physicalServer: "PHYS-HOST",
                smbIdleTimeoutSeconds: 600);

            Assert.That(config.Name, Is.EqualTo(@"\\dfs-host\docs"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.SMBNetworkStore));
        }
    }
}

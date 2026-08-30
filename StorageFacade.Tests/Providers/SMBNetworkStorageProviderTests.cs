// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Beztek.Facade.Storage;
    using Beztek.Facade.Storage.Providers;
    using Moq;
    using NUnit.Framework;
    using SMBLibrary;
    using SMBLibrary.Client;
    using FileAttributes = SMBLibrary.FileAttributes;

    [TestFixture]
    public class SMBNetworkStorageProviderTests
    {
        private readonly SMBNetworkStorageProviderConfig _config = new(
            logicalServer: "fileserver",
            shareName: "Docs",
            domain: "corp",
            username: "user",
            password: "pass",
            physicalServer: "physical");

        [Test]
        public void GetName_ReturnsUncPath()
        {
            var provider = new SMBNetworkStorageProvider(_config, Mock.Of<ISmbClientFactory>());
            Assert.That(provider.GetName(), Is.EqualTo(@"\\fileserver\docs"));
        }

        [Test]
        public void EnumerateStorageInfo_ReturnsFilesFromMockedShare()
        {
            Mock<ISMBFileStore> fileStore = CreateFileStoreMock(out Mock<ISmbClientFactory> factory);
            var fileInfo = CreateDirectoryInformation("report.pdf", isDirectory: false, size: 128);

            fileStore.Setup(f => f.QueryDirectory(
                    out It.Ref<List<QueryDirectoryFileInformation>>.IsAny,
                    It.IsAny<object>(),
                    @"*",
                    FileInformationClass.FileDirectoryInformation))
                .Callback(new QueryDirectoryCallback((out List<QueryDirectoryFileInformation> list, object _, string __, FileInformationClass ___) =>
                {
                    list = new List<QueryDirectoryFileInformation> { fileInfo };
                }))
                .Returns(NTStatus.STATUS_SUCCESS);

            var provider = new SMBNetworkStorageProvider(_config, factory.Object);
            var results = new List<StorageInfo>(provider.EnumerateStorageInfo(@"\\fileserver\Docs\inbox", false));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Name, Is.EqualTo("report.pdf"));
        }

        [Test]
        public async Task WriteAndRead_RoundTripThroughMockedSmbFileStore()
        {
            Mock<ISMBFileStore> fileStore = CreateFileStoreMock(out Mock<ISmbClientFactory> factory);
            object fileHandle = new object();

            fileStore.Setup(f => f.CreateFile(
                    out It.Ref<object>.IsAny,
                    out It.Ref<FileStatus>.IsAny,
                    It.IsAny<string>(),
                    It.IsAny<AccessMask>(),
                    It.IsAny<FileAttributes>(),
                    It.IsAny<ShareAccess>(),
                    It.IsAny<CreateDisposition>(),
                    It.IsAny<CreateOptions>(),
                    It.IsAny<SecurityContext>()))
                .Callback(new CreateFileCallback((out object handle, out FileStatus status, string _, AccessMask __, FileAttributes ___, ShareAccess ____, CreateDisposition _____, CreateOptions ______, SecurityContext _______) =>
                {
                    handle = fileHandle;
                    status = FileStatus.FILE_OPENED;
                }))
                .Returns(NTStatus.STATUS_SUCCESS);

            fileStore.Setup(f => f.WriteFile(out It.Ref<int>.IsAny, fileHandle, It.IsAny<long>(), It.IsAny<byte[]>()))
                .Callback(new WriteFileCallback((out int writtenCount, object _, long offset, byte[] data) =>
                {
                    writtenCount = data.Length;
                }))
                .Returns(NTStatus.STATUS_SUCCESS);

            fileStore.Setup(f => f.ReadFile(out It.Ref<byte[]>.IsAny, fileHandle, It.IsAny<long>(), It.IsAny<int>()))
                .Returns(new ReadFileCallback((out byte[] data, object _, long offset, int __) =>
                {
                    if (offset > 0)
                    {
                        data = Array.Empty<byte>();
                        return NTStatus.STATUS_END_OF_FILE;
                    }

                    data = Encoding.UTF8.GetBytes("smb-data");
                    return NTStatus.STATUS_SUCCESS;
                }));

            var provider = new SMBNetworkStorageProvider(_config, factory.Object);
            string path = @"\\fileserver\Docs\out\payload.txt";

            using (var writeStream = new MemoryStream(Encoding.UTF8.GetBytes("smb-data")))
                await provider.WriteStorageAsync(path, writeStream);

            using var readStream = await provider.ReadStorageAsync(new StorageInfo { LogicalPath = path });
            using var reader = new StreamReader(readStream);
            Assert.That(reader.ReadToEnd(), Is.EqualTo("smb-data"));
        }

        [Test]
        public async Task DeleteStorageAsync_RemovesFileViaMockedShare()
        {
            Mock<ISMBFileStore> fileStore = CreateFileStoreMock(out Mock<ISmbClientFactory> factory);
            object fileHandle = new object();

            fileStore.Setup(f => f.CreateFile(
                    out It.Ref<object>.IsAny,
                    out It.Ref<FileStatus>.IsAny,
                    It.IsAny<string>(),
                    It.IsAny<AccessMask>(),
                    It.IsAny<FileAttributes>(),
                    It.IsAny<ShareAccess>(),
                    It.IsAny<CreateDisposition>(),
                    It.IsAny<CreateOptions>(),
                    It.IsAny<SecurityContext>()))
                .Callback(new CreateFileCallback((out object handle, out FileStatus status, string _, AccessMask __, FileAttributes ___, ShareAccess ____, CreateDisposition _____, CreateOptions ______, SecurityContext _______) =>
                {
                    handle = fileHandle;
                    status = FileStatus.FILE_OPENED;
                }))
                .Returns(NTStatus.STATUS_SUCCESS);

            fileStore.Setup(f => f.SetFileInformation(fileHandle, It.IsAny<FileInformation>())).Returns(NTStatus.STATUS_SUCCESS);

            var provider = new SMBNetworkStorageProvider(_config, factory.Object);
            await provider.DeleteStorageAsync(@"\\fileserver\Docs\obsolete.txt");

            fileStore.Verify(f => f.SetFileInformation(fileHandle, It.IsAny<FileInformation>()), Times.Once);
        }

        [Test]
        public void GetStorageInfo_ReturnsFileMetadata()
        {
            Mock<ISMBFileStore> fileStore = CreateFileStoreMock(out Mock<ISmbClientFactory> factory);
            var fileInfo = CreateDirectoryInformation("notes.txt", isDirectory: false, size: 64);

            fileStore.Setup(f => f.QueryDirectory(
                    out It.Ref<List<QueryDirectoryFileInformation>>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<string>(),
                    FileInformationClass.FileDirectoryInformation))
                .Callback(new QueryDirectoryCallback((out List<QueryDirectoryFileInformation> list, object _, string __, FileInformationClass ___) =>
                {
                    list = new List<QueryDirectoryFileInformation> { fileInfo };
                }))
                .Returns(NTStatus.STATUS_SUCCESS);

            var provider = new SMBNetworkStorageProvider(_config, factory.Object);
            StorageInfo info = provider.GetStorageInfo(@"\\fileserver\Docs\notes.txt");

            Assert.That(info.Name, Is.EqualTo("notes.txt"));
            Assert.That(info.SizeBytes, Is.EqualTo(64));
        }

        [Test]
        public void GetStorageInfo_ThrowsWhenDirectoryQueryReturnsEmpty()
        {
            Mock<ISMBFileStore> fileStore = CreateFileStoreMock(out Mock<ISmbClientFactory> factory);

            fileStore.Setup(f => f.QueryDirectory(
                    out It.Ref<List<QueryDirectoryFileInformation>>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<string>(),
                    FileInformationClass.FileDirectoryInformation))
                .Callback(new QueryDirectoryCallback((out List<QueryDirectoryFileInformation> list, object _, string __, FileInformationClass ___) =>
                {
                    list = new List<QueryDirectoryFileInformation>();
                }))
                .Returns(NTStatus.STATUS_SUCCESS);

            var provider = new SMBNetworkStorageProvider(_config, factory.Object);
            Assert.Throws<Exception>(() => provider.GetStorageInfo(@"\\fileserver\Docs\missing.txt"));
        }

        private static Mock<ISMBFileStore> CreateFileStoreMock(out Mock<ISmbClientFactory> factory)
        {
            var smbClient = new Mock<ISMBClient>();
            var fileStore = new Mock<ISMBFileStore>();
            factory = new Mock<ISmbClientFactory>();

            factory.Setup(f => f.CreateConnectedClient()).Returns(smbClient.Object);
            smbClient.Setup(c => c.TreeConnect("Docs", out It.Ref<NTStatus>.IsAny))
                .Callback(new TreeConnectCallback((string _, out NTStatus status) => status = NTStatus.STATUS_SUCCESS))
                .Returns(fileStore.Object);

            object directoryHandle = new object();
            fileStore.Setup(f => f.CreateFile(
                    out It.Ref<object>.IsAny,
                    out It.Ref<FileStatus>.IsAny,
                    It.IsAny<string>(),
                    It.IsAny<AccessMask>(),
                    It.IsAny<FileAttributes>(),
                    It.IsAny<ShareAccess>(),
                    It.IsAny<CreateDisposition>(),
                    It.IsAny<CreateOptions>(),
                    It.IsAny<SecurityContext>()))
                .Callback(new CreateFileCallback((out object handle, out FileStatus status, string _, AccessMask __, FileAttributes ___, ShareAccess ____, CreateDisposition _____, CreateOptions ______, SecurityContext _______) =>
                {
                    handle = directoryHandle;
                    status = FileStatus.FILE_OPENED;
                }))
                .Returns(NTStatus.STATUS_SUCCESS);

            fileStore.Setup(f => f.CloseFile(It.IsAny<object>())).Returns(NTStatus.STATUS_SUCCESS);
            fileStore.Setup(f => f.Disconnect());
            smbClient.Setup(c => c.Disconnect());

            return fileStore;
        }

        private static FileDirectoryInformation CreateDirectoryInformation(string name, bool isDirectory, long size)
        {
            return new FileDirectoryInformation
            {
                FileName = name,
                FileAttributes = isDirectory ? FileAttributes.Directory : FileAttributes.Normal,
                AllocationSize = size,
                CreationTime = DateTime.UtcNow,
                LastWriteTime = DateTime.UtcNow
            };
        }

        private delegate void QueryDirectoryCallback(out List<QueryDirectoryFileInformation> list, object handle, string searchPattern, FileInformationClass informationClass);

        private delegate void CreateFileCallback(out object handle, out FileStatus fileStatus, string path, AccessMask access, FileAttributes attributes, ShareAccess shareAccess, CreateDisposition disposition, CreateOptions options, SecurityContext security);

        private delegate NTStatus ReadFileCallback(out byte[] data, object handle, long offset, int maxCount);

        private delegate void WriteFileCallback(out int numberOfBytesWritten, object handle, long offset, byte[] data);

        private delegate void TreeConnectCallback(string shareName, out NTStatus status);
    }
}

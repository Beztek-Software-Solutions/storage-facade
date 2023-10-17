// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using SMBLibrary;
    using SMBLibrary.Client;

    /// <summary>
    /// Implements the storage provider for the local filesystem
    /// </summary>
    public class SMBNetworkStorageProvider : IStorageProvider
    {
        IStorageProviderConfig StorageProviderConfig { get; }

        private SMB2Client smbClient;
        private Dictionary<string, ISMBFileStore> fileStoreCache = new Dictionary<string, ISMBFileStore>();

        public SMBNetworkStorageProvider(SMBNetworkStorageProviderConfig smbNetworkStorageProviderConfig)
        {
            this.StorageProviderConfig = smbNetworkStorageProviderConfig;

            SMBNetworkStorageProviderConfig config = (SMBNetworkStorageProviderConfig)StorageProviderConfig;

            this.smbClient = new SMB2Client();
            bool isConnected = smbClient.Connect(config.Server, SMBTransportType.DirectTCPTransport);
            if (!isConnected)
                throw new Exception($"Unable to connect to '{config.Server}'");

            NTStatus status = smbClient.Login(config.Domain, config.Username, config.Password, AuthenticationMethod.NTLMv2);
            if (status != NTStatus.STATUS_SUCCESS)
                throw new Exception($"Unable to authenticate as '{config.Username}' in domain '{config.Domain}'");
        }

        public List<string> GetShares()
        {
            return smbClient.ListShares(out NTStatus status);
        }

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string rootPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            SMBNetworkStorageProviderConfig config = (SMBNetworkStorageProviderConfig)StorageProviderConfig;
            ISMBFileStore fileStore = GetFileStore(@$"{config.ShareName}");

            NTStatus status = fileStore.CreateFile(
                out object directoryHandle,
                out FileStatus fileStatus,
                @$"{rootPath}",
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
                throw new Exception($"Unable to get the directory handle {rootPath} - {status}");

            try
            {
                fileStore.QueryDirectory(out List<QueryDirectoryFileInformation> fileList, directoryHandle, @"*", FileInformationClass.FileDirectoryInformation);

                foreach (FileDirectoryInformation fileInfo in fileList)
                {
                    StorageInfo storageInfo = GetStorageInfo(config.Server, config.ShareName, rootPath, fileInfo);
                    if (storageInfo.IsFile)
                    {
                        if (StorageFilter.IsMatch(storageFilter, storageInfo))
                            yield return storageInfo;
                    }
                    else if (isRecursive && (!".".Equals(storageInfo.Name)) && (!"..".Equals(storageInfo.Name)))
                    {
                        string subPath = rootPath.Equals("") ? @$"{storageInfo.Name}" : @$"{rootPath}/{storageInfo.Name}";
                        foreach (StorageInfo subStorageInfo in EnumerateStorageInfo(subPath, true, storageFilter))
                        {
                            yield return subStorageInfo;
                        }
                    }
                }
                yield break;
            }
            finally
            {
                fileStore.CloseFile(directoryHandle);
            }
        }

        public StorageInfo GetStorageInfo(string storagePath)
        {
            SMBNetworkStorageProviderConfig config = (SMBNetworkStorageProviderConfig)StorageProviderConfig;
            List<QueryDirectoryFileInformation> fileList;

            ISMBFileStore fileStore = GetFileStore(@$"{config.ShareName}");

            string filePath = storagePath.Substring(@$"\\{config.Server}\{config.ShareName}\".Length);
            string[] paths = filePath.Split(@"\");
            string fileName = paths[paths.Length - 1];
            string sharePath = filePath.Substring(0, filePath.Length - fileName.Length);

            NTStatus status = fileStore.CreateFile(
                out object directoryHandle,
                out FileStatus fileStatus,
                @$"{sharePath}",
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
                throw new Exception($"Unable to get the directory handle {sharePath} - {status}");

            try
            {
                fileStore.QueryDirectory(out fileList, directoryHandle, @$"{fileName}", FileInformationClass.FileDirectoryInformation);
                return GetStorageInfo(config.Server, config.ShareName, sharePath, (FileDirectoryInformation)fileList[0]);
            }
            finally
            {
                fileStore.CloseFile(directoryHandle);
            }
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            SMBNetworkStorageProviderConfig config = (SMBNetworkStorageProviderConfig)StorageProviderConfig;
            ISMBFileStore fileStore = GetFileStore(@$"{config.ShareName}");

            string filePath = storageInfo.Path.Substring(@$"\\{config.Server}\{config.ShareName}\".Length);
            string[] paths = filePath.Split(@"\");
            string fileName = paths[paths.Length - 1];
            string sharePath = filePath.Substring(0, filePath.Length - fileName.Length);

            object fileHandle = null;
            try
            {
                // Open the file
                fileStore.CreateFile(
                    out fileHandle,
                    out FileStatus fileStatus,
                    filePath,
                    AccessMask.GENERIC_READ,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE,
                    null
                );

                if (fileStatus != FileStatus.FILE_OPENED)
                    throw new Exception($"Failed to open file: {fileName} under {sharePath}: {fileStatus}");

                // Read file into MemoryStream
                MemoryStream ms = new MemoryStream();
                long offset = 0;
                while (true)
                {
                    fileStore.ReadFile(out var bytesRead, fileHandle, offset, 4096);
                    if (bytesRead == null || bytesRead.Length == 0)
                        break;

                    ms.Write(bytesRead);
                    offset += bytesRead.Length;
                }

                // Reset stream position to start
                ms.Seek(0, SeekOrigin.Begin);

                return await Task.FromResult(ms).ConfigureAwait(false);
            }
            finally
            {
                // Close the file handle
                if (fileHandle != null)
                    fileStore.CloseFile(fileHandle);
            }
        }

        public async Task WriteStorageAsync(string storagePath, Stream inputStream)
        {
            await Task.Run(() => {
                string[] paths = storagePath.Split(@"\");
                string shareName = paths[3];
                string relativePath = storagePath.Substring(paths[2].Length + paths[3].Length + 4);
                ISMBFileStore fileStore = GetFileStore(@$"{shareName}");
                try
                {
                    NTStatus status = fileStore.CreateFile(
                        out object fileHandle,
                        out FileStatus fileStatus,
                        relativePath,
                        AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                        SMBLibrary.FileAttributes.Normal,
                        ShareAccess.None,
                        CreateDisposition.FILE_OVERWRITE_IF,
                        CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                        null);
                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        byte[] buffer = new byte[8 * 1024];
                        int len;
                        while ((len = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            byte[] data = new byte[len];
                            Array.Copy(buffer, 0, data, 0, len);
                            fileStore.WriteFile(out int numberOfBytesWritten, fileHandle, 0, data);
                            if (status != NTStatus.STATUS_SUCCESS || numberOfBytesWritten != len)
                            {
                                throw new Exception($"Failed to write to file {relativePath} at share {shareName}");
                            }
                        }
                        status = fileStore.CloseFile(fileHandle);
                    }
                    else
                    {
                        throw new Exception($"Unable to get file handle to {storagePath} - {status}");
                    }
                }
                finally
                {
                    fileStore.Disconnect();
                }
            });
        }

        public async Task DeleteStorageAsync(string storagePath)
        {
            await Task.Run(() => {
                string[] paths = storagePath.Split(@"\");
                string shareName = paths[3];
                string relativePath = storagePath.Substring(paths[2].Length + paths[3].Length + 4);
                ISMBFileStore fileStore = GetFileStore(@$"{shareName}");
                object fileHandle = null;
                try
                {
                    NTStatus status = fileStore.CreateFile(
                        out fileHandle,
                        out FileStatus fileStatus,
                        storagePath,
                        AccessMask.GENERIC_WRITE | AccessMask.DELETE,
                        0,
                        ShareAccess.None,
                        CreateDisposition.FILE_OPEN,
                        CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_DELETE_ON_CLOSE,
                        null);

                    if (status == NTStatus.STATUS_SUCCESS)
                        throw new Exception($"Unable to delete {storagePath} - {status}");
                }
                finally
                {
                    if (fileHandle != null)
                        fileStore.CloseFile(fileHandle);

                    fileStore.Disconnect();
                }
            });
        }

        // Internal

        private ISMBFileStore GetFileStore(string shareName)
        {
            fileStoreCache.TryGetValue(shareName, out ISMBFileStore fileStore);
            if (fileStore == null)
            {
                SMBNetworkStorageProviderConfig config = (SMBNetworkStorageProviderConfig)StorageProviderConfig;
                NTStatus status;
                fileStore = smbClient.TreeConnect(@$"{config.ShareName}", out status);
                if (status != NTStatus.STATUS_SUCCESS)
                    throw new Exception($"Unable to load the file share '{shareName}': {status}");

                fileStoreCache.Add(shareName, fileStore);
            }
            return fileStore;
        }

        private StorageInfo GetStorageInfo(string server, string shareName, string rootPath, FileDirectoryInformation fileInfo)
        {
            StorageInfo currStorageInfo = new StorageInfo();
            currStorageInfo.IsFile = (fileInfo.FileAttributes & SMBLibrary.FileAttributes.Directory) != SMBLibrary.FileAttributes.Directory;
            currStorageInfo.Name = fileInfo.FileName;
            string parentFolderWindows = rootPath.Replace(@"/", @"\");
            if ("".Equals(parentFolderWindows))
            {
                currStorageInfo.Path = @$"\\{server}\{shareName}\{fileInfo.FileName}";
            }
            else
            {
                currStorageInfo.Path = @$"\\{server}\{shareName}\{parentFolderWindows}\{fileInfo.FileName}";
            }
            currStorageInfo.Timestamp = fileInfo.LastWriteTime;
            currStorageInfo.SizeBytes = fileInfo.Length;

            return currStorageInfo;
        }
    }
}

// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using SMBLibrary;
    using SMBLibrary.Client;
    using FileAttributes = SMBLibrary.FileAttributes;

    /// <summary>
    /// Implements the storage provider for the local filesystem
    /// </summary>
    internal class SMBNetworkStorageProvider : IStorageProvider
    {
        private SMBNetworkStorageProviderConfig storageProviderConfig;
        private Dictionary<string, ISMBFileStore> fileStoreCache = new Dictionary<string, ISMBFileStore>();
        private DateTime loggedInTime = DateTime.Now;

        internal SMBNetworkStorageProvider(SMBNetworkStorageProviderConfig smbNetworkStorageProviderConfig)
        {
            this.storageProviderConfig = smbNetworkStorageProviderConfig;

            SMBNetworkStorageProviderConfig config = (SMBNetworkStorageProviderConfig)storageProviderConfig;
        }

        public string GetName()
        {
            return storageProviderConfig.Name;
        }

        public new StorageFacadeType GetType()
        {
            return storageProviderConfig.StorageFacadeType;
        }

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string logicalPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            ISMBFileStore fileStore = GetFileStore(@$"{storageProviderConfig.ShareName}");
            string relativePath = GetRelativePath(logicalPath);
            object directoryHandle = null;

            try
            {
                NTStatus status = fileStore.CreateFile(
                    out directoryHandle,
                    out FileStatus fileStatus,
                    @$"{relativePath}",
                    AccessMask.GENERIC_READ,
                    SMBLibrary.FileAttributes.Directory,
                    ShareAccess.Read | ShareAccess.Write,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_DIRECTORY_FILE,
                    null);

                if (status != NTStatus.STATUS_SUCCESS)
                    throw new Exception($"Unable to get the directory handle {logicalPath} - {status} (tried {GetPhysicalPath(logicalPath)})");

                fileStore.QueryDirectory(out List<QueryDirectoryFileInformation> fileList, directoryHandle, @"*", FileInformationClass.FileDirectoryInformation);

                foreach (FileDirectoryInformation fileInfo in fileList)
                {
                    StorageInfo storageInfo = GetStorageInfo(relativePath, fileInfo);
                    if (storageInfo.IsFile)
                    {
                        if (StorageFilter.IsMatch(storageFilter, storageInfo))
                        {
                            yield return storageInfo;
                        }
                    }
                    else if (isRecursive && (!@".".Equals(storageInfo.Name)) && (!@"..".Equals(storageInfo.Name)))
                    {
                        foreach (StorageInfo subStorageInfo in EnumerateStorageInfo(storageInfo.LogicalPath, true, storageFilter))
                        {
                            yield return subStorageInfo;
                        }
                    }
                }
                yield break;
            }
            finally
            {
                // Close the directory handle
                if (directoryHandle != null)
                    fileStore.CloseFile(directoryHandle);
            }
        }

        public StorageInfo GetStorageInfo(string logicalPath)
        {
            List<QueryDirectoryFileInformation> fileList;

            ISMBFileStore fileStore = GetFileStore(@$"{storageProviderConfig.ShareName}");

            string relativePath = GetRelativePath(logicalPath);
            string fileName = getFileName(logicalPath);
            string relativeParentPath = relativePath.Substring(0, relativePath.Length - fileName.Length);
            object directoryHandle = null;

            try
            {
                NTStatus status = fileStore.CreateFile(
                    out directoryHandle,
                    out FileStatus fileStatus,
                    @$"{relativeParentPath}",
                    AccessMask.GENERIC_READ,
                    SMBLibrary.FileAttributes.Directory,
                    ShareAccess.Read | ShareAccess.Write,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_DIRECTORY_FILE,
                    null);

                if (status != NTStatus.STATUS_SUCCESS)
                    throw new Exception($"Unable to get the directory handle {relativeParentPath} - {status}");

                fileStore.QueryDirectory(out fileList, directoryHandle, @$"{fileName}", FileInformationClass.FileDirectoryInformation);
                if (fileList == null || fileList.Count == 0)
                    throw new Exception($"Unable to get the path to {relativeParentPath} - {status}");

                return GetStorageInfo(relativeParentPath, (FileDirectoryInformation)fileList[0]);
            }
            finally
            {
                // Close the directory handle
                if (directoryHandle != null)
                    fileStore.CloseFile(directoryHandle);
            }
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            ISMBFileStore fileStore = GetFileStore(@$"{storageProviderConfig.ShareName}");

            string relativePath = GetRelativePath(storageInfo.LogicalPath);
            string fileName = getFileName(storageInfo.LogicalPath);
            string relativeParentPath = relativePath.Substring(0, relativePath.Length - fileName.Length);
            object fileHandle = null;

            try
            {
                // Open the file
                fileStore.CreateFile(
                    out fileHandle,
                    out FileStatus fileStatus,
                    relativePath,
                    AccessMask.GENERIC_READ,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE,
                    null
                );

                if (fileStatus != FileStatus.FILE_OPENED)
                    throw new Exception($"Failed to open file: {fileName} under {relativeParentPath}: {fileStatus}");

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

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream)
        {
            string relativePath = GetRelativePath(logicalPath);
            ISMBFileStore fileStore = GetFileStore(@$"{storageProviderConfig.ShareName}");
            object fileHandle = null;

            try
            {
                await Task.Run(() =>
                {
                    NTStatus status = fileStore.CreateFile(
                           out fileHandle,
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
                        byte[] buffer = new byte[64 * 1024];
                        int len;
                        int writeOffset = 0;
                        while ((len = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (len < buffer.Length)
                            {
                                Array.Resize<byte>(ref buffer, len);
                            }
                            status = fileStore.WriteFile(out int numberOfBytesWritten, fileHandle, writeOffset, buffer);

                            if (status != NTStatus.STATUS_SUCCESS || numberOfBytesWritten != len)
                                throw new Exception($"Failed to write to file {relativePath} at share {storageProviderConfig.ShareName}");

                            writeOffset += len;
                        }
                        status = fileStore.CloseFile(fileHandle);
                    }
                    else
                    {
                        throw new Exception($"Unable to get file handle to {logicalPath} - {status}");
                    }
                });
            }
            finally
            {
                // Close the file handle
                if (fileHandle != null)
                    fileStore.CloseFile(fileHandle);
            }
        }

        public async Task DeleteStorageAsync(string logicalPath)
        {
            string relativePath = GetRelativePath(logicalPath);
            ISMBFileStore fileStore = GetFileStore(@$"{storageProviderConfig.ShareName}");
            object fileHandle = null;

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        NTStatus status = fileStore.CreateFile(
                            out fileHandle,
                            out FileStatus fileStatus,
                            relativePath,
                            AccessMask.GENERIC_WRITE | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                            FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_OPEN,
                            CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                            null);

                        if (status != NTStatus.STATUS_SUCCESS)
                            throw new Exception($"Unable to delete {logicalPath} - {status}");

                        FileDispositionInformation fileDispositionInformation = new FileDispositionInformation();
                        fileDispositionInformation.DeletePending = true;
                        status = fileStore.SetFileInformation(fileHandle, fileDispositionInformation);

                        if (status != NTStatus.STATUS_SUCCESS)
                            throw new Exception($"Unable to delete {logicalPath} - {status}");
                    }
                    finally
                    {
                        if (fileHandle != null)
                            fileStore.CloseFile(fileHandle);
                    }
                });
            }
            finally
            {
                // Close the file handle
                if (fileHandle != null)
                    fileStore.CloseFile(fileHandle);
            }
        }

        // Internal

        private StorageInfo GetStorageInfo(string relativeParentPath, FileDirectoryInformation fileInfo)
        {
            StorageInfo currStorageInfo = new StorageInfo();
            currStorageInfo.IsFile = (fileInfo.FileAttributes & SMBLibrary.FileAttributes.Directory) != SMBLibrary.FileAttributes.Directory;
            currStorageInfo.Name = fileInfo.FileName;
            string parentFolderWindows = relativeParentPath.Replace(@"/", @"\");
            if (parentFolderWindows.EndsWith(@"\"))
                parentFolderWindows = parentFolderWindows.Substring(0, parentFolderWindows.Length - 1);

            if ("".Equals(parentFolderWindows))
            {
                currStorageInfo.LogicalPath = @$"\\{storageProviderConfig.LogicalServer}\{storageProviderConfig.ShareName}\{fileInfo.FileName}";
            }
            else
            {
                currStorageInfo.LogicalPath = @$"\\{storageProviderConfig.LogicalServer}\{storageProviderConfig.ShareName}\{parentFolderWindows}\{fileInfo.FileName}";
            }
            currStorageInfo.Timestamp = fileInfo.LastWriteTime;
            currStorageInfo.SizeBytes = fileInfo.Length;

            return currStorageInfo;
        }

        private string GetPhysicalPath(string logicalPath)
        {
            if (logicalPath.ToLower().StartsWith(@$"\\{storageProviderConfig.LogicalServer}.{storageProviderConfig.Domain}") || logicalPath.ToLower().StartsWith(@$"\\{storageProviderConfig.LogicalServer}"))
            {
                return @$"\\{storageProviderConfig.PhysicalServer}\{storageProviderConfig.ShareName}\{GetRelativePath(logicalPath)}";
            }
            return null;
        }

        private string GetRelativePath(string logicalPath)
        {
            string[] paths = logicalPath.Replace(@$"/", @$"\").Split(@"\");
            StringBuilder sb = new StringBuilder();
            int splitLength = storageProviderConfig.LogicalServer.Split(@"\").Length + 3;
            if (paths.Length < splitLength)
                throw new ArgumentException($"Path {logicalPath} is not the full path");

            if (paths.Length > splitLength)
            {
                for (int index = splitLength; index < paths.Length; index++)
                {
                    sb.Append(paths[index]);
                    if (index < paths.Length - 1)
                        sb.Append(@"\");
                }
            }

            return sb.ToString();
        }

        private string getFileName(string logicalPath)
        {
            string normalizedPath = logicalPath.Replace(@$"/", @$"\");
            string[] paths = normalizedPath.Split(@"\");
            return paths.Last();
        }

        private ISMBFileStore GetFileStore(string shareName)
        {
            SMBNetworkStorageProviderConfig config = (SMBNetworkStorageProviderConfig)storageProviderConfig;
            fileStoreCache.TryGetValue(shareName, out ISMBFileStore fileStore);
            // Refresh the file store if we exceed the smb refresh client interval
            if (fileStore == null || DateTime.Compare(DateTime.Now, this.loggedInTime.AddSeconds(config.SmbRefreshClientIntervalSecs)) > 0)
            {
                this.loggedInTime = DateTime.Now;
                NTStatus status;
                fileStore = GetAuthenticatedSmbClient().TreeConnect(shareName, out status);
                if (status != NTStatus.STATUS_SUCCESS)
                    throw new Exception($"Unable to load the file share '{shareName}': {status}");

                fileStoreCache[shareName] = fileStore;
            }
            return fileStore;
        }

        private SMB2Client GetAuthenticatedSmbClient()
        {
            SMB2Client smbClient = new SMB2Client();
                bool isConnected = smbClient.Connect(storageProviderConfig.PhysicalServer, SMBTransportType.DirectTCPTransport);
                if (!isConnected)
                    throw new Exception($"Unable to connect to '{storageProviderConfig.LogicalServer}'");

                NTStatus status = smbClient.Login(storageProviderConfig.Domain, storageProviderConfig.Username, storageProviderConfig.Password, AuthenticationMethod.NTLMv2);
                if (status != NTStatus.STATUS_SUCCESS)
                    throw new Exception($"Unable to authenticate as '{storageProviderConfig.Username}' in domain '{storageProviderConfig.Domain}'");
            return smbClient;
        }
    }
}

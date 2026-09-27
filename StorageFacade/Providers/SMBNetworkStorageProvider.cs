// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
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
        private SMBNetworkStorageProviderConfig _storageProviderConfig;
        private ISmbClientFactory _smbClientFactory;

        internal SMBNetworkStorageProvider(SMBNetworkStorageProviderConfig smbNetworkStorageProviderConfig)
            : this(smbNetworkStorageProviderConfig, new SmbClientFactory(smbNetworkStorageProviderConfig))
        {
        }

        internal SMBNetworkStorageProvider(SMBNetworkStorageProviderConfig smbNetworkStorageProviderConfig, ISmbClientFactory smbClientFactory)
        {
            _storageProviderConfig = smbNetworkStorageProviderConfig;
            _smbClientFactory = smbClientFactory;
        }

        public string GetName()
        {
            return _storageProviderConfig.Name;
        }

        public new StorageFacadeType GetType()
        {
            return _storageProviderConfig.StorageFacadeType;
        }

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string logicalPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            ISMBClient smbClient = GetAuthenticatedSmbClient();
            ISMBFileStore fileStore = GetFileStore(smbClient, @$"{_storageProviderConfig.ShareName}");
            string relativePath = GetRelativePath(logicalPath);
            object directoryHandle = null;

            try
            {
                OpenDirectoryHandle(fileStore, relativePath, logicalPath, out directoryHandle);
                fileStore.QueryDirectory(
                    out List<QueryDirectoryFileInformation> fileList,
                    directoryHandle,
                    @"*",
                    FileInformationClass.FileDirectoryInformation);

                foreach (StorageInfo info in EnumerateDirectoryEntries(fileList, relativePath, isRecursive, storageFilter))
                    yield return info;
            }
            finally
            {
                if (directoryHandle != null)
                    fileStore.CloseFile(directoryHandle);

                fileStore.Disconnect();
                smbClient.Disconnect();
            }
        }

        private IEnumerable<StorageInfo> EnumerateDirectoryEntries(
            List<QueryDirectoryFileInformation> fileList,
            string relativePath,
            bool isRecursive,
            StorageFilter storageFilter)
        {
            foreach (FileDirectoryInformation fileInfo in fileList)
            {
                foreach (StorageInfo info in YieldFromDirectoryEntry(relativePath, fileInfo, isRecursive, storageFilter))
                    yield return info;
            }
        }

        private IEnumerable<StorageInfo> YieldFromDirectoryEntry(
            string relativePath,
            FileDirectoryInformation fileInfo,
            bool isRecursive,
            StorageFilter storageFilter)
        {
            StorageInfo storageInfo = GetStorageInfo(relativePath, fileInfo);
            if (storageInfo.IsFile)
            {
                if (StorageFilter.IsMatch(storageFilter, storageInfo))
                    yield return storageInfo;
                yield break;
            }

            if (!isRecursive || storageInfo.Name is "." or "..")
                yield break;

            foreach (StorageInfo subStorageInfo in EnumerateStorageInfo(storageInfo.LogicalPath, true, storageFilter))
                yield return subStorageInfo;
        }

        private void OpenDirectoryHandle(
            ISMBFileStore fileStore,
            string relativePath,
            string logicalPathForError,
            out object directoryHandle)
        {
            NTStatus status = fileStore.CreateFile(
                out directoryHandle,
                out FileStatus _,
                @$"{relativePath}",
                AccessMask.GENERIC_READ,
                FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new StorageFacadeException(
                    $"Unable to get the directory handle {logicalPathForError} - {status} (tried {GetPhysicalPath(logicalPathForError)})");
            }
        }

        public StorageInfo GetStorageInfo(string logicalPath)
        {
            List<QueryDirectoryFileInformation> fileList;

            ISMBClient smbClient = GetAuthenticatedSmbClient();
            ISMBFileStore fileStore = GetFileStore(smbClient, @$"{_storageProviderConfig.ShareName}");

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
                    FileAttributes.Directory,
                    ShareAccess.Read | ShareAccess.Write,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_DIRECTORY_FILE,
                    null);

                if (status != NTStatus.STATUS_SUCCESS)
                    throw new StorageFacadeException($"Unable to get the directory handle {relativeParentPath} - {status}");

                fileStore.QueryDirectory(out fileList, directoryHandle, @$"{fileName}", FileInformationClass.FileDirectoryInformation);
                if (fileList == null || fileList.Count == 0)
                    throw new StorageNotFoundException(logicalPath);

                return GetStorageInfo(relativeParentPath, (FileDirectoryInformation)fileList[0]);
            }
            finally
            {
                // Close the directory handle
                if (directoryHandle != null)
                    fileStore.CloseFile(directoryHandle);

                fileStore.Disconnect();
                smbClient.Disconnect();
            }
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            return await ReadStorageAsync(storageInfo.LogicalPath);
        }

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories = false)
        {
            ISMBClient smbClient = GetAuthenticatedSmbClient();
            ISMBFileStore fileStore = GetFileStore(smbClient, @$"{_storageProviderConfig.ShareName}");
            object fileHandle = null;
            try
            {
                string relativePath = GetRelativePath(logicalPath);

                // Create parent directory hierarchy if it doesn't exist
                if (createParentDirectories)
                {
                    string[] paths = relativePath.Split(@"\");
                    if (paths.Length > 1)
                    {
                        int index = 0;
                        StringBuilder sb = new StringBuilder();
                        while (index < paths.Length - 1)
                        {
                            if (index > 0) sb.Append(@"\");
                            sb.Append(paths[index]);
                            NTStatus status = fileStore.CreateFile(
                                out fileHandle,
                                out FileStatus fileStatus,
                                sb.ToString(),
                                AccessMask.SYNCHRONIZE | (AccessMask)DirectoryAccessMask.GENERIC_READ | (AccessMask)DirectoryAccessMask.DELETE,
                                FileAttributes.Normal,
                                ShareAccess.Read | ShareAccess.Delete,
                                CreateDisposition.FILE_OPEN | CreateDisposition.FILE_CREATE,
                                CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT | CreateOptions.FILE_DIRECTORY_FILE, null);

                            index++;
                        }
                    }
                }

                // Now write the file
                await Task.Run(() =>
                {
                    NTStatus status = fileStore.CreateFile(
                           out fileHandle,
                           out FileStatus fileStatus,
                           relativePath,
                           AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                           FileAttributes.Normal,
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
                                throw new StorageFacadeException($"Failed to write to file {relativePath} at share {_storageProviderConfig.ShareName}");

                            writeOffset += len;
                        }

                        var eof = new FileEndOfFileInformation { EndOfFile = writeOffset };
                        status = fileStore.SetFileInformation(fileHandle, eof);
                        if (status != NTStatus.STATUS_SUCCESS)
                            throw new StorageFacadeException($"Failed to set end-of-file for {relativePath} at share {_storageProviderConfig.ShareName}: {status}");

                        status = fileStore.CloseFile(fileHandle);
                    }
                    else
                    {
                        throw new StorageFacadeException($"Unable to get file handle to {logicalPath} - {status}");
                    }
                });
            }
            finally
            {
                // Close the file handle
                if (fileHandle != null)
                    fileStore.CloseFile(fileHandle);

                fileStore.Disconnect();
                smbClient.Disconnect();
            }
        }

        public async Task DeleteStorageAsync(string logicalPath)
        {
            string relativePath = GetRelativePath(logicalPath);
            ISMBClient smbClient = GetAuthenticatedSmbClient();
            ISMBFileStore fileStore = GetFileStore(smbClient, @$"{_storageProviderConfig.ShareName}");
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
                            throw new StorageFacadeException($"Unable to delete {logicalPath} - {status}");

                        FileDispositionInformation fileDispositionInformation = new FileDispositionInformation();
                        fileDispositionInformation.DeletePending = true;
                        status = fileStore.SetFileInformation(fileHandle, fileDispositionInformation);

                        if (status != NTStatus.STATUS_SUCCESS)
                            throw new StorageFacadeException($"Unable to delete {logicalPath} - {status}");
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

                fileStore.Disconnect();
                smbClient.Disconnect();
            }
        }

        public async Task<string> ComputeMD5Checksum(string logicalPath)
        {
            using var md5 = MD5.Create();
            using var stream = await ReadStorageAsync(logicalPath);
            return Convert.ToBase64String(md5.ComputeHash(stream));
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
                currStorageInfo.LogicalPath = @$"\\{_storageProviderConfig.LogicalServer}\{_storageProviderConfig.ShareName}\{fileInfo.FileName}";
            }
            else
            {
                currStorageInfo.LogicalPath = @$"\\{_storageProviderConfig.LogicalServer}\{_storageProviderConfig.ShareName}\{parentFolderWindows}\{fileInfo.FileName}";
            }
            DateTime lastUpdated = fileInfo.LastWriteTime;
            DateTime created = fileInfo.CreationTime;
            currStorageInfo.Timestamp = (lastUpdated.CompareTo(created) >= 0) ? lastUpdated : created;
            currStorageInfo.SizeBytes = fileInfo.EndOfFile;

            return currStorageInfo;
        }

        private string GetPhysicalPath(string logicalPath)
        {
            if (logicalPath.ToLower().StartsWith(@$"\\{_storageProviderConfig.LogicalServer}.{_storageProviderConfig.Domain}") || logicalPath.ToLower().StartsWith(@$"\\{_storageProviderConfig.LogicalServer}"))
            {
                return @$"\\{_storageProviderConfig.PhysicalServer}\{_storageProviderConfig.ShareName}\{GetRelativePath(logicalPath)}";
            }
            return null;
        }

        private string GetRelativePath(string logicalPath)
        {
            string[] paths = logicalPath.Replace(@$"/", @$"\").Split(@"\");
            StringBuilder sb = new StringBuilder();
            int splitLength = _storageProviderConfig.LogicalServer.Split(@"\").Length + 3;
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

        private ISMBFileStore GetFileStore(ISMBClient smb2Client, string shareName)
        {
            ISMBFileStore fileStore = smb2Client.TreeConnect(shareName, out NTStatus status);
            if (status != NTStatus.STATUS_SUCCESS)
                throw new StorageFacadeException($"Unable to load the file share '{shareName}': {status}");

            return fileStore;
        }

        private ISMBClient GetAuthenticatedSmbClient()
        {
            return _smbClientFactory.CreateConnectedClient();
        }

        private async Task<Stream> ReadStorageAsync(string logicalPath)
        {
            ISMBClient smbClient = GetAuthenticatedSmbClient();
            ISMBFileStore fileStore = GetFileStore(smbClient, @$"{_storageProviderConfig.ShareName}");

            string relativePath = GetRelativePath(logicalPath);
            string fileName = getFileName(logicalPath);
            string relativeParentPath = relativePath.Substring(0, relativePath.Length - fileName.Length);
            object fileHandle = null;

            try
            {
                OpenFileForRead(fileStore, relativePath, fileName, relativeParentPath, logicalPath, out fileHandle);
                MemoryStream ms = ReadFileContents(fileStore, fileHandle, relativePath);
                return await Task.FromResult(ms);
            }
            finally
            {
                if (fileHandle != null)
                    fileStore.CloseFile(fileHandle);

                fileStore.Disconnect();
                smbClient.Disconnect();
            }
        }

        private static void OpenFileForRead(
            ISMBFileStore fileStore,
            string relativePath,
            string fileName,
            string relativeParentPath,
            string logicalPath,
            out object fileHandle)
        {
            fileStore.CreateFile(
                out fileHandle,
                out FileStatus fileStatus,
                relativePath,
                AccessMask.GENERIC_READ,
                FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);

            if (fileStatus != FileStatus.FILE_OPENED)
            {
                throw new StorageNotFoundException(logicalPath);
            }
        }

        private MemoryStream ReadFileContents(ISMBFileStore fileStore, object fileHandle, string relativePath)
        {
            var ms = new MemoryStream();
            long offset = 0;
            NTStatus status = default;
            while (status != NTStatus.STATUS_END_OF_FILE)
            {
                status = fileStore.ReadFile(out var bytesRead, fileHandle, offset, 64 * 1024);

                if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_END_OF_FILE)
                {
                    throw new StorageFacadeException(
                        $"Failed to read to file {relativePath} at share {_storageProviderConfig.ShareName} - status was {status}");
                }

                if (bytesRead == null || bytesRead.Length == 0)
                    break;

                ms.Write(bytesRead);
                offset += bytesRead.Length;
            }

            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}

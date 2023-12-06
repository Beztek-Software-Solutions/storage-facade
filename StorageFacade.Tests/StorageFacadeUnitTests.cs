// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class StorageFacadeUnitTests
    {
        [Test]
        public void TestFileStorageEnumerate()
        {
            IStorageProviderConfig config = new FileStorageProviderConfig();
            IStorageFacade fileStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            StorageFilter storageFilter = new StorageFilter();
            storageFilter.Extensions = new List<string>() { ".cs", ".pdf" };
            //storageFilter.DateRange = Tuple.Create<DateTime, DateTime>(DateTime.Now.AddYears(-9), DateTime.Now);
            int index = 0;
            foreach (StorageInfo storageInfo in fileStorageFacade.EnumerateStorageInfo("/home/peter", false, storageFilter))
            {
                Console.WriteLine(ToString(storageInfo));
                index++;
            }
            Console.WriteLine($"Totally {index} local files found");
        }

        [Test]
        public async Task TestFileStorageStreamRead()
        {
            string path = @"/mnt/nas/readonly/wirelesskey.txt";
            IStorageProviderConfig config = new FileStorageProviderConfig();
            IStorageFacade fileStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            StorageInfo storageInfo = fileStorageFacade.GetStorageInfo(path);

            Stream stream = await fileStorageFacade.ReadStorageAsync(storageInfo).ConfigureAwait(false);
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                Console.WriteLine($"The file storage content was '{reader.ReadToEnd()}'");
            }
        }

        [Test]
        public async Task TestFileStorageDelete()
        {
            string path = @"/home/peter/Documents/test.txt";
            byte[] byteArray = Encoding.ASCII.GetBytes("Test write stream");
            MemoryStream stream = new MemoryStream(byteArray);

            IStorageProviderConfig config = new FileStorageProviderConfig();
            IStorageFacade storageFacade = StorageFacadeFactory.GetStorageFacade(config);
            await storageFacade.WriteStorageAsync(path, stream).ConfigureAwait(false);
            await Task.Delay(100).ConfigureAwait(false);
            await storageFacade.DeleteStorageAsync(path).ConfigureAwait(false);
            Console.WriteLine($"Wrote and Deleted file at {path}");
        }

        [Test]
        public void TestAzureBlobStorageEnumerate()
        {
            IStorageProviderConfig config = new AzureBlobStorageProviderConfig("sthumanaexport", "Y7Hu7tR8eK4WKvD89hgB546VTLj1xXNnbQ7z1bLHr0fCxHryjewi9/V+WPL5SLAontEMHkPMUJ15+AStHgaNnA==", "files");
            IStorageFacade blobStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            StorageFilter storageFilter = new StorageFilter();
            storageFilter.Extensions = new List<string>() { ".csv", ".txt" };
            //storageFilter.DateRange = Tuple.Create<DateTime, DateTime>(DateTime.Now.AddYears(-9), DateTime.Now);
            int index = 0;
            foreach (StorageInfo storageInfo in blobStorageFacade.EnumerateStorageInfo("", false, storageFilter))
            {
                Console.WriteLine(ToString(storageInfo));
                index++;
            }
            Console.WriteLine($"Totally {index} blob storage files found");
        }

        [Test]
        public async Task TestAzureBlobStorageStreamRead()
        {
            string path = @"members.json";
            IStorageProviderConfig config = new AzureBlobStorageProviderConfig("sthumanaexport", "Y7Hu7tR8eK4WKvD89hgB546VTLj1xXNnbQ7z1bLHr0fCxHryjewi9/V+WPL5SLAontEMHkPMUJ15+AStHgaNnA==", "exports");
            IStorageFacade blobStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            StorageInfo storageInfo = blobStorageFacade.GetStorageInfo(path);

            Stream stream = await blobStorageFacade.ReadStorageAsync(storageInfo).ConfigureAwait(false);
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                Console.WriteLine($"The blob storage content was '{reader.ReadToEnd()}'");
            }
        }

        [Test]
        public async Task TestComboStorageStreamRead()
        {
            string path = @"members.json";
            IStorageProviderConfig config = new AzureBlobStorageProviderConfig("sthumanaexport", "Y7Hu7tR8eK4WKvD89hgB546VTLj1xXNnbQ7z1bLHr0fCxHryjewi9/V+WPL5SLAontEMHkPMUJ15+AStHgaNnA==", "exports");
            IStorageFacade blobStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            
            StorageInfo storageInfo = blobStorageFacade.GetStorageInfo(path);

            Stream stream = await blobStorageFacade.ReadStorageAsync(storageInfo).ConfigureAwait(false);
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                Console.WriteLine($"The blob storage content was '{reader.ReadToEnd()}'");
            }
        }

        [Test]
        public async Task TestAzureBlobStorageDelete()
        {
            string path = @"/folder1/folder2/test.txt";
            byte[] byteArray = Encoding.ASCII.GetBytes("Test write stream");
            MemoryStream stream = new MemoryStream(byteArray);

            string accountName = "sthumanaexport";
            IStorageProviderConfig config = new AzureBlobStorageProviderConfig(accountName, "Y7Hu7tR8eK4WKvD89hgB546VTLj1xXNnbQ7z1bLHr0fCxHryjewi9/V+WPL5SLAontEMHkPMUJ15+AStHgaNnA==", "files");
            IStorageFacade blobStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            await blobStorageFacade.WriteStorageAsync(path, stream).ConfigureAwait(false);
            await blobStorageFacade.DeleteStorageAsync(path).ConfigureAwait(false);
            Console.WriteLine($"Wrote and Deleted {path} in Azure blob container {accountName}");
        }

        [Test]
        public void TestSMBNetworkFileStorageEnumerate()
        {
            IStorageProviderConfig config = new SMBNetworkStorageProviderConfig("thomasnas.home.com", "readonly", "home.com", "peter", "vanpet1", "192.168.1.21");
            IStorageFacade smbStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            StorageFilter storageFilter = new StorageFilter();
            //storageFilter.Extensions = new List<string>() { "mp3","jpg" };
            storageFilter.RegexPatterns = new List<string>();// { @"\.(pdf|csv|flv|jpg|mp3|JPG)$" };
            storageFilter.DateRange = Tuple.Create<DateTime, DateTime>(DateTime.Now.AddYears(-20), DateTime.Now);
            int index = 0;
            string path = @"\\thomasnas.home.com\readonly\Processed Media";
            foreach (StorageInfo storageInfo in smbStorageFacade.EnumerateStorageInfo(path, true, storageFilter))
            {
                Console.WriteLine(ToString(storageInfo));
                index++;
            }
            Console.WriteLine($"Totally {index} smb files found under {path}");
        }

        [Test]
        public void TestSMBNetworkMatrixEnumerate()
        {
            //IStorageProviderConfig config = new SMBNetworkStorageProviderConfig("MatrixEclat", @"matrixhealth.net\matrix-files", "ITDev", "matrixhealth.net", "eclatsvcdev", "yGDJYhkLRl!#3Fm7Q9H", "10.32.13.16");
            IStorageProviderConfig config = new SMBNetworkStorageProviderConfig(@"thomasnas\test", "readonly", "home.com", "Guest", "Guest", "192.168.1.21");
            //IStorageProviderConfig config = new SMBNetworkStorageProviderConfig("MatrixEclat", "phx01-file04", "ITDev", "matrixhealth.net", "eclatsvcdev", "yGDJYhkLRl!#3Fm7Q9H");
            IStorageFacade storageFacade = StorageFacadeFactory.GetStorageFacade(config);
            StorageFilter storageFilter = new StorageFilter();
            //storageFilter.RegexPatterns = new List<string>() { @"\.(pdf|csv)$" };
            storageFilter.DateRange = Tuple.Create<DateTime, DateTime>(DateTime.Now.AddYears(-20), DateTime.Now);
            int index = 0;

            foreach (StorageInfo storageInfo in storageFacade.EnumerateStorageInfo(@"\\thomasnas.home.com\test\readonly\Processed Media", false, storageFilter))
            {
                Console.WriteLine(ToString(storageInfo));
                index++;
            }
            Console.WriteLine($"Totally {index} smb matrix files found");
        }

        [Test]
        public void TestGetSMBNetworkStorageInfo()
        {
            string path = @"\\thomasnas\Movies\Batman 1 - Batman Begins.m4v";
            IStorageProviderConfig config = new SMBNetworkStorageProviderConfig("thomasnas", "Movies", "home.com", "peter", "vanpet1", "192.168.1.21");
            IStorageFacade smbStorageFacade = StorageFacadeFactory.GetStorageFacade(config);

            StorageInfo storageInfo = smbStorageFacade.GetStorageInfo(path);

            Console.WriteLine(ToString(storageInfo));
        }

        [Test]
        public async Task TestGetSMBNetworkStorageStreamRead()
        {
            string path = @"\\thomasnas\readonly\wirelesskey.txt";
            IStorageProviderConfig config = new SMBNetworkStorageProviderConfig("thomasnas", "readonly", "home.com", "peter", "vanpet1", "192.168.1.21");
            IStorageFacade smbStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            StorageInfo storageInfo = smbStorageFacade.GetStorageInfo(path);

            Stream stream = await smbStorageFacade.ReadStorageAsync(storageInfo).ConfigureAwait(false);
            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                Console.WriteLine($"The smb file content was '{reader.ReadToEnd()}'");
            }
        }

        [Test]
        public async Task TestSMBNetworkStorageDelete()
        {
            string path = @"\\thomasnas.home.com\public\Rachna's Computer\test.txt";

            byte[] byteArray = Encoding.ASCII.GetBytes("Test write stream");
            MemoryStream stream = new MemoryStream(byteArray);

            IStorageProviderConfig config = new SMBNetworkStorageProviderConfig("thomasnas", "public", "home.com", "peter", "vanpet1", "192.168.1.21");
            IStorageFacade storageFacade = StorageFacadeFactory.GetStorageFacade(config);
            await storageFacade.WriteStorageAsync(path, stream).ConfigureAwait(false);
            await storageFacade.DeleteStorageAsync(path).ConfigureAwait(false);
            Console.WriteLine($"Wrote and Deleted {path} in SMB Network storage {path}");
        }

        // Internal

        private static string ToString(StorageInfo storageInfo)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{storageInfo.LogicalPath} {storageInfo.IsFile} {storageInfo.Timestamp} {storageInfo.MimeType} {storageInfo.SizeBytes}");
            return sb.ToString();
        }
    }
}

// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class StorageFacadeUnitTests
    {
        /*[Test]
        public void TestFileStorageEnumerate()
        {
            IStorageProviderConfig config = new FileStorageProviderConfig();
            IStorageFacade fileStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            StorageFilter storageFilter = new StorageFilter();
            storageFilter.Extensions = new List<string>() { ".cs", ".pdf" };
            //storageFilter.DateRange = Tuple.Create<DateTime, DateTime>(DateTime.Now.AddYears(-9), DateTime.Now);
            int index = 0;
            foreach (StorageInfo storageInfo in fileStorageFacade.EnumerateStorageInfo(@"\", false, storageFilter))
            {
                Console.WriteLine(storageInfo.ToString());
                index++;
            }
            Console.WriteLine($"Totally {index} local files found");
        }

        [Test]
        public async Task TestFileStorageStreamRead()
        {
            string path = @"C:\Users\Peter.Thomas\SDK\Other Redistributable.txt";
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
            string path =@"C:\Users\Peter.Thomas\test.txt";
            byte[] byteArray = Encoding.ASCII.GetBytes("Test write stream");
            MemoryStream stream = new MemoryStream(byteArray);

            IStorageProviderConfig config = new FileStorageProviderConfig();
            IStorageFacade storageFacade = StorageFacadeFactory.GetStorageFacade(config);
            await storageFacade.WriteStorageAsync(path, stream).ConfigureAwait(false);
            await storageFacade.DeleteStorageAsync(path).ConfigureAwait(false);
            Console.WriteLine($"Wrote and Deleted file at {path}");
        }*/

        [Test]
        public void TestAzureBlobStorageEnumerate()
        {
            IStorageProviderConfig config = new AzureBlobStorageProviderConfig("sthumanaexport", "Y7Hu7tR8eK4WKvD89hgB546VTLj1xXNnbQ7z1bLHr0fCxHryjewi9/V+WPL5SLAontEMHkPMUJ15+AStHgaNnA==", "member-outbound");
            IStorageFacade blobStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            StorageFilter storageFilter = new StorageFilter();
            storageFilter.Extensions = new List<string>() { ".csv", ".txt", ".dat", "json" };
            //storageFilter.DateRange = Tuple.Create<DateTime, DateTime>(DateTime.Now.AddYears(-9), DateTime.Now);
            int index = 0;
            foreach (StorageInfo storageInfo in blobStorageFacade.EnumerateStorageInfo(@"https://sthumanaexport.blob.core.windows.net/member-outbound", true, storageFilter))
            {
                Console.WriteLine(storageInfo.LogicalPath);
                index++;
            }
            Console.WriteLine($"Totally {index} blob storage files found");
        }

        [Test]
        public async Task TestAzureBlobStorageStreamRead()
        {
            string path = @"https://sthumanaexport.blob.core.windows.net/files/MATRIX_LabChase_20230606_Inbound_20230705_033234.csv";
            IStorageProviderConfig config = new AzureBlobStorageProviderConfig("sthumanaexport", "Y7Hu7tR8eK4WKvD89hgB546VTLj1xXNnbQ7z1bLHr0fCxHryjewi9/V+WPL5SLAontEMHkPMUJ15+AStHgaNnA==", "files");
            //string path = @"https://sthumanaexport.blob.core.windows.net/member-outbound/input-files/members/m3/va/memberUpdate_2023-11-24T17-28-16.json";
            //IStorageProviderConfig config = new AzureBlobStorageProviderConfig("sthumanaexport", "Y7Hu7tR8eK4WKvD89hgB546VTLj1xXNnbQ7z1bLHr0fCxHryjewi9/V+WPL5SLAontEMHkPMUJ15+AStHgaNnA==", "files");
            IStorageFacade blobStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            StorageInfo storageInfo = blobStorageFacade.GetStorageInfo(path);

            Stream stream = await blobStorageFacade.ReadStorageAsync(storageInfo).ConfigureAwait(false);
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                Console.WriteLine($"The blob storage content was '{reader.ReadToEnd()}'");
            }
        }

        /*[Test]
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
            IStorageProviderConfig config = new SMBNetworkStorageProviderConfig("Guest", "192.168.1.21", "readonly", "home.com", "peter", "vanpet1");
            StorageFacade smbStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
            StorageFilter storageFilter = new StorageFilter();
            //storageFilter.Extensions = new List<string>() { ".mp3","jpg" };
            storageFilter.RegexPatterns = new List<string>() { @"\.(pdf|csv|flv|jpg|mp3|JPG)$" };
            storageFilter.DateRange = Tuple.Create<DateTime, DateTime>(DateTime.Now.AddYears(-20), DateTime.Now);
            int index = 0;
            foreach (StorageInfo storageInfo in smbStorageFacade.EnumerateStorageInfo("Processed Media", false, storageFilter))
            {
                Console.WriteLine(StorageFacade.ToString(storageInfo));
                index++;
            }
            Console.WriteLine($"Totally {index} smb files found");
        }*/

        /*[Test]
        public void TestSMBNetworkMatrixEnumerate()
        {
            IStorageProviderConfig config = new SMBNetworkStorageProviderConfig(@"matrixhealth.net\matrix-filex", "ITDev", "matrixhealth.net", "eclatsvcdev", "yGDJYhkLRl!#3Fm7Q9H", "10.32.13.16");
            IStorageFacade tmpFacade = StorageFacadeFactory.GetStorageFacade(config);
            List<IStorageFacade> storageFacades = new List<IStorageFacade>();
            storageFacades.Add(tmpFacade);
            storageFacades.Add(tmpFacade);
            storageFacades.Add(tmpFacade);
            storageFacades.Add(tmpFacade);
            Console.WriteLine(tmpFacade.GetName());
            IStorageFacade storageFacade = new ComboStorageFacade(storageFacades);

            StorageFilter storageFilter = new StorageFilter();
            //storageFilter.RegexPatterns = new List<string>() { @"\.(pdf|csv)$" };
            storageFilter.DateRange = Tuple.Create<DateTime, DateTime>(DateTime.Now.AddYears(-20), DateTime.Now);

            int index = 0;
            foreach (StorageInfo storageInfo in storageFacade.EnumerateStorageInfo(@"\\matrixhealth.net\matrix-files\ITDev\eclat", true, storageFilter))
            {
                //Console.WriteLine(storageInfo.LogicalPath);
                index++;
            }
            Console.WriteLine($"Totally {index} smb matrix files found");
            index = 0;
            foreach (StorageInfo storageInfo in storageFacade.EnumerateStorageInfo(@"\temp", true, storageFilter))
            {
                //Console.WriteLine(storageInfo.LogicalPath);
                index++;
            }
            Console.WriteLine($"Totally {index} local files found");
        }

        [Test]
        public void TestGetSMBNetworkStorageInfo()
        {
            string path = @"\\192.168.1.21\Movies\Batman 1 - Batman Begins.m4v";
            IStorageProviderConfig config = new SMBNetworkStorageProviderConfig("Home", "192.168.1.21", "Movies", "home.com", "peter", "vanpet1");
            StorageFacade smbStorageFacade = StorageFacadeFactory.GetStorageFacade(config);

            StorageInfo storageInfo = smbStorageFacade.GetStorageInfo(path);

            Console.WriteLine(StorageFacade.ToString(storageInfo));
        }

        [Test]
        public async Task TestGetSMBNetworkStorageStreamRead()
        {
            string path = @"\\192.168.1.21\readonly\wirelesskey.txt";
            IStorageProviderConfig config = new SMBNetworkStorageProviderConfig("Home", "192.168.1.21", "readonly", "home.com", "peter", "vanpet1");
            StorageFacade smbStorageFacade = StorageFacadeFactory.GetStorageFacade(config);
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
            string path = @"\\192.168.1.21\public\Rachna's Computer\test.txt";

            byte[] byteArray = Encoding.ASCII.GetBytes("Test write stream");
            MemoryStream stream = new MemoryStream(byteArray);

            IStorageProviderConfig config = new SMBNetworkStorageProviderConfig("SMBHome", "192.168.1.21", "public", "home.com", "peter", "vanpet1");
            StorageFacade storageFacade = StorageFacadeFactory.GetStorageFacade(config);
            await storageFacade.WriteStorageAsync(path, stream).ConfigureAwait(false);
            await storageFacade.DeleteStorageAsync(path).ConfigureAwait(false);
            Console.WriteLine($"Wrote and Deleted {path} in SMB Network storage {path}");
        }*/
    }
}

// Copyright (c) Beztek Software Solutions. All rights reserved.


namespace Beztek.Facade.Storage
{
    using System;
    using System.Linq;
    using MimeTypes;

    /// <summary>
    /// Class providing metadata for an underlying storage object
    /// </summary>
    public class StorageInfo
    {
        public string Name { get; set; }
        public string LogicalPath { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsDirectory { get { return !IsFile; } }
        public bool IsFile { get; set; } = false;
        public long SizeBytes { get; set; }
        public string Extension
        {
            get {
                return this.Name.Split(".").Last<string>();
            }
        }
        public string MimeType
        {
            get {
                return MimeTypeMap.GetMimeType(this.Extension);
            }
        }
    }
}

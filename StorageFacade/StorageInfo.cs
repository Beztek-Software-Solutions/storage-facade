// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.Linq;
    using MimeTypes;

    /// <summary>Metadata describing a stored file object.</summary>
    public class StorageInfo
    {
        /// <summary>File name without path.</summary>
        public string Name { get; set; }

        /// <summary>Full logical path in the facade namespace.</summary>
        public string LogicalPath { get; set; }

        /// <summary>Last modified or created timestamp (provider-dependent).</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>True when this entry represents a directory rather than a file.</summary>
        public bool IsDirectory { get { return !IsFile; } }

        /// <summary>True when this entry represents a file.</summary>
        public bool IsFile { get; set; } = false;

        /// <summary>Size in bytes.</summary>
        public long SizeBytes { get; set; }

        /// <summary>Extension segment after the last dot in <see cref="Name"/>.</summary>
        public string Extension => this.Name.Split(".").Last<string>();

        /// <summary>MIME type inferred from <see cref="Extension"/>.</summary>
        public string MimeType => MimeTypeMap.GetMimeType(this.Extension);
    }
}

// -----------------------------------------------------------------------
// <copyright file="FolderMetadata.cs" company="Dylan Addison">
//     Copyright (c) Dylan Addison. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
namespace SharePointMigrator.Model
{
    using System.Collections.Generic;

    public class FolderMetadata
    {
        public HashSet<string> FileList { get; set; }

        public HashSet<string> DirectoryList { get; set; }

        public long Size { get; set; }

        public FolderMetadata(HashSet<string> fileList, HashSet<string> directoryList, long size)
        {
            this.FileList = fileList;
            this.DirectoryList = directoryList;
            this.Size = size;
        }
    }
}
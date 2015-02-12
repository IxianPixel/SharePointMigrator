namespace SharePointMigrator.Pages
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    using Microsoft.SharePoint.Client;

    using SharePointMigrator.Model;

    /// <summary>
    /// Interaction logic for Migrate.xaml
    /// </summary>
    public partial class Migrate : UserControl
    {
        public FolderMetadata FolderMetadata { get; set; }

        public ClientContext Context;

        private readonly BackgroundWorker builderWorker = new BackgroundWorker { WorkerReportsProgress = true };

        private readonly BackgroundWorker directoryWorker = new BackgroundWorker { WorkerReportsProgress = true };

        private readonly BackgroundWorker fileWorker = new BackgroundWorker { WorkerReportsProgress = true };

        public Migrate()
        {
            InitializeComponent();
        }
        
        private void Go_Click(object sender, RoutedEventArgs e)
        {
            this.SiteUrl.IsEnabled = false;
            this.Folder.IsEnabled = false;
            this.Username.IsEnabled = false;
            this.Password.IsEnabled = false;
            this.Go.IsEnabled = false;
            
            this.AddMessage("Migration Started", MessageType.Info);

            this.SetupClientContext();

            this.builderWorker.DoWork += this.BuildFolderTree;
            this.builderWorker.ProgressChanged += this.BuilderProgressChanged;
            this.builderWorker.RunWorkerCompleted += this.BuilderCompleted;
            this.builderWorker.RunWorkerAsync(this.Folder.Text);
        }

        private void SetupClientContext()
        {
            this.AddMessage("Connecting to SharePoint", MessageType.Info);
            this.Context = new ClientContext(this.SiteUrl.Text)
            {
                RequestTimeout = 1000000,
                Credentials = new SharePointOnlineCredentials(this.Username.Text, this.Password.SecurePassword)
            };
        }

        private void BuildFolderTree(object sender, DoWorkEventArgs e)
        {
            this.builderWorker.ReportProgress(0, "Scanning Folder");

            var fileList = new HashSet<string>();
            var directoryList = new HashSet<string>();
            long size = 0;

            var directories = new Stack<string>(20);

            // TODO: Check directory exists

            directories.Push(e.Argument.ToString());

            while (directories.Count > 0)
            {
                var currentDirectory = directories.Pop();
                string[] subDirectories;

                try
                {
                    subDirectories = Directory.GetDirectories(currentDirectory);
                }
                catch (UnauthorizedAccessException)
                {
                    this.builderWorker.ReportProgress(1, string.Format("Access denied to {0}", currentDirectory));
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    this.builderWorker.ReportProgress(1, string.Format("Cannot find directory {0}", currentDirectory));
                    continue;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(currentDirectory);
                }
                catch (UnauthorizedAccessException)
                {
                    this.builderWorker.ReportProgress(1, string.Format("Access denied to {0}", currentDirectory));
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    this.builderWorker.ReportProgress(1, string.Format("Cannot find directory {0}", currentDirectory));
                    continue;
                }

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);

                    fileList.Add(fileInfo.FullName);
                    size += fileInfo.Length;
                }

                foreach (var subDirectory in subDirectories)
                {
                    var directoryInfo = new DirectoryInfo(subDirectory);
                    directoryList.Add(directoryInfo.FullName);
                    directories.Push(subDirectory);
                }
            }

            this.FolderMetadata = new FolderMetadata(fileList, directoryList, size);
        }

        private void BuilderProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 0)
            {
                this.AddMessage(e.UserState.ToString(), MessageType.Info);
            }
            else
            {
                this.AddMessage(e.UserState.ToString(), MessageType.Error);
            }
        }

        private void BuilderCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.directoryWorker.DoWork += this.MirrorDirectoryTree;
            this.directoryWorker.ProgressChanged += this.DirectoryProgressChanged;
            this.directoryWorker.RunWorkerCompleted += this.DirectoryCompleted;
            this.directoryWorker.RunWorkerAsync(this.Folder.Text);
            
            this.AddMessage("Started Directory Creation", MessageType.Info);

            this.FolderProgressValue.Text = string.Format("0/{0} Folders Created", this.FolderMetadata.DirectoryList.Count);
            this.FileProgressValue.Text = string.Format("0/{0} Files Uploaded", this.FolderMetadata.FileList.Count);
        }

        void MirrorDirectoryTree(object sender, DoWorkEventArgs e)
        {
            var docs = this.Context.Web.Lists.GetByTitle("Documents");
            var root = docs.RootFolder;
            this.Context.Load(root);
            this.Context.ExecuteQuery();

            var iterator = 0;
            foreach (var directory in this.FolderMetadata.DirectoryList)
            {
                var directoryName = directory.Replace(string.Format("{0}\\", e.Argument), "");
                var cleanDirectoryName = Regex.Replace(directoryName, "[#%*:<>?|/]", "");
                var urlDirectoryName = cleanDirectoryName.Replace('\\', '/');
                this.EnsureFolder(root, urlDirectoryName);

                iterator++;
                var stringProgress = string.Format("{0}/{1} Folders Created", iterator, this.FolderMetadata.DirectoryList.Count);
                this.directoryWorker.ReportProgress((iterator * 100) / this.FolderMetadata.DirectoryList.Count, stringProgress);
            }
        }

        private void DirectoryProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.FolderProgress.Value = e.ProgressPercentage;
            this.FolderProgressValue.Text = e.UserState.ToString();
        }

        private void DirectoryCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.fileWorker.DoWork += this.MirrorFiles;
            this.fileWorker.ProgressChanged += this.FileProgressChanges;
            this.fileWorker.RunWorkerCompleted += this.FileCompleted;
            this.fileWorker.RunWorkerAsync(this.Folder.Text);

            this.AddMessage("Started Files Upload", MessageType.Info);
        }

        void MirrorFiles(object sender, DoWorkEventArgs e)
        {
            var docs = this.Context.Web.Lists.GetByTitle("Documents");
            var root = docs.RootFolder;
            this.Context.Load(root);
            this.Context.ExecuteQuery();

            var iterator = 0;
            foreach (var fullPath in this.FolderMetadata.FileList)
            {
                var fileName = Path.GetFileName(fullPath);
                var cleanFileName = Regex.Replace(fileName, "[~#%&*{}\\/:<>?|\"]", "");
                var filePath = fullPath.Replace(string.Format("{0}\\", e.Argument), "");
                filePath = filePath.Replace(fileName, "");
                this.EnsureFile(root, fullPath, filePath, cleanFileName);

                iterator++;
                var stringProgress = string.Format("{0}/{1} Files Uploaded", iterator, this.FolderMetadata.FileList.Count);
                this.fileWorker.ReportProgress((iterator * 100) / this.FolderMetadata.FileList.Count, stringProgress);
            }
        }

        private void FileProgressChanges(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == -1)
            {
                this.AddMessage(e.UserState.ToString(), MessageType.Error);
            }
            else
            {
                this.FileProgress.Value = e.ProgressPercentage;
                this.FileProgressValue.Text = e.UserState.ToString();
            }
        }

        private void FileCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.SiteUrl.IsEnabled = true;
            this.Folder.IsEnabled = true;
            this.Username.IsEnabled = true;
            this.Password.IsEnabled = true;
            this.Go.IsEnabled = true;

            this.AddMessage("Migration Completed", MessageType.Info);
        }

        Folder EnsureFolder(Folder parentFolder, string folderPath)
        {
            //Split up the incoming path so we have the first element as the a new sub-folder name 
            //and add it to ParentFolder folders collection
            var pathElements = folderPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var head = pathElements[0];
            var newFolder = parentFolder.Folders.Add(head);
            this.Context.Load(newFolder);
            this.Context.ExecuteQuery();

            //If we have subfolders to create then the length of PathElements will be greater than 1
            if (pathElements.Length > 1)
            {
                //If we have more nested folders to create then reassemble the folder path using what we have left i.e. the tail
                var tail = string.Empty;
                for (var i = 1; i < pathElements.Length; i++)
                    tail = tail + "/" + pathElements[i];

                //Then make a recursive call to create the next subfolder
                return this.EnsureFolder(newFolder, tail);
            }
            else
                //This ensures that the folder at the end of the chain gets returned
                return newFolder;
        }

        private void EnsureFile(Folder parentFolder, string fullPath, string filePath, string fileName)
        {
            using (var fs = new FileStream(fullPath, FileMode.Open))
            {
                var fileCreationInfo = new FileCreationInformation
                {
                    ContentStream = fs,
                    Url = Path.Combine(filePath, fileName),
                    Overwrite = true
                };

                var uploadFile = parentFolder.Files.Add(fileCreationInfo);
                this.Context.Load(uploadFile);

                try
                {
                    this.Context.ExecuteQuery();
                }
                catch (Exception exception)
                {
                    this.fileWorker.ReportProgress(-1, exception.Message);
                }
            }
        }

        private void AddMessage(string value, MessageType messageType)
        {
            var datePrepended = string.Format("{0} - {1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), value);
            var listBoxItem = new ListBoxItem { IsEnabled = false, Content = datePrepended };

            if (messageType == MessageType.Error)
            {
                listBoxItem.Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            }

            this.MessageLog.Items.Add(listBoxItem);
        }
    }
}

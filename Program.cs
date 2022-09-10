using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using File = Google.Apis.Drive.v3.Data.File;

namespace WindowsFormsApp1
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }

    class GoogleDriveClass
    {
        static string[] Scopes = { DriveService.Scope.DriveFile };  //Array for working with class
        static string ApplicationName = "Google Drive Bridge";      //Application name
        public static UserCredential credential = null;             //Authorization keys
        public static string extension = ".gdb";                    //Extension for saved files


        public bool Authorize()
        {
            using (System.IO.FileStream stream =
                     new System.IO.FileStream("client-secret.json", System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                try
                {
                    string credPath = System.Environment.CurrentDirectory.ToString();
                    credPath = System.IO.Path.Combine(credPath, "drive-bridge.json");

                    credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        GoogleDriveClass.Scopes,
                        "exampletrade@gmail.com",
                        CancellationToken.None,
                        new FileDataStore(credPath, true)).Result;
                }
                catch (Exception)
                {
                    credential = null;
                }

            }
            return (credential != null);
        }

        public string GetFileId(string name)
        {
            string result = null;
            IList<File> files = GetFileList();

            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    if (file.Name == name)
                    {
                        result = file.Id;
                        break;
                    }
                }
            }
            return result;
        }


        public bool FileCreate(string name, string value, out string id)
        {
            bool result = false;
            id = null;
            if (credential == null)
                this.Authorize();
            if (credential == null)
            {
                return result;
            }
            using (var service = new Google.Apis.Drive.v3.DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            }))
            {
                var body = new File();
                body.Name = name;
                body.MimeType = "text/json";
                body.ViewersCanCopyContent = true;

                byte[] byteArray = Encoding.Default.GetBytes(value);
                using (var stream = new System.IO.MemoryStream(byteArray))
                {
                    Google.Apis.Drive.v3.FilesResource.CreateMediaUpload request = service.Files.Create(body, stream, body.MimeType);
                    if (request.Upload().Exception == null)
                    { id = request.ResponseBody.Id; result = true; }
                }
            }
            return result;
        }

        public bool FileUpdate(string name, string value)
        {
            bool result = false;
            if (credential == null)
                this.Authorize();
            if (credential == null)
            {
                return result;
            }

            string new_id;
            if (FileCreate(name, value, out new_id))
            {
                IList<File> files = GetFileList();
                if (files != null && files.Count > 0)
                {
                    result = true;
                    try
                    {
                        using (var service = new DriveService(new BaseClientService.Initializer()
                        {
                            HttpClientInitializer = credential,
                            ApplicationName = ApplicationName,
                        }))
                        {
                            foreach (var file in files)
                            {
                                if (file.Name == name && file.Id != new_id)
                                {
                                    try
                                    {
                                        Google.Apis.Drive.v3.FilesResource.DeleteRequest request = service.Files.Delete(file.Id);
                                        string res = request.Execute();
                                    }
                                    catch (Exception)
                                    {
                                        continue;
                                    }

                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        return result;
                    }

                }

            }
            return result;
        }

        public string FileRead(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                return ("Errore. File not found");
            }
            bool result = false;
            string value = null;
            if (credential == null)
                this.Authorize();
            if (credential != null)
            {
                using (var service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                }))
                {
                    Google.Apis.Drive.v3.FilesResource.GetRequest request = service.Files.Get(id);
                    using (var stream = new MemoryStream())
                    {
                        request.MediaDownloader.ProgressChanged += (IDownloadProgress progress) =>
                        {
                            if (progress.Status == DownloadStatus.Completed)
                                result = true;
                        };
                        request.Download(stream);
                        if (result)
                        {
                            int start = 0;
                            int count = (int)stream.Length;
                            value = Encoding.Default.GetString(stream.GetBuffer(), start, count);
                        }
                    }
                }
            }
            return value;
        }

        public IList<File> GetFileList()
        {
            IList<File> result = null;
            //Control control = new Control();
            if (credential == null)
                this.Authorize();
            if (credential == null)
            {
                return result;
            }
            // Create Drive API service.
            using (Google.Apis.Drive.v3.DriveService service = new Google.Apis.Drive.v3.DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            }))
            {
                try
                {
                    // Define parameters of request.
                    FilesResource.ListRequest listRequest = service.Files.List();
                    listRequest.PageSize = 1000;
                    listRequest.Fields = "nextPageToken, files(id, name, size)";

                    // List files.
                    result = listRequest.Execute().Files;
                }
                catch (Exception e)
                {
                    string mess = e.ToString();
                    return null;
                }
            }
            return result;
        }

        public string GetFileListJSON()
        {
            IList<File> files = GetFileList();
            string result = null;
            if (files != null && files.Count > 0)
            {
                result = "{ FileList { ";
                foreach (var file in files)
                {
                    if (file.Name.EndsWith(extension))
                        result += "{ " + file.Name.Replace(extension, "").Trim() + "; " + file.Id.Trim() + " }";
                }
                result += " }";
            }
            return result;
        }
    }
}

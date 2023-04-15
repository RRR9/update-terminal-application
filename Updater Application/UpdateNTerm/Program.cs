using System.IO;
using System.Net;
using System.Text;
using System;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Threading;
using log4net.Config;
using log4net;
using Shell32;
using System.Management;

namespace UpdateNTerm
{
    class Program
    {
        private static readonly string _token = "";
        private static readonly int _platform;
        private static readonly int _typeApplication;
        private static string _hash;
        private static readonly string _currentPath = AppDomain.CurrentDomain.BaseDirectory;
        private static ILog _log;

        static void Main()
        {
            string prevPath = Path.GetFullPath(Path.Combine(_currentPath, @"..\"));
            string unzipDirectory = "zipFile";


            string downloadFileName;
            string fileNameWithoutExt;
            string extension;

            XmlConfigurator.Configure(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config")));
            _log = LogManager.GetLogger(typeof(Program));

            _log.Info("Start checking update");
            if (!CheckVersion(out string urlDownload, out float nextVersion))
            {
                _log.Info("Don't need to update");
                return;
            }

            downloadFileName = Path.GetFileName(urlDownload);
            fileNameWithoutExt = Path.GetFileNameWithoutExtension(downloadFileName);
            extension = Path.GetExtension(downloadFileName);

            _log.Info("Start update app...");

            try
            {
                DownloadFile(urlDownload);

                if(Process.GetProcessesByName(fileNameWithoutExt).Length != 0)
                {
                    KillProcess(Process.GetProcessesByName(fileNameWithoutExt)[0].Id);
                    Thread.Sleep(2 * 1000);
                }
                
                if (extension.Equals(".zip"))
                {
                    ExtractZipContent(Path.Combine(_currentPath, downloadFileName), Path.Combine(_currentPath, unzipDirectory));
                    MoveFilesAndDirectories(Path.Combine(_currentPath, unzipDirectory), prevPath);
                }
                else
                {
                    MoveFile(Path.Combine(_currentPath, downloadFileName), prevPath);
                }

                SetVersion(Path.Combine(_currentPath, "info.json"), nextVersion);
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
            finally
            {
                if (Process.GetProcessesByName(fileNameWithoutExt).Length == 0)
                {
                    try
                    {
                        Process.Start(Path.Combine(prevPath, fileNameWithoutExt + ".exe"));

                        Thread.Sleep(1000);
                    }
                    catch(Exception ex)
                    {
                        _log.Error(ex);
                    }
                }
                _log.Info("End update");
            }
        }

        static void KillProcess(int pid)
        {
            ManagementObjectSearcher processSearcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection processCollection = processSearcher.Get();

            if (processCollection != null)
            {
                foreach (ManagementObject mo in processCollection)
                {
                    KillProcess(Convert.ToInt32(mo["ProcessID"]));
                }
            }
            Process proc = Process.GetProcessById(pid);
            if (!proc.HasExited && proc.ProcessName != "UpdateNTerm")
            {
                proc.Kill();
                Thread.Sleep(500);
            }
        }

        private static void MoveFile(string pathFile, string dest)
        {
            string file = Path.GetFileName(pathFile);

            if (File.Exists(Path.Combine(dest, file)))
            {
                File.Delete(Path.Combine(dest, file));
            }
            
            File.Move(pathFile, Path.Combine(dest, file));
        }

        private static void MoveFilesAndDirectories(string sourcePath, string destinationPath)
        {
            foreach(string file in Directory.GetFiles(sourcePath))
            {
                string fileName = Path.GetFileName(file);
                if(File.Exists(Path.Combine(destinationPath, fileName)))
                {
                    File.Delete(Path.Combine(destinationPath, fileName));
                }
                File.Move(file, Path.Combine(destinationPath, fileName));
            }
            foreach(string directory in Directory.GetDirectories(sourcePath))
            {
                string directoryName = Path.GetFileName(directory);
                if(Directory.Exists(Path.Combine(destinationPath, directoryName)))
                {
                    Directory.Delete(Path.Combine(destinationPath, directoryName), true);
                }
                Directory.Move(directory, Path.Combine(destinationPath, directoryName));
            }
        }

        private static void DownloadFile(string urlDownload)
        {
            WebRequest Request = WebRequest.Create(urlDownload);
            WebResponse Response;
            Request.Timeout = 100 * 1000;
            Response = Request.GetResponse();
            byte[] buffer = new byte[2 * 1000 * 1000];
            using (Stream input = Response.GetResponseStream())
            {
                using (FileStream output = new FileStream(Path.Combine(_currentPath, Path.GetFileName(urlDownload)), FileMode.Create))
                {
                    int bytesRead;

                    while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, bytesRead);
                    }
                }
            }
        }

        private static void ExtractZipContent(string zipFile, string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            if (!File.Exists(zipFile))
            {
                throw new FileNotFoundException();
            }

            var shellAppType = Type.GetTypeFromProgID("Shell.Application");
            var oShell = Activator.CreateInstance(shellAppType);
            var destinationFolder = (Folder)shellAppType.InvokeMember("NameSpace", System.Reflection.BindingFlags.InvokeMethod, null, oShell, new object[] { folderPath });
            var sourceFile = (Folder)shellAppType.InvokeMember("NameSpace", System.Reflection.BindingFlags.InvokeMethod, null, oShell, new object[] { zipFile });

            foreach (var file in sourceFile.Items())
            {
                destinationFolder.CopyHere(file, 4 | 16);
            }
        }

        private static bool CheckVersion(out string urlDownload, out float nextVersion)
        {
            urlDownload = "";
            nextVersion = -1.0F;
            bool check = false;
            try
            {
                _hash = CreateMd5Hash(Convert.ToString(_platform) + Convert.ToString(_typeApplication) + _token);
                string URL = $"http://ip:port/route?platform={_platform}&typeApplication={_typeApplication}&hash={_hash}";
                dynamic result = GetResponse(URL);
                float currentVersion = 10000.0F;

                nextVersion = Convert.ToSingle(result.versions);
                urlDownload = Convert.ToString(result.linkToDownload);

                if (!File.Exists(Path.Combine(_currentPath, "info.json")))
                {
                    File.CreateText(Path.Combine(_currentPath, "info.json")).Dispose();
                    SetVersion(Path.Combine(_currentPath, "info.json"), 1.0F);
                }

                string fileText = ReadAllText(Path.Combine(_currentPath, "info.json"));

                dynamic jsonValue = null;
                try
                {
                    jsonValue = JsonConvert.DeserializeObject(fileText);
                }
                catch
                {
                    SetVersion(Path.Combine(_currentPath, "info.json"), 1.0F);
                    fileText = ReadAllText(Path.Combine(_currentPath, "info.json"));
                    jsonValue = JsonConvert.DeserializeObject(fileText);
                }

                if(jsonValue.terminalVersion != null)
                {
                    currentVersion = (float)jsonValue.terminalVersion;
                }
                else
                {
                    SetVersion(Path.Combine(_currentPath, "info.json"), 1.0F);
                    fileText = ReadAllText(Path.Combine(_currentPath, "info.json"));
                    jsonValue = JsonConvert.DeserializeObject(fileText);
                    currentVersion = (float)jsonValue.terminalVersion;
                }
                
                if (currentVersion < nextVersion)
                {
                    check = true;
                }
            }
            catch (Exception ex)
            {
                _log.Error("Method _CheckVersion_", ex);
            }
            return check;
        }

        private static string ReadAllText(string path)
        {
            string res = "";
            using(StreamReader reader = new StreamReader(path))
            {
                res = reader.ReadToEnd();
            }
            return res;
        }

        private static void SetVersion(string path, float version)
        {
            using(StreamWriter writer = new StreamWriter(path))
            {
                JObject jObj = new JObject();
                jObj["terminalVersion"] = version;
                writer.WriteLine(jObj.ToString());
            }
        }

        private static dynamic GetResponse(string url)
        {
            dynamic result = null;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 10000;
            request.Method = "GET";
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string temp = reader.ReadToEnd();
                        result = JsonConvert.DeserializeObject(temp);
                    }
                }
            }
            return result;
        }

        public static string CreateMd5Hash(string input)
        {
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}

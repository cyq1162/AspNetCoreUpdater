
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Reflection;
using AspNetCoreUpdater.Properties;
using System.Diagnostics;

namespace AspNetCoreUpdater
{
    //for .net framework
    partial class Program
    {
        static Assembly _zipLib;
        static Assembly zipLib
        {
            get
            {
                if (_zipLib == null)
                {
                    _zipLib = Assembly.Load(Resources.ICSharpCode_SharpZipLib);
                }
                return _zipLib;
            }
        }
        static void Start() { StartForWindow(); }
        static void StartForWindow()
        {
            Write("1、初始化配置文件中...");
            string zipName = null, iisAppPoolName = null;
            InitIni(out zipName, out iisAppPoolName);
            if (!string.IsNullOrEmpty(zipName) && !string.IsNullOrEmpty(iisAppPoolName))
            {
                string zipFile = runPath + zipName;
                if (!File.Exists(zipFile))
                {
                    WriteAndExit("Can't find the file : " + zipFile);
                }
                Write("2、读取并解压Zip文件中...");
                List<string> dllNames = GetZipDllNames(zipFile);

                Write("...");
                if (dllNames.Count > 0)
                {
                    try
                    {
                        int i = 0;
                        foreach (string dllName in dllNames)
                        {
                            i++;
                            string dllFile = runPath + dllName;
                            if (File.Exists(dllFile))
                            {
                                string temp = "__" + DateTime.Now.Ticks + i + ".temp";
                                File.Move(dllFile, runPath + temp);
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        WriteAndExit(err.Message);
                    }

                }
                Write("...");
                ExtractToDirectory(zipFile, runPath);

                Write("3、重启 IIS 应用程序池中...");
                string[] items = iisAppPoolName.Split(',');
                foreach (var item in items)
                {
                    ReStartApp(item);
                }
                Console.WriteLine("-------------升级完成-------------");
                Write("4、等待应用程序结束并清理缓存文件中，预计1分钟左右...");
                while (true)
                {
                    System.Threading.Thread.Sleep(3000);
                    try
                    {
                        string[] files = Directory.GetFiles(runPath, "*.temp");
                        foreach (var file in files)
                        {
                            File.Delete(file);
                        }
                        break;
                    }
                    catch (Exception err)
                    {
                        Write("...");
                        System.Threading.Thread.Sleep(5000);
                    }

                }
                Thread thread = new Thread(new ThreadStart(Exit));
                thread.Start();

                Write("清理完成，5秒后自动退出。");
            }
        }
        static void Exit()
        {
            Thread.Sleep(5000);
            System.Environment.Exit(0);
        }
        static void InitIni(out string zipName, out string iisAppPoolName)
        {
            zipName = null;
            iisAppPoolName = null;
            try
            {
                string ini = runPath + "AspNetCoreUpdaterForWindow.ini";
                if (!File.Exists(ini))
                {
                    WriteAndExit("Can't find the AspNetCoreUpdaterForWindow.ini");
                }
                string[] items = File.ReadAllLines(ini);
                foreach (string item in items)
                {
                    if (!string.IsNullOrEmpty(item) && !item.StartsWith("#"))
                    {
                        string[] keyValue = item.Split('=');
                        if (keyValue[0] == "zipName" && keyValue.Length > 0)
                        {
                            zipName = keyValue[1].Trim();
                        }
                        else if (keyValue[0] == "iisAppPoolName" && keyValue.Length > 0)
                        {
                            iisAppPoolName = keyValue[1].Trim();
                        }
                    }
                }
                if (string.IsNullOrEmpty(zipName) || string.IsNullOrEmpty(iisAppPoolName))
                {
                    WriteAndExit("AspNetCoreUpdaterForWindow.ini zipName or iisAppPoolName can't be empty.");
                }
            }
            catch (Exception err)
            {
                WriteAndExit(err.Message);
            }
        }
        static List<string> GetZipDllNames(string zipFile)
        {

            List<string> dllNames = new List<string>();
            try
            {
                object zipStream = zipLib.CreateInstance("ICSharpCode.SharpZipLib.Zip.ZipInputStream", false, BindingFlags.CreateInstance, null, new object[] { File.Open(zipFile, FileMode.Open) }, null, null);
                Type zipType = zipStream.GetType();

                while (true)
                {
                    object zipEntry = zipType.GetMethod("GetNextEntry").Invoke(zipStream, null);
                    if (zipEntry == null) { break; }
                    string name = zipEntry.ToString();// zipEntry.GetType().GetProperty("Name").GetValue(zipEntry, null).ToString();
                    if (name.EndsWith(".dll"))
                    {
                        dllNames.Add(name);
                    }
                }
            }
            catch (Exception err)
            {
                WriteAndExit(err.Message);
            }
            return dllNames;
        }
        static void ExtractToDirectory(string zipFile, string toFolder)
        {
            try
            {
                object fastZip = zipLib.CreateInstance("ICSharpCode.SharpZipLib.Zip.FastZip", false, BindingFlags.CreateInstance, null, null, null, null);
                Type zipType = fastZip.GetType();
                MethodInfo[] methods = zipType.GetMethods();
                foreach (var method in methods)
                {
                    if (method.Name == "ExtractZip" && method.GetParameters().Length == 3)
                    {
                        method.Invoke(fastZip, new object[] { zipFile, toFolder, "" });
                        break;
                    }
                }
                System.Threading.Thread.Sleep(1000);//等待解压完成。
            }
            catch (Exception err)
            {
                WriteAndExit(err.Message);
            }
        }
        static void ReStartApp(string appPool)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = @"c:\Windows\System32\inetsrv\appcmd.exe";
                info.Arguments = @"recycle apppool " + appPool;
                info.UseShellExecute = false;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                Process.Start(info);
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
            }
        }
    }
}

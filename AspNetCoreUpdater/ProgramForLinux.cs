using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AspNetCoreUpdater
{
    partial class Program
    {
        static void Start() { StartForLinux(); }
        static void StartForLinux()
        {
            Write("1、初始化配置文件中...");
            string zipName = null, nginxPath = null, dotnetCmd = null, dotnetKillPort = null;
            InitIni(out zipName, out nginxPath, out dotnetCmd, out dotnetKillPort);
            if (!string.IsNullOrEmpty(zipName) && !string.IsNullOrEmpty(nginxPath))
            {
                string zipFile = runPath + zipName;
                if (!File.Exists(zipFile))
                {
                    Console.WriteLine("Can't find the file : " + zipFile);
                }
                Write("2、读取并解压Zip文件中...");
                ExtractToDirectory(zipFile, runPath);
                Write("3、启动新DotNet应用程序中...");
                StartNewDotNet(dotnetCmd);
                Write("4、修改Ngix配置并重启中...");
                ReadNgix(nginxPath, dotnetKillPort, dotnetCmd);
                Console.WriteLine("-------------升级完成-------------");
                Write("5、等待原旧应用程序处理完旧逻辑，预计等待1分钟...");
                KillPort(dotnetKillPort);
            }
            Thread thread = new Thread(new ThreadStart(Exit));
            thread.Start();

            Write("任务完成，5秒后自动退出。");
        }
        static void Exit()
        {
            Thread.Sleep(5000);
            System.Environment.Exit(0);
        }
        static void InitIni(out string zipName, out string nginxPath, out string dotnetCmd, out string dotnetKillPort)
        {
            zipName = null;
            nginxPath = null;
            dotnetCmd = null;
            dotnetKillPort = null;
            try
            {
                string ini = runPath + "AspNetCoreUpdaterForLinux.ini";
                if (!File.Exists(ini))
                {
                    WriteAndExit("Can't find the AspNetCoreUpdaterForLinux.ini");
                }
                string[] items = File.ReadAllLines(ini);
                foreach (string item in items)
                {
                    if (!string.IsNullOrEmpty(item) && !item.StartsWith("#"))
                    {
                        string[] keyValue = item.Split('=');
                        if (keyValue.Length > 0)
                        {
                            string key = keyValue[0];
                            if (key == "zipName")
                            {
                                zipName = item.Substring(key.Length + 1).Trim();
                            }
                            else if (key == "nginxPath")
                            {
                                nginxPath = item.Substring(key.Length + 1).Trim();
                            }
                            else if (key == "dotnetCmd")
                            {
                                dotnetCmd = item.Substring(key.Length + 1).Trim();
                            }
                            else if (key == "dotnetKillPort")
                            {
                                dotnetKillPort = item.Substring(key.Length + 1).Trim();
                            }
                        }
                    }
                }
                if (string.IsNullOrEmpty(zipName) || string.IsNullOrEmpty(nginxPath))
                {
                    WriteAndExit("AspNetCoreUpdaterForLinux.ini zipName or nginxPath can't be empty.");
                }
            }
            catch (Exception err)
            {
                WriteAndExit(err.Message);
            }
        }
        static void ExtractToDirectory(string zipFile, string toFolder)
        {
            ZipFile.ExtractToDirectory(zipFile, toFolder, Encoding.UTF8, true);
            System.Threading.Thread.Sleep(1000);//等待解压完成。
        }
        static void StartNewDotNet(string dotnetCmd)
        {
            //如果存在，则不处理
            string port = GetPort(dotnetCmd);
            if (!string.IsNullOrEmpty(port))
            {
                RunCmd("lsof -i:" + port);
                if (!string.IsNullOrEmpty(dotNetPID))
                {
                    Write("  "+port + " 端口已启动该程序中...");
                    return;
                }
            }
            //1、执行新命令：
            string[] items = dotnetCmd.Split(',');
            foreach (string item in items)
            {
                RunCmd(item);
            }
        }
        static void ReadNgix(string nginxPath, string oldPort, string dotnetCmd)
        {
            
            //1、复制
            string confName = nginxPath.Substring(nginxPath.LastIndexOf('/') + 1);
            string nginxFile = runPath + confName;
            if (File.Exists(nginxFile))
            {
                Write("  Nginx配置转移中...");
                File.Move(nginxFile, nginxPath, true);
            }
            else if (!string.IsNullOrEmpty(oldPort) && !oldPort.Contains(",") && !string.IsNullOrEmpty(dotnetCmd) && dotnetCmd.Contains(':'))
            {
                if (File.Exists(nginxPath))
                {
                    Write("  Nginx配置修改中...");
                    string newPort = GetPort(dotnetCmd);
                    string text = File.ReadAllText(nginxPath, Encoding.UTF8);
                    if (text.Contains(":" + oldPort.Trim()))
                    {
                        text = text.Replace(":" + oldPort, ":" + newPort);
                    }
                    File.WriteAllText(nginxPath, text, Encoding.UTF8);
                }
            }
            Write("  重新加载Nginx配置中...");
            //刷新nginx
            RunCmd("nginx -s reload");
        }
        static void KillPort(string port)
        {
            //Thread.Sleep(60000);
            Write("  关闭旧应用程序【kill " + port + "】中...");//kill -9 $(netstat alp | grep:5001)
            string[] items = port.Split(',');
            foreach (string item in items)
            {
                RunCmd("lsof -i:" + item.Trim());
                if (!string.IsNullOrEmpty(dotNetPID))
                {
                    RunCmd("kill -9 " + dotNetPID);
                }
            }
        }
        static string dotNetPID = "";
        static void RunCmd(string cmd)
        {
            Write("  执行命令：" + cmd);
            string cmdName, cmdArg;

            string[] item = cmd.Split(' ');
            cmdName = item[0];
            cmdArg = cmd.Substring(cmdName.Length + 1);
            if (cmdName.ToLower() == "dotnet")
            {
                Process.Start("cd", runPath);
            }
            if (cmdName == "lsof")
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = cmdName;
                    process.StartInfo.Arguments = cmdArg;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    dotNetPID = GetPID(process.StandardOutput.ReadToEnd());
                }
            }
            else
            {
                Process.Start(cmdName, cmdArg);
            }

        }
        private static string GetPID(string result)
        {
            //Write("lsof 命令Outpu回调数据：" + result);
            if (!string.IsNullOrEmpty(result))
            {
                string[] items = result.Split(' ', '\n', '\t', '\r');
                bool chkPID = false;
                foreach (string item in items)
                {
                    if (!string.IsNullOrEmpty(item))
                    {
                        if (item.Trim() == "dotnet") { chkPID = true; }
                        else if (chkPID)
                        {
                            return item.Trim();
                        }
                    }
                }
            }
            return string.Empty;
        }
        private static string GetPort(string dotnetCmd)
        {
            string port = dotnetCmd.Substring(dotnetCmd.LastIndexOf(':') + 1);
            return port.Trim();
        }
    }
}

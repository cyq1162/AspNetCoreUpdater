/*
 * 作者：路过秋天
 * 博客：https://www.cnblogs.com/cyq1162
 * 开源：https://github.com/cyq1162
 */
using System;
using System.IO;


namespace AspNetCoreUpdater
{
    partial class Program
    {
        static string runPath = AppDomain.CurrentDomain.BaseDirectory;
        static void Main(string[] args)
        {
            Start();
            Console.Read();
        }
       
        static void Write(string msg)
        {
            Console.WriteLine(msg);
        }
        static void WriteAndExit(string msg)
        {
            Console.WriteLine(msg);
            Console.Read();
            System.Environment.Exit(0);
        }
    }
}

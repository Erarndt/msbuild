using CLRDetourWrapper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleAppFramework
{
    internal class Program
    {
        static AsyncLocal<string> AsyncLocalString = new AsyncLocal<string>();
        static AsyncLocal<int> AsyncLocalInt = new AsyncLocal<int>();
        static async Task Main(string[] args)
        {
            while (!Debugger.IsAttached)
            {
                Thread.Sleep(50);
            }
            string ev = Environment.GetEnvironmentVariable("APPDATA");
            string fp = Path.GetFullPath("foo\\..\\test.txt");
            var wrapper2 = new DetourWrapper(AsyncLocalString);
            AsyncLocalString.Value = @"C:\src\roslyn\..\";

            string fp2 = Path.GetFullPath("foo\\..\\test.txt");

            string currentDir = Environment.CurrentDirectory;
            ;
            Environment.CurrentDirectory = "a";
            ;

            Process.Start(new ProcessStartInfo
            {
                FileName = @"C:\Users\erarndt\source\repos\ConsoleApp2\ConsoleApp2\bin\Debug\net472\ConsoleApp2.exe"
            });

            string dir = Environment.CurrentDirectory;

            AsyncLocalString.Value = "first";
            string fullName = Path.GetFileName("foo");

            dir = Environment.CurrentDirectory;
            AsyncLocalInt.Value = 300;
            Task t1 = WriteToConsoleAsync();

            AsyncLocalString.Value = "second";
            dir = Environment.CurrentDirectory;
            AsyncLocalInt.Value = 500;
            Task t2 = WriteToConsoleAsync();

            await t1;
            await t2;
            Console.WriteLine("Hello, World!");

            Console.WriteLine("Done sleeping");
            ;
        }

        static async Task WriteToConsoleAsync()
        {
            await Task.Delay(500);
            Console.WriteLine("Starting " + AsyncLocalString.Value);
            Stopwatch stopwatch = Stopwatch.StartNew();
            Thread.Sleep(1000);
            stopwatch.Stop();
            Console.WriteLine("Current Directory " + Path.GetFullPath("TestFile.cs"));
            Console.WriteLine("Ending " + AsyncLocalString.Value);
            Console.WriteLine("Duration " + stopwatch.ElapsedMilliseconds);
        }
    }
}

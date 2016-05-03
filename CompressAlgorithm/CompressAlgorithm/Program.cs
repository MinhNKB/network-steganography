using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using System.Diagnostics;

namespace CompressAlgorithm
{
    class Program
    {
        static int totalBytes = 0;
        static float totalCompressRatio = 0;
        static double totalTime = 0;

        static void Main()
        {
            string[] fileEntries = Directory.GetFiles("Dataset", "*.txt");
            for (int i = 0; i < fileEntries.Length; ++i)
            {
                Console.WriteLine((i + 1) + "/" + fileEntries.Length + " file(s) executed");
                Run(fileEntries[i]);
            }
            StreamWriter writer = new StreamWriter("Result.txt");
            writer.WriteLine("Average size (in byte): " + ((float)totalBytes / (float)fileEntries.Length));
            writer.WriteLine("Average ratio: " + ((float)totalCompressRatio / (float)fileEntries.Length));
            writer.WriteLine("Average time: " + ((float)totalTime / (float)fileEntries.Length));
            writer.Close();
        }

        private static void Run(string fileName)
        {
            DateTime startTime = DateTime.Now;
            string data = File.ReadAllText(fileName);
            byte[] byteData = Encoding.ASCII.GetBytes(data);


            //Using Gzip
            //byte[] compress = CompressUsingGzip(byteData);

            //Using Bzip2
            //BZip2.Compress(File.OpenRead(fileName), File.Create("tmp.txt"), true, 4096);
            //byte[] compress = Encoding.ASCII.GetBytes(File.ReadAllText("tmp.txt"));

            ProcessStartInfo p = new ProcessStartInfo();
            p.FileName = @"C:\Program Files\7-Zip\7z.exe";

            // 2
            // Use 7-zip
            // specify a=archive and -tgzip=gzip
            // and then target file in quotes followed by source file in quotes
            //
            p.Arguments = "a " + fileName + ".zip " + fileName;
            p.WindowStyle = ProcessWindowStyle.Hidden;

            // 3.
            // Start process and wait for it to exit
            //
            Process x = Process.Start(p);
            x.WaitForExit();
            byte[] compress = Encoding.ASCII.GetBytes(File.ReadAllText(fileName + ".zip"));

            totalBytes += byteData.Length;
            totalCompressRatio += (((float)compress.Length) / ((float)byteData.Length));
            totalTime += (DateTime.Now.Subtract(startTime).TotalSeconds);
        }

        /// <summary>
        /// Compresses byte array to new byte array.
        /// </summary>
        public static byte[] CompressUsingGzip(byte[] raw)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(memory,
                CompressionLevel.Optimal, true))
                {
                    gzip.Write(raw, 0, raw.Length);
                }
                return memory.ToArray();
            }
        }
    }
}

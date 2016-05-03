using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CompressAlgorithm
{
    class Program
    {
        
        static void Main()
        {
            string data = "This data";
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            byte[] compress = CompressUsingGzip(byteData);
            Console.WriteLine(byteData.Length);
            Console.WriteLine(compress.Length);

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

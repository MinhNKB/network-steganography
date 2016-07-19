using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ReceiverTCP
{
    class Program
    {
        static string ip;
        static int startPort;
        static int delay;
        static int numberOfRunTimes;
        static int startIndex;
        static int numberOfThreads;
        static int compressAlgorithm;

        static string stringOriginalData;

        static Exception ex = null;

        static int[] numberOfPortsArray;

        static int currentNumberOfPortsIndex = 0;

        static void Main(string[] args)
        {
            inputHostInfo();

            StreamReader originalDataReader = new StreamReader("Data.txt");
            stringOriginalData = originalDataReader.ReadToEnd();
            originalDataReader.Close();

            //int count = startIndex;
            for (int count = startIndex; count < numberOfRunTimes; ++count)
            {
                for (currentNumberOfPortsIndex = 0; currentNumberOfPortsIndex < numberOfPortsArray.Length; ++currentNumberOfPortsIndex)
                {
                    numberOfThreads = numberOfPortsArray[currentNumberOfPortsIndex];

                    StreamWriter resultWriter = new StreamWriter("Result.txt", true);
                    resultWriter.WriteLine("---------- Number of ports used: {0}, Index: {1} ----------", numberOfThreads, count);
                    resultWriter.Close();
                    delay = 450;
                    for (int i = 0; i < 1; ++i)
                    {
                        List<Receiver> receivers = new List<Receiver>();
                        List<Thread> threads = new List<Thread>();
                        try
                        {

                            for (int j = 0; j < numberOfThreads; ++j)
                            {
                                Receiver receiver = new Receiver(ip, (startPort + j), delay, count, compressAlgorithm != -1);
                                Thread thread = new Thread(receiver.run);
                                thread.Start();
                                receivers.Add(receiver);
                                threads.Add(thread);
                            }

                            for (int j = 0; j < threads.Count; ++j)
                            {
                                threads[j].Join();
                            }


                            writeFinishMessage(receivers, count);
                            delay -= 50;
                        }
                        catch (Exception ex)
                        {
                            writeLineLogMessage(ex.ToString());
                            for (int j = 0; j < threads.Count; ++j)
                                threads[j].Abort();
                            --i;
                        }
                    }
                }
            }
                    
                
        }

        private static string decompressData(string compressedBinaryData)
        {
            byte[] data = Receiver.convertStringBytesToBytes(compressedBinaryData);
            return System.Text.Encoding.UTF8.GetString(decompress(data));
        }

        private static byte[] decompress(byte[] gzip)
        {
            // Create a GZIP stream with decompression mode.
            // ... Then create a buffer and write into while reading from the GZIP stream.
            using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }

        private static void writeLineLogMessage(string message)
        {
            StreamWriter writer = new StreamWriter("Log_main.txt", true);
            writer.WriteLine("{0}: {1}", DateTime.Now.ToString(), message);
            writer.Close();
        }

        private static void writeFinishMessage(List<Receiver> receivers, int count)
        {
            Console.WriteLine("Finished!");


            StreamWriter binaryWriter = new StreamWriter("(" + count + ")-(Ports_" + numberOfThreads + ")-" + delay + "-Binary" + ".txt", false);
            StreamWriter stringWriter = new StreamWriter("(" + count + ")-(Ports_" + numberOfThreads + ")-" + delay + "-String" + ".txt", false);
            StreamWriter resultWriter = new StreamWriter("Result.txt", true);

            string binaryData = getBinaryData(receivers);
            binaryWriter.Write(binaryData);
            binaryWriter.Close();

            string stringData = "";
            if (compressAlgorithm == -1)
                stringData = getStringData(receivers);
            else if (compressAlgorithm == 0)
                stringData = decompressData(binaryData);

            stringWriter.Write(stringData);
            stringWriter.Close();


            int countSimilar = CountSimilarBetweenTwoString(stringOriginalData, stringData);
            double percentage = (double)countSimilar / (double)stringOriginalData.Length * 100;

            int numberOfACKs = getNumberOfACKs(receivers);
            int numberOfNACKs = getNumberOfNACKs(receivers);

            resultWriter.WriteLine("\tDelay " + delay);
            resultWriter.WriteLine("\t\tNumber of ACK: " + numberOfACKs);
            resultWriter.WriteLine("\t\tNumber of NACK: " + numberOfNACKs);
            resultWriter.WriteLine("\t\tTime: " + DateTime.Now.Subtract(receivers[0].StartTime).TotalSeconds);
            resultWriter.WriteLine("\t\tSimilar characters: " + countSimilar.ToString());
            resultWriter.WriteLine("\t\tString length: " + stringOriginalData.Length);
            resultWriter.WriteLine("\t\tPercentage: " + percentage.ToString("00.00"));
            resultWriter.Close();
        }

        private static int getNumberOfNACKs(List<Receiver> receivers)
        {
            int result = 0;
            for (int i = 0; i < receivers.Count; ++i)
                result += receivers[i].NumberOfNACKs;
            return result;
        }

        private static int getNumberOfACKs(List<Receiver> receivers)
        {
            int result = 0;
            for (int i = 0; i < receivers.Count; ++i)
                result += receivers[i].NumberOfACKs;
            return result;
        }

        private static string getStringData(List<Receiver> receivers)
        {
            string result = "";
            for (int i = 0; i < receivers.Count; ++i)
                result += receivers[i].StringData;
            return result;
        }

        private static string getBinaryData(List<Receiver> receivers)
        {
            string result = "";
            for (int i = 0; i < receivers.Count; ++i)
                result += receivers[i].BinaryData;
            return result;
        }

        private static int CountSimilarBetweenTwoString(string dataString, string resultString)
        {
            if (dataString.Length != resultString.Length)
                return -1;
            int count = 0;
            for (int i = 0; i < dataString.Length; i++)
                if (dataString[i] == resultString[i])
                    count++;
            return count;
        }

        private static void inputHostInfo()
        {
            StreamReader reader = new StreamReader("HostInfo.txt");
            ip = "192.168.1.200";
            startPort = Int32.Parse(reader.ReadLine());
            numberOfRunTimes = 50;
            startIndex = 0;
            //numberOfThreads = Int32.Parse(reader.ReadLine());
            compressAlgorithm = -1;
            //numberOfPortsArray = new int[18] { 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200};
            numberOfPortsArray = new int[1] { 1 };
            reader.Close();
        }
    }
}

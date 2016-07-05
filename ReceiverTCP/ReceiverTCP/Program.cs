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
        static int port;
        static int delay;
        static int numberOfRunTimes;
        static int startIndex;
        static int compressAlgorithm;

        static string stringOriginalData;

        static TcpListener tcpListener;
        static TcpClient tcpClient;
        static NetworkStream networkStream;
        static readonly Object networkStreamLock = new Object();

        //static int[] numberOfThreadsArray = new int[1] {1};
        static int[] numberOfThreadsArray;
        static int[] delayTypes;

        static void Main(string[] args)
        {
            inputHostInfo();

            StreamReader originalDataReader = new StreamReader("Data.txt");
            stringOriginalData = originalDataReader.ReadToEnd();
            originalDataReader.Close();

            for (int i = 0; i < numberOfThreadsArray.Length; ++i)
            {
                for (int j = 0; j < numberOfRunTimes; ++j)
                {
                    
                    for (int k = 0; k < delayTypes.Length; ++k)
                    {
                        delay = delayTypes[k];
                        StreamWriter resultWriter = new StreamWriter("Result.txt", true);
                        resultWriter.WriteLine("---------- Number of threads used: {0}, Index: {1} ----------", numberOfThreadsArray[i], j);
                        resultWriter.WriteLine("delay");
                        resultWriter.Close();
                        try
                        {

                            initConnection();

                            List<Receiver> receivers = new List<Receiver>();
                            int numberOfFinishSignals = 0;

                            for (int l = 0; l < numberOfThreadsArray[i]; ++l)
                            {
                                Receiver receiver = new Receiver(delay, j, l, networkStream, networkStreamLock);
                                receivers.Add(receiver);
                            }

                            while (numberOfFinishSignals < numberOfThreadsArray[i])
                            {
                                byte[] receivedPacket = receivePacket();
                                int index = Convert.ToInt32(receivedPacket[0]);
                                //Thread thread = new Thread(() => receivers[index].processNewPacket(receivedPacket, DateTime.Now));
                                //thread.Start();

                                receivers[0].processNewPacket(receivedPacket, DateTime.Now);
                                //receivers[index].processNewPacket(receivedPacket, DateTime.Now);
                                if (receivedPacket[1] == 1)
                                    ++numberOfFinishSignals;
                            }
                            //Thread sleepThread = new Thread(() => sleep(5000));
                            //sleepThread.Start();
                            //sleepThread.Join();

                            tcpListener.Stop();
                            tcpClient.Close();
                            networkStream.Close();
                            writeFinishMessage(receivers, j, numberOfThreadsArray[i]);
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                k--;
                                Console.WriteLine("Main: {0}", ex.ToString());
                                writeLineLogMessage(ex.ToString());
                                tcpListener.Stop();
                                tcpClient.Close();
                                networkStream.Close();
                            }
                            catch (Exception) { }
                            //throw new Exception("Port " + port + ": " + ex.ToString());
                        }
                    }
                }
            }
        }

        private static void sleep(int millisecond)
        {
            Thread.Sleep(millisecond);
        }

        private static byte[] receivePacket()
        {
            //lock (networkStreamLock)
            {
                byte[] tcpPacket = new byte[2];
                networkStream.Read(tcpPacket, 0, tcpPacket.Length);
                return tcpPacket;
            }
        }

        private static void initConnection()
        {
            tcpListener = new TcpListener(IPAddress.Parse(ip), port);
            tcpListener.Start();

            Console.WriteLine("Waiting for sender...");
            writeLineLogMessage("Waiting for sender...");
            tcpClient = tcpListener.AcceptTcpClient();

            Console.WriteLine("Connected!");
            writeLineLogMessage("Connected!");
            networkStream = tcpClient.GetStream();
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

        private static void writeFinishMessage(List<Receiver> receivers, int runTimesIndex, int numberOfThreads)
        {
            Console.WriteLine("Finished!");


            StreamWriter binaryWriter = new StreamWriter("(" + runTimesIndex + ")-(Threads_" + numberOfThreads + ")-" + delay + "-Binary" + ".txt", false);
            StreamWriter stringWriter = new StreamWriter("(" + runTimesIndex + ")-(Threads_" + numberOfThreads + ")-" + delay + "-String" + ".txt", false);
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
            //StreamReader reader = new StreamReader("HostInfo.txt");
            ip = "192.168.1.200";
            port = 4040;
            delay = 100;
            numberOfRunTimes = 50;
            startIndex = 0;
            compressAlgorithm = -1;
            numberOfThreadsArray = new int[1] { 1 };
            delayTypes = new int[5] { 80, 90, 100, 110, 120 };
            //reader.Close();
        }
    }
}

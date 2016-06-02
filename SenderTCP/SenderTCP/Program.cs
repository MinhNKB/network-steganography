using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SenderTCP
{
    class Program
    {       
        static string IP;
        static int port;
        static int[] listOfDelays;
        static int delay;
        static int timesToRun;
        static int[] arrayOfNumbersOfThreads;
        static int numberOfThreads;
        static bool useOnePort;
        static bool useZip;

        static string content;    
        static int countACK, countNACK;                                      
        static string[] binaryContents;

        static TcpClient[] tcpClients;
        static NetworkStream[] networkStreams;
        static List<Thread> threads;
        
        static void Main(string[] args)
        {        
            Log.WriteLine("Loged");
            inputReceiverInfo();

            for (int k = 0; k < arrayOfNumbersOfThreads.Length; k++)
            {
                numberOfThreads = arrayOfNumbersOfThreads[k];
                binaryContents = new string[numberOfThreads];
                if (!useOnePort)
                {
                    tcpClients = new TcpClient[numberOfThreads];
                    networkStreams = new NetworkStream[numberOfThreads];
                }
                else
                {
                    tcpClients = new TcpClient[1];
                    networkStreams = new NetworkStream[1];
                }
                
                prepareBinaryData();
                for (int i = 0; i < timesToRun; ++i)
                {
                    try
                    {
                        delay = listOfDelays[i % listOfDelays.Length];
                        countACK = 0;
                        countNACK = 0;
                        threads = new List<Thread>();
                        Console.WriteLine("------------The " + i + "th run------------");
                        Console.WriteLine("Run with delay: " + delay.ToString());
                        for (int j = 0; j < numberOfThreads; j++)
                        {                            
                            Thread thread = new Thread(new ParameterizedThreadStart(Program.run));
                            threads.Add(thread);
                            if (!useOnePort)
                            {
                                connectToReceiver(j);
                                thread.Start(j);
                            }
                            else
                            {
                                if(j==0)
                                    connectToReceiver(0);
                                thread.Start(0);
                            }
                        }
                        foreach (Thread thread in threads)
                            thread.Join();                                                         
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception: " + ex.Message);
                        foreach (Thread thread in threads)                            
                            thread.Abort();
                        i--;
                        Thread.Sleep(5000);
                    }
                    finally
                    {                        
                        threads.Clear();
                        foreach (TcpClient tcpClient in tcpClients)
                            tcpClient.Close();
                        foreach (NetworkStream networkStream in networkStreams)
                            networkStream.Close();       
                    }
                }               
            }
            Log.Close();
            Console.ReadKey();
        }
       
        private static void run(object obj)
        {
            int index = (int)obj;
                                               
            int i = 0;
            string temp = "";
            bool checkNew = true;           

            while (i <= binaryContents[index].Length)
            {
                if (i == binaryContents[index].Length)
                    sendAPacket(index, 1);
                else
                {
                    if (checkNew)
                    {
                        temp = "";
                        byte u;
                        for (int j = 0; j < 8; j++)
                        {
                            temp += binaryContents[index][i + j];
                            string k = "" + binaryContents[index][i + j];
                        }
                        if (temp != "00000000")
                        {
                            u = Convert.ToByte(temp, 2);
                            byte crc = Crc8.ComputeChecksum(u);
                            temp += Convert.ToString(crc, 2).PadLeft(8, '0');
                        }

                    }
                    if (temp == "00000000")
                    {
                        sendAPacket(index, 0);
                    }
                    else
                    {
                        sendAPacket(index, 2);
                        for (int j = 0; j < temp.Length; j++)
                        {
                            if (temp[j] == '1')
                            {
                                Thread.Sleep(delay);
                                sendAPacket(index, 2);
                            }
                            else if (temp[j] == '0')
                                sendAPacket(index, 2);
                        }
                    }
                }
                Console.WriteLine(index + "Waiting for ack");
                byte[] ACK = new byte[1];
                networkStreams[index].Read(ACK, 0, 1);
                Console.WriteLine(index + "acked");
                if (ACK[0] == 1)
                {
                    i += 8;
                    checkNew = true;
                    countACK++;
                    Console.WriteLine("ACK " + index + " : " + countACK);
                }
                else
                {
                    checkNew = false;
                    countNACK++;
                    Console.WriteLine("NACK " + index + " : " + countNACK);
                }

            }
            Console.WriteLine(index + " finished!");                                
        }

        private static void connectToReceiver(int index)
        {
            tcpClients[index] = new TcpClient(IP, port + index);
            Console.WriteLine("Port: " + (port + index));
            tcpClients[index].NoDelay = true;
            networkStreams[index] = tcpClients[index].GetStream();
        }

        private static void prepareBinaryData()
        {
            StreamReader fileReader = new StreamReader("Data.txt");
            content = fileReader.ReadToEnd();

            byte[] byteData = Encoding.ASCII.GetBytes(content);
            if(useZip)
                byteData = CompressUsingGzip(byteData);
            List<byte> listOfData = new List<byte>(byteData);          

            int sizeOfAChunk = (int)Math.Ceiling((double)byteData.Length / numberOfThreads);
            
            for (int i = 0; i < numberOfThreads; i++)
            {
                if (listOfData.Count > 0)
                {
                    if (i == numberOfThreads - 1)
                        binaryContents[i] = ToBinary(listOfData.ToArray()).Replace(" ", "");
                    else
                    {
                        if (sizeOfAChunk < listOfData.Count)
                        {
                            binaryContents[i] = ToBinary(listOfData.GetRange(0, sizeOfAChunk).ToArray()).Replace(" ", "");
                            listOfData.RemoveRange(0, sizeOfAChunk);
                        }
                        else
                        {
                            binaryContents[i] = ToBinary(listOfData.ToArray()).Replace(" ", "");
                            listOfData.Clear();
                        }
                    }
                }
                else
                    binaryContents[i] = "";
            }
                                                       
            //To-do: apply compress algorithm here
                
            fileReader.Close();
        }

        private static string ConvertStringToBinaryString(string iContent)
        {
            string lResult = ToBinary(ConvertToByteArray(iContent, Encoding.ASCII));
            lResult = lResult.Replace(" ", "");
            return lResult;
        }

        private static void inputReceiverInfo()
        {
            StreamReader reader = new StreamReader("ReceiverInfo.txt");
            string line;
            line = reader.ReadLine();
            IP = line.Remove(0, line.IndexOf(' ') + 1);
            line = reader.ReadLine();
            port = Int32.Parse(line.Remove(0, line.IndexOf(' ') + 1));

            line = reader.ReadLine();
            string[] delayString = line.Remove(0, line.IndexOf(' ') + 1).Split(' ');            
            listOfDelays = new int[delayString.Length];
            for (int i = 0; i < delayString.Length; i++)
            {
                listOfDelays[i] = Int32.Parse(delayString[i]);
            }

            line = reader.ReadLine();
            timesToRun = Int32.Parse(line.Remove(0, line.IndexOf(' ') + 1)) * listOfDelays.Length;

            line = reader.ReadLine();
            string[] numberOfPortString = line.Remove(0, line.IndexOf(' ') + 1).Split(' ');
            arrayOfNumbersOfThreads = new int[numberOfPortString.Length];
            for (int i = 0; i < numberOfPortString.Length;i++ )
            {
                arrayOfNumbersOfThreads[i] = Int32.Parse(numberOfPortString[i]);
            }

            line = reader.ReadLine();
            useOnePort = line.Remove(0, line.IndexOf(' ') + 1) == "true" ? true : false;
            line = reader.ReadLine();
            useZip = line.Remove(0, line.IndexOf(' ') + 1) == "true" ? true : false;
            reader.Close();
        }
        
        static void sendAPacket(int index, byte data)
        {
            if (data == 0 || data == 1)
                Console.WriteLine("Port " + index + " send: " + data);
            byte[] packet = new byte[1] {data};
            networkStreams[index].Write(packet, 0, packet.Length);
        }
        
        static void sendPacket(int index,Object obj)
        {
            byte[] data = objectToByteArray(obj);
            networkStreams[index].Write(data, 0, data.Length);
        }

        static byte[] objectToByteArray(Object obj)
        {
            if (obj == null)
                return null;
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        public static byte[] ConvertToByteArray(string str, Encoding encoding)
        {
            return encoding.GetBytes(str);
        }

        public static String ToBinary(Byte[] data)
        {
            return string.Join(" ", data.Select(byt => Convert.ToString(byt, 2).PadLeft(8, '0')).ToArray());
        }

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

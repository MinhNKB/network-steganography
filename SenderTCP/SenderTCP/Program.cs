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
        static TcpClient tcpClient;
        static NetworkStream networkStream;

       
        static string IP;
        static int port;
        static int timesToRun;

        //static int delay;
        static int countACK, countNACK;
        
        static string content;
        static string binaryContent;
      
        const int DELAY_COUNT = 1;

        static int[] ports = new int[6] {1, 60, 70, 80, 90, 100 };
        
        static int numberOfPort;
        static string[] binaryContents;
        static TcpClient[] tcpClients;
        static NetworkStream[] networkStreams;
        static List<Thread> threads;
        static int delayCount = 0;
        static void Main(string[] args)
        {        
            Log.WriteLine("Loged");
            inputReceiverInfo();

            for (int k = 0; k < ports.Length; k++)
            {
                numberOfPort = ports[k];
                binaryContents = new string[numberOfPort];
                tcpClients = new TcpClient[numberOfPort];
                networkStreams = new NetworkStream[numberOfPort];
                prepareBinaryData();
                for (int i = 0; i < timesToRun; ++i)
                {
                    try
                    {
                        delayCount = i % DELAY_COUNT;
                        countACK = 0;
                        countNACK = 0;
                        threads = new List<Thread>();
                        Console.WriteLine("------------The " + i + "th run------------");
                        Console.WriteLine("Run with delay: " + (delayCount * 200 + 200).ToString());
                        for (int j = 0; j < numberOfPort; j++)
                        {
                            Thread thread = new Thread(new ParameterizedThreadStart(Program.run));
                            threads.Add(thread);
                            thread.Start(j);
                        }
                        foreach (Thread thread in threads)
                            thread.Join();
                      
                        threads.Clear();
                        //if(i+1==timesToRun && delayCount+1<DELAY_COUNT)
                        //{
                        //    delayCount++;
                        //    i = -1;
                        //}

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception: " + ex.Message);
                        foreach (Thread thread in threads)
                            thread.Abort();
                        threads.Clear();
                        i--;
                        Thread.Sleep(5000);
                    }
                }               
            }
            Log.Close();
            Console.ReadKey();
        }
       
        private static void run(object obj)
        {
            int index = (int)obj;

            int delay = delayCount * 200 + 200;                        
            Thread.Sleep(1000);

            int i = 0;
            string temp = "";
            bool checkNew = true;

            connectToReceiver(index);

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

                byte[] ACK = new byte[1];
                networkStreams[index].Read(ACK, 0, 1);
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
            tcpClients[index].Close();
            networkStreams[index].Close();
                     
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
            byte[] compress = CompressUsingGzip(byteData);
            List<byte> listOfData = new List<byte>(byteData);          

            int sizeOfAChunk = (int)Math.Ceiling((double)byteData.Length / numberOfPort);
            
            for (int i = 0; i < numberOfPort; i++)
            {
                if (i == numberOfPort - 1)
                    binaryContents[i] = ToBinary(listOfData.ToArray()).Replace(" ","");// + "00000011";
                else
                {
                    binaryContents[i] = ToBinary(listOfData.GetRange(0,sizeOfAChunk).ToArray()).Replace(" ","");// +"00000011";
                    listOfData.RemoveRange(0, sizeOfAChunk);
                }             
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
            IP = reader.ReadLine();
            port = Int32.Parse(reader.ReadLine());
            timesToRun = Int32.Parse(reader.ReadLine());
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
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
        static string fileName;

        static string content;    
        static int countACK, countNACK;                                      
        static string[] binaryContents;

        static TcpClient[] tcpClients;
        static NetworkStream[] networkStreams;
        static List<Thread> threads;
        static byte[] checkAck;                      
        static void Main(string[] args)
        {           
            inputReceiverInfo();
            for (int i = 0; i < timesToRun; ++i)
            {                
                for (int k = 0; k < arrayOfNumbersOfThreads.Length; k++)
                {
                    Log.WriteLine("------------" + arrayOfNumbersOfThreads[k] + " port------------");                   

                    numberOfThreads = arrayOfNumbersOfThreads[k];
                    checkAck = new byte[numberOfThreads];
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
                            }
                            else
                            {
                                if (j == 0)
                                    connectToReceiver(0);
                            }
                            thread.Start(j);
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
                        Thread.Sleep(5000);
                    }

                }
            }           
            Log.Close();
            Console.WriteLine("ALL FINISHED!!!!!!");
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
                Console.WriteLine(index + " current: " + i);
                checkAck[index] = 2; //reset
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
                //Console.WriteLine(index + "Waiting for signal");
                bool check = CheckForAck(index);                
                if (check)
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

        private static bool CheckForAck(int index)
        {
            if (!useOnePort)
            {
                byte[] ACK = new byte[1];
                networkStreams[index].Read(ACK, 0, 1);
                Console.WriteLine(index + " receive: " + ACK[0]);
                checkAck[index] = ACK[0];
            }
            else
            {
                byte[] ACK = new byte[2];
                networkStreams[0].Read(ACK, 0, 2);
                Console.WriteLine(ACK[0] + " receive: " + ACK[1]);
                checkAck[ACK[0]] = ACK[1];
            }
            while(checkAck[index]==2)
            {
                
            }
            if (checkAck[index] == 1)
                return true;
            else
                return false;
        }

        private static void connectToReceiver(int index)
        {            
            tcpClients[index] = new TcpClient(IP, port + index);
            Console.WriteLine("Port: " + (port + index));            
            networkStreams[index] = tcpClients[index].GetStream();
        }

        private static void prepareBinaryData()
        {
            StreamReader fileReader = null;
            byte[] byteData;
            if (!useZip)
            {
                fileReader = new StreamReader(fileName);
                content = fileReader.ReadToEnd();

                byteData = Encoding.ASCII.GetBytes(content);
            }
            else
            {
                byteData = CompressUsingBZIP2(fileName);
            }
            List<byte> listOfData = new List<byte>(byteData);          
            
            int mimimumSizeOfAChunk = byteData.Length / numberOfThreads;
            int countChunkHaveMoreData = byteData.Length % numberOfThreads;
            int actualSize;
            for (int i = 0; i < numberOfThreads; i++)
            {
                if (i < countChunkHaveMoreData)
                    actualSize = mimimumSizeOfAChunk + 1;
                else
                    actualSize = mimimumSizeOfAChunk;
                if (listOfData.Count > 0)
                {
                    if (i == numberOfThreads - 1)
                        binaryContents[i] = ToBinary(listOfData.ToArray()).Replace(" ", "");
                    else
                    {
                        if (actualSize < listOfData.Count)
                        {
                            binaryContents[i] = ToBinary(listOfData.GetRange(0, actualSize).ToArray()).Replace(" ", "");
                            listOfData.RemoveRange(0, actualSize);
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
                
            if(!useZip)
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
            StreamReader reader = new StreamReader("Config.txt");
            string line;
            line = reader.ReadLine();
            fileName = line.Remove(0, line.IndexOf(' ') + 1);
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
            Stopwatch watch = new Stopwatch();
            watch.Start();
            if (!useOnePort)
            {
                byte[] packet = new byte[1] { data };
             
                networkStreams[index].Write(packet, 0, packet.Length);
                
            }
            else
            {
                byte[] packet = new byte[2] { (byte)index, data };
                networkStreams[0].Write(packet, 0, packet.Length);
            }
            watch.Stop();
            double k = watch.ElapsedTicks / TimeSpan.TicksPerMillisecond;
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
       
        private static byte[] CompressUsingBZIP2(string fileName)
        {
            string compressedFileName = "tmp" + DateTime.Now.Millisecond + ".bz2";
            ProcessStartInfo p = new ProcessStartInfo();
            p.FileName = @"C:\Program Files\7-Zip\7z.exe";

            p.Arguments = "a -tbzip2 \"" + compressedFileName + "\" \"" + fileName + "\" -mx=9";

            p.WindowStyle = ProcessWindowStyle.Hidden;

            Process x = Process.Start(p);
            x.WaitForExit();

            byte[] result = File.ReadAllBytes(compressedFileName);
            File.Delete(compressedFileName);

            return result;
        }        
    }
}

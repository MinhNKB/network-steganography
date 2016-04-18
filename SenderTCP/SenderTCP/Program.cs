using System;
using System.Collections.Generic;
using System.IO;
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

        static int delay;
        static int countACK, countNACK;
        
        static string content;
        static string binaryContent;

        static void Main(string[] args)
        {        
            Log.WriteLine("Loged");
            inputReceiverInfo();
            for (int i = 0; i < timesToRun; ++i)
            {
                run(i);
                Thread.Sleep(10000);
            }
            Log.Close();
        }

        private static void run(int prefix)
        {
            prepareBinaryData();
            
            for (int delayCount = 0; delayCount < 4; delayCount++)
            {
                delay = delayCount * 200 + 200;
                countACK = 0;
                countNACK = 0;
                Thread.Sleep(5000);

                int i = 0;
                string temp = "";
                bool checkNew = true;

                if (connectToReceiver(i) == false)
                    return;

                while (i < binaryContent.Length)
                {
                    try
                    {
                        Console.WriteLine("------Sending a character------");
                        if (checkNew)
                        {
                            temp = "";
                            byte u;
                            for (int j = 0; j < 8; j++)
                            {
                                temp += binaryContent[i + j];
                                string k = "" + binaryContent[i + j];
                            }
                            if (temp == "00000000")
                            {
                                i += 8;
                                continue;
                            }
                            u = Convert.ToByte(temp, 2);
                            byte crc = Crc8.ComputeChecksum(u);
                            temp += Convert.ToString(crc, 2).PadLeft(8, '0');
                        }
                        sendEmptyPacket();
                        for (int j = 0; j < temp.Length; j++)
                        {
                            Console.WriteLine("Sending: " + temp[j]);
                            if (temp[j] == '1')
                            {
                                Thread.Sleep(delay);
                                sendEmptyPacket();
                            }
                            else if (temp[j] == '0')
                                sendEmptyPacket();
                        }

                        byte[] ACK = new byte[1];
                        networkStream.Read(ACK, 0, 1);
                        if (ACK[0] == 1)
                        {
                            i += 8;
                            checkNew = true;
                            countACK++;
                            Console.WriteLine("ACK: " + countACK);
                        }
                        else
                        {
                            checkNew = false;
                            countNACK++;
                            Console.WriteLine("NACK: " + countNACK);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine(ex.ToString());
                        if (connectToReceiver(i) == false)
                            return;
                    }
                }
                Console.WriteLine("Finished!");
                tcpClient.Close();
                networkStream.Close();
            }
        }

        private static bool connectToReceiver(int index)
        {
            for (int i = 0; i < 10; ++i)
            {
                try
                {
                    tcpClient = new TcpClient(IP, port);
                    tcpClient.NoDelay = true;
                    networkStream = tcpClient.GetStream();
                    sendPacket(index);
                    return true;
                }
                catch (Exception connectEx)
                {
                    Log.WriteLine(connectEx.ToString());
                    Thread.Sleep(10000);
                }
            }
            return false;
        }

        private static void prepareBinaryData()
        {
            StreamReader fileReader = new StreamReader("Data.txt");
            content = fileReader.ReadToEnd();

            binaryContent = ToBinary(ConvertToByteArray(content, Encoding.ASCII));
            binaryContent += "00000011";
            binaryContent = binaryContent.Replace(" ", "");

            //To-do: apply compress algorithm here

            fileReader.Close();
        }

        private static void inputReceiverInfo()
        {
            StreamReader reader = new StreamReader("ReceiverInfo.txt");
            IP = reader.ReadLine();
            port = Int32.Parse(reader.ReadLine());
            timesToRun = Int32.Parse(reader.ReadLine());
            reader.Close();
        }
        
        static void sendEmptyPacket()
        {
            byte[] data = new byte[1];            
            networkStream.Write(data, 0, data.Length);
        }
        
        static void sendPacket(Object obj)
        {
            byte[] data = objectToByteArray(obj);
            networkStream.Write(data, 0, data.Length);
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

    }
}

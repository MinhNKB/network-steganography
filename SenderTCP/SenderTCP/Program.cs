using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
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
        static int delay;
        static int countACK, countNACK;
        static string content;
        static void Main(string[] args)
        {        
            Log.WriteLine("Loged");

            //while (true)
            //{
            IP = "171.248.63.200";
            port = 5050;

            StreamReader fileReader = new StreamReader("Test.txt");
            content = fileReader.ReadToEnd();

            string binaryString = ToBinary(ConvertToByteArray(content, Encoding.ASCII));
            binaryString += "00000011";
            binaryString = binaryString.Replace(" ", "");
            for (int delayCount = 0; delayCount < 4; delayCount++)
            {                
                delay = delayCount*200 + 200;               
                for (int countLoop = 0; countLoop < 2; countLoop++)
                {
                    countACK = 0;
                    countNACK = 0;
                    Thread.Sleep(5000);
                    tcpClient = new TcpClient(IP, port);
                    tcpClient.NoDelay = true;
                    networkStream = tcpClient.GetStream();

                    int i = 0;
                    string temp = "";
                    bool checkNew = true;
                    while (i < binaryString.Length)
                    {
                        Console.WriteLine("------Sending a character---------");
                        if (checkNew)
                        {
                            temp = "";
                            byte u;
                            for (int j = 0; j < 8; j++)
                            {
                                temp += binaryString[i + j];
                                string k = "" + binaryString[i + j];
                            }
                            if (countLoop == 1) //Do check sum
                            {
                                u = Convert.ToByte(temp, 2);
                                byte crc = Crc8.ComputeChecksum(u);
                                temp += Convert.ToString(crc, 2).PadLeft(8, '0');
                            }
                        }
                        sendPacket();

                        for (int j = 0; j < temp.Length; j++)
                        {
                            Console.WriteLine("Sending: " + temp[j]);
                            if (temp[j] == '1')
                            {
                                Thread.Sleep(delay);
                                sendPacket();
                            }
                            else if (temp[j] == '0')
                                sendPacket();
                        }

                        if (countLoop == 1)
                        {
                            byte[] ACK = new byte[1];
                            networkStream.Read(ACK, 0, 1);
                            if (ACK[0] == 1)
                            {
                                i += 8;
                                checkNew = true;
                                //countNACK = 0;
                                countACK++;
                                Console.WriteLine("ACK: " + countACK);
                                //if (countACK >= 5)
                                //{
                                //    delay -= 100;
                                //}
                            }
                            else
                            {
                                checkNew = false;
                                //countACK = 0;
                                countNACK++;
                                Console.WriteLine("NACK: " + countNACK);
                                //if (countNACK >= 5)
                                //{
                                //    delay *= 2;
                                //    countNACK = 0;
                                //}
                            }
                        }
                        else
                            i += 8;
                    }
                    Console.WriteLine("Finished!");
                    tcpClient.Close();
                    networkStream.Close();
                }
            }
            //}

            Log.Close();
        }
        
        static void sendPacket()
        {
            byte[] data = new byte[1];            
            networkStream.Write(data, 0, data.Length);
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


    public static class Crc8
    {
        static byte[] table = new byte[256];
        // x8 + x7 + x6 + x4 + x2 + 1
        const byte poly = 0xd5;

        public static byte ComputeChecksum(params byte[] bytes)
        {
            byte crc = 0;
            if (bytes != null && bytes.Length > 0)
            {
                foreach (byte b in bytes)
                {
                    crc = table[crc ^ b];
                }
            }
            return crc;
        }

        static Crc8()
        {
            for (int i = 0; i < 256; ++i)
            {
                int temp = i;
                for (int j = 0; j < 8; ++j)
                {
                    if ((temp & 0x80) != 0)
                    {
                        temp = (temp << 1) ^ poly;
                    }
                    else
                    {
                        temp <<= 1;
                    }
                }
                table[i] = (byte)temp;
            }
        }
    }

    class Log
    {
        static StreamWriter fileWriter;      

        static Log()
        {
            fileWriter = new StreamWriter("Log.txt");        

        }

        static public void WriteLine(string logString)
        {
            fileWriter.WriteLine(logString);
        }        

       static public void Close()
        {
            fileWriter.Close();
        }
    }
}

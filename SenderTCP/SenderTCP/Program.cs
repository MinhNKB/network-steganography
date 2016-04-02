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
            //byte crc = Crc8.ComputeChecksum(1, 0, 1, 0, 1, 1, 1, 1);
            //byte check = Crc8.ComputeChecksum(1, 0, 1, 0, 1, 1, 1, 1, crc);
            //// here check should equal 0 to show that the checksum is accurate
            //if (check != 0)
            //{
            //    Console.WriteLine("Error in the checksum: " + crc);
            //}
            //else
            //{
            //    Console.WriteLine("Done: " + crc);

            //}
            //Console.ReadKey();


            while (true)
            {
                //Console.Write("Receiver IP: ");
                //IP = Console.ReadLine();
                //Console.Write("Receiver port: ");
                //port = Int32.Parse(Console.ReadLine());              
                //Console.Write("Delay: ");
                //int delay = int.Parse(Console.ReadLine());

                IP = "171.248.28.109";
                port = 5050;
                delay = 900;
                countACK = 0;
                countNACK = 0;

                //Console.Write("Message: ");
                //content = Console.ReadLine();

                StreamReader fileReader = new StreamReader("Test.txt");
                content = fileReader.ReadToEnd();


                string binaryString = ToBinary(ConvertToByteArray(content, Encoding.ASCII));
                binaryString += "00000011";
                binaryString = binaryString.Replace(" ", "");
                tcpClient = new TcpClient(IP, port);
                networkStream = tcpClient.GetStream();
               
                int i = 0;
                string temp = "";
                bool checkNew = true;
                while(i<binaryString.Length)
                {
                    if (checkNew)
                    {
                        temp = "";
                        byte u;
                        //byte[] arguments = new byte[8];
                        for (int j = 0; j < 8; j++)
                        {
                            temp += binaryString[i + j];
                            string k = "" + binaryString[i + j];
                            //arguments[j] = byte.Parse(k);
                        }
                        u = Convert.ToByte(temp, 2);
                        byte crc = Crc8.ComputeChecksum(u);
                        temp += Convert.ToString(crc, 2).PadLeft(8, '0');
                    }
                    sendPacket();
                    int count = 0;
                    for (int j = 0; j < temp.Length;j++ )
                    {                      
                        Console.WriteLine(temp[j]);
                        if (temp[j] == '1')
                        {
                            Console.WriteLine("Delay: " + delay);
                            Thread.Sleep(delay);
                            sendPacket();
                        }
                        else if (temp[j] == '0')
                            sendPacket();
                        count++;
                    }
                    Console.WriteLine(count);
                    byte[] ACK = new byte[1];
                    networkStream.Read(ACK, 0, 1);
                    if(ACK[0]==1)
                    {                     
                        i += 8;
                        checkNew = true;
                        countNACK = 0;
                        countACK++;
                        Console.WriteLine("ACK " + countACK);
                        if(countACK>=5)
                        {
                            delay -= 50;                           
                            Console.WriteLine("New delay: " + delay);
                        }
                    }
                    else
                    {                        
                        checkNew = false;
                        countACK = 0;
                        countNACK++;
                        Console.WriteLine("NACK " + countNACK);
                        if (countNACK >= 5)
                        {
                            delay += 100;
                            countNACK = 0;
                            Console.WriteLine("New delay: " + delay);
                        }
                    }
                }
                Console.WriteLine("Finished!");
                tcpClient.Close();
                networkStream.Close();
            }
            
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

}

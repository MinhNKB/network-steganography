using System;
using System.Collections.Generic;
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
        static void Main(string[] args)
        {
            while (true)
            {
                Console.Write("Receiver IP: ");
                IP = Console.ReadLine();
                Console.Write("Receiver port: ");
                port = Int32.Parse(Console.ReadLine());

                int delay;
                Console.Write("Delay: ");
                delay = int.Parse(Console.ReadLine());

                Console.Write("Message: "); 
                string content = Console.ReadLine();
                string binaryString = ToBinary(ConvertToByteArray(content, Encoding.ASCII));
                binaryString += "00000011";

                tcpClient = new TcpClient(IP, port);
                networkStream = tcpClient.GetStream();
                sendPacket();

                for (int i = 0; i < binaryString.Length; i++)
                {
                    if (binaryString[i] == '1')
                    {
                        Thread.Sleep(delay);
                        sendPacket();
                    }
                    else if (binaryString[i] == '0')
                        sendPacket();
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
}

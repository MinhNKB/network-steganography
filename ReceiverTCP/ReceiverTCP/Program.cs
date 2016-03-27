using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ReceiverTCP
{
    class Program
    {
        static TcpListener tcpListener;
        static TcpClient tcpClient;
        static NetworkStream networkStream;
        static string ip;
        static int port;

        static void Main(string[] args)
        {
            while (true)
            {
                string receivedBits = "";
                DateTime lastReceived;

                Console.Write("Receiver IP: ");
                ip = Console.ReadLine();

                Console.Write("Receiver Port: ");
                port = Int32.Parse(Console.ReadLine());

                Console.Write("Delay: ");
                int delay = Int32.Parse(Console.ReadLine());

                tcpListener = new TcpListener(IPAddress.Parse(ip), port);
                tcpListener.Start();
                Console.WriteLine("Waiting for sender...");
                tcpClient = tcpListener.AcceptTcpClient();
                Console.WriteLine("Connected!");
                networkStream = tcpClient.GetStream();

                receivedData();
                lastReceived = DateTime.Now;
                Console.WriteLine("Received start signal");
                Console.Write("Received message: ");
                while (true)
                {
                    receivedData();
                    DateTime currentReceived = DateTime.Now;
                    if (currentReceived.Subtract(lastReceived).Milliseconds > delay)
                    {
                        receivedBits += 1;
                    }
                    else
                    {
                        receivedBits += 0;
                    }
                    lastReceived = currentReceived;
                    if (receivedBits.Length == 8)
                    {
                        if (receivedBits == "00000011")
                        {
                            Console.WriteLine();
                            break;
                        }
                        string decodedString = System.Text.Encoding.UTF8.GetString(convertBytesToString(receivedBits));
                        Console.Write(decodedString);
                        receivedBits = "";

                    }
                }
                tcpListener.Stop();
            }
        }


        static void receivedData()
        {
            byte[] data = new byte[1];
            networkStream.Read(data, 0, 1);
        }
        static byte[] convertBytesToString(string input)
        {
            int numOfBytes = input.Length / 8;
            byte[] bytes = new byte[numOfBytes];
            for (int i = 0; i < numOfBytes; ++i)
            {
                bytes[i] = Convert.ToByte(input.Substring(8 * i, 8), 2);
            }
            return bytes;
        }
    }
}

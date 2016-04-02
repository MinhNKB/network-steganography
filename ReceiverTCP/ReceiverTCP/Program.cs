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
        static int count = 0;
        static bool isAckConsecutive = false;
        static bool isNackConsecutive = false;

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

                DateTime startTime = DateTime.Now;

                networkStream = tcpClient.GetStream();
                receiveData();
                lastReceived = DateTime.Now;

                while (true)
                {
                    receiveData();
                    DateTime currentReceived = DateTime.Now;
                    if (currentReceived.Subtract(lastReceived).Milliseconds > delay)
                    {
                        receivedBits += 1;
                        Console.Write(1);
                        Console.Write(" " + currentReceived.Subtract(lastReceived).Milliseconds + " ");
                    }
                    else
                    {
                        Console.Write(0);
                        Console.Write(" " + currentReceived.Subtract(lastReceived).Milliseconds + " ");
                        receivedBits += 0;
                    }
                    lastReceived = currentReceived;
                    if (receivedBits.Length == 16)
                    {
                        string receivedData = receivedBits.Substring(0, 8);
                        string receivedCrc = receivedBits.Substring(8, 8);
                        if (checkSum(receivedData, receivedCrc) == false)
                        {
                            byte[] nack = new byte[1];
                            nack[0] = 0;
                            networkStream.Write(nack, 0, nack.Length);
                            isAckConsecutive = false;

                            if (isNackConsecutive == true)
                            {
                                count++;
                                if (count == 5)
                                {
                                    delay += 50;
                                    count = 0;
                                    isNackConsecutive = false;
                                }
                            }
                            else
                            {
                                isNackConsecutive = true;
                                count = 1;
                            }

                            Console.WriteLine();
                            Console.WriteLine("Ack: " + isAckConsecutive);
                            Console.WriteLine("Nack:" + isNackConsecutive);
                            Console.WriteLine("Count: " + count);
                            Console.WriteLine("Delay: " + delay);
                            Console.WriteLine("Sent NACK!");
                            receivedBits = "";
                            receiveData();
                            lastReceived = DateTime.Now;
                        }
                        else
                        {
                            byte[] ack = new byte[1];
                            ack[0] = 1;
                            networkStream.Write(ack, 0, ack.Length);
                            isNackConsecutive = false;

                            if (isAckConsecutive == true)
                            {
                                count++;
                                if (count >= 5)
                                    delay -= 25;
                            }
                            else
                            {
                                isAckConsecutive = true;
                                count = 1;
                            }
                            Console.WriteLine();
                            Console.WriteLine("Ack: " + isAckConsecutive);
                            Console.WriteLine("Nack:" + isNackConsecutive);
                            Console.WriteLine("Count: " + count);
                            Console.WriteLine("Delay: " + delay);
                            Console.WriteLine("Sent ACK!");

                            if (receivedData == "00000011")
                            {
                                Console.WriteLine();
                                Console.WriteLine("Finished!");
                                Console.WriteLine(DateTime.Now.Subtract(startTime).Seconds);
                                break;
                            }
                            string decodedString = System.Text.Encoding.UTF8.GetString(convertStringBytesToBytes(receivedData));
                            Console.WriteLine(decodedString);
                            receivedBits = "";
                            receiveData();
                            lastReceived = DateTime.Now;
                        }
                    }
                }
                tcpListener.Stop();
            }
        }

        static bool checkSum(string receivedData, string receivedCrc)
        {
            byte data = convertStringBytesToBytes(receivedData)[0];
            byte crc = convertStringBytesToBytes(receivedCrc)[0];
            byte check = Crc8.ComputeChecksum(data, crc);
            if (check != 0)
                return false;
            return true;
        }
        static void receiveData()
        {
            byte[] data = new byte[1];
            networkStream.Read(data, 0, 1);
        }
        static byte[] convertStringBytesToBytes(string input)
        {
            int numOfBytes = input.Length / 8;
            byte[] bytes = new byte[numOfBytes];
            for (int i = 0; i < numOfBytes; ++i)
            {
                bytes[i] = Convert.ToByte(input.Substring(8 * i, 8), 2);
            }
            return bytes;
        }  
        static byte[] convertStringToBytes(string input, Encoding encoding)
        {
            return encoding.GetBytes(input);
        }
    }
}

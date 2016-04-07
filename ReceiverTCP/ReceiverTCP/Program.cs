using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ReceiverTCP
{
    class Program
    {
        static string ip;
        static int port;
        static int delay;

        static TcpListener tcpListener;
        static TcpClient tcpClient;
        static NetworkStream networkStream;

        static DateTime startTime;
        static DateTime lastReceived;
        static DateTime currentReceived;

        static string receivedBits = "";
        static string decodedString = "";

        static void Main(string[] args)
        {
            for (int i = 0; i < 2; ++i)
            {
                inputHostInfo();
                initConnection();

                receiveData(i);

                tcpListener.Stop();
            }
        }

        //stategy - 0:Pure 1:HasCRC
        private static void receiveData(int strategy)
        {
            switch (strategy)
            {
                case 0:
                    applyPure();
                    break;
                case 1:
                    applyCRC();
                    break;
            }
        }

        private static void applyPure()
        {
            receivedBits = "";
            decodedString = "";

            StreamWriter binaryWriter = new StreamWriter("Pure-Binary-" + delay + ".txt", false);
            StreamWriter stringWriter = new StreamWriter("Pure-String-" + delay + ".txt", false);
            StreamWriter infoWriter = new StreamWriter("Pure-Info-" + delay + ".txt", false);

            receiveFirstEmptySignal();

            while (true)
            {
                receiveSignal();
                currentReceived = DateTime.Now;

                processNewBit();
                lastReceived = currentReceived;

                if (receivedBits.Length == 8)
                {
                    Console.WriteLine(receivedBits);
                    binaryWriter.WriteLine(receivedBits);

                    if (receivedBits == "00000011")
                    {
                        writeFinishMessage();
                        infoWriter.WriteLine("Time: " + DateTime.Now.Subtract(startTime).Seconds);
                        break;
                    }

                    string decodedCharacter = System.Text.Encoding.UTF8.GetString(convertStringBytesToBytes(receivedBits));
                    decodedString += decodedCharacter;

                    stringWriter.Write(decodedCharacter);
                    Console.WriteLine(decodedString);
                    receiveFirstEmptySignal();
                }
            }

            binaryWriter.Close();
            stringWriter.Close();
            infoWriter.Close();
        }


        
        private static void applyCRC()
        {
            int numberOfACKs = 0;
            int numberOfNACKs = 0;
            receivedBits = "";
            decodedString = "";

            StreamWriter binaryWriter = new StreamWriter("CRC-Binary-" + delay + ".txt", false);
            StreamWriter stringWriter = new StreamWriter("CRC-String-" + delay + ".txt", false);
            StreamWriter infoWriter = new StreamWriter("CRC-Info-" + delay + ".txt", false);

            receiveFirstEmptySignal();

            while (true)
            {
                receiveSignal();
                currentReceived = DateTime.Now;

                processNewBit();
                lastReceived = currentReceived;

                if (receivedBits.Length == 16)
                {
                    Console.WriteLine(receivedBits);

                    string receivedData = receivedBits.Substring(0, 8);
                    string receivedCrc = receivedBits.Substring(8, 8);
                    if (checkSum(receivedData, receivedCrc) == false)
                    {
                        sendResponse(false);
                        ++numberOfNACKs;
                        Console.WriteLine("NACK: " + numberOfNACKs);
                    }
                    else
                    {
                        sendResponse(true);
                        ++numberOfACKs;
                        Console.WriteLine("ACK: " + numberOfACKs);
                        binaryWriter.WriteLine(receivedBits);

                        if (receivedData == "00000011")
                        {
                            writeFinishMessage();
                            infoWriter.WriteLine("Number of ACK: " + numberOfACKs);
                            infoWriter.WriteLine("Number of NACK: " + numberOfNACKs);
                            infoWriter.WriteLine("Time: " + DateTime.Now.Subtract(startTime).Seconds);
                            break;
                        }
                        
                        string decodedCharacter = System.Text.Encoding.UTF8.GetString(convertStringBytesToBytes(receivedData));
                        decodedString += decodedCharacter;

                        stringWriter.Write(decodedCharacter);
                        Console.WriteLine(decodedString);
                    }
                    receiveFirstEmptySignal();
                }
            }

            binaryWriter.Close();
            stringWriter.Close();
            infoWriter.Close();
        }

        private static void receiveFirstEmptySignal()
        {
            receivedBits = "";
            receiveSignal();
            lastReceived = DateTime.Now;
        }

        private static void writeFinishMessage()
        {
            Console.WriteLine("Finished!");
            Console.WriteLine(DateTime.Now.Subtract(startTime).Seconds);
        }

        private static void sendResponse(bool isACK)
        {
            byte[] response = new byte[1];
            if (isACK)
                response[0] = 1;
            else
                response[0] = 0;
            networkStream.Write(response, 0, response.Length);
            
        }


        static int consecutiveCount = 0;
        static bool isAckConsecutive = false;
        static bool isNackConsecutive = false;
        private static void adjustACKDelay()
        {
            isNackConsecutive = false;

            if (isAckConsecutive == true)
            {
                consecutiveCount++;
                if (consecutiveCount >= 5)
                    delay -= 25;
            }
            else
            {
                isAckConsecutive = true;
                consecutiveCount = 1;
            }
            writeDelayAdjustmentDetail();
        }

        private static void writeDelayAdjustmentDetail()
        {
            Console.WriteLine();
            Console.WriteLine("Ack: " + isAckConsecutive);
            Console.WriteLine("Nack:" + isNackConsecutive);
            Console.WriteLine("Count: " + consecutiveCount);
            Console.WriteLine("Delay: " + delay);
            Console.WriteLine("Sent ACK!");
        }


        private static void adjustNACKDelay()
        {
            isAckConsecutive = false;
            if (isNackConsecutive == true)
            {
                consecutiveCount++;
                if (consecutiveCount == 5)
                {
                    delay += 50;
                    consecutiveCount = 0;
                    isNackConsecutive = false;
                }
            }
            else
            {
                isNackConsecutive = true;
                consecutiveCount = 1;
            }
            writeDelayAdjustmentDetail();
        }
        private static void processNewBit()
        {
            if (currentReceived.Subtract(lastReceived).Milliseconds > delay)
                receivedBits += 1;
            else
                receivedBits += 0;
        }

        private static void initConnection()
        {
            tcpListener = new TcpListener(IPAddress.Parse(ip), port);
            tcpListener.Start();

            Console.WriteLine("Waiting for sender...");
            tcpClient = tcpListener.AcceptTcpClient();

            Console.WriteLine("Connected!");
            startTime = DateTime.Now;

            networkStream = tcpClient.GetStream();
        }

        private static void inputHostInfo()
        {
            //Console.Write("Receiver IP: ");
            //ip = Console.ReadLine();

            //Console.Write("Receiver Port: ");
            //port = Int32.Parse(Console.ReadLine());

            //Console.Write("Delay: ");
            //delay = Int32.Parse(Console.ReadLine());
            ip = "192.168.1.106";
            port = 5050;
            delay = 300;
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
        static void receiveSignal()
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

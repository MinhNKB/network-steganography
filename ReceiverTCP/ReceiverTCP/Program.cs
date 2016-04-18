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

        static int prefix = 0;

        static void Main(string[] args)
        {
            while (true)
            {
                delay = 100;
                for (int i = 0; i < 4; ++i)
                {
                    receiveData(1);
                    delay += 100;
                }
                ++prefix;
            }
        }

        //stategy - 0:Pure 1:HasCRC
        private static void receiveData(int strategy)
        {
            switch (strategy)
            {
                case 0:
                    Console.WriteLine("Starting Pure - " + delay);
                    applyPure();
                    break;
                case 1:
                    Console.WriteLine("Starting CRC - " + delay);
                    applyCRC();
                    break;
            }
        }

        private static void applyPure()
        {
            StreamWriter binaryWriter = new StreamWriter("Pure-Binary-" + delay + ".txt", false);
            StreamWriter stringWriter = new StreamWriter("Pure-String-" + delay + ".txt", false);
            StreamWriter infoWriter = new StreamWriter("Pure-Info-" + delay + ".txt", false);
            try
            {
                inputHostInfo();
                initConnection();

                int numberOfReceivedBits = 0;
                receivedBits = "";
                decodedString = "";

                receiveFirstEmptySignal();

                while (true)
                {
                    receiveSignal();
                    currentReceived = DateTime.Now;

                    processNewBit();
                    lastReceived = currentReceived;

                    numberOfReceivedBits++;

                    if (receivedBits.Length == 8)
                    {
                        Console.WriteLine(receivedBits);
                        binaryWriter.Write(receivedBits);

                        if (numberOfReceivedBits == 8040)
                        {
                            writeFinishMessage();
                            infoWriter.WriteLine("Time: " + DateTime.Now.Subtract(startTime).TotalSeconds);
                            break;
                        }

                        string decodedCharacter = System.Text.Encoding.UTF8.GetString(convertStringBytesToBytes(receivedBits));
                        decodedString += decodedCharacter;

                        stringWriter.Write(decodedCharacter);
                        Console.WriteLine(decodedString);
                        receiveFirstEmptySignal();
                    }


                }
            }
            finally
            {
                tcpListener.Stop();
                tcpClient.Close();
                networkStream.Close();

                binaryWriter.Close();
                stringWriter.Close();
                infoWriter.Close();
            }
        }
        
        private static void applyCRC()
        {
            StreamWriter binaryWriter = new StreamWriter("CRC-Binary-" + delay + ".txt", false);
            StreamWriter stringWriter = new StreamWriter("CRC-String-" + delay + ".txt", false);
            StreamWriter infoWriter = new StreamWriter("CRC-Info-" + delay + ".txt", false);

            try
            {
                inputHostInfo();
                initConnection();

                int numberOfACKs = 0;
                int numberOfNACKs = 0;
                receivedBits = "";
                decodedString = "";
                

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
                            //adjustNACKDelay();
                            ++numberOfNACKs;
                            Console.WriteLine("NACK: " + numberOfNACKs);
                        }
                        else
                        {
                            sendResponse(true);
                            //adjustACKDelay();
                            ++numberOfACKs;
                            Console.WriteLine("ACK: " + numberOfACKs);
                            binaryWriter.Write(receivedBits);

                            if (receivedData == "00000011")
                            {
                                writeFinishMessage();
                                infoWriter.WriteLine("Number of ACK: " + numberOfACKs);
                                infoWriter.WriteLine("Number of NACK: " + numberOfNACKs);
                                infoWriter.WriteLine("Time: " + DateTime.Now.Subtract(startTime).TotalSeconds);
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
            }
            finally
            {
                tcpListener.Stop();
                tcpClient.Close();
                networkStream.Close();

                binaryWriter.Close();
                stringWriter.Close();
                infoWriter.Close();
            }
            

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
                    delay -= 50;
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
            Console.WriteLine("Nack: " + isNackConsecutive);
            Console.WriteLine("Count: " + consecutiveCount);
            Console.WriteLine("Delay: " + delay);
        }


        private static void adjustNACKDelay()
        {
            isAckConsecutive = false;
            if (isNackConsecutive == true)
            {
                consecutiveCount++;
                if (consecutiveCount == 5)
                {
                    delay *= 2;
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
            {
                //Console.WriteLine(currentReceived.Subtract(lastReceived).Milliseconds + "-1");
                receivedBits += 1;
            }
            else 
            {
                //Console.WriteLine(currentReceived.Subtract(lastReceived).Milliseconds + "-0");
                receivedBits += 0;
            }
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
            StreamReader reader = new StreamReader("HostInfo.txt");
            ip = reader.ReadLine();
            port = Int32.Parse(reader.ReadLine());
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
            networkStream.ReadTimeout = 20000;
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

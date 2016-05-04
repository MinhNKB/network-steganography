using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ReceiverTCP
{
    class Program
    {
        static string ip;
        static int port;
        static int delay;
        static int runTimes;

        static TcpListener tcpListener;
        static TcpClient tcpClient;
        static NetworkStream networkStream;

        static DateTime startTime;
        static DateTime lastReceived;
        static DateTime currentReceived;

        static string receivedByte = "";

        static string binaryContent = "";
        static string stringContent = "";
        static string compressedStringContent = "";
        static string compressedBinaryContent = "";

        static int numberOfACKs = 0;
        static int numberOfNACKs = 0;

        static int prefix = 0;

        static String dataString;

        private static void initValues()
        {
            startTime = DateTime.Now;
            numberOfACKs = 0;
            numberOfNACKs = 0;
            receivedByte = "";
            binaryContent = "";
            compressedStringContent = "";
            compressedBinaryContent = "";
            stringContent = "";
        }

        static void Main(string[] args)
        {
            inputHostInfo();

            StreamReader originalDataReader = new StreamReader("Data.txt");
            dataString = originalDataReader.ReadToEnd();
            originalDataReader.Close();

            while (prefix < runTimes)
            {
                StreamWriter resultWriter = new StreamWriter("Result.txt", true);
                resultWriter.WriteLine("---------- " + prefix + " ----------");
                resultWriter.Close();
                delay = 100;
                receiveData(1);
                //for (int i = 0; i < 4; ++i)
                //{
                //    receiveData(1);
                //    delay += 100;
                //}
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
                receivedByte = "";
                stringContent = "";
                compressedStringContent = "";

                receiveFirstEmptySignal();

                while (true)
                {
                    receiveSignal();
                    currentReceived = DateTime.Now;

                    processNewBit();
                    lastReceived = currentReceived;

                    numberOfReceivedBits++;

                    if (receivedByte.Length == 8)
                    {
                        Console.WriteLine(receivedByte);
                        binaryWriter.Write(receivedByte);

                        if (numberOfReceivedBits == 8040)
                        {
                            writeFinishMessage();
                            infoWriter.WriteLine("Time: " + DateTime.Now.Subtract(startTime).TotalSeconds);
                            break;
                        }

                        //string decodedCharacter = System.Text.Encoding.UTF8.GetString(convertStringBytesToBytes(receivedByte));
                        //stringContent += decodedCharacter;
                        compressedStringContent += receivedByte;
                        //stringWriter.Write(decodedCharacter);
                        //Console.WriteLine(stringContent);
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
            bool isFinised = false;
            while(isFinised == false)
            {
                try
                {
                    initConnection();
                    initValues();

                    receiveFirstEmptySignal();
                    while (true)
                    {
                        receiveSignal();
                        currentReceived = DateTime.Now;

                        processNewBit();
                        lastReceived = currentReceived;

                        if (receivedByte.Length == 16)
                        {
                            Console.WriteLine(receivedByte);

                            string receivedData = receivedByte.Substring(0, 8);
                            string receivedCrc = receivedByte.Substring(8, 8);
                            //if (checkSum(receivedData, receivedCrc) == false || receivedData == "00000000")
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
                                binaryContent += receivedByte;

                                compressedStringContent += receivedData;

                                //if (receivedData == "00000011")
                                if (numberOfACKs == 612)
                                {
                                    tcpListener.Stop();
                                    tcpClient.Close();
                                    networkStream.Close();
                                    
                                    writeFinishMessage();
                                    isFinised = true;
                                    break;
                                }

                                //string decodedCharacter = System.Text.Encoding.UTF8.GetString(convertStringBytesToBytes(receivedData));
                                //stringContent += decodedCharacter;
                                //Console.WriteLine(stringContent);
                            }
                            receiveFirstEmptySignal();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    
                    tcpListener.Stop();
                    tcpClient.Close();
                    networkStream.Close();
                }
            }
        }

        

        private static void receiveFirstEmptySignal()
        {
            receivedByte = "";
            receiveSignal();
            lastReceived = DateTime.Now;
        }

        private static void writeFinishMessage()
        {
            decompressData();
            Console.WriteLine("Finished!");
            Console.WriteLine(DateTime.Now.Subtract(startTime).Seconds);


            StreamWriter binaryWriter = new StreamWriter("(" + prefix + ")-" + delay + "-Binary" + ".txt", false);
            StreamWriter stringWriter = new StreamWriter("(" + prefix + ")-" + delay + "-String" + ".txt", false);
            StreamWriter resultWriter = new StreamWriter("Result.txt", true);

            binaryWriter.Write(binaryContent);
            binaryWriter.Close();

            stringWriter.Write(stringContent);
            stringWriter.Close();


            int countSimilar = CountSimilarBetweenTwoString(dataString, stringContent);
            double percentage = (double)countSimilar / (double)dataString.Length * 100;

            resultWriter.WriteLine("\tDelay " + delay);
            resultWriter.WriteLine("\t\tNumber of ACK: " + numberOfACKs);
            resultWriter.WriteLine("\t\tNumber of NACK: " + numberOfNACKs);
            resultWriter.WriteLine("\t\tTime: " + DateTime.Now.Subtract(startTime).TotalSeconds);
            resultWriter.WriteLine("\t\tSimilar characters: " + countSimilar.ToString());
            resultWriter.WriteLine("\t\tString length: " + dataString.Length);
            resultWriter.WriteLine("\t\tPercentage: " + percentage.ToString("00.00"));
            resultWriter.Close();
        }

        private static void decompressData()
        {
            byte[] data = convertStringBytesToBytes(compressedStringContent);
            stringContent = System.Text.Encoding.UTF8.GetString(Decompress(data));
        }

        static byte[] Decompress(byte[] gzip)
        {
            // Create a GZIP stream with decompression mode.
            // ... Then create a buffer and write into while reading from the GZIP stream.
            using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }

        private static int CountSimilarBetweenTwoString(string dataString, string resultString)
        {
            if (dataString.Length != resultString.Length)
                return -1;
            int count = 0;
            for (int i = 0; i < dataString.Length; i++)
                if (dataString[i] == resultString[i])
                    count++;
            return count;
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
                receivedByte += 1;
            }
            else 
            {
                //Console.WriteLine(currentReceived.Subtract(lastReceived).Milliseconds + "-0");
                receivedByte += 0;
            }
        }

        private static void initConnection()
        {
            tcpListener = new TcpListener(IPAddress.Parse(ip), port);
            tcpListener.Start();

            Console.WriteLine("Waiting for sender...");
            tcpClient = tcpListener.AcceptTcpClient();

            Console.WriteLine("Connected!");
            networkStream = tcpClient.GetStream();
        }

        private static void inputHostInfo()
        {
            StreamReader reader = new StreamReader("HostInfo.txt");
            ip = reader.ReadLine();
            port = Int32.Parse(reader.ReadLine());
            runTimes = Int32.Parse(reader.ReadLine());
            prefix = Int32.Parse(reader.ReadLine());
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

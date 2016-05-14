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
    class Receiver
    {
        private string ip;
        private int port;
        private int delay;
        private int index;
        private bool isCompressed;

        private TcpListener tcpListener;
        private TcpClient tcpClient;
        private NetworkStream networkStream;

        private int numberOfACKs = 0;

        public int NumberOfACKs
        {
            get { return numberOfACKs; }
            set { numberOfACKs = value; }
        }

        private int numberOfNACKs = 0;

        public int NumberOfNACKs
        {
            get { return numberOfNACKs; }
            set { numberOfNACKs = value; }
        }

        private DateTime startTime;

        public DateTime StartTime
        {
            get { return startTime; }
            set { startTime = value; }
        }

        private DateTime lastReceived;
        private DateTime currentReceived;


        private string binaryData = "";

        public string BinaryData
        {
            get { return binaryData; }
            set { binaryData = value; }
        }
        private string stringData = "";

        public string StringData
        {
            get { return stringData; }
            set { stringData = value; }
        }

        public Receiver(string ip, int port, int delay, int index, bool isComressed)
        {
            this.ip = ip;
            this.port = port;
            this.delay = delay;
            this.index = index;
            this.isCompressed = isComressed;
        }

        private string tmpReceivedByte = "";

        private byte[] tcpPacket;

        public void run()
        {
            bool isFinised = false;
            while (isFinised == false)
            {
                try
                {
                    initConnection();
                    initValues();

                    while (true)
                    {
                        receiveFirstEmptySignal();
                        if (tcpPacket[0] == 0 && isCompressed == true)
                        {
                            sendResponse(true);
                            //adjustACKDelay();
                            ++numberOfACKs;
                            writeLineLogMessage("Number of ACKs: " + numberOfACKs);
                            Console.WriteLine("Port {0} number of ACKs: {1}", port, numberOfACKs);
                            binaryData += "00000000";
                            continue;
                        }
                        else if (tcpPacket[0] == 1 && isCompressed == true)
                        {
                            sendResponse(true);
                            //adjustACKDelay();
                            ++numberOfACKs;
                            writeLineLogMessage("Number of ACKs: " + numberOfACKs);
                            Console.WriteLine("Port {0} number of ACKs: {1}", port, numberOfACKs);
                            tcpListener.Stop();
                            tcpClient.Close();
                            networkStream.Close();
                            isFinised = true;
                            return;
                        }
                        else
                            break;
                    }
                    

                    while (true)
                    {
                        receiveSignal();
                        currentReceived = DateTime.Now;

                        processNewBit();
                        lastReceived = currentReceived;

                        if (tmpReceivedByte.Length == 16)
                        {
                            Console.WriteLine(tmpReceivedByte);
                            writeLineLogMessage(tmpReceivedByte);

                            string receivedData = tmpReceivedByte.Substring(0, 8);
                            string receivedCrc = tmpReceivedByte.Substring(8, 8);

                            if (checkSum(receivedData, receivedCrc) == false || receivedData == "00000000")
                            {
                                sendResponse(false);
                                //adjustNACKDelay();
                                ++numberOfNACKs;
                                writeLineLogMessage("Number of NACKs: " + numberOfNACKs);
                                Console.WriteLine("Port {0} number of NACKs: {1}", port, numberOfNACKs);
                            }
                            else
                            {
                                sendResponse(true);
                                //adjustACKDelay();
                                ++numberOfACKs;
                                writeLineLogMessage("Number of ACKs: " + numberOfACKs);
                                Console.WriteLine("Port {0} number of ACKs: {1}", port, numberOfACKs);

                                if (receivedData == "00000011" && isCompressed == false)
                                {
                                    tcpListener.Stop();
                                    tcpClient.Close();
                                    networkStream.Close();
                                    isFinised = true;
                                    break;
                                }

                                binaryData += receivedData;
                                if (isCompressed == false)
                                {
                                    string decodedCharacter = System.Text.Encoding.UTF8.GetString(convertStringBytesToBytes(receivedData));
                                    stringData += decodedCharacter;
                                }
                                
                                //Console.WriteLine(stringData);
                            }
                            while (true)
                            {
                                receiveFirstEmptySignal();
                                if (tcpPacket[0] == 0 && isCompressed == true)
                                {
                                    sendResponse(true);
                                    //adjustACKDelay();
                                    ++numberOfACKs;
                                    writeLineLogMessage("Number of ACKs: " + numberOfACKs);
                                    Console.WriteLine("Port {0} number of ACKs: {1}", port, numberOfACKs);
                                    binaryData += "00000000";
                                    continue;
                                }
                                else if (tcpPacket[0] == 1 && isCompressed == true)
                                {
                                    sendResponse(true);
                                    //adjustACKDelay();
                                    ++numberOfACKs;
                                    writeLineLogMessage("Number of ACKs: " + numberOfACKs);
                                    Console.WriteLine("Port {0} number of ACKs: {1}", port, numberOfACKs);
                                    tcpListener.Stop();
                                    tcpClient.Close();
                                    networkStream.Close();
                                    isFinised = true;
                                    return;
                                }
                                else
                                    break;
                            }

                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Port {0}: {1}", port, ex.ToString());
                    writeLineLogMessage(ex.ToString());
                    try
                    {
                        tcpListener.Stop();
                    }
                    catch (Exception) { }
                    //throw new Exception("Port " + port + ": " + ex.ToString());
                }
            }
        }

        private void initValues()
        {
            startTime = DateTime.Now;
            numberOfACKs = 0;
            numberOfNACKs = 0;
            tmpReceivedByte = "";
            binaryData = "";
            stringData = "";
        }

        private void receiveFirstEmptySignal()
        {
            tmpReceivedByte = "";
            receiveSignal();
            lastReceived = DateTime.Now;
        }

        

        private void sendResponse(bool isACK)
        {
            byte[] response = new byte[1];
            if (isACK)
                response[0] = 1;
            else
                response[0] = 0;
            networkStream.Write(response, 0, response.Length);

        }

        private void processNewBit()
        {
            if (currentReceived.Subtract(lastReceived).Milliseconds > delay)
            {
                //Console.WriteLine(currentReceived.Subtract(lastReceived).Milliseconds + "-1");
                tmpReceivedByte += 1;
            }
            else
            {
                //Console.WriteLine(currentReceived.Subtract(lastReceived).Milliseconds + "-0");
                tmpReceivedByte += 0;
            }
        }

        private void initConnection()
        {
            tcpListener = new TcpListener(IPAddress.Parse(ip), port);
            tcpListener.Start();

            Console.WriteLine("Waiting for sender on port {0}...", port);
            writeLineLogMessage("Waiting for sender...");
            tcpClient = tcpListener.AcceptTcpClient();

            Console.WriteLine("Port {0} connected!", port);
            writeLineLogMessage("Connected!");
            networkStream = tcpClient.GetStream();
        }



        private bool checkSum(string receivedData, string receivedCrc)
        {
            byte data = convertStringBytesToBytes(receivedData)[0];
            byte crc = convertStringBytesToBytes(receivedCrc)[0];
            byte check = Crc8.ComputeChecksum(data, crc);
            if (check != 0)
                return false;
            return true;
        }


        private void receiveSignal()
        {
            tcpPacket = new byte[1];
            networkStream.ReadTimeout = 20000;
            networkStream.Read(tcpPacket, 0, 1);
        }

        public static byte[] convertStringBytesToBytes(string input)
        {
            int numOfBytes = input.Length / 8;
            byte[] bytes = new byte[numOfBytes];
            for (int i = 0; i < numOfBytes; ++i)
            {
                bytes[i] = Convert.ToByte(input.Substring(8 * i, 8), 2);
            }
            return bytes;
        }

        private byte[] convertStringToBytes(string input, Encoding encoding)
        {
            return encoding.GetBytes(input);
        }

        private void writeLogMessage(string message)
        {
            StreamWriter writer = new StreamWriter("Log_{0}.txt", true);
            writer.Write("({0}) {1}: {2}", index, DateTime.Now.ToString(), message);
            writer.Close();
        }

        private void writeLineLogMessage(string message)
        {
            StreamWriter writer = new StreamWriter("Log_" + port + ".txt", true);
            writer.WriteLine("({0}) {1}: {2}", index, DateTime.Now.ToString(), message);
            writer.Close();
        }

        //private int consecutiveCount = 0;
        //private bool isAckConsecutive = false;
        //private bool isNackConsecutive = false;
        //private void adjustACKDelay()
        //{
        //    isNackConsecutive = false;

        //    if (isAckConsecutive == true)
        //    {
        //        consecutiveCount++;
        //        if (consecutiveCount >= 5)
        //            delay -= 50;
        //    }
        //    else
        //    {
        //        isAckConsecutive = true;
        //        consecutiveCount = 1;
        //    }
        //    writeDelayAdjustmentDetail();
        //}

        //private void writeDelayAdjustmentDetail()
        //{
        //    Console.WriteLine();
        //    Console.WriteLine("Ack: " + isAckConsecutive);
        //    Console.WriteLine("Nack: " + isNackConsecutive);
        //    Console.WriteLine("Count: " + consecutiveCount);
        //    Console.WriteLine("Delay: " + delay);
        //}


        //private void adjustNACKDelay()
        //{
        //    isAckConsecutive = false;
        //    if (isNackConsecutive == true)
        //    {
        //        consecutiveCount++;
        //        if (consecutiveCount == 5)
        //        {
        //            delay *= 2;
        //            consecutiveCount = 0;
        //            isNackConsecutive = false;
        //        }
        //    }
        //    else
        //    {
        //        isNackConsecutive = true;
        //        consecutiveCount = 1;
        //    }
        //    writeDelayAdjustmentDetail();
        //}
    }
}

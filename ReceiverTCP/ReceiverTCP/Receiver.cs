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
        private int delay;
        private int runTimesIndex;
        private int threadIndex;
        
        private NetworkStream networkStream;
        private Object networkStreamLock;

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

        public Receiver(int delay, int runTimesIndex, int threadIndex, NetworkStream networkStream, Object networkStreamLock)
        {
            this.delay = delay;
            this.runTimesIndex = runTimesIndex;
            this.threadIndex = threadIndex;
            this.networkStream = networkStream;
            this.networkStreamLock = networkStreamLock;
            initValues();
        }

        private string tmpReceivedByte = "";

        private bool isStarted = false;

        public void processNewPacket(byte[] receivedPacket, DateTime receiveTime)
        {
            try
            {
                if (isStarted == false)
                {
                    startTime = DateTime.Now;
                    isStarted = true;
                }

                if (receivedPacket[1] == 0)
                {
                    lastReceived = receiveTime;
                    //writeLineLogMessage("Received 00000000");
                    sendResponse(true);
                    //adjustACKDelay();
                    ++numberOfACKs;
                    //writeLineLogMessage("Number of ACKs: " + numberOfACKs);
                    //Console.WriteLine("Thread {0} number of ACKs: {1}", threadIndex, numberOfACKs);
                    binaryData += "00000000";
                    return;
                }

                if (receivedPacket[1] == 1)
                {
                    lastReceived = receiveTime;
                    //writeLineLogMessage("Received finish signal");
                    sendResponse(true);
                    //adjustACKDelay();
                    ++numberOfACKs;
                    //writeLineLogMessage("Number of ACKs: " + numberOfACKs);
                    //Console.WriteLine("Thread {0} number of ACKs: {1}", threadIndex, numberOfACKs);
                    return;
                }

                if (tmpReceivedByte == "empty")
                {
                    lastReceived = DateTime.Now;
                    tmpReceivedByte = "";
                    return;
                }

                currentReceived = receiveTime;
                processNewBit();
                lastReceived = currentReceived;

                if (tmpReceivedByte.Length == 16)
                {
                    //Console.WriteLine(tmpReceivedByte);
                    //writeLineLogMessage(tmpReceivedByte);

                    string receivedData = tmpReceivedByte.Substring(0, 8);
                    string receivedCrc = tmpReceivedByte.Substring(8, 8);

                    if (checkSum(receivedData, receivedCrc) == false || receivedData == "00000000")
                    {
                        //writeLineLogMessage("NACK");
                        sendResponse(false);
                        //adjustNACKDelay();
                        ++numberOfNACKs;
                        //writeLineLogMessage("Number of NACKs: " + numberOfNACKs);
                        Console.WriteLine("Thread {0}: number of NACKs: {1}", threadIndex, numberOfNACKs);
                    }
                    else
                    {
                        //writeLineLogMessage("ACK");
                        sendResponse(true);
                        //adjustACKDelay();
                        ++numberOfACKs;
                        //writeLineLogMessage("Number of ACKs: " + numberOfACKs);
                        Console.WriteLine("Thread {0}: number of ACKs: {1}", threadIndex, numberOfACKs);

                        binaryData += receivedData;

                        string decodedCharacter = System.Text.Encoding.UTF8.GetString(convertStringBytesToBytes(receivedData));
                        stringData += decodedCharacter;
                        //writeLineLogMessage(stringData);
                    }
                    tmpReceivedByte = "empty";
                }
            }
            catch (Exception ex)
            {
                writeLineLogMessage(ex.Message);
                Console.WriteLine("Thread {0}: ", ex.Message);
            }
        }

        private void initValues()
        {
            numberOfACKs = 0;
            numberOfNACKs = 0;
            tmpReceivedByte = "empty";
            binaryData = "";
            stringData = "";
        }

        private void sendResponse(bool isACK)
        {
            //lock (networkStreamLock)
            {
                byte[] response = new byte[2];
                response[0] = Convert.ToByte(threadIndex);
                if (isACK)
                    response[1] = 1;
                else
                    response[1] = 0;
                networkStream.Write(response, 0, response.Length);
            }
            
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


        private bool checkSum(string receivedData, string receivedCrc)
        {
            byte data = convertStringBytesToBytes(receivedData)[0];
            byte crc = convertStringBytesToBytes(receivedCrc)[0];
            byte check = Crc8.ComputeChecksum(data, crc);
            if (check != 0)
                return false;
            return true;
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
            StreamWriter writer = new StreamWriter("Log_" + threadIndex + ".txt", true);
            writer.Write("({0}) {1}: {2}", runTimesIndex, DateTime.Now.ToString(), message);
            writer.Close();
        }

        private void writeLineLogMessage(string message)
        {
            StreamWriter writer = new StreamWriter("Log_" + threadIndex + ".txt", true);
            writer.WriteLine("({0}) {1}: {2}", runTimesIndex, DateTime.Now.ToString(), message);
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

using System.IO;

namespace ReceiverTCP
{
    class Log
    {
        static StreamWriter fileWriter;

        static Log()
        {
            fileWriter = new StreamWriter("Log.txt", true);
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

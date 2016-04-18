using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SenderTCP
{
    class Log
    {
        static StreamWriter fileWriter;

        static Log()
        {
            fileWriter = new StreamWriter("Log.txt");
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

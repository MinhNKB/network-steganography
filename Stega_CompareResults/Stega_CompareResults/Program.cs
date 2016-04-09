using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stega_CompareResults
{
    class Program
    {
        const int NUMBER_OF_RESULT = 3;
        static void Main(string[] args)
        {
            StreamReader reader;
            reader = new StreamReader("data.txt");
            String dataString = reader.ReadToEnd();
            reader.Close();
            for(int i=0;i< NUMBER_OF_RESULT;i++)
            {
                reader = new StreamReader("result" + i.ToString() + ".txt");
                String resultString = reader.ReadToEnd();
                reader.Close();

                int countSimilar = CountSimilarBetweenTwoString(dataString, resultString);
                double percentage = (double)countSimilar / (double)dataString.Length * 100;

                Console.WriteLine("Result " + i.ToString() + ": " + countSimilar.ToString() + "/" + dataString.Length + " - " + percentage.ToString("00.00") + "%");
            }
            Console.ReadKey();
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
    }
}

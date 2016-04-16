using System;
using System.Linq;
using System.Security.Cryptography;
using System.Timers;

namespace FuzzyMatching
{
    public class MainClass
    {
        static void Main()
        {
            var converter = new CloneFinder("LinuxKernel.xml");
            Console.WriteLine("Loading XML-document...");

            if (converter.TryLoadXmlDocument())
            {
                converter.ConvertXmlToPlainText();
                Console.WriteLine("XML was transformed to plain text");
                Console.ReadLine();

                converter.LemmatizeText();
                Console.WriteLine("Text was lemmatized");
                Console.ReadLine();

                converter.StemText();
                Console.WriteLine("Text was stemmed");
                Console.ReadLine();

                converter.BuildAlphabet();
                Console.WriteLine("Alphabet was built");
                Console.ReadLine();

                converter.SplitTextToFragments();
                Console.WriteLine("Text was splitted into fragments");
                Console.ReadLine();

                var a = DateTime.Now;
                converter.FindFuzzyRepetitions();
                Console.WriteLine("All segments were compared");
                Console.WriteLine(DateTime.Now - a);
                Console.ReadLine();

                converter.RestoreXmlFormatting();
                Console.WriteLine("Formatting was restored");
                Console.ReadLine();
            }
            else
            {
                Console.WriteLine("Could not read XML document. Probably it contains formatting mistakes.");
            }
        }
    }
}

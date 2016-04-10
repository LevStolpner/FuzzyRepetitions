using System;
using System.Timers;

namespace FuzzyMatching
{
    public class MainClass
    {
        static void Main()
        {
            var converter = new FormattingConverter("LinuxKernel.xml");

            if (converter.TryLoadXmlDocument())
            {
                converter.RemoveXmlFormatting();

                converter.LemmatizeText();
                Console.WriteLine("Text was lemmatized");
                Console.ReadLine();

                converter.StemText();
                Console.WriteLine("Text was stemmed");
                Console.ReadLine();

                converter.BuildAlphabet();
                Console.WriteLine("Alphabet was built");
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

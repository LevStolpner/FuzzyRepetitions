using System;

namespace FuzzyMatching
{
    public class MainClass
    {
        static void Main()          //TODO: create command line interface for using algorithm
        {
            //settings: size of fragment, max edit distance, difference between hashes, using multithreading if true, if false - one thread
            //var converter = new CloneFinder(@"C:\Users\Leva\Documents\Visual Studio 2013\Projects\FuzzyRepetitions\FuzzyMatching\LinuxKernel.xml", 20, 10, 2);
            var converter = new CloneFinder("LinuxKernel.xml", 20, 10, 2, true);
            converter.Run();
        }
    }
}

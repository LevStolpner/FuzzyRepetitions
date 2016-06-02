namespace FuzzyMatching
{
    public class MainClass
    {
        static void Main()
        {
            //settings: size of fragment, max edit distance, difference between hashes
            //var converter = new CloneFinder(@"C:\Users\Leva\Documents\Visual Studio 2013\Projects\FuzzyRepetitions\FuzzyMatching\LinuxKernel.xml", 20, 10, 2);
            var converter = new CloneFinder("LinuxKernel.xml", 20, 10, 2);
            //parameter - true if using multithreading, if false - one thread
            converter.Run(true);
            //TODO: create command line interface for using algorithm
        }
    }
}

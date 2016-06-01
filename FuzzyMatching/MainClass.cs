namespace FuzzyMatching
{
    public class MainClass
    {
        static void Main()
        {
            var converter = new CloneFinder("LinuxKernel.xml", 20, 10, 2); //settings: size of fragment, max edit distance, difference between hashes
            converter.Run(true);                                           //parameter - true if using multithreading, if false - one thread
        }
    }
}

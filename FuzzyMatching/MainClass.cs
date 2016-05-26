namespace FuzzyMatching
{
    public class MainClass
    {
        static void Main()
        {
            var converter = new CloneFinder("LinuxKernel.xml", 20, 10, 2); //here we pass all settings for our algorithm:
            converter.Run();                                               //first - size of fragment, second - max edit distance between them,
                                                                           //third - difference between hashes of fragments to compare
        }
    }
}

namespace FuzzyMatching
{
    public class MainClass
    {
        static void Main()
        {
            var converter = new CloneFinder("LinuxKernel.xml", 20); //here we will pass all settings for our algorithm to work
            converter.Run();                                    //here we call run method of clone finder to run the algorithm
        }
    }
}

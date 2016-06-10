using System;

namespace FuzzyMatching
{
    public class MainClass
    {
        static void Main(string[] args)
        {
            //arguments: path to xml file, size of fragment, max edit distance, difference between hashes,
            //using multithreading if true, if false - one thread
            int fragmentSize, editDist, hashDist;
            var multithreaded = false;

            if (args != null && args.Length >= 4 && int.TryParse(args[1], out fragmentSize) &&
                int.TryParse(args[2], out editDist) && int.TryParse(args[3], out hashDist))
            {
                var filePath = args[0]; 
                if (args.Length == 5 && args[4] == "m")
                {
                    multithreaded = true;
                }

                var converter = new CloneFinder(filePath, fragmentSize, editDist, hashDist, multithreaded);
                converter.Run();
            }
            else
            {
                Console.WriteLine("Incorrect input parameters.");
            }
        }
    }
}

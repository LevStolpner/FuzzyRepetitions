using System;

namespace CloneFinder
{
    public class Runner
    {
        static string FilePath = string.Empty;
        // was LinuxKernel.xml 20 10 2 1
        static int FragmentSize, EditDist = 20, HashDist = 10, NumberOfThreads = 1;
        static string Language = "English";

        static int Main(string[] args)
        {

            //arguments: path to xml file, size of fragment, max edit distance, difference between hashes,
            //using multithreading: 1 - one thread used, 2 - two threads, 3 - three threads
            try
            {
                var options = new NDesk.Options.OptionSet {
                    { "document=", v => FilePath = v },
                    { "frgamentsize=", v => FragmentSize = int.Parse(v)},
                    { "maxeditdist=", v => EditDist= int.Parse(v) },
                    { "maxhashdist=", v => HashDist = int.Parse(v) },
                    { "threads=", v => NumberOfThreads = int.Parse(v) },
                    { "language=", v => Language = v }
                }.Parse(args);

                var converter = new CloneFinder(FilePath, FragmentSize, EditDist, HashDist, NumberOfThreads, new LanguageSupport(Language));
                converter.Run();
				return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Exception was thrown: {0}", exception.Message);
                Console.Error.WriteLine("Stack:");
                Console.Error.WriteLine(exception.StackTrace);
                return -1;
            }

        }
    }
}

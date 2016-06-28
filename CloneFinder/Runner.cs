﻿using System;

namespace CloneFinder
{
    public class Runner
    {
        static void Main(string[] args)
        {
            //arguments: path to xml file, size of fragment, max edit distance, difference between hashes,
            //using multithreading: 1 - one thread used, 2 - two threads, 3 - three threads
            try
            {
                // was LinuxKernel.xml 20 10 2 1
                int fragmentSize, editDist, hashDist, numberOfThreads;
                if (args != null && args.Length == 5 && int.TryParse(args[1], out fragmentSize) &&
                int.TryParse(args[2], out editDist) && int.TryParse(args[3], out hashDist) && int.TryParse(args[4], out numberOfThreads))
                {
                    var filePath = args[0];
                    var converter = new CloneFinder(filePath, fragmentSize, editDist, hashDist, numberOfThreads);
                    converter.Run();
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Exception was thrown: {0}", exception.Message);
            }

        }
    }
}

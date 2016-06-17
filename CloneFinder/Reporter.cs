using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CloneFinder
{
    class Reporter
    {
        public static void Report(List<List<List<CloneFinder.Fragment>>> data, string filename)
        {
            LinkedList<string> result = new LinkedList<string>();
            int counter = 0;
            int gcounter = 0;
            foreach(var g in data)
            {
                result.AddLast(String.Format("======== {0,4} ========", ++counter));
                int ccounter = 0;
                foreach (var clo in g)
                {
                    result.AddLast(String.Format("---- {0,4} / {1,3} ----", counter, ++ccounter));
                    foreach(var clof in clo)
                        result.AddLast(clof.Repr);
                    gcounter++;
                }
            }
            result.AddLast("Total clones: " + gcounter);

            File.WriteAllLines(filename, result.ToArray(), Encoding.UTF8);
        }
    }
}

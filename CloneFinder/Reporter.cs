using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CloneFinder
{
    class Reporter
    {
        public static void Report(List<List<List<CloneFinder.Fragment>>> data, string wholeText, string filename)
        {
            LinkedList<string> result = new LinkedList<string>();
            int counter = 0;
            int gcounter = 0;

            var ymlGroups = new List<object>();

            foreach (var g in data)
            {
                result.AddLast(String.Format("======== {0,4} ========", ++counter));
                int ccounter = 0;

                var ymlGroup = new List<object>();

                foreach (var clo in g)
                {
                    result.AddLast(String.Format("---- {0,4} / {1,3} ----", counter, ++ccounter));

                    var ymlClone = new List<object>();

                    foreach (var clof in clo) {
                        ymlClone.Add(new
                        {
                            filename = filename,
                            offset = clof.StartOffset,
                            length = clof.LengthInChars
                        });
                        result.AddLast(/*clof.Repr*/clof.GetText(wholeText));
                    }

                    var cloneOffset = clo.First().StartOffset;
                    var cloneLength = clo.Last().StartOffset + clo.Last().LengthInChars - clo.First().StartOffset;
                    var cloneText = wholeText.Substring(cloneOffset, cloneLength);

                    ymlGroup.Add(new
                    {
                        fuzzyclone = new {
                            offset = cloneOffset,
                            length = cloneLength,
                            text = cloneText,
                            fragments = ymlClone.ToArray()
                        }
                    });
                    gcounter++;
                }

                ymlGroups.Add(new {
                    fuzzygroup = ymlGroup.ToArray()
                });
            }
            result.AddLast("Total clones: " + gcounter);

            File.WriteAllLines(filename + ".report", result.ToArray(), Encoding.UTF8);
            using (TextWriter tw = File.CreateText(filename + ".fuzzyclones.yml")) {
                new YamlDotNet.Serialization.Serializer().Serialize(
                    tw,
                    ymlGroups.ToArray()
                );
            }
        }
    }
}

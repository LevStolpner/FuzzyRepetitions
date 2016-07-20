using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace CloneFinder
{
    class Reporter
    {
        protected static IEnumerable<T> ConcatA<T>(IEnumerable<IEnumerable<T>> src) {
            foreach (var c in src)
                foreach (var t in c)
                    yield return t;
        }

        private static Regex tagRegex = new Regex("<.*?>", RegexOptions.Compiled);
        private static Regex spacesRegex = new Regex("\\s+", RegexOptions.Compiled);
        /// <summary>Strip XML tags from text</summary>
        /// <remarks>We probably want better implementation for it</remarks>
        /// <param name="text">Source fragment of XML, mostly unbalanced</param>
        /// <returns></returns>
        private static string CleanupTags(string text)
        {
            var tagStripped = tagRegex.Replace(text, " ");
            var spacesRemoves = spacesRegex.Replace(tagStripped, " ");
            return spacesRemoves;
        }

        public static void Report(List<List<List<CloneFinder.Fragment>>> data, string wholeText, string filename)
        {
            LinkedList<string> result = new LinkedList<string>();
            int counter = 0;
            int gcounter = 0;

            var xmlGroups = new XElement("fuzzygroups");

            foreach (var g in data)
            {
                result.AddLast(String.Format("======== {0,4} ========", ++counter));
                int ccounter = 0;

                var xmlGroup = new XElement("fuzzygroup");

                foreach (var clo in g)
                {
                    var cloneOffset = clo.First().StartOffset;
                    var cloneLength = clo.Last().StartOffset + clo.Last().LengthInChars - clo.First().StartOffset;
                    var rawCloneText = wholeText.Substring(cloneOffset, cloneLength);
                    var cloneWords = string.Join(" ", from cf in clo select cf.Repr);

                    result.AddLast(String.Format("---- {0,4} / {1,3} ----", counter, ++ccounter));
                    result.AddLast(cloneWords);

                    var xmlClone = new XElement("fuzzyclone",
                        new XAttribute("filename", filename),
                        new XAttribute("offset", cloneOffset),
                        new XAttribute("length", cloneLength),
                        new XElement("sourcetext",
                            CleanupTags(rawCloneText)
                        ),
                        new XElement("rawsource",
                            rawCloneText
                        ),
                        new XElement("sourcewords",
                            cloneWords
                        ),
                        new XElement("fragments",
                            from clof in clo select new XElement("fuzzyfragment",
                                new XAttribute("offset", clof.StartOffset),
                                new XAttribute("length", clof.LengthInChars),
                                clof.Repr
                            )
                        )
                    );

                    xmlGroup.Add(xmlClone);
                    gcounter++;
                }

                xmlGroup.SetAttributeValue("id", counter - 1);
                xmlGroups.Add(xmlGroup);
            }
            result.AddLast("Total clones: " + gcounter);

            File.WriteAllLines(filename + ".report", result.ToArray(), Encoding.UTF8);
            xmlGroups.Save(filename + ".fuzzyclones.xml");
        }
    }
}

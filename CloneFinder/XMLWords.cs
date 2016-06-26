using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CloneFinder
{
    /// <summary>
    /// Author: Dmitry V. Luciv, partially basing on orignnal implementation by Lev Stolpner
    /// Returns text words in XML and their offsets
    /// </summary>
    class XMLWords
    {
        private static List<Tuple<string, int>> GetTextWords(string input, int baseOffset)
        {
            var delimeters = new[] {
                ' ', ',', '.', ')', '(', '{', '}', '[', ']', ':', ';', '!', '?', '"', '\'', '/', '\\', '-', '+', '=', '*', '<', '>' ,
                '\t', '\n', '\r' // and also those
            };

            var prewords = input.Split(delimeters/*, StringSplitOptions.RemoveEmptyEntries*/).ToList();     //text is splitted by delimeters
            // empty strings between two delimiters, each word adds 1 + its length to offset
            var preoffsets = new int[prewords.Count];
            {
                var coffs = 0;
                for (var i = 0; i < preoffsets.Length; ++i)
                {
                    preoffsets[i] = coffs;
                    coffs += prewords[i].Length + 1;
                }
            }

            var wordsOffsets = Enumerable.
                Zip(prewords, preoffsets, (w, o) => new Tuple<string, int>(w, o + baseOffset)).
                Where(wo => !string.IsNullOrEmpty(wo.Item1));

            return wordsOffsets.ToList();
        }

        protected readonly string UnixXml;
        // protected readonly string[] lines;
        protected readonly int[] lineoffsets;

        protected static string Dos2Unix(string s)
        {
            return s.Replace("\r", "");
        }

        public XMLWords(string InpuXml)
        {
            this.UnixXml = Dos2Unix(InpuXml); // Only \n, only hardcore
            var lines = UnixXml.Split('\n');
            lineoffsets = new int[lines.Length];

            var curoffs = 0;
            for(var i = 0; i < lines.Length; ++i)
            {
                lineoffsets[i] = curoffs;
                curoffs += lines[i].Length + 1;
            }
        }

        /// <summary>
        /// Converts line and column numbers to codepoint offset
        /// </summary>
        /// <param name="line">1 based line number</param>
        /// <param name="col">1 based column number</param>
        /// <returns></returns>
        protected int LineCol2Offset(int line, int col)
        {
            return lineoffsets[line - 1] + col - 1;
        }

        public string GetReformattedXML()
        {
            return UnixXml;
        }

        public List<Tuple<string, int>> GetWords()
        {
            var results = new List<Tuple<string, int>>();

            // encode and decode it back (lol do something with it)
            using (var sreader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(UnixXml))))
            // then get XML of it
            using (var xreader = XmlReader.Create(sreader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
            {
                var coordinates = (IXmlLineInfo) xreader;
                while (xreader.Read())
                {
                    var ln = coordinates.LineNumber; var cn = coordinates.LinePosition;
                    var offset = LineCol2Offset(ln, cn);

                    var textNode = xreader.Value; // we should not use this due to unescaping
                    var sourceText = string.Empty;
                    switch (xreader.NodeType)
                    {
                        case XmlNodeType.Text:
                            // textEscaped = Dos2Unix(new System.Xml.Linq.XText(textNode).ToString());
                            // source documents often have improper escapes, so this is likely an only option
                            var endOffset = UnixXml.IndexOf("<", offset + 1);
                            sourceText = UnixXml.Substring(offset, endOffset - offset);
                            break;
                        case XmlNodeType.CDATA:
                            sourceText = Dos2Unix(new System.Xml.Linq.XCData(textNode).ToString());
                            sourceText = sourceText.Substring(9, sourceText.Length - 3 - 9);
                            break;
                        default:
                            continue;
                    }

                    var length = sourceText.Length;

#if true
                    // check results
                    var osv = UnixXml.Substring(offset, length);
                    if (osv != sourceText)
                        Console.WriteLine("{0}:{1} -- {2} / {3}", coordinates.LineNumber, coordinates.LinePosition, osv, sourceText);
#endif
                    results.AddRange(GetTextWords(sourceText, offset));
                }

            }

#if true
            // check results
            foreach(var word in results)
                if(UnixXml.Substring(word.Item2, word.Item1.Length) != word.Item1)
                    System.Console.Error.WriteLine("{0}: {1} != {2}", word.Item2, UnixXml.Substring(word.Item2, word.Item1.Length), word.Item1);
#endif

            return results;
        }
    }
}

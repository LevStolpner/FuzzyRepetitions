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
    /// Authors: Geoff McElhanon (http://stackoverflow.com/users/368847/g-mac), http://stackoverflow.com/users/444977/kornman00, http://stackoverflow.com/users/3310192/vikas;
    /// Assembled together by Dmitry V. Luciv
    /// Uses:
    ///     - http://g-m-a-c.blogspot.ru/2013/11/determine-exact-position-of-xmlreader.html (https://web.archive.org/web/20160626061520/http://g-m-a-c.blogspot.ru/2013/11/determine-exact-position-of-xmlreader.html)
    ///     - http://stackoverflow.com/a/22924802/539470 (http://web.archive.org/save/_embed/http://stackoverflow.com/questions/2160533/getting-the-current-position-from-an-xmlreader/22924802#22924802)
    /// </summary>
    public static class XmlReaderExtensions
    {
        private const long DefaultStreamReaderBufferSize = 1024;

        public static long GetPosition(this XmlReader xr, StreamReader underlyingStreamReader)
        {
            long pos = -1;
            while (pos < 0)
            {
                // Get the position of the FileStream
                underlyingStreamReader.Peek();
                long fileStreamPos = underlyingStreamReader.BaseStream.Position;

                //            long fileStreamPos = GetStreamReaderBasePosition(underlyingStreamReader);
                // Get current XmlReader state
                long xmlReaderBufferLength = GetXmlReaderBufferLength(xr);
                long xmlReaderBufferPos = GetXmlReaderBufferPosition(xr);

                // Get current StreamReader state
                long streamReaderBufferLength = GetStreamReaderBufferLength(underlyingStreamReader);
                long streamReaderBufferPos = GetStreamReaderBufferPos(underlyingStreamReader);
                long preambleSize = GetStreamReaderPreambleSize(underlyingStreamReader);


                // Calculate the actual file position
                pos = fileStreamPos
                    - (streamReaderBufferLength == DefaultStreamReaderBufferSize ? DefaultStreamReaderBufferSize : 0)
                    - xmlReaderBufferLength
                    + xmlReaderBufferPos + streamReaderBufferPos;// -preambleSize;
            }
            return pos;
        }
        #region Supporting methods

        private static PropertyInfo _xmlReaderBufferSizeProperty;

        private static long GetXmlReaderBufferLength(XmlReader xr)
        {
            if (_xmlReaderBufferSizeProperty == null)
            {
                _xmlReaderBufferSizeProperty = xr.GetType()
                                                 .GetProperty("DtdParserProxy_ParsingBufferLength",
                                                              BindingFlags.Instance | BindingFlags.NonPublic);
            }

            return (int)_xmlReaderBufferSizeProperty.GetValue(xr);
        }

        private static PropertyInfo _xmlReaderBufferPositionProperty;

        private static int GetXmlReaderBufferPosition(XmlReader xr)
        {
            if (_xmlReaderBufferPositionProperty == null)
            {
                _xmlReaderBufferPositionProperty = xr.GetType()
                                                     .GetProperty("DtdParserProxy_CurrentPosition",
                                                                  BindingFlags.Instance | BindingFlags.NonPublic);
            }

            return (int)_xmlReaderBufferPositionProperty.GetValue(xr);
        }

        private static PropertyInfo _streamReaderPreambleProperty;

        private static long GetStreamReaderPreambleSize(StreamReader sr)
        {
            /*
            if (_streamReaderPreambleProperty == null)
            {
                _streamReaderPreambleProperty = sr.GetType()
                                                  .GetProperty("Preamble_Prop",
                                                               BindingFlags.Instance | BindingFlags.NonPublic);
            }

            return ((byte[])_streamReaderPreambleProperty.GetValue(sr)).Length;
            */
            return sr.CurrentEncoding.GetPreamble().Length;
        }

        private static PropertyInfo _streamReaderByteLenProperty;
        private static PropertyInfo _streamReaderBufferPositionProperty;

        private static long GetStreamReaderBufferLength(StreamReader sr)
        {
            FieldInfo _streamReaderByteLenField = sr.GetType()
                                                .GetField("charLen",
                                                            BindingFlags.Instance | BindingFlags.NonPublic);

            var fValue = (int)_streamReaderByteLenField.GetValue(sr);

            return fValue;
        }

        private static int GetStreamReaderBufferPos(StreamReader sr)
        {
            FieldInfo _streamReaderBufferPositionField = sr.GetType()
                                                .GetField("charPos",
                                                            BindingFlags.Instance | BindingFlags.NonPublic);
            int fvalue = (int)_streamReaderBufferPositionField.GetValue(sr);

            return fvalue;
        }
        #endregion
    }

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

        public static List<Tuple<string, int>> GetWords(string xml)
        {
            var results = new List<Tuple<string, int>>();

            // encode and decode it back (lol do something with it)
            using (var sreader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(xml))))
            // then get XML of it
            using (var xreader = XmlReader.Create(sreader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
            {
                while (xreader.Read())
                {
                    if (xreader.NodeType != XmlNodeType.Text) continue;
                    var textNode = xreader.Value;
                    var offset = xreader.GetPosition(sreader) - textNode.Length;
                    results.AddRange(GetTextWords(textNode, (int)offset /* Docs of >= 2GiB? Not now... */));
                }

            }
            return results;
        }
    }
}

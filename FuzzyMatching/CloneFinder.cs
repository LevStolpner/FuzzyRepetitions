using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using LemmaSharp;
using Iveonik.Stemmers;

namespace FuzzyMatching
{
    public class CloneFinder
    {
        private const int SizeOfSegment = 20;
        private static readonly char[] Delimeters = { ' ', ',', '.', ')', '(', '{', '}', '[', ']', ':', ';', '!', '?', '"', '\'',
                                                      '/', '\\', '-',  '+', '=', '*', '<', '>' };

        private readonly Dictionary<string, string> _replacingValues;
        private readonly List<string> _alphabet;
        private readonly string _initialDocumentName;
        private readonly XmlReaderSettings _readerSettings;
        private readonly ILemmatizer _lemmatizer;
        private readonly EnglishStemmer _stemmer;
        private XmlDocument _document;
        private string _text;
        private string _lemmatizedText;
        private string _stemmedText;
        private string[] _words;
        private int[] _numbers;
        private Fragment[] _arrayOfFragments;          //in this array we will store segments by size of a segment with overlap
        //positions of words?

        public CloneFinder(string initialDocumentName)
        {
            if (!String.IsNullOrEmpty(initialDocumentName))
            {
                _initialDocumentName = initialDocumentName;
                _readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                _lemmatizer = new LemmatizerPrebuiltCompact(LanguagePrebuilt.English);
                _stemmer = new EnglishStemmer();

                _replacingValues = new Dictionary<string, string> { { "\n", " " }, { "\t", " " }, { "   ", " " }, { "  ", " " } };
                _alphabet = new List<string>();
                _text = String.Empty;
                _lemmatizedText = String.Empty;
                _stemmedText = String.Empty;
            }
            else
            {
                throw new ArgumentNullException("initialDocumentName");
            }
        }

        public bool TryLoadXmlDocument()
        {
            try
            {
                _document = new XmlDocument();
                _document.Load(_initialDocumentName);
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        public void ConvertXmlToPlainText()
        {
            if (_document == null) return;

            var sb = new StringBuilder();

            try
            {
                using (var reader = XmlReader.Create(_initialDocumentName, _readerSettings))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Text) continue;
                        sb.Append(reader.Value);
                        sb.Append(' ');
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

            SaveStringToFile(sb.ToString());

            SaveText(sb);
        }

        public void RestoreXmlFormatting()
        {
            if (_document == null) return;

            _document.Save(_initialDocumentName);
        }

        public void LemmatizeText()
        {
            if (String.IsNullOrEmpty(_text)) return;

            _words = _text.Split(Delimeters, StringSplitOptions.RemoveEmptyEntries);
            var lemmatizedText = new StringBuilder();

            foreach (var word in _words.Select(x => x.ToLower()))
            {
                lemmatizedText.Append(_lemmatizer.Lemmatize(word));
                lemmatizedText.Append(' ');
            }

            _lemmatizedText = lemmatizedText.ToString();

            SaveStringToFile(_lemmatizedText);
        }

        public void StemText()
        {
            if (String.IsNullOrEmpty(_lemmatizedText)) return;

            _words = _lemmatizedText.Split(Delimeters, StringSplitOptions.RemoveEmptyEntries);
            var stemmedText = new StringBuilder();

            foreach (var word in _words.Select(x => x.ToLower()))
            {
                stemmedText.Append(_stemmer.Stem(word));
                stemmedText.Append(' ');
            }

            _stemmedText = stemmedText.ToString();

            SaveStringToFile(_stemmedText);
        }

        public void BuildAlphabet()
        {
            if (String.IsNullOrEmpty(_stemmedText)) return;

            _words = _stemmedText.Split(Delimeters, StringSplitOptions.RemoveEmptyEntries);

            var temp = _words.ToList();

            foreach (var word in temp.Where(word => _alphabet.Find(x => x == word) == null))
            {
                _alphabet.Add(word);
            }

            _alphabet.Sort();
            var length = _words.Length;
            _numbers = new int[length];

            for (var i = 0; i < length; i++)
            {
                _numbers[i] = _alphabet.BinarySearch(0, _alphabet.Count, _words[i], Comparer<string>.Default);
            }
        }

        public void SplitTextToFragments()
        {
            if (String.IsNullOrEmpty(_stemmedText) || _alphabet == null) return;

            var length = _words.Length;

            var numberOfFragments = length / SizeOfSegment;

            _arrayOfFragments = new Fragment[numberOfFragments]; //or divide by some part of segment to create overlap
            //last fragment should be worked with after the loop to remove if conditions
            for (int i = 0, k = 0; i < length; i += SizeOfSegment, k++)
            {
                var symbols = new char[SizeOfSegment * 2 / 3]; //remove hardcode

                if (k >= numberOfFragments) break;
                for (var j = i; j < i + SizeOfSegment * 2 / 3; j++)
                {
                    if (j < length)
                        symbols[j - i] = _words[j][0];
                }

                _arrayOfFragments[k] = new Fragment(i, SignatureHash(symbols));
            }

            //now we have array of starting positions of our parts for comparison (do we need to store them?)
        }

        public void FindFuzzyRepetitions()
        {
            if (String.IsNullOrEmpty(_stemmedText) || _alphabet == null) return;

            var counter = 0;
            var counter2 = 0;
            var counter3 = 0;

            var length = _arrayOfFragments.Length;
            var hashLength = _arrayOfFragments[0].Hash.Length;

            //TODO: we should have a mechanism for gathering clones
            for (var i = 300; i < length; i++)
            {
                for (var j = i + 1; j < length; j += 1)   //maybe step is bigger, than 1 segment?
                {
                    //first we need faster hash func to differ similar fragments from absolutely different
                    counter3++;
                    //CalculateLevensteinInstruction(_arrayOfFragments[i].Position, _arrayOfFragments[j].Position);
                    if (!CompareHashes(hashLength, _arrayOfFragments[i].Hash, _arrayOfFragments[j].Hash)) continue;
                    counter2++;
                    if (CompareFragments(_arrayOfFragments[i].Position, _arrayOfFragments[j].Position)) counter++;
                }
            }

            Console.WriteLine("All comparisons: {2} Similar hashes: {0}, similar fragments: {1}", counter2, counter, counter3);
        }

        private bool CompareHashes(int length, bool[] firstHash, bool[] secondHash)
        {
            var counter = 0;

            for (var i = 0; i < length; i++)
            {
                if (firstHash[i] != secondHash[i]) counter++;
                if (counter > length / 4) break;
            }

            return counter <= length / 4;
        }

        private bool[] SignatureHash(char[] symbols)
        {
            //use bit operations and use 1 int, comparing hashes with logical operations and counting differences in bits
            bool[] hash = { false, false, false, false, false, false, false, false, false, false };
            foreach (var symbol in symbols)
            {
                switch (symbol)
                {
                    case 'a':
                    case 'b':
                    case 'c':
                        hash[0] = true;
                        break;
                    case 'd':
                    case 'e':
                    case 'f':
                        hash[1] = true;
                        break;
                    case 'g':
                    case 'h':
                    case 'i':
                        hash[2] = true;
                        break;
                    case 'j':
                    case 'k':
                    case 'l':
                        hash[3] = true;
                        break;
                    case 'm':
                    case 'n':
                    case 'o':
                        hash[4] = true;
                        break;
                    case 'p':
                    case 'q':
                    case 'r':
                        hash[5] = true;
                        break;
                    case 's':
                    case 't':
                    case 'u':
                        hash[6] = true;
                        break;
                    case 'v':
                    case 'w':
                    case 'x':
                        hash[7] = true;
                        break;
                    default:
                        hash[8] = true;
                        break;
                }
            }

            return hash;
        }

        private bool CompareFragments(int firstSegmentPosition, int secondSegmentPosition)
        {
            var counter = SizeOfSegment / 2; // here we set how much wrong words we can find while comparing two segments
            var tmpNumbers1 = new StringBuilder();
            var tmpNumbers2 = new StringBuilder();
            //TODO: use algorithms of fuzzy matching to compare two sets faster! (signature hashing/matching 2 strings/computing edit distance/..)
            for (var i = 0; i < SizeOfSegment; i++)
            {
                if (counter <= 0) return false;
                if (counter + i >= SizeOfSegment)
                {
                    Console.WriteLine("Similar fragments found: {0} {1}", tmpNumbers1, tmpNumbers2);
                    return true;
                }

                if (_numbers[firstSegmentPosition + i] == _numbers[secondSegmentPosition + i])
                {
                    tmpNumbers1.Append(_words[firstSegmentPosition + i]);
                    tmpNumbers1.Append(' ');
                    tmpNumbers2.Append(_words[secondSegmentPosition + i]);
                    tmpNumbers2.Append(' ');
                }
                else
                {
                    counter--;
                }
            }

            Console.WriteLine("Similar fragments found: {0}\n {1}", tmpNumbers1, tmpNumbers2);
            return true;
        }

        public int CalculateLevensteinInstruction(int firstSegmentPosition, int secondSegmentPosition)
        {
            int[,] D = new int[SizeOfSegment, SizeOfSegment];
            var tmp = new int[3];

            D[0, 0] = 0;
            for (var j = 1; j < SizeOfSegment; j++)
            {
                D[0, j] = D[0, j - 1] + 1;
            }
            for (var i = 1; i < SizeOfSegment; i++)
            {
                D[i, 0] = D[i - 1, 0] + 1;
                for (var j = 1; j < SizeOfSegment; j++)
                {
                    if (_numbers[firstSegmentPosition + i] != _numbers[secondSegmentPosition + i])
                    {
                        tmp[0] = D[i - 1, j] + 1;
                        tmp[1] = D[i, j - 1] + 1;
                        tmp[2] = D[i - 1, j - 1] + 1;
                        D[i, j] = tmp.Min();
                    }
                    else
                    {
                        D[i, j] = D[i - 1, j - 1];
                    }
                }
            }

            if (D[SizeOfSegment - 1, SizeOfSegment - 1] < SizeOfSegment / 2)
            {
                Console.WriteLine("Similar fragments found: {0} {2} \n {1} {3}", firstSegmentPosition, secondSegmentPosition,
                    _words[firstSegmentPosition], _words[secondSegmentPosition]);
            }

            return D[SizeOfSegment - 1, SizeOfSegment - 1];
        }


        private void SaveStringToFile(string textToSave)
        {
            try
            {
                using (var streamWriter = new StreamWriter(_initialDocumentName))
                {
                    streamWriter.Write(textToSave);
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        private void SaveText(StringBuilder initialText)
        {
            foreach (var k in _replacingValues.Keys)
            {
                initialText.Replace(k, _replacingValues[k]);
            }

            _text = initialText.ToString();
        }

        public string ReturnText()
        {
            return _text;
        }
    }
}
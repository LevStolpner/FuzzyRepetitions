using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const int HashLength = 9;
        private readonly string _documentName;
        private XmlDocument _document;
        private readonly int _fragmentSize;
        private readonly int[,] _d;
        private int[] _numbers; //TODO: find a way not to store it here
        private List<string> _alphabet = new List<string>();

        public CloneFinder(string documentName, int sizeOfFragment)
        {
            if (!String.IsNullOrEmpty(documentName))
            {
                _documentName = documentName;
                _fragmentSize = sizeOfFragment;
                _d = new int[_fragmentSize, _fragmentSize];

                for (var i = 0; i < _fragmentSize; i++)
                {
                    for (var j = 0; j < _fragmentSize; j++)
                    {
                        _d[i, j] = _fragmentSize * 100;
                    }
                }
            }
            else
            {
                throw new ArgumentNullException("documentName");
            }
        }

        private struct Word
        {
            public string Value;
            public int NumberInAlphabet;

            public Word(string word, int number)
            {
                Value = word;
                NumberInAlphabet = number;
            }
        }

        private class Fragment
        {
            public int Position;
            public int Length;
            public int HashValue;

            public Fragment(int position, int hash)
            {
                Position = position;
                Length = 1;
                HashValue = hash;
            }
        }

        public void Run()
        {
            Console.WriteLine("Loading XML-document...");

            if (TryLoadXmlDocument())
            {
                Console.WriteLine("XML document was loaded");
                var text = ConvertXmlToText(_documentName, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
                Console.WriteLine("XML was transformed to plain text");

                text = Preprocess(text);
                Console.WriteLine("Text was preprocessed");

                var fragments = Split(text);
                Console.WriteLine("Text was splitted into fragments");

                var a = DateTime.Now;
                var clones = FindClones(fragments);
                Console.WriteLine("All segments were compared");
                Console.WriteLine(DateTime.Now - a);

                a = DateTime.Now;
                var groupedClones = GroupAndExpand(clones);
                Console.WriteLine("All clones were grouped and expanded");
                Console.WriteLine(DateTime.Now - a);

                foreach (var t in groupedClones)
                {
                    var sb = new StringBuilder();

                    foreach (var t1 in t)
                    {
                        for (var k = t1.Position;
                            k < t1.Position + t1.Length * _fragmentSize;
                            k++)
                        {
                            sb.Append(_alphabet[_numbers[k]]);
                            sb.Append(' ');
                        }
                        sb.AppendLine();
                    }

                    SaveStringToFile(sb.ToString());
                }

                RestoreXml();
                Console.WriteLine("Formatting was restored");
            }
            else
            {
                Console.WriteLine("Could not read XML document. Probably it contains formatting mistakes.");
            }

            Console.ReadLine();
        }

        private bool TryLoadXmlDocument()
        {
            try
            {
                _document = new XmlDocument();
                _document.Load(_documentName);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private string ConvertXmlToText(string documentName, XmlReaderSettings settings)
        {
            if (_document == null) return null;

            var sb = new StringBuilder();

            try
            {
                using (var reader = XmlReader.Create(documentName, settings))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Text) continue;
                        sb.Append(reader.Value);
                        sb.Append(' ');
                    }
                }

                var replacingValues = new Dictionary<string, string> { { "\n", " " }, { "\t", " " }, { "   ", " " }, { "  ", " " } };

                foreach (var k in replacingValues.Keys)
                {
                    sb.Replace(k, replacingValues[k]);
                }

                return sb.ToString();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        private string Preprocess(string text)
        {
            if (String.IsNullOrEmpty(text)) return null;

            var lemmatizer = new LemmatizerPrebuiltCompact(LanguagePrebuilt.English);
            var stemmer = new EnglishStemmer();
            var delimeters = new[] { ' ', ',', '.', ')', '(', '{', '}', '[', ']', ':', ';', '!', '?', '"', '\'', '/', '\\', '-', '+', '=', '*', '<', '>' };
            var words = text.Split(delimeters, StringSplitOptions.RemoveEmptyEntries);
            var length = words.Length;
            var alphabet = new List<string>();
            var preprocessedText = new StringBuilder();

            for (var i = 0; i < words.Length; i++)
            {
                var newWord = stemmer.Stem(lemmatizer.Lemmatize(words[i].ToLower()));
                words[i] = newWord;
                preprocessedText.Append(newWord);
                preprocessedText.Append(' ');

                if (alphabet.Find(x => x == newWord) == null)
                {
                    alphabet.Add(newWord);
                }
            }

            alphabet.Sort();
            _alphabet = alphabet;
            _numbers = new int[length];

            for (var i = 0; i < length; i++)
            {
                _numbers[i] = alphabet.BinarySearch(0, alphabet.Count, words[i], Comparer<string>.Default);
            }

            return preprocessedText.ToString();
        }

        private Fragment[] Split(string text)
        {
            if (String.IsNullOrEmpty(text)) return null;

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var length = words.Length;
            var numberOfFragments = length / _fragmentSize;
            var arrayOfFragments = new Fragment[numberOfFragments];
            //last fragment should be worked with after the loop to remove if conditions

            for (int i = 0, k = 0; i < length; i += _fragmentSize, k++)
            {
                var symbols = new char[_fragmentSize * 2 / 3]; //TODO:remove hardcode

                if (k >= numberOfFragments) break;
                for (var j = i; j < i + _fragmentSize * 2 / 3; j++)
                {
                    if (j < length)
                        symbols[j - i] = words[j][0];
                }

                arrayOfFragments[k] = new Fragment(i, Hash(symbols));
            }

            return arrayOfFragments;
        }

        private static int Hash(char[] symbols)
        {
            var hash = 0;
            foreach (var symbol in symbols)
            {
                switch (symbol)
                {
                    case 'a':
                    case 'b':
                    case 'c':
                        hash = hash | 1;
                        break;
                    case 'd':
                    case 'e':
                    case 'f':
                        hash = hash | 2;
                        break;
                    case 'g':
                    case 'h':
                    case 'i':
                        hash = hash | 4;
                        break;
                    case 'j':
                    case 'k':
                    case 'l':
                        hash = hash | 8;
                        break;
                    case 'm':
                    case 'n':
                    case 'o':
                        hash = hash | 16;
                        break;
                    case 'p':
                    case 'q':
                    case 'r':
                        hash = hash | 32;
                        break;
                    case 's':
                    case 't':
                    case 'u':
                        hash = hash | 64;
                        break;
                    case 'v':
                    case 'w':
                    case 'x':
                        hash = hash | 128;
                        break;
                    default:
                        hash = hash | 256;
                        break;
                }
            }

            return hash;
        }

        private List<List<Fragment>> FindClones(Fragment[] fragments)
        {
            if (fragments == null) return null;

            var counter = 0;
            var counter2 = 0;
            var counter3 = 0;
            var cloneStorage = new List<List<Fragment>>();
            var length = fragments.Length;

            for (var i = 0; i < length; i++)
            {
                for (var j = i + 1; j < length; j += 1)   //if overlap, step should be different (soo, no overlap?)
                {
                    counter++;
                    if (!CompareHashes(fragments[i].HashValue, fragments[j].HashValue)) continue;
                    counter2++;
                    if (!CompareFragments(fragments[i].Position, fragments[j].Position)) continue;

                    cloneStorage.Add(new List<Fragment> { fragments[i], fragments[j] });
                    counter3++;
                }
            }

            Console.WriteLine("All comparisons: {0} Similar hashes: {1}, similar fragments: {2}", counter, counter2, counter3);
            return cloneStorage;
        }

        private bool CompareHashes(int firstHash, int secondHash)
        {
            var diff = firstHash ^ secondHash;

            var count = 0;
            while (diff != 0)
            {
                count++;
                diff &= (diff - 1);
            }

            return count <= HashLength / 4; //TODO: this parameter should be a setting
        }

        private bool CompareFragments(int firstPosition, int secondPosition)
        {
            var d = _d;
            d[0, 0] = 0;
            var tmp = new int[3];
            int p = _fragmentSize / 4;

            for (var i = 1; i < _fragmentSize; i++)
            {
                var border = Math.Min(_fragmentSize, i + p);
                for (var j = Math.Max(1, i - p); j < border; j++)
                {
                    tmp[0] = _numbers[firstPosition + i] == _numbers[secondPosition + j] ? d[i - 1, j - 1] : d[i - 1, j - 1] + 1;
                    tmp[1] = d[i - 1, j] + 1;
                    tmp[2] = d[i, j - 1] + 1;
                    d[i, j] = tmp.Min();
                }
            }

            return d[_fragmentSize - 1, _fragmentSize - 1] <= _fragmentSize / 2;
        }

        private List<List<Fragment>> GroupAndExpand(List<List<Fragment>> fragments)
        {
            var a = Group(fragments).Select(Expand).ToList();

            return a;
        }

        private List<List<Fragment>> Group(List<List<Fragment>> fragments)
        {
            var firstList = fragments;
            var secondList = new List<List<Fragment>>();

            while (Undistributed(firstList))
            {
                secondList.Add(fragments[0]);

                for (var i = 1; i < firstList.Count; i++)
                {
                    var newList = true;

                    for (var j = 0; j < firstList[i].Count; j++)
                    {
                        var index = secondList.FindIndex(x => x.Exists(y => y.Position == firstList[i][j].Position));

                        if (index < 0) continue;
                        newList = false;
                        firstList[i].ForEach(delegate(Fragment fragment)
                        {
                            if (!secondList[index].Exists(x => x.Position == fragment.Position))
                            {
                                secondList[index].Add(fragment);
                            }
                        });
                        break;
                    }

                    if (newList)
                    {
                        secondList.Add(firstList[i]);
                    }
                }

                firstList = secondList;
                secondList = new List<List<Fragment>>();
            }

            return firstList;

            //algorithm of grouping:
            //first, we look through list of groups (firstly it is pairs)
            //then, we regroup into new list of groups, adding pairs into one group if they have common fragment
            //after that, here we go again: look through list of new groups and regroup again
            //we do that until we have list of groups, which doesn't have any common elements
            //after that grouping we can expand fragments in groups by Expand method
        }

        private bool Undistributed(List<List<Fragment>> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    if (list[i].Exists(x => list[j].Exists(y => y.Position == x.Position)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private List<Fragment> Expand(List<Fragment> fragments)
        {
            var expandLeft = true;
            var expandRight = true;
            var lastFragmentPosition = fragments[fragments.Count - 1].Position;

            while (expandLeft && fragments.TrueForAll(x => x.Position - 1 >= 0))
            {
                for (var i = 0; i < fragments.Count - 1; i++)
                {
                    if (CompareFragments(fragments[i].Position - 1, fragments[i + 1].Position - 1)) continue;
                    expandLeft = false;
                    break;
                }

                if (!expandLeft) continue;
                foreach (var t in fragments)
                {
                    t.Position -= 1;
                    t.Length += 1;
                }
            }

            while (expandRight && fragments.TrueForAll(x => x.Position + 1 > lastFragmentPosition))
            {
                for (var i = 0; i < fragments.Count - 1; i++)
                {
                    if (CompareFragments(fragments[i].Position + 1, fragments[i + 1].Position + 1)) continue;
                    expandRight = false;
                    break;
                }

                if (!expandRight) continue;
                foreach (var t in fragments)
                {
                    t.Position -= 1;
                    t.Length += 1;
                }
            }

            return fragments;
        }

        private bool CalculateLevensteinInstruction(int firstSegmentPosition, int secondSegmentPosition)
        {
            var d = new int[_fragmentSize, _fragmentSize];
            var tmp = new int[3];

            d[0, 0] = 0;
            for (var j = 1; j < _fragmentSize; j++)
            {
                d[0, j] = d[0, j - 1] + 1;
            }
            for (var i = 1; i < _fragmentSize; i++)
            {
                d[i, 0] = d[i - 1, 0] + 1;
                for (var j = 1; j < _fragmentSize; j++)
                {
                    if (_numbers[firstSegmentPosition + i] != _numbers[secondSegmentPosition + j])
                    {
                        tmp[0] = d[i - 1, j] + 1;
                        tmp[1] = d[i, j - 1] + 1;
                        tmp[2] = d[i - 1, j - 1] + 1;
                        d[i, j] = tmp.Min();
                    }
                    else
                    {
                        d[i, j] = d[i - 1, j - 1];
                    }
                }
            }

            return d[_fragmentSize - 1, _fragmentSize - 1] <= _fragmentSize / 2;
        }

        private void RestoreXml()
        {
            if (_document == null) return;

            _document.Save(_documentName);
        }

        private void SaveStringToFile(string textToSave)
        {
            try
            {
                using (var streamWriter = new StreamWriter(_documentName))
                {
                    streamWriter.Write(textToSave);
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using LemmaSharp;
using Iveonik.Stemmers;

namespace FuzzyMatching
{
    public class CloneFinder
    {
        private readonly string _documentName;
        private XmlDocument _document;
        private readonly int _fragmentSize;
        private readonly int _numberOfDifferences;
        private readonly int _hashFragmentDifference;
        private readonly int[][] _d;

        public CloneFinder(string documentName, int sizeOfFragment, int numberOfDifferences, int hashFragmentDifference)
        {
            if (!String.IsNullOrEmpty(documentName))
            {
                _documentName = documentName;
                _fragmentSize = sizeOfFragment;
                _numberOfDifferences = numberOfDifferences;
                _hashFragmentDifference = hashFragmentDifference;
                _d = new int[_fragmentSize][];

                for (var i = 0; i < _fragmentSize; i++)
                {
                    _d[i] = new int[_fragmentSize];
                    for (var j = 0; j < _fragmentSize; j++)
                    {
                        _d[i][j] = _fragmentSize * 100;        //this table is needed for fast algorithm, calculating edit distance
                    }
                }
            }
            else
            {
                throw new ArgumentNullException("documentName");
            }
        }

        private class Fragment
        {
            public readonly int Position;
            public readonly int[] Words;
            public readonly int HashValue;
            public Fragment(int position)
            {
                Position = position;
                HashValue = 0;
            }
            public Fragment(int position, int[] words, int hash)
            {
                Position = position;
                Words = words;
                HashValue = hash;
            }
        }

        public void Run()
        {
            if (TryLoadXmlDocument())
            {
                var text = ConvertXmlToText(_documentName, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
                var fragments = Preprocess(text);
                var fragments2 = new Fragment[fragments.Length];
                Array.Copy(fragments, fragments2, fragments.Length);
                var a = DateTime.Now;

                //var clones = FindClones(fragments.ToList(), 0, fragments.Length);

                var firstTask = Task.Factory.StartNew(() => FindClones(fragments.ToList(), 0, fragments.Length / 3));
                var secondTask = Task.Factory.StartNew(() => FindClones(fragments2.ToList(), fragments.Length / 3 + 1, fragments.Length));
                Task.WaitAll(firstTask, secondTask);
                var clones = firstTask.Result;
                var clones2 = secondTask.Result;
                clones.AddRange(clones2);
                Console.WriteLine(DateTime.Now - a);

                var expandedClones = Group(clones).Select(x => Expand(x, fragments)).ToList();
                var newGroupedClones = DeleteIntersections(expandedClones);
                var numberOfGroups = newGroupedClones.Count;
                var averageSizeOfGroup = newGroupedClones.Sum(t => t.Count) / newGroupedClones.Count;
                var averageSizeOfClone = newGroupedClones.Sum(t => t.Sum(t1 => t1.Count)) * _fragmentSize / averageSizeOfGroup / numberOfGroups;
                Console.WriteLine("Statistics: ");
                Console.WriteLine("Number of groups: {0}\nAverage size of group: {1}\nAverageSizeOfClone: {2}", numberOfGroups, averageSizeOfGroup, averageSizeOfClone);
                Console.ReadLine();
                RestoreXml();
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
                Console.WriteLine(e.Message);
                return false;
            }
        }

        private string ConvertXmlToText(string documentName, XmlReaderSettings settings)
        {
            if (_document == null) return null;

            try
            {
                return ReadDocument(documentName, settings);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        private Fragment[] Preprocess(string text)
        {
            if (String.IsNullOrEmpty(text)) return null;

            var lemmatizer = new LemmatizerPrebuiltCompact(LanguagePrebuilt.English);
            var stemmer = new EnglishStemmer();
            var delimeters = new[] { ' ', ',', '.', ')', '(', '{', '}', '[', ']', ':', ';', '!', '?', '"', '\'', '/', '\\', '-', '+', '=', '*', '<', '>' };
            var words = text.Split(delimeters, StringSplitOptions.RemoveEmptyEntries);
            var alphabet = new List<string>();

            var preprocessedText = NormalizeAndCreateAlphabet(words.ToList(), lemmatizer, stemmer, ref alphabet); //this part can be parallelized
            var textInNumbers = ConvertTextWithNewAlphabet(words, alphabet);                                      //by splitting words in different lists

            return Split(preprocessedText, textInNumbers);
        }

        private Fragment[] Split(string text, int[] newText)
        {
            if (String.IsNullOrEmpty(text)) return null;

            return FragmentText(text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), newText);
        }

        private List<List<Fragment>> FindClones(List<Fragment> fragments, int searchStartPosition,
            int searchStopPosition)
        {
            if (fragments == null) return null;
            var cloneStorage = new List<List<Fragment>>();

            var length = fragments.Count;

            for (var i = searchStartPosition; i < searchStopPosition; i++)
            {
                for (var j = i + 1; j < length; j += 1)
                {
                    if (!CompareHashes(fragments[i].HashValue, fragments[j].HashValue)) continue;
                    if (!CompareFragments(fragments[i], fragments[j])) continue;
                    cloneStorage.Add(new List<Fragment> { fragments[i], fragments[j] });
                }
            }

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

            return count <= _hashFragmentDifference;
        }

        private bool CompareFragments(Fragment first, Fragment second)
        {
                var d = _d.Select(a => a.ToArray()).ToArray();
                d[0][0] = 0;
                var tmp = new int[3];
                var p = _fragmentSize / 4;

                for (var i = 1; i < _fragmentSize; i++)
                {
                    var border = Math.Min(_fragmentSize, i + p);
                    for (var j = Math.Max(1, i - p); j < border; j++)
                    {
                        tmp[0] = first.Words[i] == second.Words[j] ? d[i - 1][j - 1] : d[i - 1][j - 1] + 1;
                        tmp[1] = d[i - 1][j] + 1;
                        tmp[2] = d[i][j - 1] + 1;
                        d[i][j] = tmp.Min();
                    }
                }

                return d[_fragmentSize - 1][_fragmentSize - 1] <= _numberOfDifferences;
        }

        private List<List<Fragment>> Group(List<List<Fragment>> fragments)
        {
            while (Undistributed(fragments))
            {
                fragments = RegroupClones(fragments);
            }

            return fragments;
        }

        private List<List<Fragment>> Expand(List<Fragment> groupOFragments, Fragment[] allFragments)
        {
            var result = groupOFragments.Select(t => new List<Fragment> { t }).ToList();

            return ExpandRight(ExpandLeft(result, allFragments), allFragments);
        }

        private List<List<List<Fragment>>> DeleteIntersections(List<List<List<Fragment>>> groupedFragments)
        {
            //after regrouping some groups may have same sequence of fragments, thus one of conflicted groups of clones should be removed
            var result = new List<List<List<Fragment>>>();
            var excludedFromSearch = new List<int>();

            for (var i = 0; i < groupedFragments.Count; i++)
            {
                result = DeleteIntersectionsOfGroup(result, i, groupedFragments, ref excludedFromSearch);
            }

            return result;
        }

        #region Auxiliary methods

        private string ReadDocument(string documentName, XmlReaderSettings settings)
        {
            var sb = new StringBuilder();

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

        private string NormalizeAndCreateAlphabet(List<string> words, LemmatizerPrebuiltCompact lemmatizer, EnglishStemmer stemmer, ref List<string> alphabet)
        {
            var preprocessedText = new StringBuilder();

            for (var i = 0; i < words.Count; i++)
            {
                var newWord = stemmer.Stem(lemmatizer.Lemmatize(words[i].ToLower())); //firstly the words are lemmatized, then stemmed
                words[i] = newWord;
                preprocessedText.Append(newWord);
                preprocessedText.Append(' ');

                if (alphabet.Find(x => x == newWord) == null)
                {
                    alphabet.Add(newWord);
                }
            }

            alphabet.Sort();

            return preprocessedText.ToString();
        }

        private int[] ConvertTextWithNewAlphabet(string[] words, List<string> alphabet)
        {
            var numbers = new int[words.Length];

            var length = words.Length;

            for (var i = 0; i < length; i++)
            {
                numbers[i] = alphabet.BinarySearch(0, alphabet.Count, words[i], Comparer<string>.Default); //convert text to numbers from new alphabet
            }

            return numbers;
        }

        private Fragment[] FragmentText(string[] words, int[] newText)
        {
            var wordsLength = words.Length;
            var numberOfFragments = wordsLength / _fragmentSize;
            var arrayOfFragments = new Fragment[numberOfFragments];

            for (int i = 0, k = 0; i < wordsLength; i += _fragmentSize, k++)
            {
                var symbols = new char[_fragmentSize];
                var numbers = new int[_fragmentSize];

                if (k >= numberOfFragments) break;
                for (var j = i; j < i + _fragmentSize; j++)
                {
                    numbers[j - i] = newText[j];
                    if (j < wordsLength) symbols[j - i] = words[j][0];
                }

                arrayOfFragments[k] = new Fragment(k, numbers, Hash(symbols));
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

        private bool Undistributed(List<List<Fragment>> list)
        {
            //this function checks in list of groups of clones, if different groups have same fragment
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

        private List<List<Fragment>> RegroupClones(List<List<Fragment>> firstList)
        {
            //algorithm of grouping:
            //firstly, look through list of groups (in beginning just pairs)
            //then, regroup them by joining pairs if they have common fragment
            //after that look through list of new groups and regroup again
            //do that until there is list of groups with no common elements
            //after that grouping we can expand fragments in groups
            var secondList = new List<List<Fragment>> { firstList[0] };

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

            return secondList;
        }

        private List<List<Fragment>> ExpandLeft(List<List<Fragment>> groupOfClones, Fragment[] allFragments)
        {
            var expandLeft = true;
            //comparing neighbour fragments from the left
            while (expandLeft && groupOfClones.TrueForAll(x => x.First().Position - 1 >= 0))
            {
                //code below checks if we can expand to the left, because if some elements of the group cannot, expanding to left stops
                for (var i = 0; i < groupOfClones.Count - 1; i++)
                {
                    if (CompareFragments(allFragments.First(x => x.Position == groupOfClones[i].First().Position - 1),
                        allFragments.First(x => x.Position == groupOfClones[i + 1].First().Position - 1))) continue;
                    expandLeft = false;
                    break;
                }

                if (!expandLeft) continue;
                foreach (var t in groupOfClones)
                {
                    t.Insert(0, new Fragment(t.First().Position - _fragmentSize));
                }
            }

            return groupOfClones;
        }

        private List<List<Fragment>> ExpandRight(List<List<Fragment>> groupOfClones, Fragment[] allFragments)
        {
            var expandRight = true;
            var lastFragmentPosition = allFragments[allFragments.Length - 1].Position;

            //comparing neighbour fragments from the right
            while (expandRight && groupOfClones.TrueForAll(x => x.Last().Position + 1 <= lastFragmentPosition))
            {
                //code below checks if we can expand to the right, because if some elements of the group cannot, expanding to right stops
                for (var i = 0; i < groupOfClones.Count - 1; i++)
                {
                    if (CompareFragments(allFragments.First(x => x.Position == groupOfClones[i].Last().Position + 1),
                        allFragments.First(x => x.Position == groupOfClones[i + 1].Last().Position + 1))) continue;
                    expandRight = false;
                    break;
                }

                if (!expandRight) continue;
                foreach (var t in groupOfClones)
                {
                    t.Add(new Fragment(t.Last().Position + 1));
                }
            }

            return groupOfClones;
        }

        private List<List<List<Fragment>>> DeleteIntersectionsOfGroup(List<List<List<Fragment>>> result, int currentListId,
            List<List<List<Fragment>>> groupedFragments, ref List<int> excludedFromSearch)
        {
            if (excludedFromSearch.Contains(currentListId)) return result;

            var currentList = groupedFragments[currentListId];
            var foundIntersection = false;

            for (var j = currentListId + 1; j < groupedFragments.Count; j++)
            {
                FindIntersectionsBetweenGroups(result, currentListId, j, groupedFragments, ref foundIntersection, ref excludedFromSearch);

                if (foundIntersection)
                {
                    break;
                }
            }

            if (!foundIntersection)
            {
                //if there are no intersections for current list, we exclude it from farther search and add to the result
                result.Add(currentList);
                excludedFromSearch.Add(currentListId);
            }

            return result;
        }

        private void FindIntersectionsBetweenGroups(List<List<List<Fragment>>> result, int currentListId, int secondListId,
            List<List<List<Fragment>>> groupedFragments, ref bool foundIntersection, ref List<int> excludedFromSearch)
        {
            if (excludedFromSearch.Contains(secondListId)) return;
            var currentList = groupedFragments[currentListId];
            var comparedList = groupedFragments[secondListId]; //another group of clones

            //comparing one group with another to see, does it have same fragments with currentList

            var cloneWithSameFragments =
                currentList.Find(clone => clone.Exists(fragment => comparedList.Exists(clone2 =>
                    clone2.Exists(fragment2 => fragment2.Position == fragment.Position))));

            if (cloneWithSameFragments != null)
            {
                //now function m*n^2 will show, which of groups should be deleted, where m = size of group, n = size of clone in fragments
                foundIntersection = true;
                var currentListValue = currentList.Count * cloneWithSameFragments.Count * cloneWithSameFragments.Count;
                var secondClone = comparedList.Find(clone2 =>
                    clone2.Exists(fragment2 => cloneWithSameFragments.Exists(fragment => fragment2.Position == fragment.Position)));
                var comparedListValue = comparedList.Count * secondClone.Count * secondClone.Count;

                var list = currentListValue >= comparedListValue ? currentList : comparedList;
                result.Add(list);
                excludedFromSearch.Add(currentListId); //exclude fragments from search, which had conflicted already
                excludedFromSearch.Add(secondListId); //maybde TODO: not include in this lists groups in case there are 3 or more groups with same fragments
                //TODO: exclude only groups, lost in competition :) others should go on
            }
        }

        private void RestoreXml()
        {
            if (_document == null) return;

            _document.Save(_documentName);
        }

        //private void SaveStringToFile(string textToSave)
        //{
        //    try
        //    {
        //        using (var streamWriter = new StreamWriter(_documentName))
        //        {
        //            streamWriter.Write(textToSave);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception(e.Message);
        //    }
        //}
        #endregion
    }
}
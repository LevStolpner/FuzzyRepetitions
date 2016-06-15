using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using LemmaSharp;
using Iveonik.Stemmers;

namespace CloneFinder
{
    public class CloneFinder
    {
        private const int HashLength = 9;
        private readonly string _documentPath;

        private readonly int _fragmentSize;
        private readonly int _numberOfDifferences;           //parameter for maximal edit distance between fragments
        private readonly int _hashFragmentDifference;        //parameter for hash value difference between fragments
        private readonly int _numberOfThreads;

        private readonly int[][] _d;                         //table, which will be used for fast calculating edit distance algorithm

        public CloneFinder(string documentPath, int sizeOfFragment, int numberOfDifferences, int hashFragmentDifference, int numberOfThreads)
        {
            if (String.IsNullOrEmpty(documentPath))
            {
                throw new ArgumentNullException("documentPath");
            }
            if (sizeOfFragment <= 0 || numberOfDifferences <= 0 || hashFragmentDifference <= 0 ||
                sizeOfFragment <= numberOfDifferences || hashFragmentDifference >= HashLength ||
                numberOfThreads < 1 || numberOfThreads > 3)
            {
                throw new Exception("Incorrect arguments");
            }

            _documentPath = documentPath;
            _fragmentSize = sizeOfFragment;
            _numberOfDifferences = numberOfDifferences;
            _hashFragmentDifference = hashFragmentDifference;
            _numberOfThreads = numberOfThreads;
            _d = new int[_fragmentSize][];

            for (var i = 0; i < _fragmentSize; i++)
            {
                _d[i] = new int[_fragmentSize];
                for (var j = 0; j < _fragmentSize; j++)
                {
                    _d[i][j] = _fragmentSize * 100;        //initializing table with big numbers for needs of algorithm
                }
            }
        }

        private class Fragment
        {
            public readonly int Position;
            public readonly int[] Words;
            public readonly int HashValue;
            public Fragment(int position, int[] words, int hash)
            {
                Position = position;
                Words = words;
                HashValue = hash;
            }
        }

        public void Run()
        {
            var text = ConvertXmlToText(_documentPath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            var fragments = Preprocess(text);      //preprocessing includes lemmatizing, stemming, creating alphabet and splitting to fragments
            List<List<Fragment>> clones;

            var a = DateTime.Now;
            switch (_numberOfThreads)
            {
                //here only one thread will be used to compare fragments
                case 1:
                    clones = FindClones(fragments.ToList(), 0, fragments.Length);
                    break;
                //two threads will be used to compare fragments from separate parts of text
                case 2:
                    {
                        var firstTask = Task.Factory.StartNew(() => FindClones(fragments.ToList(), 0, fragments.Length / 3));
                        var secondTask = Task.Factory.StartNew(() => FindClones(fragments.ToList(), fragments.Length / 3 + 1, fragments.Length));
                        Task.WaitAll(firstTask, secondTask);
                        clones = firstTask.Result;
                        clones.AddRange(secondTask.Result);
                        break;
                    }
                //three threads will be used to compare fragments from separate parts of text
                default:
                    {
                        var firstTask = Task.Factory.StartNew(() => FindClones(fragments.ToList(), 0, fragments.Length / 4));
                        var secondTask = Task.Factory.StartNew(() => FindClones(fragments.ToList(), fragments.Length / 4 + 1, fragments.Length / 2));
                        var thirdTask = Task.Factory.StartNew(() => FindClones(fragments.ToList(), fragments.Length / 2 + 1, fragments.Length));
                        Task.WaitAll(firstTask, secondTask, thirdTask);
                        clones = firstTask.Result;
                        clones.AddRange(secondTask.Result);
                        clones.AddRange(thirdTask.Result);
                        break;
                    }
            }
            Console.WriteLine(DateTime.Now - a);

            //clone pairs are being gathered into groups of similar fragments, after that each of grouped clones gets expanded
            var expandedClones = Group(clones).Select(x => Expand(x, fragments)).ToList();
            //expanded clones may have intersections between clones from different groups, so some of them are redundant
            a = DateTime.Now;
            var newGroupedClones = DeleteIntersections(expandedClones);
            while (HasIntersections(newGroupedClones))
                newGroupedClones = DeleteIntersections(newGroupedClones);
            Console.WriteLine(DateTime.Now - a);

            //TODO: if there are same words from left or right fragments, there should be a method joining them to clones
            var numberOfGroups = newGroupedClones.Count;
            var averageSizeOfGroup = newGroupedClones.Sum(t => t.Count) / newGroupedClones.Count;
            var averageSizeOfClone = newGroupedClones.Sum(t => t.Sum(t1 => t1.Count)) * _fragmentSize / averageSizeOfGroup / numberOfGroups;
            Console.WriteLine("Statistics: ");
            Console.WriteLine("Number of groups: {0}\nAverage size of group: {1}\nAverageSizeOfClone: {2}", numberOfGroups, averageSizeOfGroup, averageSizeOfClone);
        }

        private string ConvertXmlToText(string documentPath, XmlReaderSettings settings)
        {
            if (String.IsNullOrEmpty(documentPath) || settings == null)
            {
                throw new Exception("Incorrect parameters for converting XML-document");
            }

            return ReadDocument(documentPath, settings);
        }

        private Fragment[] Preprocess(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                throw new ArgumentNullException("text");
            }

            var lemmatizer = new LemmatizerPrebuiltCompact(LanguagePrebuilt.English);
            var stemmer = new EnglishStemmer();
            var delimeters = new[] { ' ', ',', '.', ')', '(', '{', '}', '[', ']', ':', ';', '!', '?', '"', '\'', '/', '\\', '-', '+', '=', '*', '<', '>' };
            var words = text.Split(delimeters, StringSplitOptions.RemoveEmptyEntries).ToList();     //text is splitted by delimeters
            var alphabet = new List<string>();
            //this part can be parallelized by splitting words in different lists
            var preprocessedText = NormalizeAndCreateAlphabet(words, lemmatizer, stemmer, alphabet);
            var textInNumbers = ConvertTextWithNewAlphabet(words.ToArray(), alphabet);    //instead of words in text there will be numbers (of word in alphabet)

            return Split(preprocessedText, textInNumbers);
        }

        private Fragment[] Split(string text, int[] newText)
        {
            if (String.IsNullOrEmpty(text) || newText == null || newText.Length == 0)
            {
                throw new Exception("Incorrect parameters for splitting text to fragments");
            }
            //This method will return an array of Fragments
            return FragmentText(text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), newText);
        }

        private List<List<Fragment>> FindClones(List<Fragment> fragments, int searchStartPosition, int searchStopPosition)
        {
            if (fragments == null || fragments.Count == 0 || searchStartPosition < 0 ||
                searchStopPosition > fragments.Count || searchStartPosition >= searchStopPosition)
            {
                throw new Exception("Incorrect parameters for comparing clones");
            }

            var cloneStorage = new List<List<Fragment>>();
            var length = fragments.Count;
            //this loop will go through all fragments from start position to stop position and compare them to every other fragment in text
            for (var i = searchStartPosition; i < searchStopPosition; i++)
            {
                for (var j = i + 1; j < length; j += 1)
                {
                    //first hashes from fragments are compared, if they are similar, then fragments are compared by calculating edit distance
                    if (!CompareHashes(fragments[i].HashValue, fragments[j].HashValue)) continue;
                    if (!CompareFragments(fragments[i], fragments[j])) continue;
                    cloneStorage.Add(new List<Fragment> { fragments[i], fragments[j] }); //store found pairs of similar fragments
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
            if (first == null || second == null)
            {
                throw new Exception("Null arguments while comparing fragments");
            }

            var d = _d.Select(a => a.ToArray()).ToArray(); //method needs a copy of initialized table
            d[0][0] = 0;                                   //so there will be no problem with accessing same resource by threads
            var tmp = new int[3];
            var p = _fragmentSize / 4;
            //algorithm, calculating edit distance between fragments
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

            return d[_fragmentSize - 1][_fragmentSize - 1] <= _numberOfDifferences; //if edit distance is less than some threshold, method returns true
        }

        private List<List<Fragment>> Group(List<List<Fragment>> fragments)
        {
            if (fragments == null || fragments.Count == 0)
            {
                throw new ArgumentNullException("fragments");
            }

            while (Undistributed(fragments)) //if some groups have same fragments, they need to be grouped in one 
            {
                fragments = RegroupClones(fragments);
            }

            return fragments;
        }

        private List<List<Fragment>> Expand(List<Fragment> groupOFragments, Fragment[] allFragments)
        {
            if (groupOFragments == null || groupOFragments.Count == 0 || allFragments == null ||
                allFragments.Length == 0)
            {
                throw new Exception("Null arguments while expanding groups");
            }

            var result = groupOFragments.Select(t => new List<Fragment> { t }).ToList(); //instead of one fragment, clones will be a list of them

            return ExpandRight(ExpandLeft(result, allFragments), allFragments);          //expanding groups in both directions
        }

        private List<List<List<Fragment>>> DeleteIntersections(List<List<List<Fragment>>> groupedFragments)
        {
            if (groupedFragments == null || groupedFragments.Count == 0)
            {
                throw new ArgumentNullException("groupedFragments");
            }

            //after regrouping some groups may have same sequence of fragments, thus one of conflicted groups of clones should be removed
            var result = new List<List<List<Fragment>>>();
            var excludedFromSearch = new List<int>();

            for (var i = 0; i < groupedFragments.Count; i++)
            {
                //method will find and delete intersections with i-th group in list
                result = DeleteIntersectionsOfGroup(result, i, groupedFragments, excludedFromSearch);
            }

            return result;
        }

        #region Auxiliary methods

        private string ReadDocument(string documentPath, XmlReaderSettings settings)
        {
            if (String.IsNullOrEmpty(documentPath) || settings == null)
            {
                throw new Exception("Null parameters while reading XML-document");
            }

            var sb = new StringBuilder();

            using (var reader = XmlReader.Create(documentPath, settings))  //here would work absolute or relative path
            {
                var xmlInfo = (IXmlLineInfo)reader;                        //adding a possibility to save positions of words
                //TODO: save coordinates of nodes (words?) and store them in fragments (begin and end?)
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Text) continue;
                    sb.Append(reader.Value);
                    sb.Append(' ');

                    var line = xmlInfo.LineNumber;
                    var position = xmlInfo.LinePosition;
                }
            }

            //replacing some symbols in text to decrease its size
            var replacingValues = new Dictionary<string, string> { { "\n", " " }, { "\t", " " }, { "   ", " " }, { "  ", " " } };

            foreach (var k in replacingValues.Keys)
            {
                sb.Replace(k, replacingValues[k]);
            }

            return sb.ToString();
        }

        private string NormalizeAndCreateAlphabet(List<string> words, ILemmatizer lemmatizer, IStemmer stemmer, List<string> alphabet)
        {
            if (words == null || lemmatizer == null || stemmer == null)
            {
                throw new Exception("Null parameters while normalizing text");
            }

            var preprocessedText = new StringBuilder();

            for (var i = 0; i < words.Count; i++)
            {
                var newWord = stemmer.Stem(lemmatizer.Lemmatize(words[i].ToLower())); //firstly the words are lemmatized, then stemmed
                words[i] = newWord;
                preprocessedText.Append(newWord);
                preprocessedText.Append(' ');

                if (alphabet.Find(x => x == newWord) == null)
                {
                    alphabet.Add(newWord);            //creating alphabet of words in text
                }
            }

            alphabet.Sort();                          //alphabet is sorted so other method could use it to convert words to numbers in text

            return preprocessedText.ToString();
        }

        private int[] ConvertTextWithNewAlphabet(string[] words, List<string> alphabet)
        {
            if (words == null || alphabet == null)
            {
                throw new Exception("Null parameters while converting to plain text");
            }

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
            if (words == null || words.Length == 0 || newText == null || newText.Length == 0)
            {
                throw new Exception("Incorrect parameters for fragmenting text");
            }

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
                    numbers[j - i] = newText[j];                            //numbers would represent words that are in current fragment
                    if (j < wordsLength) symbols[j - i] = words[j][0];      //symbols are first letters of each word in fragment
                }

                arrayOfFragments[k] = new Fragment(k, numbers, Hash(symbols));    //hash function uses array of first letters
            }

            return arrayOfFragments;
        }

        private static int Hash(char[] symbols)
        {
            //signature hashing: if array contains symbol from one of the groups below, some matched bit in hash value is set to 1 
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
            if (list == null)
            {
                throw new ArgumentNullException("list");
            }

            //this function checks list of groups of clones, if different groups have same fragment
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

        private bool HasIntersections(List<List<List<Fragment>>> listOfGroups)
        {
            if (listOfGroups == null)
            {
                throw new ArgumentNullException("listOfGroups");
            }

            //this function checks list of groups of clones, if different groups have same fragment
            for (var i = 0; i < listOfGroups.Count; i++)
            {
                for (var j = i + 1; j < listOfGroups.Count; j++)
                {
                    if (listOfGroups[i].Exists(x => x.Exists(y => listOfGroups[j].Exists(z => z.Exists(w => w.Position == y.Position)))))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private List<List<Fragment>> RegroupClones(List<List<Fragment>> firstList)
        {
            if (firstList == null)
            {
                throw new ArgumentNullException("firstList");
            }

            //algorithm of grouping:
            //firstly, look through list of groups (in beginning just pairs)
            //then, regroup them by joining pairs if they have common fragment
            //after that look through list of new groups and regroup again
            //do that until there is list of groups with no common elements
            //after that grouping we can expand fragments in groups
            var secondList = new List<List<Fragment>> { firstList[0] };     //new list will always contain better grouped clones, then previous one

            for (var i = 1; i < firstList.Count; i++)
            {
                var newList = true;

                for (var j = 0; j < firstList[i].Count; j++)
                {
                    var index = secondList.FindIndex(x => x.Exists(y => y.Position == firstList[i][j].Position)); //found same fragment in other group

                    if (index < 0) continue;
                    newList = false;
                    firstList[i].ForEach(delegate(Fragment fragment)
                    {
                        if (!secondList[index].Exists(x => x.Position == fragment.Position))
                        {
                            secondList[index].Add(fragment); //add fragments to group if they are not there already
                        }
                    });
                    break;
                }

                if (newList)
                {
                    secondList.Add(firstList[i]); //add a group to list if it had no same fragments with other ones
                }
            }

            return secondList;
        }

        private List<List<Fragment>> ExpandLeft(List<List<Fragment>> groupOfClones, Fragment[] allFragments)
        {
            if (groupOfClones == null || allFragments == null)
            {
                throw new Exception("Null parameters for expanding to left");
            }

            var expandLeft = true;
            //comparing neighbour fragments from the left
            while (expandLeft && groupOfClones.TrueForAll(x => x.First().Position - 1 >= 0))
            {
                //code below checks if we can expand to the left, because if some elements of the group cannot, expanding to left stops
                for (var i = 0; i < groupOfClones.Count - 1; i++)
                {
                    //if neighbour fragments from the left are similar for different clones in one group
                    if (CompareFragments(allFragments.First(x => x.Position == groupOfClones[i].First().Position - 1),
                        allFragments.First(x => x.Position == groupOfClones[i + 1].First().Position - 1))) continue;
                    expandLeft = false;
                    break;
                }

                if (!expandLeft) continue;
                foreach (var t in groupOfClones)
                {
                    t.Insert(0, allFragments[t.First().Position - 1]);
                    //add fragments from the left to beginning of clones 
                }
            }

            return groupOfClones;
        }

        private List<List<Fragment>> ExpandRight(List<List<Fragment>> groupOfClones, Fragment[] allFragments)
        {
            if (groupOfClones == null || allFragments == null)
            {
                throw new Exception("Null parameters for expanding to right");
            }

            var expandRight = true;
            var lastFragmentPosition = allFragments[allFragments.Length - 1].Position;

            //comparing neighbour fragments from the right
            while (expandRight && groupOfClones.TrueForAll(x => x.Last().Position + 1 <= lastFragmentPosition))
            {
                //code below checks if we can expand to the right, because if some elements of the group cannot, expanding to right stops
                for (var i = 0; i < groupOfClones.Count - 1; i++)
                {
                    //if neighbour fragments from the right are similar for different clones in one group
                    if (CompareFragments(allFragments.First(x => x.Position == groupOfClones[i].Last().Position + 1),
                        allFragments.First(x => x.Position == groupOfClones[i + 1].Last().Position + 1))) continue;
                    expandRight = false;
                    break;
                }

                if (!expandRight) continue;
                foreach (var t in groupOfClones)
                {
                    t.Add(allFragments[t.Last().Position + 1]);
                    //add fragments from the right to the end of clones 
                }
            }

            return groupOfClones;
        }

        private List<List<List<Fragment>>> DeleteIntersectionsOfGroup(List<List<List<Fragment>>> result, int currentListId,
            List<List<List<Fragment>>> groupedFragments, List<int> excludedFromSearch)
        {
            if (excludedFromSearch.Contains(currentListId)) return result;     //groups can be excluded because of intesections

            var currentList = groupedFragments[currentListId];
            var foundIntersection = false;

            for (var j = currentListId + 1; j < groupedFragments.Count; j++)
            {
                //for two groups method would find intersections between them
                FindIntersectionsBetweenGroups(result, currentListId, j, groupedFragments, ref foundIntersection, excludedFromSearch);

                if (foundIntersection) break;
            }

            if (!foundIntersection)
            {
                result.Add(currentList);
                excludedFromSearch.Add(currentListId);
            }
            //if there are no intersections for current list, we exclude it from farther search and add to the result

            return result;
        }

        private void FindIntersectionsBetweenGroups(List<List<List<Fragment>>> result, int currentListId, int secondListId,
            List<List<List<Fragment>>> groupedFragments, ref bool foundIntersection, List<int> excludedFromSearch)
        {
            if (excludedFromSearch.Contains(secondListId)) return;
            var currentList = groupedFragments[currentListId];
            var comparedList = groupedFragments[secondListId]; //another group of clones
            List<Fragment> secondCloneWithIntersections = null;

            //comparing one group with another to see, does it have same fragments with currentList

            var firstCloneWithIntersections = currentList.Find(clone => clone.Exists(fragment =>
                {
                    secondCloneWithIntersections = comparedList.Find(clone2 =>
                        clone2.Exists(fragment2 => fragment2.Position == fragment.Position));
                    return secondCloneWithIntersections != null;
                }));

            if (firstCloneWithIntersections == null || secondCloneWithIntersections == null) return;
            //now function m*n^2 will show, which of groups should be deleted, where m = size of group, n = size of clone in fragments
            var currentListValue = currentList.Count * firstCloneWithIntersections.Count * firstCloneWithIntersections.Count;
            var comparedListValue = comparedList.Count * secondCloneWithIntersections.Count * secondCloneWithIntersections.Count;

            result.Add(currentListValue >= comparedListValue ? currentList : comparedList);
            excludedFromSearch.Add(currentListId); //exclude fragments from search, which had conflicted already
            excludedFromSearch.Add(secondListId);

            foundIntersection = true;
        }
        #endregion
    }
}
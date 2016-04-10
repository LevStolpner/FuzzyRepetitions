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
    public class FormattingConverter
    {
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
        private string _textWithAlphabeticCodes;
        private Dictionary<WordPosition, string> _textWithPositions = new Dictionary<WordPosition, string>();

        public FormattingConverter(string initialDocumentName)
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
                _textWithAlphabeticCodes = String.Empty;
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
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public void RemoveXmlFormatting()
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

            var words = _text.Split(Delimeters, StringSplitOptions.RemoveEmptyEntries);
            var lemmatizedText = new StringBuilder();

            foreach (var word in words.Select(x => x.ToLower()))
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

            var words = _lemmatizedText.Split(Delimeters, StringSplitOptions.RemoveEmptyEntries);
            var stemmedText = new StringBuilder();

            foreach (var word in words.Select(x => x.ToLower()))
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

            var words = _stemmedText.Split(Delimeters, StringSplitOptions.RemoveEmptyEntries).ToList();

            foreach (var word in words.Where(word => _alphabet.Find(x => x == word) == null))
            {
                _alphabet.Add(word);
            }

            _alphabet.Sort();

            for (var i = 0; i < words.Count; i++)
            {
                words[i] = _alphabet.FindIndex(x => x == words[i]).ToString();
            }

            _textWithAlphabeticCodes = String.Join(" ", words);

            SaveStringToFile(_textWithAlphabeticCodes);
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
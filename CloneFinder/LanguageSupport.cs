using Iveonik.Stemmers;
using LemmaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloneFinder
{
    public class LanguageSupport
    {
        public readonly IStemmer Stemmer;
        public readonly LemmatizerPrebuiltCompact Lemmatizer;

        public LanguageSupport(string language)
        {
            switch (language)
            {
                case "English":
                    Lemmatizer = new LemmaSharp.LemmatizerPrebuiltCompact(LemmaSharp.LanguagePrebuilt.English);
                    Stemmer = new EnglishStemmer();
                    break;
                case "Russian":
                    Lemmatizer = new LemmaSharp.LemmatizerPrebuiltCompact(LemmaSharp.LanguagePrebuilt.Russian);
                    Stemmer = new RussianStemmer();
                    break;
                default:
                    throw new NotSupportedException("Language " + language + "is not suported.");
            }
        }
    }
}

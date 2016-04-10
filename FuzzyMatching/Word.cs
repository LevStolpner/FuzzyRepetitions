using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FuzzyMatching
{
    public struct Word
    {
        public string Value;
        public int NumberInAlphabet;

        public Word(string word, int number)
        {
            Value = word;
            NumberInAlphabet = number;
        }
    }
}

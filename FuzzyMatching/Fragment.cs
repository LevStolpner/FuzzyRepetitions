using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FuzzyMatching
{
    public struct Fragment
    {
        public readonly int Position;
        public readonly bool[] Hash;

        public Fragment(int position, bool[] hash)
        {
            Position = position;
            Hash = hash;
        }
    }
}

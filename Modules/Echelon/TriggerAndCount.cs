using System;

namespace Echelon
{
    public class TriggerAndCount : IComparable<TriggerAndCount>
    {
        public string TriggerString { get; set; }
        public long Count { get; set; }

        public int CompareTo(TriggerAndCount other)
        {
            int ret = Count.CompareTo(other.Count);
            if (ret != 0)
            {
                return ret;
            }

            return TriggerString.CompareTo(other.TriggerString);
        }
    }
}

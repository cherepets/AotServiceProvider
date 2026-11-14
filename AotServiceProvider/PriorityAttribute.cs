using System;

namespace AotServiceProvider
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class PriorityAttribute : Attribute
    {
        public const int Min = int.MinValue;
        public const int Max = int.MaxValue;

        public int? Value { get; }

        public PriorityAttribute()
        {
        }

        public PriorityAttribute(int value)
        {
            Value = value;
        }
    }
}

using System;

namespace PlusAPIToolset.BsodaToolkit {
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class SodaAttribute : Attribute {
        public string Name { get; }

        public SodaAttribute(string name) {
            Name = name;
        }
    }
}

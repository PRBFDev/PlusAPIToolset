using System;

namespace PlusAPIToolset.BsodaToolkit {
    [Flags]
    public enum BsodaFloor {
        None = 0,
        F1 = 1 << 0,
        F2 = 1 << 1,
        F3 = 1 << 2,
        F4 = 1 << 3,
        F5 = 1 << 4,
        END = 1 << 5,
        PIT = 1 << 6,
        AllFloors = F1 | F2 | F3 | F4 | F5 | END,
        All = AllFloors | PIT
    }
}

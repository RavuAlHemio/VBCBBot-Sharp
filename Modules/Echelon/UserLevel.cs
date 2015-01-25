using System;

namespace Echelon
{
    public enum UserLevel
    {
        /// <summary>
        /// A normal user.
        /// </summary>
        Agent,

        /// <summary>
        /// A user who may modify the trigger list.
        /// </summary>
        Spymaster,

        /// <summary>
        /// A user who may not even ask for their own incident count.
        /// </summary>
        Terrorist
    }
}

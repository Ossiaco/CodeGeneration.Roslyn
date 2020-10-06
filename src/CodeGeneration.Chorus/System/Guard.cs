//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

namespace System
{
    /// <summary>
    /// Defines the <see cref="Guard" />.
    /// </summary>
#if INTERNAL_GUARD
    internal static class Guard
#else
    public static class Guard
#endif
    {
        /// <summary>
        /// Gets the Throw
        /// Gets the <see cref="ExceptionRaiser" />.
        /// </summary>
        public static ExceptionRaiser Throw { get; } = new ExceptionRaiser();

        /// <summary>
        /// Gets the verify.
        /// </summary>
        ///
        /// <value>
        /// The verify.
        /// </value>
        public static Verifier Verify { get; } = new Verifier();

        /// <summary>
        /// Provides a condition to throw an exception.
        ///     Use <c>when: [your condition]</c> to throw an exception in C# 7.2 or later.
        /// </summary>
        /// <param name="when">A condition to throw an exception.</param>
        /// <returns>A passed <paramref name="when" /> value.</returns>
        public static bool When(bool when) => when;
    }
}

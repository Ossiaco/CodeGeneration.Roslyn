//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

#nullable enable

namespace System
{
    using System.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A verifier.
    /// </summary>
#if INTERNAL_GUARD
    internal class Verifier
#else
    public class Verifier
#endif
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Verifier"/> class.
        /// </summary>
        internal Verifier()
        {
        }

        /// <summary>
        /// First not empty or null.
        /// </summary>
        ///
        /// <param name="args"> A variable-length parameters list containing arguments. </param>
        ///
        /// <returns>
        /// A string.
        /// </returns>
        public string? FirstNotEmptyOrNull(params string?[] args) => args.FirstOrDefault(a => !string.IsNullOrEmpty(a));

        /// <summary>
        /// Not empty or null.
        /// </summary>
        ///
        /// <param name="value">        The value. </param>
        /// <param name="paramName">    Name of the parameter. </param>
        ///
        /// <returns>
        /// A string.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string IsNotEmptyOrNull(string? value, string paramName)
        {
            Guard.Throw.ArgumentNullException(string.IsNullOrEmpty(value), paramName, $"{paramName} cannot be null or empty");
            return value;
        }

        /// <summary>
        /// Not empty or null.
        /// </summary>
        ///
        /// <param name="value">        The value. </param>
        /// <param name="paramName">    Name of the parameter. </param>
        /// <param name="message">      The message. </param>
        ///
        /// <returns>
        /// A string.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string IsNotEmptyOrNull(string? value, string paramName, string message)
        {
            Guard.Throw.ArgumentNullException(string.IsNullOrEmpty(value), paramName, message);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T IsNotNull<T>(T? value, string paramName, string? message = null)
            where T : class
        {
            Guard.Throw.NullReferenceException(value == null, message ?? $"{paramName} cannot be null");
            return value;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T IsNotNull<T>(T? value, string paramName, string? message = null)
            where T : struct
        {
            Guard.Throw.NullReferenceException(value == null, message ?? $"{paramName} cannot be null");
            return value.Value;
        }

    }
}
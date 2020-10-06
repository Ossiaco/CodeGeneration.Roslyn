//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

#nullable enable
namespace System
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="ExceptionRaiser" />.
    /// </summary>
#if INTERNAL_GUARD
    internal class ExceptionRaiser
#else
    public class ExceptionRaiser
#endif
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionRaiser"/> class.
        /// </summary>
        internal ExceptionRaiser()
        {
        }

        /// <summary>
        /// Raises the <see cref="AggregateException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerExceptions">The inner exceptions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AggregateException([DoesNotReturnIf(true)] bool when, string? message = null, params Exception[] innerExceptions)
        {
            if (when)
            {
                throw new AggregateException(message, innerExceptions);
            }
        }

        /// <summary>
        /// Raises the <see cref="AmbiguousMatchException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AmbiguousMatchException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new AmbiguousMatchException(message, innerException);
            }
        }

        /// <summary>
        /// ArgumentException.
        /// </summary>
        /// <param name="when">The when<see cref="bool"/>.</param>
        public void ArgumentException([DoesNotReturnIf(true)] bool when)
        {
            if (when)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Raises the <see cref="System.ArgumentException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="paramName">Name of the parameter.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentException(
            [DoesNotReturnIf(true)] bool when,
            string? message = null,
            string? paramName = null,
            Exception? innerException = null)
        {
            if (when)
            {
                throw new ArgumentException(message, paramName, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.ArgumentNullException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="paramName">The parameter.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentNullException([DoesNotReturnIf(true)] bool when, string paramName)
        {
            if (when)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.ArgumentNullException" />.
        /// </summary>
        /// <typeparam name="T">The type to evaluate.</typeparam>
        /// <param name="param">The parameter to evaluate.</param>
        /// <param name="paramName">Name of the parameter.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentNullException<T>(T? param, string paramName)
            where T : class
            => this.ArgumentNullException(param == null, paramName);

        /// <summary>
        /// Raises the <see cref="System.ArgumentNullException" />.
        /// </summary>
        /// <typeparam name="T">The type to evaluate.</typeparam>
        /// <param name="param">The parameter to evaluate.</param>
        /// <param name="paramName">Name of the parameter.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentNullException<T>(T? param, string paramName)
            where T : struct
            => this.ArgumentNullException(param == null, paramName);

        /// <summary>
        /// Raises the <see cref="System.ArgumentNullException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentNullException([DoesNotReturnIf(true)] bool when, string message, Exception innerException)
        {
            if (when)
            {
                throw new ArgumentNullException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.ArgumentNullException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="paramName">The parameter.</param>
        /// <param name="message">The message.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentNullException([DoesNotReturnIf(true)] bool when, string paramName, string message)
        {
            if (when)
            {
                throw new ArgumentNullException(paramName, message);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.ArgumentNullException" />.
        /// </summary>
        /// <typeparam name="T">The type to evaluate.</typeparam>
        /// <param name="param">The parameter to evaluate.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentNullException<T>(T param, string message, Exception innerException)
            where T : class
            => this.ArgumentNullException(param == null, message, innerException);

        /// <summary>
        /// Raises the <see cref="System.ArgumentNullException" />.
        /// </summary>
        /// <typeparam name="T">The type to evaluate.</typeparam>
        /// <param name="param">The parameter to evaluate.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentNullException<T>(T? param, string message, Exception innerException)
            where T : struct
            => this.ArgumentNullException(param == null, message, innerException);

        /// <summary>
        /// Raises the <see cref="System.ArgumentNullException" />.
        /// </summary>
        /// <typeparam name="T">The type to evaluate.</typeparam>
        /// <param name="param">The parameter to evaluate.</param>
        /// <param name="paramName">Name of the parameter.</param>
        /// <param name="message">The message.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentNullException<T>(T? param, string paramName, string message)
            where T : class
            => this.ArgumentNullException(param == null, paramName, message);

        /// <summary>
        /// Raises the <see cref="System.ArgumentNullException" />.
        /// </summary>
        /// <typeparam name="T">The type to evaluate.</typeparam>
        /// <param name="param">The parameter to evaluate.</param>
        /// <param name="paramName">Name of the parameter.</param>
        /// <param name="message">The message.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentNullException<T>(T? param, string paramName, string message)
            where T : struct
            => this.ArgumentNullException(param == null, paramName, message);

        /// <summary>
        /// Raises the <see cref="System.ArgumentOutOfRangeException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentOutOfRangeException([DoesNotReturnIf(true)] bool when, string message, Exception innerException)
        {
            if (when)
            {
                throw new ArgumentOutOfRangeException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.ArgumentOutOfRangeException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="paramName">Name of the parameter.</param>
        /// <param name="actualValue">The value of the argument that causes this exception. </param>
        /// <param name="message">The message.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ArgumentOutOfRangeException(
            [DoesNotReturnIf(true)] bool when,
            string? paramName = null,
            object? actualValue = null,
            string? message = null)
        {
            if (when)
            {
                throw new ArgumentOutOfRangeException(paramName, actualValue, message);
            }
        }

        /// <summary>
        /// Raises the <see cref="AuthenticationException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AuthenticationException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new AuthenticationException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="DirectoryNotFoundException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DirectoryNotFoundException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new DirectoryNotFoundException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="DllNotFoundException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DllNotFoundException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new DllNotFoundException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="DriveNotFoundException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DriveNotFoundException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new DriveNotFoundException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.Exception" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exception([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new Exception(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="FileLoadException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="fileName">The file name.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FileLoadException(
            [DoesNotReturnIf(true)] bool when,
            string? message = null,
            string? fileName = null,
            Exception? innerException = null)
        {
            if (when)
            {
                throw new FileLoadException(message, fileName, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="FileNotFoundException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="fileName">The file name.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FileNotFoundException(
            [DoesNotReturnIf(true)] bool when,
            string? message = null,
            string? fileName = null,
            Exception? innerException = null)
        {
            if (when)
            {
                throw new FileNotFoundException(message, fileName, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="FormatException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FormatException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new FormatException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="IndexOutOfRangeException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IndexOutOfRangeException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new IndexOutOfRangeException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.InvalidCastException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="errorCode">The error code (HRESULT).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvalidCastException([DoesNotReturnIf(true)] bool when, string message, int errorCode)
        {
            if (when)
            {
                throw new InvalidCastException(message, errorCode);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.InvalidCastException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvalidCastException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new InvalidCastException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="InvalidCredentialException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvalidCredentialException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new InvalidCredentialException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="InvalidDataException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvalidDataException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new InvalidDataException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="InvalidOperationException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvalidOperationException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new InvalidOperationException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="KeyNotFoundException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void KeyNotFoundException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new KeyNotFoundException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.MissingFieldException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="className">The class name.</param>
        /// <param name="fieldName">The field name.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MissingFieldException([DoesNotReturnIf(true)] bool when, string? className = null, string? fieldName = null)
        {
            if (when)
            {
                throw new MissingFieldException(className, fieldName);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.MissingFieldException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MissingFieldException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new MissingFieldException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.MissingMemberException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MissingMemberException([DoesNotReturnIf(true)] bool when, string message, Exception innerException)
        {
            if (when)
            {
                throw new MissingMemberException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.MissingMemberException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="className">The class name.</param>
        /// <param name="memberName">The member name.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MissingMemberException([DoesNotReturnIf(true)] bool when, string? className = null, string? memberName = null)
        {
            if (when)
            {
                throw new MissingMemberException(className, memberName);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.MissingMethodException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="className">The class name.</param>
        /// <param name="methodName">The method name.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MissingMethodException([DoesNotReturnIf(true)] bool when, string? className = null, string? methodName = null)
        {
            if (when)
            {
                throw new MissingMethodException(className, methodName);
            }
        }

        /// <summary>
        /// Raises the <see cref="System.MissingMethodException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MissingMethodException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new MissingMethodException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="NotImplementedException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NotImplementedException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new NotImplementedException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="NotSupportedException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NotSupportedException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new NotSupportedException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="NullReferenceException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NullReferenceException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new NullReferenceException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="NullReferenceException" />.
        /// </summary>
        /// <typeparam name="T">The type to evaluate.</typeparam>
        /// <param name="value">The reference to evaluate.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NullReferenceException<T>(T? value, string? message = null, Exception? innerException = null)
            where T : class
            => this.NullReferenceException(value == null, message, innerException);

        /// <summary>
        /// Initializes a new instance of the System.ObjectDisposedException class with a
        ///     string containing the name of the disposed object.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="objectName">The name of the disposed object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ObjectDisposedException([DoesNotReturnIf(true)] bool when, string objectName)
        {
            if (when)
            {
                throw new ObjectDisposedException(objectName);
            }
        }

        /// <summary>
        /// Initializes a new instance of the System.ObjectDisposedException class with a
        ///     specified error message and a reference to the inner exception that is the cause
        ///     of this exception.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ObjectDisposedException([DoesNotReturnIf(true)] bool when, string message, Exception innerException)
        {
            if (when)
            {
                throw new ObjectDisposedException(message, innerException);
            }
        }

        /// <summary>
        /// Initializes a new instance of the System.ObjectDisposedException class with the
        ///     specified object name and message.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="objectName">The name of the disposed object.</param>
        /// <param name="message">The message.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ObjectDisposedException([DoesNotReturnIf(true)] bool when, string objectName, string message)
        {
            if (when)
            {
                throw new ObjectDisposedException(objectName, message);
            }
        }

        /// <summary>
        /// Raises the <see cref="OperationCanceledException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="token">The cancellation token.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OperationCanceledException(
            [DoesNotReturnIf(true)] bool when,
            string? message = null,
            Exception? innerException = null,
            CancellationToken? token = null)
        {
            if (when)
            {
                throw new OperationCanceledException(message, innerException, token ?? CancellationToken.None);
            }
        }

        /// <summary>
        /// Raises the <see cref="PathTooLongException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PathTooLongException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new PathTooLongException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="SerializationException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SerializationException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new SerializationException(message, innerException);
            }
        }

        /// <summary>
        /// Raises
        ///     the <see cref="Threading.Tasks.TaskCanceledException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="task">The task.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TaskCanceledException([DoesNotReturnIf(true)] bool when, Task task)
        {
            if (when)
            {
                throw new TaskCanceledException(task);
            }
        }

        /// <summary>
        /// Raises
        ///     the <see cref="Threading.Tasks.TaskCanceledException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TaskCanceledException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new TaskCanceledException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="TimeoutException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TimeoutException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new TimeoutException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="TypeLoadException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TypeLoadException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new TypeLoadException(message, innerException);
            }
        }

        /// <summary>
        /// Raises the <see cref="UnauthorizedAccessException" />.
        /// </summary>
        /// <param name="when">A condition to throw the exception.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnauthorizedAccessException([DoesNotReturnIf(true)] bool when, string? message = null, Exception? innerException = null)
        {
            if (when)
            {
                throw new UnauthorizedAccessException(message, innerException);
            }
        }
    }
}
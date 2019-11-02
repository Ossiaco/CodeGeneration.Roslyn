namespace CodeGeneration.Chorus.Tests
{
    using System;

    internal class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> action;

        public SynchronousProgress(Action<T> action)
        {
            this.action = action;
        }

        public void Report(T value)
        {
            action(value);
        }
    }

}

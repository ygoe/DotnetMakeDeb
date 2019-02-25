using System;

namespace DotnetMakeDeb.Tar
{
    public class TarException : Exception
    {
        public TarException(string message) : base(message)
        {
        }
    }
}

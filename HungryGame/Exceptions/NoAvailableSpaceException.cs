using System.Runtime.Serialization;

namespace HungryGame
{
    [Serializable]
    public class NoAvailableSpaceException : Exception
    {
        public NoAvailableSpaceException()
        {
        }

        public NoAvailableSpaceException(string? message) : base(message)
        {
        }

        public NoAvailableSpaceException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected NoAvailableSpaceException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
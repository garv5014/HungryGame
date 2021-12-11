using System.Runtime.Serialization;

namespace HungryHippos
{
    [Serializable]
    internal class InvalidGameStateException : Exception
    {
        public InvalidGameStateException()
        {
        }

        public InvalidGameStateException(string? message) : base(message)
        {
        }

        public InvalidGameStateException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected InvalidGameStateException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
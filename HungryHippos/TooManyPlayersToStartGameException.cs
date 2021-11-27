using System.Runtime.Serialization;

namespace HungryHippos
{
    [Serializable]
    internal class TooManyPlayersToStartGameException : Exception
    {
        public TooManyPlayersToStartGameException()
        {
        }

        public TooManyPlayersToStartGameException(string? message) : base(message)
        {
        }

        public TooManyPlayersToStartGameException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected TooManyPlayersToStartGameException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
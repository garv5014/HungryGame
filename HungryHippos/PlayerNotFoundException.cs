using System.Runtime.Serialization;

namespace HungryHippos
{
    [Serializable]
    internal class PlayerNotFoundException : Exception
    {
        public PlayerNotFoundException()
        {
        }

        public PlayerNotFoundException(string? message) : base(message)
        {
        }

        public PlayerNotFoundException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected PlayerNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
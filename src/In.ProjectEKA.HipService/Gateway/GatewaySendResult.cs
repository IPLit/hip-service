namespace In.ProjectEKA.HipService.Gateway
{
    public sealed class GatewaySendResult
    {
        private GatewaySendResult(bool succeeded, bool alreadyLinked)
        {
            Succeeded = succeeded;
            AlreadyLinked = alreadyLinked;
        }

        public bool Succeeded { get; }

        public bool AlreadyLinked { get; }

        public static readonly GatewaySendResult Success = new GatewaySendResult(true, false);

        public static readonly GatewaySendResult DuplicateLink = new GatewaySendResult(false, true);

        public static readonly GatewaySendResult Failure = new GatewaySendResult(false, false);
    }
}

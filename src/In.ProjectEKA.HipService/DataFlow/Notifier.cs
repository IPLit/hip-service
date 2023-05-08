namespace In.ProjectEKA.HipService.DataFlow
{
    public class Notifier
    {
        public Notifier(Type type, string id)
        {
            Type tp = (Type) type;
            Type = tp.ToString();
            Id = id;
        }

        public string Type { get; }
        public string Id { get; }
    }
}
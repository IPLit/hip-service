namespace In.ProjectEKA.HipService.UserAuth.Model
{
    public class Requester
    {
        public string id { get; }
        public string type { get; }

        public Requester(string id, string type)
        {
            this.id = id;
            this.type = type;
        }
    }
}
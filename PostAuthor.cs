using System.Runtime.Serialization;

namespace VkClient
{
    [DataContract]
    public struct PostAuthor
    {
        [DataMember]
        public string Name;
        [DataMember]
        public string Url;

        public PostAuthor(string name, string url)
        {
            Name = name;
            Url = url;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

using System.Runtime.Serialization;

namespace VkClient
{
    [DataContract]
    public class Author
    {
        [DataMember]
        public string Name;

        [DataMember]
        public string Url;

        [DataMember]
        public string ID;

        public Author(string name, string url)
        {
            Name = name;
            Url = url;
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            Author a = obj as Author;

            if (a == null)
                return false;

            return a.ID?.Equals(ID) ?? ID == null;
        }
    }
}

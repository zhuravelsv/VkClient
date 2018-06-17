using System.Collections.Generic;
using System.Runtime.Serialization;

namespace VkClient
{
    [DataContract(IsReference = true)]
    public class Post
    {
        [DataMember]
        public string Url { get; set; }

        [DataMember]
        public string Text { get; set; }

        [DataMember]
        public string Time { get; set; }

        [DataMember]
        public Author Author { get; set; }

        [DataMember]
        public int LikesCount { get; set; }

        [DataMember]
        public int RepostsCount { get; set; }

        [DataMember]
        public List<EmbeddedImage> Images { get; set; }

        public Post()
        {
            Images = new List<EmbeddedImage>();
        }
    }
}

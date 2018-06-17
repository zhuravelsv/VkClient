using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace VkClient
{
    [DataContract]
    public class Message
    {
        [DataMember]
        public Author Author { get; set; }

        [DataMember]
        public Dialog Dialog { get; set; }

        [DataMember]
        public string Text { get; set; }

        [DataMember]
        public DateTime ReceiptTime { get; set; }

        public List<EmbeddedImage> Images { get; set; }

        public Message()
        {
            Images = new List<EmbeddedImage>();
            Text = "";
        }

        public override string ToString()
        {
            return Text;
        }

        public override bool Equals(object obj)
        {
            Message m = obj as Message;

            if (m == null)
                return false;

            return (m.Text?.Equals(Text) ?? Text == null) && m.ReceiptTime.Equals(ReceiptTime) && (m.Author?.Equals(Author) ?? Author == null);
        }
    }
}

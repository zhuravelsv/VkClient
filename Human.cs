using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace VkClient
{
    [DataContract(IsReference = true)]
    public class Human
    {
        [DataMember]
        public string PageURL;
        [DataMember]
        public Dictionary<string, string> Data;
        [DataMember]
        public string Name;
        [DataMember]
        public string Id;
        [DataMember]
        public DateTime LastActivity;

        public Human()
        {
            Data = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkClient
{
    public class RequestResult<T> : Result
    {
        public T Data;

        public string Info { get; set; }

        public string Page { get; set; }

        public RequestResult(T data)
        {
            Data = data;
        }
    }
}

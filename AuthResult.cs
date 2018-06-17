using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkClient
{
    public class AuthResult : Result
    {
        public bool Result;
        public bool Blocked;

        public AuthResult(bool result, bool blocked)
        {
            Result = result;
            Blocked = blocked;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkClient
{
    public class AddFriendResult : Result
    {
        public bool Result;
        public bool LimitIsSettled;

        public AddFriendResult(bool result, bool limit)
        {
            Result = result;
            LimitIsSettled = limit;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkClient
{
    public class InviteResult : Result
    {
        public bool Result;
        public bool InviteBlocked;
        public bool LimitIsSettled;
        public string Page;

        public InviteResult(bool result, bool limit, bool blocked, string page)
        {
            Result = result;
            LimitIsSettled = limit;
            InviteBlocked = blocked;
            Page = page;
        }
    }
}

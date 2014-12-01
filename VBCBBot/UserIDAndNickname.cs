using System;

namespace VBCBBot
{
    public struct UserIDAndNickname
    {
        public long UserID;
        public string Nickname;

        public UserIDAndNickname(long userID, string nickname)
        {
            UserID = userID;
            Nickname = nickname;
        }
    }
}


namespace VBCBBot
{
    public struct UserIDAndNickname
    {
        public readonly long UserID;
        public readonly string Nickname;

        public UserIDAndNickname(long userID, string nickname)
        {
            UserID = userID;
            Nickname = nickname;
        }
    }
}

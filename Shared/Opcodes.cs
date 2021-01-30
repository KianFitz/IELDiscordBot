using System;
using System.Collections.Generic;
using System.Text;

namespace Shared
{
    public class Opcodes
    {
        public const int CMSG_IDENTIFY = 0x001;
        public const int SMSG_IDENTIFY_ACK = 0x002;
        public const int SMSG_REQUEST_ACK = 0x003;
        public const int CMSG_PLAYER_IN_DISCORD = 0x004;
        public const int CMSG_PLAYER_SIGNUP_ACCEPTED = 0x005;
        public const int CMSG_PLAYER_FA_ROLE = 0x006;
        public const int CMSG_DSN_CALCULATION = 0x007;
        public const int CMSG_REQUEST_LEAGUE = 0x008;
        public const int SMSG_LEAGUE_RESPONSE = 0x009;
        public const int CMSG_NEW_NICKNAME = 0x010;
    }
}

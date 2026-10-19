using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Sockets;
using System.Text;

namespace Windows_Forms_Chat
{
    public class ClientSocket
    {
        public Socket socket;
        public const int BUFFER_SIZE = 2048;
        public byte[] buffer = new byte[BUFFER_SIZE];
        public string username; // Username of the client - Andrew
        public bool connectionState; // True = connected, False = disconnected - Andrew
        public string lastWhisperedUsername; // Username of the last user that this client whispered to - Andrew
        public bool isModerator; // True = moderator, False = not a moderator - Andrew
    }
}

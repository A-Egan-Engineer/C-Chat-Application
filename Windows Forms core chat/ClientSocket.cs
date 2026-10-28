using System.Net.Sockets;

namespace Windows_Forms_Chat
{
    public class ClientSocket
    {
        public Socket socket;
        public const int BUFFER_SIZE = 2048;
        public byte[] buffer = new byte[BUFFER_SIZE]; 
        // Username of the client - Andrew
        public string username;
        // Username of the last user that this client whispered to - Andrew
        public ClientSocket userToReply;
        // True = connected, False = disconnected - Andrew
        public bool connectionState = false;
        // True = moderator, False = not a moderator - Andrew
        public bool isModerator = false; 
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Windows_Forms_Chat
{
    public class TCPChatServer : TCPChatBase
    {

        public Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public List<ClientSocket> clientSockets = new List<ClientSocket>();
        public bool usernameUsed = false;
        public string oldUsername = null;

        public static TCPChatServer createInstance(int port, TextBox chatTextBox)
        {
            TCPChatServer tcp = null;
            if (port > 0 && port < 65535 && chatTextBox != null)
            {
                tcp = new TCPChatServer();
                tcp.port = port;
                tcp.chatTextBox = chatTextBox;

            }
            return tcp;
        }

        public void SetupServer()
        {
            chatTextBox.Text += "Setting up server...\n\n";
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            serverSocket.Listen(0);
            serverSocket.BeginAccept(AcceptCallback, this);
            chatTextBox.Text += "Server setup complete\n\n";
        }

        public void CloseAllSockets()
        {
            foreach (ClientSocket clientSocket in clientSockets)
            {
                clientSocket.socket.Shutdown(SocketShutdown.Both);
                clientSocket.socket.Close();
            }
            clientSockets.Clear();
            serverSocket.Close();
        }

        public void AcceptCallback(IAsyncResult AR)
        {
            Socket joiningSocket;

            try
            {
                joiningSocket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            ClientSocket newClientSocket = new ClientSocket();
            newClientSocket.socket = joiningSocket;

            clientSockets.Add(newClientSocket);
            
            joiningSocket.BeginReceive(newClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, newClientSocket);
            AddToChat("Client connected, waiting for request...\n\n");

            
            serverSocket.BeginAccept(AcceptCallback, null);
        }
        /// <summary>
        /// 28th of October 2026 - Andrew
        /// Chat handling for host. Handles command inputs and logic with nested 
        /// if/else statements. Satisfies Step Five of the Assessment 2 brief, covering
        /// !mod to trigger moderator status on client sockets and listing active mods
        /// with !mods command.
        /// </summary>
        public void HostChatHandler(string input)
        {
            if (input.ToLower().StartsWith("!mod") && !input.ToLower().StartsWith("!mods"))
            {

                ClientSocket targetClient;

                string targetUser = input.Substring(4).Trim();
                targetClient = FindClientByUsername(targetUser);

                if (string.IsNullOrWhiteSpace(targetUser))
                {
                    AddToChat("Username cannot be empty, please enter a valid username (!mod <username>)");
                    return;
                }
                if (targetClient == null && targetUser.Length > 0)
                {
                    AddToChat("User is not connected or found, please try again!");
                    return;
                }
                else
                {
                    if (targetClient.isModerator == false)
                    {
                        targetClient.isModerator = true;
                        byte[] data = Encoding.ASCII.GetBytes("You have been promoted to moderator");
                        targetClient.socket.Send(data);
                        AddToChat($"{targetClient.username} has been promoted to moderator");
                    }
                    else
                    {
                        targetClient.isModerator = false;
                        byte[] data = Encoding.ASCII.GetBytes("You have been demoted from moderator to user");
                        targetClient.socket.Send(data);
                        AddToChat($"{targetClient.username} has been demoted from moderator to user");
                    }
                }
            }
            if (input.ToLower().StartsWith("!mods"))
            {
                AddToChat("Current Sever Moderators:\n");

                foreach (ClientSocket c in clientSockets)
                {
                    if (c.username != null && c.isModerator == true)
                    {
                        AddToChat($"{c.username}");
                    }
                }
            }
            else
            {
                if (!input.StartsWith("!"))
                {
                    SendToAll("Host: " + input, null);
                    AddToChat("Host: " + input);
                }
            }    
        }
        /// <summary>
        /// 28th of October 2026 - Andrew
        /// Follows good reusable code practices by creating a user search,
        /// utlised to find client sockets by their username based on the 
        /// paramete input. Ignores case-sensitivity.
        /// </summary>
        private ClientSocket FindClientByUsername(string username)
        {
            foreach (ClientSocket c in clientSockets)
            {
                if (c.username != null && c.username.Equals(username, StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }    
            }
            return null;
        }

        /// <summary>
        /// 28th of October 2026 - Andrew
        /// Rearms the client sockets to ensure they continue listening for further inputs.
        /// The buffer is where incoming data gets written and buffer size handles the byte cap
        /// for the data. The socket flag indicates the type of packet to be sent i.e. broadcast,
        /// multicast or other behaviours for data transmission.
        /// </summary>
        public async void RearmCallback (IAsyncResult AR)
        {
            ClientSocket currentClientSocket = (ClientSocket)AR.AsyncState;
            currentClientSocket.socket.BeginReceive(currentClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, currentClientSocket);
        }

        public async void ReceiveCallback(IAsyncResult AR)
        {
            ClientSocket currentClientSocket = (ClientSocket)AR.AsyncState;

            currentClientSocket.connectionState = true; 

            int received;

            try
            {
                received = currentClientSocket.socket.EndReceive(AR);
            }
            catch (SocketException)
            {
                AddToChat("Client forcefully disconnected");
                
                currentClientSocket.socket.Close();
                clientSockets.Remove(currentClientSocket);
                return;
            }
            catch(ObjectDisposedException)
            {
                return;
            }

            byte[] recBuf = new byte[received];
            Array.Copy(currentClientSocket.buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf);

            /// <summary>
            /// 28th of October 2026 - Andrew
            /// !username mod that satifies Step One of the brief
            /// Allows users to assign there own username when joining
            /// the server for the first time. Handles null check, 
            /// compares input to exsisting users to avoid duplicate usernames,
            /// If checks pass, assign username. Otherwise, if username unassigned
            /// and user attempt to chat, prompt to set username.
            /// </summary>
            if (currentClientSocket.username == null) 
            {
                if (text.ToLower().StartsWith("!username")) 
                {
                    string newUsername = text.Substring(9).Trim(); 

                    if (string.IsNullOrWhiteSpace(newUsername)) 
                    {
                        byte[] data = Encoding.ASCII.GetBytes("Username cannot be empty. Please choose a valid username.");
                        currentClientSocket.socket.Send(data);
                        AddToChat("Client attempted to set an empty username");
                        RearmCallback(AR);
                        return; 
                    }

                    foreach (ClientSocket c in clientSockets) 
                    {
                        if (c.username != null && c.username.Equals(newUsername, StringComparison.OrdinalIgnoreCase)) 
                        {
                            byte[] data = Encoding.ASCII.GetBytes("Username already taken!\n");
                            currentClientSocket.socket.Send(data);
                            AddToChat($"Client attempted to set duplicate username: {newUsername}");
                            usernameUsed = true;
                            if (usernameUsed == true)
                            {
                                await DisconnectClient(currentClientSocket); 
                                usernameUsed = false;
                                return;
                            }
                        }
                    }
                    currentClientSocket.username = newUsername; 
                    AddToChat($"User {newUsername} has joined the chat!");
                    byte[] welcomeData = Encoding.ASCII.GetBytes($"Welcome, {newUsername}!"); 
                    currentClientSocket.socket.Send(welcomeData); 

                }
                else
                {
                    byte[] data = Encoding.ASCII.GetBytes("Please set your username using !username <your_username>");
                    currentClientSocket.socket.Send(data);
                    AddToChat("Client attempted to send message without setting username");
                    RearmCallback(AR);
                    return; 
                }
            }
            /// <summary>
            /// 28th of October 2026 - Andrew
            /// Null check on client socket username. If username is assigned 
            /// check (if duplicate, disconnect session). Otherwise, oldusername
            /// is stored for broadcast to all clients, that user has updated 
            /// their username. 
            /// </summary>
            if (currentClientSocket.username != null)
            {
                if (text.ToLower().StartsWith("!user") && !text.ToLower().StartsWith("!username"))
                {
                    string newUsername = text.Substring(6).Trim();
                    if (string.IsNullOrWhiteSpace(newUsername))
                    {
                        byte[] data = Encoding.ASCII.GetBytes("Username cannot be empty. Please choose a valid username.");
                        currentClientSocket.socket.Send(data);
                        AddToChat("Client attempted to set an empty username");
                        RearmCallback(AR);
                        return; 
                    }
                    foreach (ClientSocket c in clientSockets)
                    {
                        if (c.username != null && c.username.Equals(newUsername, StringComparison.OrdinalIgnoreCase)) 
                        {
                            byte[] data = Encoding.ASCII.GetBytes("Username already taken!\n");
                            currentClientSocket.socket.Send(data);
                            AddToChat($"Client attempted to set duplicate username: {newUsername}");
                            usernameUsed = true;
                            if (usernameUsed == true)
                            {
                                await DisconnectClient(currentClientSocket);
                                usernameUsed = false;
                                return;
                            }
                        }
                    }
                    oldUsername = currentClientSocket.username; 
                    currentClientSocket.username = newUsername; 
                    AddToChat($"User {oldUsername} changed their username to: {newUsername}"); 
                    byte[] usernameChangedData = Encoding.ASCII.GetBytes($"Username updated to {newUsername}"); 
                    SendToAll($"User {oldUsername} changed their username to {newUsername}", currentClientSocket); 
                }
            }
            /// <summary>
            /// 28th of October 2026 - Andrew
            /// Displays available commands for use to client
            /// </summary>
            if (text.ToLower().StartsWith("!commands") || text.ToLower().StartsWith("!c"))
            {
                byte[] data = Encoding.ASCII.GetBytes("Commands are !commands/!c (Shows available commands) \n !about/!a (Shows server information) \n !who (Lists connected users) \n !whisper/w (Sends a private message) \n !reply/!r (Replies to the last private message) \n !exit (Disconnects from the server)");
                currentClientSocket.socket.Send(data);
                AddToChat("Commands sent to: " + currentClientSocket.username);
            }
            /// <summary>
            /// 28th of October 2026 - Andrew
            /// Sorts client sockets in list by alpabetical order and
            /// sends client the list of connected user clients. 
            /// </summary>
            if (text.ToLower().StartsWith("!who")) 
            {
                clientSockets.OrderBy(c => c.username).ToList(); 
                byte[] data = Encoding.ASCII.GetBytes("Connected users: " + string.Join(", ", clientSockets.ConvertAll(c => c.username)));
                currentClientSocket.socket.Send(data);
                AddToChat("Connected users sent to: " + currentClientSocket.username);
            }
            /// <summary>
            /// 28th of October 2026 - Andrew
            /// Send the client information about the creator, purpose and year
            /// of development.
            /// </summary>
            if (text.ToLower().StartsWith("!about") || text.ToLower().StartsWith("!a")) 
            {
                byte[] data = Encoding.ASCII.GetBytes("Server: This is a simple chat server MVP that was provided by Torrens University Australia for NDS203 Networking and Databases. It allows multiple clients to connect and chat with each other via TCP/IP. It was co-developed by Andrew Egan in October of 2026");
                currentClientSocket.socket.Send(data);
                AddToChat("About information sent to: " + currentClientSocket.username);
            }
            /// <summary>
            /// 28th of October 2026 - Andrew
            /// Whisper command that clients use to send private messages 
            /// to the target user. This data only transmits to the targetClient
            /// socket. Data is encoded before being sent. Satisfies Step Four of
            /// the assessment, along with !who, !about, and !reply.
            /// </summary>
            if (text.ToLower().StartsWith("!whisper") || text.ToLower().StartsWith("!w") && !text.ToLower().StartsWith("!who"))
            {
                string[] parts = text.Split(' ', 3);
                if (parts.Length < 3)
                {
                    byte[] data = Encoding.ASCII.GetBytes("Usage: !whisper <username> <message>");
                    currentClientSocket.socket.Send(data);
                    AddToChat("Whisper command usage sent to: " + currentClientSocket.username);
                }
                else
                {
                    string targetUsername = parts[1];
                    string message = parts[2];
                    ClientSocket targetClient = FindClientByUsername(targetUsername);
                    if (targetClient != null)
                    {
                        byte[] data = Encoding.ASCII.GetBytes($"Whisper from {currentClientSocket.username}: {message}");
                        targetClient.socket.Send(data);
                        data = Encoding.ASCII.GetBytes($"Whisper to {targetClient.username}: {message}");
                        currentClientSocket.socket.Send(data);
                        AddToChat($"Whisper from {currentClientSocket.username} to {targetClient.username}: {message}");
                        targetClient.userToReply = currentClientSocket;
                    }
                    else
                    {
                        byte[] data = Encoding.ASCII.GetBytes($"User {targetUsername} not found, whisper failed.");
                        currentClientSocket.socket.Send(data);
                        AddToChat($"Whisper failed: User {targetUsername} not found for {currentClientSocket.username}");
                    }
                }
            }
            /// <summary>
            /// 28th of October 2026 - Andrew
            /// Custom command to satisfy the final component of Step Four.
            /// The client receives a whisper and the client who sent the 
            /// message is stored is replyTarget. The user can then reply
            /// with their own private whisper back, using !reply command.
            /// There is no need for the replying client to list the target
            /// user in the chat, as it updates the target user based on 
            /// the last whisperer.
            /// </summary>
            if (text.ToLower().StartsWith("!reply") || text.ToLower().StartsWith("!r"))
            {
                string message = text.Substring(6).Trim();
                ClientSocket replyTarget = currentClientSocket.userToReply;

                if (string.IsNullOrWhiteSpace(message))
                {
                    byte[] data = Encoding.ASCII.GetBytes($"Please type a message to reply, !reply <message>");
                    currentClientSocket.socket.Send(data);
                }
                else if (replyTarget == null)
                {
                    byte[] data = Encoding.ASCII.GetBytes($"No user available to reply too");
                    currentClientSocket.socket.Send(data);
                }
                else if (!clientSockets.Contains(replyTarget))
                {
                    byte[] data = Encoding.ASCII.GetBytes($"User you are trying to reply to is offline");
                    currentClientSocket.socket.Send(data);
                    currentClientSocket.userToReply = null;
                }
                else
                {
                    byte[] data = Encoding.ASCII.GetBytes($"Reply from {currentClientSocket.username} : {message}");
                    replyTarget.socket.Send(data);
                    data = Encoding.ASCII.GetBytes($"Reply to {replyTarget.username} : {message}");
                    currentClientSocket.socket.Send(data);
                    replyTarget.userToReply = currentClientSocket;
                }
            }
            /// <summary>
            /// 28th of October 2026 - Andrew
            /// Satisfies the !kick command require for Step Five in the brief.
            /// Only clients with moderator status can access this command, which
            /// can only be assigned by the server using !mod <username>.
            /// </summary>
            if (text.ToLower().StartsWith("!kick") || text.ToLower().StartsWith("!k"))
            {
                ClientSocket targetClient;

                string targetUser = text.Substring(5).Trim();

                targetClient = FindClientByUsername(targetUser);

                if (currentClientSocket.isModerator == true)
                {
                    if (string.IsNullOrWhiteSpace(targetUser))
                    {
                        byte[] data = Encoding.ASCII.GetBytes($"Username variable empty, please enter a valid username to kick");
                        currentClientSocket.socket.Send(data);
                        RearmCallback(AR);
                        return;
                    }
                    if (targetClient == null)
                    {
                        byte[] data = Encoding.ASCII.GetBytes("No user found, please try again");
                        currentClientSocket.socket.Send(data);
                    }
                    else
                    {
                        targetClient.socket.Shutdown(SocketShutdown.Both);
                        targetClient.socket.Close();
                        clientSockets.Remove(targetClient);
                        targetClient.connectionState = false;
                        SendToAll($"{targetUser.ToLower()} has been kicked from the server", currentClientSocket);
                        AddToChat($"{targetUser.ToLower()} was kicked from the server by {currentClientSocket.username}");
                    }
                }
                else if (currentClientSocket.isModerator == false)
                {
                    byte[] data = Encoding.ASCII.GetBytes($"You don't have the required permissions to kick users");
                    currentClientSocket.socket.Send(data);
                }
            }
            if (text.ToLower() == "!exit" || text.ToLower() == "!e")
            {
                await DisconnectClient(currentClientSocket);
                return;
            }
            else
            {
                if (!text.StartsWith("!"))
                {
                    SendToAll(currentClientSocket.username + ": " + text, currentClientSocket);
                }
            }
            RearmCallback(AR);
        }

        public void SendToAll(string str, ClientSocket from)
        {
            foreach (ClientSocket c in clientSockets)
            {
                if (from == null || !from.socket.Equals(c))
                {
                    byte[] data = Encoding.ASCII.GetBytes(str);
                    c.socket.Send(data);
                }
            }
        }

        /// <summary>
        /// 28th of October 2026 - Andrew
        /// DisconnectClient handles the disconnect message, delays 5 
        /// seconds before the user timesout and the clients socket is 
        /// shutdown and removed from the instance. Connection status
        /// updates to false (disconnected).
        /// </summary>
        public async Task DisconnectClient(ClientSocket client)
        {
            if (client != null)
            {
                byte[] data = Encoding.ASCII.GetBytes("You have been disconnected. The connection will close in 5 seconds.");
                client.socket.Send(data);
                await Task.Delay(5000);
                client.socket.Shutdown(SocketShutdown.Both);
                client.socket.Close();
                clientSockets.Remove(client);
                client.connectionState = false;
                AddToChat("Client disconnected");
            }
        }
    }
}

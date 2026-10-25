using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics.Eventing.Reader;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection.Metadata;

//https://github.com/AbleOpus/NetworkingSamples/blob/master/MultiServer/Program.cs
namespace Windows_Forms_Chat
{
    public class TCPChatServer : TCPChatBase
    {

        public Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //connected clients
        public List<ClientSocket> clientSockets = new List<ClientSocket>();
        public bool usernameUsed = false;
        public string oldUsername = null;

        public static TCPChatServer createInstance(int port, TextBox chatTextBox)
        {
            TCPChatServer tcp = null;
            //setup if port within range and valid chat box given
            if (port > 0 && port < 65535 && chatTextBox != null)
            {
                tcp = new TCPChatServer();
                tcp.port = port;
                tcp.chatTextBox = chatTextBox;

            }

            //return empty if user not enter useful details
            return tcp;
        }

        public void SetupServer()
        {
            chatTextBox.Text += "Setting up server...\n\n";
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            serverSocket.Listen(0);
            //kick off thread to read connecting clients, when one connects, it'll call out AcceptCallback function
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
            catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }

            ClientSocket newClientSocket = new ClientSocket();
            newClientSocket.socket = joiningSocket;

            clientSockets.Add(newClientSocket);
            //start a thread to listen out for this new joining socket. Therefore there is a thread open for each client
            joiningSocket.BeginReceive(newClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, newClientSocket);
            AddToChat("Client connected, waiting for request...\n\n");

            //we finished this accept thread, better kick off another so more people can join
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        public void HostChatHandler(string input)
        {
            if (input.ToLower().StartsWith("!mod"))
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
            else
            {
                if (!input.StartsWith("!"))
                {
                    SendToAll("Host: " + input, null);
                    AddToChat("Host: " + input);
                }
            }    
        }

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

        /* Callback function for handling received data from clients */
        public async void ReceiveCallback(IAsyncResult AR)
        {
            ClientSocket currentClientSocket = (ClientSocket)AR.AsyncState;

            currentClientSocket.connectionState = true; // Set the connection state to true to indicate that the client is active - Andrew

            int received;

            try
            {
                received = currentClientSocket.socket.EndReceive(AR);
            }
            catch (SocketException)
            {
                AddToChat("Client forcefully disconnected");
                // Don't shutdown because the socket may be disposed and its disconnected anyway.
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

            if (currentClientSocket.username == null) // Check if username is not set and command is !username - Andrew
            {
                if (text.ToLower().StartsWith("!username")) // Check if the command is !username - Andrew
                {
                    string newUsername = text.Substring(9).Trim(); // Extract the username from the command - Andrew

                    if (string.IsNullOrWhiteSpace(newUsername)) // Check if the username is empty or whitespace - Andrew
                    {
                        byte[] data = Encoding.ASCII.GetBytes("Username cannot be empty. Please choose a valid username.");
                        currentClientSocket.socket.Send(data);
                        AddToChat("Client attempted to set an empty username");
                        currentClientSocket.socket.BeginReceive(currentClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, currentClientSocket);
                        return; // Exit the method to prevent further processing
                    }

                    foreach (ClientSocket c in clientSockets) // Check for duplicate usernames - Andrew
                    {
                        if (c.username != null && c.username.Equals(newUsername, StringComparison.OrdinalIgnoreCase)) // Compare usernames case-insensitively - Andrew
                        {
                            byte[] data = Encoding.ASCII.GetBytes("Username already taken!\n");
                            currentClientSocket.socket.Send(data);
                            AddToChat($"Client attempted to set duplicate username: {newUsername}");
                            usernameUsed = true;
                            if (usernameUsed == true)
                            {
                                await DisconnectClient(currentClientSocket); // Disconnect the client if the username is already taken - Andrew
                                usernameUsed = false;
                                return;
                            }
                        }
                    }
                    currentClientSocket.username = newUsername; // Set the username - Andrew
                    AddToChat($"User {newUsername} has joined the chat!"); // Log the new user joining the chat - Andrew
                    byte[] welcomeData = Encoding.ASCII.GetBytes($"Welcome, {newUsername}!"); // Create a welcome message for the currentClientSocket - Andrew
                    currentClientSocket.socket.Send(welcomeData); // Send a welcome message to the currentClientSocket - Andrew

                }
                else
                {
                    byte[] data = Encoding.ASCII.GetBytes("Please set your username using !username <your_username>");
                    currentClientSocket.socket.Send(data);
                    AddToChat("Client attempted to send message without setting username");
                    currentClientSocket.socket.BeginReceive(currentClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, currentClientSocket);
                    return; // Exit the method to prevent further processing
                }
            }
            if (currentClientSocket.username != null)
            {
                if (text.ToLower().StartsWith("!user") && !text.ToLower().StartsWith("!username"))
                {
                    string newUsername = text.Substring(6).Trim(); // Extract the new username from the command - Andrew
                    if (string.IsNullOrWhiteSpace(newUsername)) // Check if the new username is empty or whitespace - Andrew
                    {
                        byte[] data = Encoding.ASCII.GetBytes("Username cannot be empty. Please choose a valid username.");
                        currentClientSocket.socket.Send(data);
                        AddToChat("Client attempted to set an empty username");
                        currentClientSocket.socket.BeginReceive(currentClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, currentClientSocket);
                        return; // Exit the method to prevent further processing
                    }
                    foreach (ClientSocket c in clientSockets) // Check for duplicate usernames - Andrew
                    {
                        if (c.username != null && c.username.Equals(newUsername, StringComparison.OrdinalIgnoreCase)) // Compare usernames case-insensitively - Andrew
                        {
                            byte[] data = Encoding.ASCII.GetBytes("Username already taken!\n");
                            currentClientSocket.socket.Send(data);
                            AddToChat($"Client attempted to set duplicate username: {newUsername}");
                            usernameUsed = true;
                            if (usernameUsed == true)
                            {
                                await DisconnectClient(currentClientSocket); // Disconnect the client if the username is already taken - Andrew
                                usernameUsed = false;
                                return;
                            }
                        }
                    }
                    oldUsername = currentClientSocket.username; // Store the old username for notification purposes - Andrew
                    currentClientSocket.username = newUsername; // Set the new username to currentClientSocket - Andrew
                    AddToChat($"User {oldUsername} changed their username to: {newUsername}"); // Log the username change in the chat - Andrew
                    byte[] usernameChangedData = Encoding.ASCII.GetBytes($"Username updated to {newUsername}"); // Create a message to notify all clients of the username change - Andrew
                    SendToAll($"User {oldUsername} changed their username to {newUsername}", currentClientSocket); // Push the username change notification to all clients - Andrew
                }
            }
            if (text.ToLower().StartsWith("!commands")) // Client requested time
            {
                byte[] data = Encoding.ASCII.GetBytes("Commands are !commands !about !who !whisper !exit");
                currentClientSocket.socket.Send(data);
                AddToChat("Commands sent to: " + currentClientSocket.username);
            }
            if (text.ToLower().StartsWith("!who")) // Client requested time
            {
                clientSockets.OrderBy(c => c.username).ToList(); // Sort the list of connected users alphabetically - Andrew
                byte[] data = Encoding.ASCII.GetBytes("Connected users: " + string.Join(", ", clientSockets.ConvertAll(c => c.username)));
                currentClientSocket.socket.Send(data);
                AddToChat("Connected users sent to: " + currentClientSocket.username);
            }
            if (text.ToLower().StartsWith("!about")) // Client requested time
            {
                byte[] data = Encoding.ASCII.GetBytes("Server: This is a simple chat server MVP that was provided by Torrens University Australia for NDS203 Networking and Databases. It allows multiple clients to connect and chat with each other via TCP/IP. It was co-developed by Andrew Egan in October of 2026");
                currentClientSocket.socket.Send(data);
                AddToChat("About information sent to: " + currentClientSocket.username);
            }
            if (text.ToLower().StartsWith("!whisper")) // Client requested to whisper to another user
            {
                string[] parts = text.Split(' ', 3); // Split the command into parts
                if (parts.Length < 3)
                {
                    byte[] data = Encoding.ASCII.GetBytes("Usage: !whisper <username> <message>");
                    currentClientSocket.socket.Send(data);
                    AddToChat("Whisper command usage sent to: " + currentClientSocket.username);
                }
                else
                {
                    string targetUsername = parts[1]; // The username to whisper to
                    string message = parts[2]; // The message to send
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
            if (text.ToLower().StartsWith("!reply"))
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
            if (text.ToLower().StartsWith("!kick"))
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
                        currentClientSocket.socket.BeginReceive(currentClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, currentClientSocket);
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
            if (text.ToLower() == "!exit") // Client wants to exit gracefully
            {
                // Always Shutdown before closing
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
            currentClientSocket.socket.BeginReceive(currentClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, currentClientSocket);
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

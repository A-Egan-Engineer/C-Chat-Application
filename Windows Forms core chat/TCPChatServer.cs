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

        /* Callback function for handling received data from clients */
        public async void ReceiveCallback(IAsyncResult AR)
        {
            ClientSocket currentClientSocket = (ClientSocket)AR.AsyncState;

            currentClientSocket.state = true; // Set the state to true to indicate that the client is active - Andrew

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

            byte[] recBuf = new byte[received];
            Array.Copy(currentClientSocket.buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf);

            if (currentClientSocket.username == null) // Check if username is not set and command is !username - Andrew
            {
                if (text.StartsWith("!username")) // Check if the command is !username - Andrew
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
                if (text.StartsWith("!user") && !text.StartsWith("!username"))
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
                    byte[] usernameChangedData = Encoding.ASCII.GetBytes($"Username updated to {newUsername}"); // Create a message to notify all clients of the username change - Andrew
                    SendToAll($"User {oldUsername} changed their username to {newUsername}", currentClientSocket); // Push the username change notification to all clients - Andrew
                }
            }
            if (text.ToLower() == "!commands") // Client requested time
            {
                byte[] data = Encoding.ASCII.GetBytes("Commands are !commands !about !who !whisper !exit");
                currentClientSocket.socket.Send(data);
                AddToChat("Commands sent to: " + currentClientSocket.username);
            }
            if (text.ToLower() == "!who") // Client requested time
            {
                clientSockets.OrderBy(c => c.username).ToList(); // Sort the list of connected users alphabetically - Andrew
                byte[] data = Encoding.ASCII.GetBytes("Connected users: " + string.Join(", ", clientSockets.ConvertAll(c => c.username)));
                currentClientSocket.socket.Send(data);
                AddToChat("Connected users sent to: " + currentClientSocket.username);
            }
            if (text.ToLower() == "!exit") // Client wants to exit gracefully
            {
                // Always Shutdown before closing
                await DisconnectClient(currentClientSocket);
                return;
            }
            else
            {
                if (text.StartsWith("!username"))
                {
                    AddToChat("Client updated username to: " + currentClientSocket.username);
                }
                if (text.StartsWith("!user"))
                {
                    AddToChat("User " + oldUsername + " updated username to: " + currentClientSocket.username);
                }
                else if (!text.StartsWith("!username") && !text.StartsWith("!user") && !text.StartsWith("!commands") && !text.StartsWith("!who") && !text.StartsWith("!exit"))
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
                client.state = false;
                AddToChat("Client disconnected");
            }
        }
    }
}

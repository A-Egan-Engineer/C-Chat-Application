# NDS203 Assessment 2: TCP Chat Application User Guide

|---|---|
| **Student** | Andrew Egan
| **Student ID** | A00200033
| **Unit** | NDS203 Networking and Databases
| **Facilitator** | Daniel Wheat |
| **Date** | 15/11/2026
| **Repository Link** | https://github.com/A-Egan-Engineer/C-Chat-Application

---

## 1. Overview

The application is a Windows Forms chat program written in C#. It uses TCP (Transmission Control Protocol, a reliable connection-based network protocol) sockets. One running instance acts as the **host** (server). 
Any number of other instances act as **clients** and connect to the host via a IP address and port number. Once connected, clients can send messages to everyone, send private messages (whispers) to one other user, 
and reply privately to the last whisper they received. The host can promote clients to moderators, who can remove (kick) other users from the chat.

**Features covered by this guide:**

- Hosting and joining a chat server
- Setting and changing a username
- Public chat, private whispers and replies
- Moderator promotion, listing and kicking
- Tic-tac-toe board - TBC for Assessment 3

---

## 2. Requirements

| Item | Requirement |
|---|---|
| Operating system | Windows 10 (1909) or newer
| .NET SDK | 10.0.401 or newer
| IDE | Visual Studio Community 2026
| Network | All instances on the same machine, or on the same LAN (Local Area Network)

---

## 3. Building and Running

1. Download or clone the repository from GitHub/Submission Portal and extract it locally.
2. Open `Windows_Forms_core_chat.csproj` in Visual Studio.
3. Build the solution: (Ctrl+Shift+B) or select **Build > Build Solution** from the menu.
4. Run and debug the application: (F5) or select **Debug > Start Debugging** from the menu.
5. Run one instance to host by selecting the **Host Server** button.
6. Run a second instance to join as client by selecting the **Join Server** button and entering the host's IP address and port.
7. Use the command !username to set your name before sending any other messages, then use !commands to see the available commands.
8. Multiple client instances can be run on the same machine to test the chat functionality and commands.

---

## 4. Interface Reference

| Control | Default | Purpose |
|---|---|---|
| **My Port** | 6666 | The port the host listens on
| **Server Port** | 6666 | The port of the host you are joining
| **Server IP** | 127.0.0.1 | The IP address of the host. `127.0.0.1` means the same machine
| **Host Server** | n/a | Starts a server on **My Port**
| **Join Server** | n/a | Connects to the host at **Server IP** and **Server Port**
| **Chat window** | n/a | Shows messages, system notices and errors. Read only
| **Type box**  Where you type messages and commands. Press **Enter** or click **Send**

---

## 5. Hosting a Server

1. Launch the application.
2. Enter a port in **My Port** (or keep 6666).
3. Click **Host Server**.
4. The chat window should show:
   ```
   Setting up server...

   Server setup complete
   ```
5. Give the **Server IP** and port to anyone who needs to join.

---

## 6. Joining a Server

1. Launch a second instance.
2. Enter the host's IP in **Server IP** and its port in **Server Port**.
3. Click **Join Server**.
4. The chat window shows `Connection attempt 1`, then `Connected`.
5. **Set a username before anything else.** Type:
   ```
   !username <username>
   ```
	6. The server replies `Welcome, <username>!` and the host sees `User <username> has joined the chat!`.

Until a username is set, any other message returns: `Please set your username using !username <your_username>`.

---

## 7. Commands

Commands start with `!` and are not case sensitive. Text that does not start with `!` is sent to everyone as a normal message.

### 7.1 Client commands

| Command | Shortcut | Syntax | What it does | Access
|---|---|---|---|---|
| `!username` | none | `!username <name>` | Sets your name on first join. Blank names are rejected. | All
| `!user` | none | `!user <name>` | Changes your name after it is set. Everyone else is notified. | All
| `!commands` | `!c` | `!commands` | Lists available commands. | All
| `!who` | `!w` | `!who` | Lists connected users. | All
| `!about` | `!a` | `!about` | Shows the server's purpose and author. | All
| `!whisper` | `!w` | `!whisper <name> <message>` | Sends a private message to one user. | All
| `!reply` | `!r` | `!reply <message>` | Replies privately to the last user who whispered you. | All
| `!kick` | `!k` | `!kick <name>` | Removes a user from the server. | Moderators only
| `!exit` | `!e` | `!exit` | Disconnects you. The connection closes after 5 seconds. | All

### 7.2 Host commands

Type these in the host's own type box.

| Command | Syntax | What it does |
|---|---|---|
| `!mod` | `!mod <name>` | Promotes a connected user to moderator. Running it again on a moderator demotes them.
| *plain text* | `Hello` | Broadcast to all clients, shown as `Host: Hello`

### 7.3 Behaviour notes

- **Duplicate usernames** (case insensitive): the client sees `Username already taken!` and is disconnected after 5 seconds.
- **Whispers** are delivered only to the sender and recipient, but the host's chat window logs every whisper.
- **Kicking** closes the target's connection and announces it to the other clients and the host.
- **Moderator status** can only be granted by the host. A moderator cannot promote anyone.

---

## 8. References

- Base project and brief: Torrens University Australia, NDS203.

- Microsoft Learn. (2025, October 22). Use Sockets to send and receive data over TCP. Microsoft. Learn.Microsoft.Com. https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/sockets/socket-service
	Useful for understanding the TCP socket programming model and how data is both sent and received via Transmission Control Protocol (TCP).

- Maria. (2025, August 10). C# Networking: Raw Sockets, TCP, and UDP Programming. DEV Community. DEV Community. https://dev.to/chakewitz/c-networking-raw-sockets-tcp-and-udp-programming-46oc
	This article provides a practical overview of TCP and UDP socket programming in C#, including reference to building a chat application. It also covers the differences between TCP and UDP, and how to implement both protocols in C#.

- Async / Await in C# https://www.tutorialspoint.com/csharp/csharp_asyn_await.htm
	This tutorial explains the use of asynchronous programming in C# using the async and await keywords, which is relevant for handling network operations without blocking the main thread.

- C# - if...else Statement https://www.tutorialspoint.com/csharp/if_else_statement_in_csharp.htm
	 This tutorial covers if else statements in C#, which is essential for implementing conditional logic in the chat application, such as handling different commands and user inputs.
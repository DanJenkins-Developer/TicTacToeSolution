using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;


IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync("localhost");
IPAddress ipAddress = IPAddress.Loopback;//ipHostInfo.AddressList[0];
IPEndPoint ipEndPoint = new(ipAddress, 11_000);

using Socket listener = new(
    ipEndPoint.AddressFamily,
    SocketType.Stream,
    ProtocolType.Tcp
);


listener.Bind(ipEndPoint);
listener.Listen(100);



ConcurrentDictionary<string, Socket> connectedClients = new ConcurrentDictionary<string, Socket>();

while (true)
{
    //connectedClients.Clear();

    Console.WriteLine("Listening for connections...");
    AcceptClientsAsync(listener, connectedClients);

    Console.WriteLine("Two clients connected");
    Console.WriteLine(connectedClients.Count);

    var tasks = new List<Task>();
    foreach (var kvp in connectedClients)
    {
        tasks.Add(HandleClientAsync(kvp));
    }

    await Task.WhenAll(tasks);

}
async Task HandleClientAsync(KeyValuePair<string, Socket> client)
{
    var clientSocket = client.Value;

    //var eom = "<|EOM|>";
    var eom = "\n";
    //var endConnection = "<|END|>";



    while (true)
    {
        Console.WriteLine("Inside while loop for client :: " + client.Key);
        Console.WriteLine("Client count :: " + connectedClients.Count);

        if (connectedClients.Count == 2)
        {
            try
            {
                string recievedMessage = await receiveMessage(clientSocket);
                //if (recievedMessage.IndexOf(eom) > -1)
                if (recievedMessage != "QUIT")
                {

                    Console.WriteLine($"Socket server received message: \"{recievedMessage.Replace(eom, "")}\"");
                    //await sendAck(clientSocket);
                    await sendMessage(clientSocket, "Your message was received");
                    break;

                }
                //else if (recievedMessage.IndexOf(endConnection) > -1)
                else if (recievedMessage == "QUIT")
                {
                    Console.WriteLine("Inside QUIT");
                    // Disconnect client
                    //await sendAck(clientSocket);

                    // clientSocket.Shutdown(SocketShutdown.Both);
                    // clientSocket.Close();
                    // connectedClients.TryRemove(client.Key, out _);
                    await HandleQuit(clientSocket, client);

                    Console.WriteLine("Client disconnected");
                    Console.WriteLine("New client count :: " + connectedClients.Count);

                    break;

                }
                //string response = recievedMessage + " <|EOM|>";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing client {client.Key}: {ex.Message}");
                await HandleQuit(clientSocket, client);
                break;
            }
        }
        else if (connectedClients.Count == 1)
        {
            await sendAck(clientSocket);
            break;
        }
    }

}
async Task HandleQuit(Socket clientSocket, KeyValuePair<string, Socket> client)
{
    await DisconnectClient(clientSocket, client);

}
async Task DisconnectClient(Socket clientSocket, KeyValuePair<string, Socket> client)
{
    // Disconnect client
    await sendAck(clientSocket);

    clientSocket.Shutdown(SocketShutdown.Both);
    clientSocket.Close();
    connectedClients.TryRemove(client.Key, out _);
}
async void AcceptClientsAsync(Socket listener, ConcurrentDictionary<string, Socket> connectedClients)
{
    SemaphoreSlim clientConnectedSemaphore = new SemaphoreSlim(2, 2);

    while (connectedClients.Count < 2)
    {

        _ = Task.Run(async () =>
        {
            var handler = await listener.AcceptAsync();
            string clientId = Guid.NewGuid().ToString();
            connectedClients.TryAdd(clientId, handler);
            Console.WriteLine($"Client {clientId} connected");

            await clientConnectedSemaphore.WaitAsync();
            clientConnectedSemaphore.Release();
        });
    }

    foreach (var client in connectedClients)
    {
        var message = "<|start|>";
        await sendMessage(client.Value, message);
    }
}
async Task sendMessage(Socket handler, string message)
{
    var echoBytes = Encoding.UTF8.GetBytes(message);
    await handler.SendAsync(echoBytes, 0);
    Console.WriteLine($"Socket server sent message: \"{message}\"");
}
async Task sendAck(Socket handler)
{
    var ackMessage = "<|ACK|>";
    var echoBytes = Encoding.UTF8.GetBytes(ackMessage);
    await handler.SendAsync(echoBytes, 0);
    //Console.WriteLine($"Socket server sent acknowledgment: \"{ackMessage}\"");
    Console.WriteLine($"Socket server sent acknowledgment: \"{ackMessage}\"");
}
async Task<string> receiveMessage(Socket handler)
{
    Console.WriteLine("Waiting for message");

    var task = Task.Run(async () =>
    {
        var buffer = new byte[1_024];
        var received = await handler.ReceiveAsync(buffer, SocketFlags.None);
        var response = Encoding.UTF8.GetString(buffer, 0, received);
        return response;
    });
    // var buffer = new byte[1_024];
    // var received = await handler.ReceiveAsync(buffer, SocketFlags.None);
    // var response = Encoding.UTF8.GetString(buffer, 0, received);

    var message = await task;
    //Console.WriteLine($"Socket server received message: \"{response}\"");
    return message;
}

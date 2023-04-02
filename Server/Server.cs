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
SemaphoreSlim clientConnectedSemaphore = new SemaphoreSlim(2, 2);



while (true)
{

    Console.WriteLine("Listening for connections...");
    AcceptClientsAsync(listener, connectedClients, clientConnectedSemaphore);

    Console.WriteLine("Two clients connected");
    Console.WriteLine(connectedClients.Count);

    while (connectedClients.Count == 2)
    {

        var tasks = new List<Task>();

        foreach (var kvp in connectedClients)
        {
            var clientSocket = kvp.Value;

            var task = Task.Run(async () =>
            {
                while (true)
                {
                    var eom = "<|EOM|>";
                    try
                    {
                        string recievedMessage = await receiveMessage(clientSocket);
                        if (recievedMessage.IndexOf(eom) > -1)
                        {

                            Console.WriteLine($"Socket server received message: \"{recievedMessage.Replace(eom, "")}\"");
                            await sendAck(clientSocket);
                            break;

                        }
                        else
                        {
                            // Disconnect client
                            clientSocket.Shutdown(SocketShutdown.Both);
                            clientSocket.Close();
                            connectedClients.TryRemove(kvp.Key, out _);

                        }
                        //string response = recievedMessage + " <|EOM|>";
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing client {kvp.Key}: {ex.Message}");
                        break;
                    }
                }
            });

            tasks.Add(task);
        }
        await Task.WhenAll(tasks);
    }


    break;
}
async void AcceptClientsAsync(Socket listener, ConcurrentDictionary<string, Socket> connectedClients, SemaphoreSlim clientConnectedSemaphore)
{
    Console.WriteLine("Hello");
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
    Console.WriteLine($"Socket server sent acknowledgment: \"{ackMessage}\"");
}
async Task<string> receiveMessage(Socket handler)
{
    Console.WriteLine("Waiting for message");
    var buffer = new byte[1_024];
    var received = await handler.ReceiveAsync(buffer, SocketFlags.None);
    var response = Encoding.UTF8.GetString(buffer, 0, received);


    //Console.WriteLine($"Socket server received message: \"{response}\"");
    return response;
}

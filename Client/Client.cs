﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Net;

IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync("localhost");
IPAddress ipAddress = IPAddress.Loopback;//ipHostInfo.AddressList[0];
IPEndPoint ipEndPoint = new(ipAddress, 11_000);

using Socket client = new(
    ipEndPoint.AddressFamily,
    SocketType.Stream,
    ProtocolType.Tcp
);

//Console.WriteLine("This is a test");

await client.ConnectAsync(ipEndPoint);
string startResponse = await receiveMessage();

Console.WriteLine($"Socket client received message: \"{startResponse}\"");


if (startResponse == "<|start|>")
{

    while (true)
    {
        //var message = "Hi friends! <|EOM|>";
        Console.Write("Enter message to send :: ");
        var message = Console.ReadLine();
        // var messageBytes = Encoding.UTF8.GetBytes(message);
        // _ = await client.SendAsync(messageBytes, SocketFlags.None);
        // Console.WriteLine($"Socket client sent message: \"{message}\"");

        await Task.Delay(TimeSpan.FromSeconds(1));

        await sendMessage(message + " \n");
        //await sendMessage(message);

        var buffer = new byte[1_024];
        var received = await client.ReceiveAsync(buffer, SocketFlags.None);
        var response = Encoding.UTF8.GetString(buffer, 0, received);
        if (response == "<|ACK|>")
        {
            Console.WriteLine($"Socket client received acknowledgment: \"{response}\"");
            break;
        }
        else
        {
            Console.WriteLine($"Socket client received message: \"{response}\"");
        }


    }
}
else
{
    Console.WriteLine("Server did not respond with start message");
}





async Task sendMessage(string message)
{
    var messageBytes = Encoding.UTF8.GetBytes(message);
    _ = await client.SendAsync(messageBytes, SocketFlags.None);
    Console.WriteLine($"Socket client sent message: \"{message}\"");
}
async Task<string> receiveMessage()
{
    Console.WriteLine("Receiving message");
    var buffer = new byte[1_024];
    var received = await client.ReceiveAsync(buffer, SocketFlags.None);
    var response = Encoding.UTF8.GetString(buffer, 0, received);
    //Console.WriteLine($"Socket client received message: \"{response}\"");
    return response;
}
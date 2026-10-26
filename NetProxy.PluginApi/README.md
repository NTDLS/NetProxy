# NetProxy.PluginApi

The API for writing plug-ins for [NetProxy](https://github.com/NTDLS/NetProxy), a free reverse proxy, load balancer and traffic router for Windows and Linux (HTTP, HTTPS, TCP and UDP).

A plug-in is a .NET assembly with a class that derives from `NpPlugin`. You override the events you care about; the ones you do not override cost nothing. This package has no dependencies, and you do not need the source of NetProxy to build a plug-in.

## Write one

1. Create a class library for the same .NET version as the service (net10.0) and add the package:

   ```
   dotnet add package NetProxy.PluginApi
   ```

2. Derive from `NpPlugin`, mark the class with `[NpPlugin]`, and override events:

   ```csharp
   using NetProxy.PluginApi;

   [NpPlugin("Blocker", Version = "1.0", Author = "You", Description = "Refuses connections from the clients it is told about.")]
   public class BlockerPlugin : NpPlugin
   {
       private HashSet<string> _blocked = [];

       public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
       {
           // Settings are the name=value lines that are typed in the console for the plug-in.
           _blocked = Host.Settings.TryGetValue("BlockClients", out var list)
               ? list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet()
               : [];
           return ValueTask.CompletedTask;
       }

       public override ValueTask<NpVerdict> OnConnectingAsync(INpConnection connection, CancellationToken cancellationToken)
           => ValueTask.FromResult(_blocked.Contains(connection.ClientAddress)
               ? NpVerdict.Deny($"{connection.ClientAddress} is blocked")
               : NpVerdict.Allow);
   }
   ```

3. Build, then copy the plug-in assembly to the machine that the service runs on. In the NetProxy console choose *Settings, Plugins, Add*, point it at the `.dll`, give it its settings, and tick the plug-in on the *Plugins* tab of each proxy that should use it.

## What it can do

* **Service events:** starting, stopping, a timer tick, management logins, errors.
* **Proxy events:** starting (can say no), started, stopped, configuration changed.
* **Connection events:** refused, TLS established, connecting (can say no), endpoint connecting (can say no), connected, failed, idle timeout, disconnecting, disconnected.
* **Data events:** `OnDataReceivedAsync`, `OnDataSendingAsync` and `OnDataSentAsync`. They sit between the two sides of a connection, so for an HTTPS proxy that terminates TLS they see the **decrypted** data before it is encrypted again for the endpoint. A plug-in can replace the data, drop it, or close the connection.
* **UDP:** every datagram is a data event.

A plug-in that throws, or takes longer than its time limit, is logged and skipped for that event; the connection carries on. A new build can be loaded with *Reload* in the console without restarting the service.

## The API assembly is not copied to your output

The service supplies `NetProxy.PluginApi` to the plug-ins it loads, and a second copy next to your plug-in would be a different set of types. The package therefore leaves it out of your build output automatically. (If a test project needs the assembly beside it, set `<NetProxyPluginApiCopyLocal>true</NetProxyPluginApiCopyLocal>`.)

Match the major version of the package to the NetProxy service you run it on.

## More

`NetProxy.SamplePlugin` in the repository is a small working example, and the NetProxy README describes every event and how plug-ins are set up.

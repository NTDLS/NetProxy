# NetProxy Plugins: Tests

The tests of every plug-in in this folder and of `Common`, in one xUnit project, a folder for each plug-in.

```
dotnet test NetProxy.Plugins\Tests
```

They start a plug-in the way the service does (attached to a host, then `OnServiceStarting`, with a data folder of its own that is removed afterwards), send it events as a proxy would, and look at what it does to the data: `Support\PluginTestBase.cs` and `Support\Fakes.cs` (a connection, a host, a clock that only moves when it is told to). The Metrics tests make real requests to the metrics server on a free port; no test needs anything else from the machine. They do not load the plug-ins through the real service's plug-in loader: for that, add the built dll in the console, or see `NetProxy.Tests\Plugins` for how a test loads one.

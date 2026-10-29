# NetProxy Alerts

Tells you in Discord, Slack, a webhook of your own or an e-mail when something is wrong. A plug-in for [NetProxy](https://github.com/NTDLS/NetProxy), in the project `NetProxy.Plugins.Alerts`; it builds to `NetProxy.Plugins.Alerts.dll`.

## Install

1. Run `Publish.bat` in the folder above (or take `NetProxy.Plugins.Alerts.zip`) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: next to the plug-in there is `NetProxy.Plugins.Common.dll`. The same files run on Windows and Linux.
2. In the management console open **Plugins** > **Add**, pick `NetProxy.Plugins.Alerts.dll`, leave the type name empty (the dll holds one plug-in), and put the settings in *Settings*, one `name=value` per line. All of them are optional unless this page says otherwise.
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for. The plug-ins of a proxy are called in the order of the list.

## Settings and behaviour

Channels (set the ones you want):

| setting | |
|---|---|
| `Discord.Url`, `Slack.Url` | An incoming webhook. |
| `Webhook.Url`, `Webhook.Authorization` | Any URL that takes a JSON post (`{source, severity, key, title, text, timeUtc}`). The authorization (for example `Bearer abc`) is only sent to this one. |
| `Smtp.Host`, `Smtp.Port`, `Smtp.User`, `Smtp.Password`, `Smtp.From`, `Smtp.To`, `Smtp.Tls` | E-mail (`Smtp.To` is a list; TLS and port 587 by default). |

What it tells: an endpoint that could not be connected to `FailuresBeforeDown` (3) times in a row, and when it is back with how long it was down; a proxy with no working endpoint; a proxy that could not start, and when it does; errors the service reports (`AlertOnErrors`); `FailedLoginsBeforeAlert` (3) failed logins to the management interface within 10 minutes. The same thing is told once in `MinSecondsBetween` (300), with a count of how many were held back; a recovery is always told. `NotifyOnStart=true` sends a message when the plug-in starts, to test the channels. `Source` names the machine in the messages.

Alerts are sent from a task of their own. An endpoint is only found to be down when a client tries it. Put this plug-in above the Maintenance Page in the list: a plug-in that answers for a proxy with no endpoint ends that event for the plug-ins after it.

## Good to know

* A setting that is wrong is logged and the default is used: the plug-in still loads.
* It never gets in the way of the traffic: if it fails, the traffic goes through (or, for a plug-in that guards something, the connection is closed).

## Building and testing

The tests are in `../Tests` (`dotnet test NetProxy.Plugins\Tests`). The shared code is in [`../Common`](../Common).

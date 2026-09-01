using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BepInEx;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using UnityEngine;

namespace DiscordNotifs
{
    public class DiscordClient
    {
        private string _token;
        private WebSocket _ws;
        private Thread _heartbeatThread;
        private bool _isRunning = false;
        private string _myUserId;
        private Dictionary<string, string> _guilds = new Dictionary<string, string>();
        private Dictionary<string, int> _dmChannelTypes = new Dictionary<string, int>();
        private BepInEx.Logging.ManualLogSource _logger;

        public DiscordClient(string token, BepInEx.Logging.ManualLogSource logger)
        {
            _token = token;
            _logger = logger;
        }

        public async Task StartAsync()
        {
            _isRunning = true;
            _ws = new WebSocket("wss://gateway.discord.gg/?v=9&encoding=json");
            _ws.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;
            
            _ws.OnMessage += (sender, e) =>
            {
                if (e.IsText)
                {
                    HandleMessage(e.Data);
                }
            };

            _ws.OnError += (sender, e) => _logger.LogError("Discord WS Error: " + e.Message);
            _ws.OnClose += (sender, e) => _logger.LogWarning($"Discord WS Closed: {e.Code} - {e.Reason}");

            _ws.ConnectAsync();
        }

        public void Stop()
        {
            _isRunning = false;
            if (_ws != null && _ws.ReadyState == WebSocketState.Open)
            {
                _ws.Close();
            }
        }

        private void HandleMessage(string json)
        {
            try
            {
                var payload = JObject.Parse(json);
                int op = payload["op"]?.Value<int>() ?? -1;

                if (op == 10)
                {
                    int heartbeatInterval = payload["d"]["heartbeat_interval"].Value<int>();
                    StartHeartbeat(heartbeatInterval);
                    SendIdentify();
                }
                else if (op == 0)
                {
                    string t = payload["t"]?.Value<string>();
                    
                    if (t == "READY")
                    {
                        _myUserId = payload["d"]["user"]["id"].Value<string>();
                        _logger.LogInfo($"Discord authenticated as {_myUserId}");

                        if (payload["d"]["guilds"] != null)
                        {
                            foreach (var g in payload["d"]["guilds"])
                            {
                                string id = g["id"]?.Value<string>();
                                string name = g["name"]?.Value<string>();
                                if (id != null && name != null)
                                {
                                    _guilds[id] = name;
                                }
                            }
                        }
                    }
                    else if (t == "GUILD_CREATE")
                    {
                        var d = payload["d"];
                        string id = d["id"]?.Value<string>();
                        string name = d["name"]?.Value<string>();
                        if (id != null && name != null)
                        {
                            _guilds[id] = name;
                        }
                    }
                    else if (t == "MESSAGE_CREATE")
                    {
                        var d = payload["d"];
                        string authorId = d["author"]["id"].Value<string>();
                        string content = d["content"].Value<string>();
                        string authorName = d["author"]["username"].Value<string>();
                        string guildId = d["guild_id"]?.Value<string>();
                        int msgType = d["type"] != null ? d["type"].Value<int>() : 0;
                        bool isDm = guildId == null;
                        
                        string avatarHash = d["author"]["avatar"]?.Value<string>();
                        string avatarUrl = avatarHash != null ? $"https://cdn.discordapp.com/avatars/{authorId}/{avatarHash}.png?size=64" : null;

                        if (msgType == 3) // Call
                        {
                            if (authorId != _myUserId)
                            {
                                QueueNotification($"Discord : {authorName}", $"{authorName} is calling you!", 5f, avatarUrl);
                            }
                        }
                        else
                        {
                            // check if it mentions me
                            bool mentionsMe = false;
                            if (d["mentions"] != null)
                            {
                                foreach (var mention in d["mentions"])
                                {
                                    if (mention["id"].Value<string>() == _myUserId)
                                    {
                                        mentionsMe = true;
                                        break;
                                    }
                                }
                            }

                            content = content.Replace($"<@{_myUserId}>", "").Replace($"<@!{_myUserId}>", "").Trim();

                            if (authorId != _myUserId)
                            {
                                if (isDm)
                                {
                                    string channelId = d["channel_id"]?.Value<string>();
                                    int dmType = GetDmChannelType(channelId);
                                    
                                    // dmType 1 is 1-on-1 DM, dmType 3 is Group DM
                                    if (dmType == 1 || (dmType == 3 && mentionsMe))
                                    {
                                        string prefix = dmType == 3 ? "Group" : "DM";
                                        QueueNotification($"Discord : {prefix}", $"{authorName}: {content}", 5f, avatarUrl);
                                    }
                                }
                                else if (mentionsMe)
                                {
                                    string serverName = GetGuildName(guildId);
                                    QueueNotification($"Discord : {serverName}", $"Ping from {authorName}: {content}", 5f, avatarUrl);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling message: " + ex.Message);
            }
        }

        private string GetGuildName(string guildId)
        {
            if (_guilds.TryGetValue(guildId, out var cachedName))
                return cachedName;

            try
            {
                using (var webClient = new WebClient())
                {
                    webClient.Headers.Add("Authorization", _token);
                    string json = webClient.DownloadString("https://discord.com/api/v9/users/@me/guilds");
                    var arr = JArray.Parse(json);
                    foreach (var g in arr)
                    {
                        string id = g["id"]?.Value<string>();
                        string name = g["name"]?.Value<string>();
                        if (id != null && name != null)
                        {
                            _guilds[id] = name;
                        }
                    }
                }
                
                if (_guilds.TryGetValue(guildId, out var newlyCachedName))
                    return newlyCachedName;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to fetch user guilds: {ex.Message}");
            }
            
            return "Server";
        }

        private int GetDmChannelType(string channelId)
        {
            if (string.IsNullOrEmpty(channelId)) return 1;
            
            if (_dmChannelTypes.TryGetValue(channelId, out int type))
                return type;
                
            try
            {
                using (var webClient = new WebClient())
                {
                    webClient.Headers.Add("Authorization", _token);
                    string json = webClient.DownloadString($"https://discord.com/api/v9/channels/{channelId}");
                    var obj = JObject.Parse(json);
                    int ctype = obj["type"] != null ? obj["type"].Value<int>() : 1;
                    _dmChannelTypes[channelId] = ctype;
                    return ctype;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to fetch channel type: {ex.Message}");
            }
            
            return 1;
        }

        private void StartHeartbeat(int intervalMs)
        {
            _heartbeatThread = new Thread(() =>
            {
                while (_isRunning)
                {
                    Thread.Sleep(intervalMs);
                    if (_ws != null && _ws.ReadyState == WebSocketState.Open)
                    {
                        _ws.Send("{\"op\": 1, \"d\": null}");
                    }
                }
            });
            _heartbeatThread.IsBackground = true;
            _heartbeatThread.Start();
        }

        private void SendIdentify()
        {
            var identifyPayload = new JObject
            {
                ["op"] = 2,
                ["d"] = new JObject
                {
                    ["token"] = _token,
                    ["capabilities"] = 16381,
                    ["properties"] = new JObject
                    {
                        // THIS IS TO PREVENT YOUR DISCORD ACCOUNT GETTING LIMITED, I MADE THE MISTAKE OF NOT PUTTING THIS HERE
                        ["os"] = "Windows",
                        ["browser"] = "Chrome",
                        ["device"] = "",
                        ["system_locale"] = "en-US",
                        ["browser_user_agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                        ["browser_version"] = "120.0.0.0",
                        ["os_version"] = "10",
                        ["referrer"] = "",
                        ["referring_domain"] = "",
                        ["referring_domain_current"] = "",
                        ["release_channel"] = "stable",
                        ["client_build_number"] = 258838,
                        ["client_event_source"] = null
                    }
                }
            };
            
            _ws.Send(identifyPayload.ToString());
        }

        private void QueueNotification(string title, string content, float duration, string imageUrl = null)
        {
            Dispatcher.Enqueue(() =>
            {
                try {
                    if (NotifManager.Instance != null)
                    {
                        NotifManager.Instance.QueueNotification(title, content, duration, imageUrl);
                    }
                } catch(Exception ex) {
                    _logger.LogError("Failed to send notification: " + ex);
                }
            });
        }
    }
}

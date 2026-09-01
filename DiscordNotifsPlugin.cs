using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace DiscordNotifs
{
    [BepInPlugin("com.poopoovr.discordnotifs", "Discord Notifications", "1.0.0")]
    public class DiscordNotifsPlugin : BaseUnityPlugin
    {
        private ConfigEntry<string> _tokenConfig;
        public static ConfigEntry<string> HandChoiceConfig;
        private DiscordClient _client;

        void Awake()
        {
            _tokenConfig = Config.Bind("General", "DiscordToken", "", "Your Discord User Token");
            HandChoiceConfig = Config.Bind("General", "HandChoice", "Left", "Which hand to attach the VR UI to (Left or Right)");

            var notifObj = new GameObject("DiscordNotifManager");
            notifObj.AddComponent<NotifManager>();
            
            if (string.IsNullOrEmpty(_tokenConfig.Value))
            {
                Logger.LogWarning("No Discord token provided. Discord Notifications will not run.");
                return;
            }

            _client = new DiscordClient(_tokenConfig.Value, this.Logger);
            _ = _client.StartAsync();
            Logger.LogInfo("Discord Notifications Plugin initialized.");
        }

        void OnDestroy()
        {
            if (_client != null)
            {
                _client.Stop();
            }
        }

        void Update()
        {
            Dispatcher.Update();
        }
    }

    public static class Dispatcher
    {
        private static readonly System.Collections.Concurrent.ConcurrentQueue<Action> _executionQueue = new System.Collections.Concurrent.ConcurrentQueue<Action>();

        public static void Enqueue(Action action)
        {
            _executionQueue.Enqueue(action);
        }

        public static void Update()
        {
            while (_executionQueue.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DarkBot;
using Discord;
using DarkBot.Whitelist;
using Discord.WebSocket;


namespace DarkBot_Chastity
{
    public class Chastity : BotModule
    {
        private DiscordSocketClient _client = null;
        private Whitelist _whitelist = null;
        private Dictionary<ulong, UserInfo> database = new Dictionary<ulong, UserInfo>();
        private long nextUnlock = 0;
        private ulong chastityRole = 727435100282945566UL;
        private ulong chastityChannel = 774853837756170250UL;

        public Task Initialize(IServiceProvider service)
        {
            _whitelist = service.GetService(typeof(Whitelist)) as Whitelist;
            _client = service.GetService(typeof(DiscordSocketClient)) as DiscordSocketClient;
            _client.Ready += OnReady;
            _client.MessageReceived += HandleMessage;
            LoadDatabase();
            return Task.CompletedTask;
        }

        private Task OnReady()
        {
            Task.Run(HeartBeat);
            LoadDatabase();
            Log(LogSeverity.Info, "Chastity ready!");
            return Task.CompletedTask;
        }

        private async void HeartBeat()
        {
            bool isRunning = true;
            while (isRunning == true)
            {
                long currentTime = DateTime.UtcNow.Ticks;
                UserInfo removeValue = null;
                if (nextUnlock > 0 && currentTime > nextUnlock)
                {
                    lock (database)
                    {
                        foreach (KeyValuePair<ulong, UserInfo> kvp in database)
                        {
                            if (kvp.Value.releaseTime > 0 && currentTime > kvp.Value.releaseTime)
                            {
                                removeValue = kvp.Value;
                                break;
                            }
                        }
                        if (removeValue != null)
                        {
                            database.Remove(removeValue.userID);
                            SaveDatabase();
                        }
                    }
                    if (removeValue != null)
                    {
                        SocketGuild sg = _client.GetGuild(removeValue.serverID);
                        if (sg == null)
                        {
                            continue;
                        }
                        SocketGuildUser sgu = sg.GetUser(removeValue.userID);
                        if (sg == null)
                        {
                            continue;
                        }
                        FreeUser(sg, sgu);
                    }
                }
                await Task.Delay(1000);
            }
        }

        private async void FreeUser(SocketGuild sg, SocketGuildUser sgu)
        {
            lock (database)
            {
                if (database.ContainsKey(sgu.Id))
                {
                    database.Remove(sgu.Id);
                    SaveDatabase();
                }
            }
            if (sg == null)
            {
                return;
            }
            if (sgu == null)
            {
                return;
            }
            SocketTextChannel stc = sg.GetTextChannel(chastityChannel);
            if (stc == null)
            {
                return;
            }
            SocketRole chastity = sg.GetRole(chastityRole);
            if (chastity == null)
            {
                return;
            }
            await sgu.RemoveRoleAsync(chastity);
            await stc.SendMessageAsync($"{sgu.Mention}, you are now unlocked!");
            Log(LogSeverity.Info, $"Unlocked {sgu.Username}");
        }

        private async Task HandleMessage(SocketMessage socketMessage)
        {
            SocketUserMessage message = socketMessage as SocketUserMessage;
            if (message == null || message.Author.IsBot)
            {
                return;
            }
            SocketTextChannel channel = message.Channel as SocketTextChannel;
            if (channel == null)
            {
                return;
            }
            if (!_whitelist.ObjectOK("chastity", channel.Id))
            {
                return;
            }
            long endTime = 0;
            int years = 0;
            int months = 0;
            int weeks = 0;
            int days = 0;
            int hours = 0;
            int minutes = 0;
            bool isCheck = false;
            bool isLock = false;
            bool isFree = false;
            string[] chunks = socketMessage.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string lastChunk = "";
            foreach (string chunk in chunks)
            {
                if (isFree)
                {
                    isLock = false;
                    isCheck = false;
                    SocketGuildUser sgu = message.Author as SocketGuildUser;
                    if (sgu == null)
                    {
                        return;
                    }
                    if (sgu.GuildPermissions.BanMembers)
                    {
                        if (chunk.Length > 5 && chunk.StartsWith("<@!") && chunk.EndsWith(">"))
                        {
                            string freeIDString = chunk.Substring(3, chunk.Length - 4);
                            if (ulong.TryParse(freeIDString, out ulong freeID))
                            {
                                SocketGuildUser freeUser = channel.Guild.GetUser(freeID);
                                if (freeUser == null)
                                {
                                    return;
                                }
                                FreeUser(channel.Guild, freeUser);
                            }
                        }
                    }
                    break;
                }
                string chunkLower = chunk.ToLower();
                int lastChunkInt = 0;
                bool lastChunkIsNumber = Int32.TryParse(lastChunk, out lastChunkInt);
                if (!lastChunkIsNumber)
                {
                    lastChunkInt = 0;
                }
                if (chunkLower == "forever")
                {
                    isLock = true;
                    endTime = -1;
                }
                if (chunkLower == "check")
                {
                    isLock = false;
                    isCheck = true;
                }
                if (chunkLower == "free")
                {
                    isFree = true;
                }
                if (chunkLower.StartsWith("year"))
                {
                    isLock = true;
                    years = lastChunkInt;
                }
                if (chunkLower.StartsWith("month"))
                {
                    isLock = true;
                    months = lastChunkInt;
                }
                if (chunkLower.StartsWith("week"))
                {
                    isLock = true;
                    weeks = lastChunkInt;
                }
                if (chunkLower.StartsWith("day"))
                {
                    isLock = true;
                    days = lastChunkInt;
                }
                if (chunkLower.StartsWith("hour"))
                {
                    isLock = true;
                    hours = lastChunkInt;
                }
                if (chunkLower.StartsWith("min"))
                {
                    isLock = true;
                    minutes = lastChunkInt;
                }
                lastChunk = chunk;
            }
            if (isLock)
            {
                long currentTime = System.DateTime.UtcNow.Ticks;
                if (endTime != -1)
                {
                    endTime = currentTime + (years * TimeSpan.TicksPerDay * 365) + (months * TimeSpan.TicksPerDay * 30) + (weeks * TimeSpan.TicksPerDay * 7) + (days * TimeSpan.TicksPerDay) + (hours * TimeSpan.TicksPerHour) + (minutes * TimeSpan.TicksPerMinute);
                }
                bool databaseHasValue = false;
                UserInfo ui = null;
                lock (database)
                {
                    databaseHasValue = database.TryGetValue(message.Author.Id, out ui);
                }
                if (databaseHasValue)
                {
                    if (ui.releaseTime > currentTime)
                    {
                        await channel.SendMessageAsync($"{message.Author.Mention}, you are already in chastity.");
                    }
                }
                else
                {
                    ui = new UserInfo(message.Author.Id, channel.Guild.Id, endTime);
                    lock (database)
                    {
                        database.Add(ui.userID, ui);
                        SaveDatabase();
                    }
                    SocketGuild sg = channel.Guild;
                    if (sg == null)
                    {
                        return;
                    }
                    SocketGuildUser sgu = channel.GetUser(message.Author.Id);
                    if (sgu == null)
                    {
                        return;
                    }
                    SocketRole chastity = sg.GetRole(chastityRole);
                    if (chastity == null)
                    {
                        return;
                    }
                    await sgu.AddRoleAsync(chastity);
                    if (endTime == -1)
                    {
                        await channel.SendMessageAsync($"{message.Author.Mention}, you are now in chastity forever.");
                    }
                    else
                    {
                        DateTime dt = new DateTime(endTime, DateTimeKind.Utc);
                        await channel.SendMessageAsync($"{message.Author.Mention}, you are now in chastity until {dt.ToString()}.");
                    }
                }
            }
            if (isCheck)
            {
                PrintCheck(channel, message);
            }
        }

        private async void PrintCheck(SocketTextChannel channel, SocketUserMessage message)
        {
            Log(LogSeverity.Info, $"Check by {message.Author.Username}");
            long currentTime = System.DateTime.UtcNow.Ticks;
            UserInfo ui = null;
            if (database.TryGetValue(message.Author.Id, out ui))
            {
                if (ui.releaseTime == -1)
                {
                    await channel.SendMessageAsync($"{message.Author.Mention}, you will never be unlocked.");
                    return;
                }
                if (ui.releaseTime > currentTime)
                {
                    DateTime dt = new DateTime(ui.releaseTime, DateTimeKind.Utc);
                    await channel.SendMessageAsync($"{message.Author.Mention}, you will be unlocked on {dt.ToString()}.");
                    return;
                }
            }
            await channel.SendMessageAsync($"{message.Author.Mention}, you are not in chastity.");
        }

        private void Log(LogSeverity severity, string text)
        {
            LogMessage logMessage = new LogMessage(severity, "Chastity", text);
            Program.LogAsync(logMessage);
        }

        private void LoadDatabase()
        {
            lock (database)
            {
                database.Clear();
                string dataString = DataStore.Load("Chastity");
                if (dataString == null)
                {
                    return;
                }
                string currentLine = null;
                nextUnlock = 0;
                using (StringReader sr = new StringReader(dataString))
                {
                    while ((currentLine = sr.ReadLine()) != null)
                    {
                        UserInfo ui = new UserInfo(currentLine);
                        if (ui.isOK)
                        {
                            database.Add(ui.userID, ui);
                            if (ui.releaseTime > 0 && nextUnlock > ui.releaseTime || nextUnlock == 0)
                            {
                                nextUnlock = ui.releaseTime;
                            }
                        }
                    }
                }
            }
        }

        private void SaveDatabase()
        {
            lock (database)
            {
                StringBuilder sb = new StringBuilder();
                nextUnlock = 0;
                foreach (KeyValuePair<ulong, UserInfo> ui in database)
                {
                    sb.AppendLine($"{ui.Key}={ui.Value.serverID},{ui.Value.releaseTime}");
                    if (ui.Value.releaseTime > 0 && nextUnlock > ui.Value.releaseTime || nextUnlock == 0)
                    {
                        nextUnlock = ui.Value.releaseTime;
                    }
                }
                DataStore.Save("Chastity", sb.ToString());
            }
        }

        private class UserInfo
        {
            public ulong userID;
            public ulong serverID;
            public long releaseTime;
            public bool isOK = true;

            public UserInfo(string input)
            {
                int split1 = input.IndexOf("=");
                int split2 = input.IndexOf(",");
                string userIDString = input.Substring(0, split1);
                string serverIDString = input.Substring(split1 + 1, split2 - split1 - 1);
                string releaseString = input.Substring(split2 + 1);
                isOK = ulong.TryParse(userIDString, out userID);
                isOK &= ulong.TryParse(serverIDString, out serverID);
                isOK &= long.TryParse(releaseString, out releaseTime);
            }

            public UserInfo(ulong userID, ulong serverID, long releaseTime)
            {
                this.userID = userID;
                this.serverID = serverID;
                this.releaseTime = releaseTime;
            }
        }
    }

}
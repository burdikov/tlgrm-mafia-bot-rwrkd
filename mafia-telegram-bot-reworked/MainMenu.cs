using System;
using System.Collections.Generic;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace mafia_telegram_bot_reworked
{
    internal class MainMenu
    {
        private class PlayerData
        {
            public bool Greeted;
            public uint? Room;
            public int Tries;
        }

        private static readonly Dictionary<long, PlayerData> PlayerDataDict = new Dictionary<long, PlayerData>();
        private static readonly Dictionary<uint, GameRoom> IdToRoom = new Dictionary<uint, GameRoom>();

        public static async void HandleMessage(Message msg)
        {
            if (msg.Type != MessageType.TextMessage) return;
            var id = msg.Chat.Id;

            if (!PlayerDataDict.ContainsKey(id))
                lock (PlayerDataDict)
                  if (!PlayerDataDict.ContainsKey(id))
                        PlayerDataDict.Add(id, new PlayerData { Greeted = false });



            if (!PlayerDataDict[id].Greeted)
            {
                var meToSend = false;
                lock (PlayerDataDict[id])
                  if (!PlayerDataDict[id].Greeted)
                    {
                        meToSend = true;
                        PlayerDataDict[id].Greeted = true;
                        Console.WriteLine(DateTime.Now + " " + ChatToStr(msg.Chat) + " начал новый чат");
                    }
                if (meToSend)
                {
                    await Program.Bot.SendTextMessageAsync(id, "Добро пожаловать! Этот бот предназначен для автоматической раздачи карт в игре мафия.\n" +
                      "Для начала игры создайте комнату или подключитесь к уже существующей.");
                    await Program.Bot.SendTextMessageAsync(id, "Введите номер комнаты.", false, false, 0, new ForceReply {Force = true });
                    return;
                }
            }

            if (PlayerDataDict[id].Room != null)
            {
                IdToRoom[(uint)PlayerDataDict[id].Room].HandleMessage(msg);
                return;
            }

            try
            {
                var desiredId = Convert.ToUInt32(msg.Text);
                lock (PlayerDataDict[id])
                {
                    if (PlayerDataDict[id].Room == null)
                    {
                        lock (IdToRoom)
                        {
                            if (IdToRoom.ContainsKey(desiredId))
                                IdToRoom[desiredId].AddMember(msg.Chat);
                            else
                                IdToRoom.Add(desiredId, new GameRoom(msg.Chat, desiredId));
                        }
                        PlayerDataDict[id].Room = desiredId;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(DateTime.Now + " <error: " + Program.ExceptionCounter++ + " " + e.Message +
                    "\nВ чате с: " + ChatToStr(msg.Chat) + ">");
                PlayerDataDict[id].Tries++;

                if (PlayerDataDict[id].Tries < 5)
                    await Program.Bot.SendTextMessageAsync(id, "Введите номер комнаты.", false, false, 0,
                        new ForceReply {Force = true});
            }
        }

        public static void HandleCallbackQuery(CallbackQuery q)
        {
            //if (q.Message == null) return;

            var id = q.Message.Chat.Id;
            if (PlayerDataDict.ContainsKey(id))
                {
                    if (PlayerDataDict[id].Room.HasValue)
                    {f
                        IdToRoom[PlayerDataDict[id].Room.Value].HandleCallbackQuery(q);
                        return;
                    }
                }
            Program.Bot.AnswerCallbackQueryAsync(q.Id);
        }

        public static async void LeaveRoom(Chat c)
        {
            var x = PlayerDataDict[c.Id].Room;

            PlayerDataDict[c.Id].Room = null;
            await Program.Bot.SendTextMessageAsync(c.Id, "Введите номер комнаты.", false, false, 0, new ForceReply { Force = true });

            Console.WriteLine(DateTime.Now + " " + ChatToStr(c) + " вышел из комнаты " + x);
        }

        public static string ChatToStr(Chat c) => c.Id + " " + c.FirstName + (c.LastName != null ? " " + c.LastName : "");

    }
}

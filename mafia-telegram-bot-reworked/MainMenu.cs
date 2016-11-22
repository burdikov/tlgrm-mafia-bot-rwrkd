using System;
using System.Collections.Generic;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace mafia_telegram_bot_reworked
{
    class MainMenu
    {
        class PlayerData
        {
            public bool Greeted;
            public uint? Room;// = null;
            public int Tries;
        }

        static Dictionary<long, PlayerData> PlayerDataDict = new Dictionary<long, PlayerData>();
        static Dictionary<uint, GameRoom> IdToRoom = new Dictionary<uint, GameRoom>();

        public static async void HandleMessage(Message msg)
        {
            long id = msg.Chat.Id;

            if (!PlayerDataDict.ContainsKey(id))
                lock (PlayerDataDict)
                  if (!PlayerDataDict.ContainsKey(id))
                        PlayerDataDict.Add(id, new PlayerData() { Greeted = false });



            if (!PlayerDataDict[id].Greeted)
            {
                bool meToSend = false;
                lock (PlayerDataDict[id])
                  if (!PlayerDataDict[id].Greeted)
                    {
                        meToSend = true;
                        PlayerDataDict[id].Greeted = true;
                    }
                if (meToSend)
                {
                    await Program.Bot.SendTextMessageAsync(id, "Добро пожаловать! Этот бот предназначен для автоматической раздачи карт в игре мафия.\n" +
                      "Для начала игры создайте комнату или подключитесь к уже существующей.");
                    await Program.Bot.SendTextMessageAsync(id, "Введите номер комнаты.", false, false, 0, new ForceReply() {Force = true });
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
                uint desiredID = Convert.ToUInt32(msg.Text);
                lock (PlayerDataDict[id])
                {
                    if (PlayerDataDict[id].Room == null)
                    {
                        lock (IdToRoom)
                        {
                            if (IdToRoom.ContainsKey(desiredID))
                                IdToRoom[desiredID].AddMember(msg.Chat);
                            else
                                IdToRoom.Add(desiredID, new GameRoom(msg.Chat, desiredID));
                        }
                        PlayerDataDict[id].Room = desiredID;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("==EXCEPTION: " + Program.ExceptionCounter++ + " CODE MAINE==\n" + e.Message + 
                    "\nВ чате с: " + msg.Chat.Id + " " + msg.Chat.FirstName + " " + msg.Chat.LastName);
                PlayerDataDict[id].Tries++;
                await Program.Bot.SendTextMessageAsync(id, "Введите номер комнаты.", false, false, 0, new ForceReply() {Force = true });
                switch (PlayerDataDict[id].Tries)
                {
                    case 10: await Program.Bot.SendTextMessageAsync(id, "Так и будешь слать мне всякую хрень или начнешь уже играть?"); break;
                    case 20: await Program.Bot.SendTextMessageAsync(id, "Возможно, тебе не стоит играть в эту игру. Она не для людей с интеллектом хлебушка."); break;
                    case 100: await Program.Bot.SendTextMessageAsync(id, "А ты упёртый!"); break;
                    case 1000: await Program.Bot.SendTextMessageAsync(id, "Даже слишком упёртый!"); break;
                    default:
                        break;
                }
            }
        }

        public static void HandleCallbackQuery(CallbackQuery q)
        {
            long? idx = q.Message?.Chat.Id;  
            if (idx != null)
            {
                long id = (long)idx;
                if (PlayerDataDict.ContainsKey(id))
                {
                    if (PlayerDataDict[id].Room != null)
                    {
                        IdToRoom[(uint)PlayerDataDict[id].Room].HandleCallbackQuery(q);
                    }
                }
            }
        }

        public static async void LeaveRoom(Chat c)
        {
            PlayerDataDict[c.Id].Room = null;
            await Program.Bot.SendTextMessageAsync(c.Id, "Введите номер комнаты.", false, false, 0, new ForceReply() { Force = true });
        }
    }
}

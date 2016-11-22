using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace mafia_telegram_bot_reworked
{
    class GameRoom
    {
        enum Roles
        {
            Ведущий,
            Девушка,
            Мафия,
            Якудза,
            Доктор,
            Маньяк,
            Комиссар,
            Мирный
        }

        static private ReplyKeyboardMarkup markupAdmin;
        static private ReplyKeyboardMarkup markupDefault;
        static private InlineKeyboardMarkup markupInlineConf;
        
        public uint ID { get; }

        private ObservableCollection<Chat> Members { get; }
        private uint[] CurrentConfiguration { get; }

        private long Admin { get { return Members.Count > 0 ? Members[0].Id : -1; } }

        private string PlayersList { get; set; }
        private string CurrentConfigurationString { get; set; }

        private object lockList = new object();
        private object lockConfig = new object();

        Dictionary<string, Action<Chat>> Actions { get; }

        static GameRoom()
        {
            KeyboardButton[][] admin = new KeyboardButton[3][];
            admin[0] = new KeyboardButton[] { Strings.NewGame };
            admin[1] = new KeyboardButton[] { Strings.PlayersNumber, Strings.Configuration };
            admin[2] = new KeyboardButton[] { Strings.LeaveRoom, Strings.RoomID };
            markupAdmin = new ReplyKeyboardMarkup(admin, true);

            KeyboardButton[][] def = new KeyboardButton[2][];
            def[0] = new KeyboardButton[] { Strings.PlayersNumber, Strings.Configuration };
            def[1] = new KeyboardButton[] { Strings.LeaveRoom, Strings.RoomID };
            markupDefault = new ReplyKeyboardMarkup(def, true);

            InlineKeyboardButton[][] conf = new InlineKeyboardButton[2][];
            conf[0] = new InlineKeyboardButton[] { "0", "1", "2", "3", "4" };
            conf[1] = new InlineKeyboardButton[] { "Отмена" };
            markupInlineConf = new InlineKeyboardMarkup(conf);
        }

        private GameRoom()
        {
            Actions = new Dictionary<string, Action<Chat>>{
        { Strings.LeaveRoom, LeaveRoom },
        { Strings.NewGame, NewGame },
        { Strings.Configuration, Configuration },
        { Strings.PlayersNumber, PlayersNumber },
        { Strings.RoomID, RoomID },
        { Strings.ShowMeRole, ShowMeRole },
        { Strings.RemainingRoles, RemainingRoles }
            };
        }

        public GameRoom(Chat c, uint x) : this()
        {
            ID = x;
            Members = new ObservableCollection<Chat>();
            Members.CollectionChanged += Members_CollectionChanged;
            lock (lockList)
            {
                Members.Add(c);
            }
            CurrentConfiguration = new uint[8] { 1, 1, 4, 0, 1, 1, 1, 4 };
            UpdateConfigurationString();
            GreetAdmin(c);
        }

        private void Members_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            PlayersList = "*Список игроков:*\n";
            int i = 1;
            foreach (var member in Members)
            {
                PlayersList += i++ + ". " + member.FirstName + "\n";
            }
        }

        public async void AddMember(Chat c)
        {
            Task<Message> [] tasks = null;
            if (Members.Count > 0)
            {
                tasks = new Task<Message>[Members.Count];
                for (int i = 0; i < Members.Count; i++)
                {
                    tasks[i] = Program.Bot.SendTextMessageAsync(Members[i].Id, "В комнату зашёл игрок " + c.FirstName);
                }
            }
            lock (lockList)
            {
                Members.Add(c);
            }
            if (tasks != null) Task.WaitAll(tasks);
            await Program.Bot.SendTextMessageAsync(c.Id, "Вы вошли в комнату " + ID + ".", false, false,0,markupDefault);
            if (c.Id == Admin) GreetAdmin(c);
        }

        static int stage = 1;                           // Не должны быть статическими!!
        static uint[] tempConf = new uint[8];
        public async void HandleCallbackQuery(CallbackQuery q)
        {
            
            long id = q.Message.Chat.Id;
            if (id != Admin)
            {
                await Program.Bot.AnswerCallbackQueryAsync(q.Id, "Вы не являетесь администратором этой комнаты.");
                return;
            }
            if (q.Data == "Отмена")
            {
                stage = 1;
                await Program.Bot.AnswerCallbackQueryAsync(q.Id, "Изменения отменены.");
                await Program.Bot.EditMessageTextAsync(id, q.Message.MessageId, "_Шанс упущен._",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown, false, new InlineKeyboardMarkup(new InlineKeyboardButton[] { "Не жми" }));
                return;
            }
            if (q.Data == "Сохранить")
            {
                if (stage == 8)
                {
                    for (int i = 1; i < 8; i++)
                    {
                        CurrentConfiguration[i] = tempConf[i];
                    }
                    stage = 1;
                    UpdateConfigurationString();
                    await Program.Bot.AnswerCallbackQueryAsync(q.Id, "Изменения сохранены.");
                    await Program.Bot.EditMessageReplyMarkupAsync(id, q.Message.MessageId, new InlineKeyboardMarkup(new InlineKeyboardButton[] { "Ввести заново" }));
                    //await Program.Bot.EditMessageTextAsync(id, q.Message.MessageId, "_Желание потрачено._",
                    //Telegram.Bot.Types.Enums.ParseMode.Markdown, false, new InlineKeyboardMarkup(new InlineKeyboardButton[] { "Не жми" }));
                    return;
                }
                else
                {
                    await Program.Bot.AnswerCallbackQueryAsync(q.Id, "Читерок типо?");
                }
                
            }
            Program.Bot.AnswerCallbackQueryAsync(q.Id);
            switch (stage)
            {
                case 1:
                    await Program.Bot.EditMessageTextAsync(id, q.Message.MessageId, "Выберите количество карт *" + ((Roles)(stage++)).ToString() + "*",
                Telegram.Bot.Types.Enums.ParseMode.Markdown, false, markupInlineConf);
                    break;
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                    tempConf[stage - 1] = Convert.ToUInt32(q.Data);
                    await Program.Bot.EditMessageTextAsync(id, q.Message.MessageId, "Выберите количество карт *" + ((Roles)(stage++)).ToString() + "*",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown, false, new ReplyKeyboardHide() { HideKeyboard = true });//markupInlineConf);
                    break;
                case 8:
                    tempConf[stage - 1] = Convert.ToUInt32(q.Data);
                    string resultingConf = "*Введенная конфигурация:*\n";
                    for (int i = 0; i < 8; i++)
                    {
                        resultingConf += ((Roles)i).ToString() + ": " + tempConf[i] + "\n";
                    }
                    InlineKeyboardMarkup mkp = new InlineKeyboardMarkup( new InlineKeyboardButton[] {"Сохранить", "Отмена"});
                    await Program.Bot.EditMessageTextAsync(id, q.Message.MessageId, resultingConf, Telegram.Bot.Types.Enums.ParseMode.Markdown,
                        false, mkp);
                    break;
                default:
                    break;
            }

            {

                
            }
        }

        private async void RemainingRoles(Chat c)
        {
            throw new NotImplementedException();
        }
        private async void ShowMeRole(Chat c)
        {
            throw new NotImplementedException();
        }
        private async void RoomID(Chat c)
        {
            await Program.Bot.SendTextMessageAsync(c.Id, "ID комнаты: " + ID);
        }
        private async void PlayersNumber(Chat c)
        {
            string membersList;
            lock (lockList)
            {
                membersList = string.Copy(PlayersList);
            }
            await Program.Bot.SendTextMessageAsync(c.Id, membersList, false, false, 0,
                null, Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }
        private async void Configuration(Chat c)
        {
            string buf;
            lock (lockConfig)
            {
                buf = string.Copy(CurrentConfigurationString);
            }
            await Program.Bot.SendTextMessageAsync(c.Id, buf, false, false, 0,
                c.Id == Admin ? (IReplyMarkup)new InlineKeyboardMarkup(new InlineKeyboardButton[] { "Задать новую" }) : markupDefault,
                Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }
        private async void NewGame(Chat c)
        {

        }
        private void LeaveRoom(Chat c)
        {
            MainMenu.LeaveRoom(c);
            bool admin = c.Id == Admin;
            lock (lockList)
            {
                int i = Members.IndexOf(Members.Where(x => c.Id == x.Id).First());
                Members.RemoveAt(i);
            }

            if (Members.Count > 0)
            {
                var tasks = new Task<Message>[Members.Count];
                for (int i = 0; i < Members.Count; i++)
                {
                    tasks[i] = Program.Bot.SendTextMessageAsync(Members[i].Id, "Игрок " + c.FirstName + " вышел из комнаты.");
                }
                Task.WaitAll(tasks);
                if (admin) GreetAdmin(Members[0]);
            }
        }

        private void UpdateConfigurationString()
        {
            CurrentConfigurationString = "*Текущая конфигурация:*\n";
            for (int i = 0; i < CurrentConfiguration.Length; i++)
            {
                CurrentConfigurationString += (Roles)i + ": " + CurrentConfiguration[i] + "\n";
            }
        }

        

        

        private async void GreetAdmin(Chat c)
        {
            await Program.Bot.SendTextMessageAsync(c.Id, "Теперь вы *администратор* комнаты " + ID + ". Вы можете менять конфигурацию и начинать игру.",
              false, false, 0, markupAdmin, Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }

        public void HandleMessage(Message msg)
        {
            if (Actions.ContainsKey(msg.Text)) Actions[msg.Text](msg.Chat);
        }

        //******************************************************
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace mafia_telegram_bot_reworked
{
    class GameRoom
    {
        private enum Role
        {
            Ведущий = 0,
            Девушка = 1,
            Мафия = 2,
            Якудза = 3,
            Доктор = 4,
            Маньяк = 5,
            Комиссар = 6,
            Мирный = 7
        }

        private static readonly ReplyKeyboardMarkup MarkupAdmin;
        private static readonly ReplyKeyboardMarkup MarkupDefault;
        private static readonly InlineKeyboardMarkup MarkupInlineConf;

        public uint Id { get; }

        private ObservableCollection<Chat> Members { get; }
        private uint[] CurrentConfiguration { get; }

        private long Admin { get; }

        private string PlayersList { get; set; } = "";

        private string CurrentConfigurationString { get; set; } = "";

        private Dictionary<string, Action<Chat>> Actions { get; }

        static GameRoom()
        {
            var admin = new KeyboardButton[3][];
            admin[0] = new KeyboardButton[] {Strings.NewGame, Strings.RemainingRoles};
            admin[1] = new KeyboardButton[] {Strings.PlayersNumber, Strings.Configuration};
            admin[2] = new KeyboardButton[] {Strings.LeaveRoom, Strings.RoomID};
            MarkupAdmin = new ReplyKeyboardMarkup(admin, true);

            var def = new KeyboardButton[2][];
            def[0] = new KeyboardButton[] { Strings.PlayersNumber, Strings.Configuration };
            def[1] = new KeyboardButton[] { Strings.LeaveRoom, Strings.RoomID };
            MarkupDefault = new ReplyKeyboardMarkup(def, true);

            var conf = new InlineKeyboardButton[2][];
            conf[0] = new InlineKeyboardButton[] { "0", "1", "2", "3", "4" };
            conf[1] = new InlineKeyboardButton[] { "Отмена" };
            MarkupInlineConf = new InlineKeyboardMarkup(conf);
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

        public GameRoom(Chat c, uint id) : this()
        {
            Id = id;
            Admin = c.Id;
            Members = new ObservableCollection<Chat>();
            Members.CollectionChanged += Members_CollectionChanged;
            lock (Members)
            {
                Members.Add(c);
            }
            CurrentConfiguration = new uint[8] { 1, 1, 2, 2, 1, 1, 1, 3 };
            UpdateConfigurationString();
            GreetAdmin(c.Id);

            Console.WriteLine(DateTime.Now + " " + MainMenu.ChatToStr(c) + " cоздал комнату " + Id);
        }

        private void Members_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            lock (PlayersList)
            {
                PlayersList = "*Список игроков:*\n";
                var i = 1;
                foreach (var member in Members)
                {
                    PlayersList += i++ + ". " + member.FirstName + "\n";
                }
            }
        }

        public async void AddMember(Chat c)
        {
            Task<Message> [] tasks = null;
            if (Members.Count > 0)
            {
                tasks = new Task<Message>[Members.Count];
                for (var i = 0; i < Members.Count; i++)
                {
                    tasks[i] = Program.Bot.SendTextMessageAsync(Members[i].Id, "В комнату зашёл игрок " + c.FirstName);
                }
            }
            lock (Members)
            {
                Members.Add(c);
            }

            Console.WriteLine(DateTime.Now + " " + MainMenu.ChatToStr(c) + " вошёл в комнату " + Id);

            if (tasks != null) Task.WaitAll(tasks);
            await Program.Bot.SendTextMessageAsync(c.Id, "Вы вошли в комнату " + Id + ".", false, false, 0,
                c.Id == Admin ? MarkupAdmin : MarkupDefault);
        }

        private int _stage;
        private uint[] _tempConf;
        private int _currentMessageId;

        private readonly AutoResetEvent SomeoneHandlingCallbackQuery = new AutoResetEvent(false);

        private bool _1, _2;

        public async void HandleCallbackQuery(CallbackQuery q)
        {
            if (_currentMessageId != q.Message.MessageId)
            {
                Program.Bot.AnswerCallbackQueryAsync(q.Id, "Это сообщение устарело и не может быть использовано.");
                return;
            }

            if (SomeoneHandlingCallbackQuery.WaitOne(0))
            {
                var chatId = q.Message.Chat.Id;

                switch (q.Data)
                {
                    case "Задать":
                        if (!_1)
                        {
                            _1 = true;
                            _2 = false;

                            _stage = 1;
                            _tempConf = new uint[8];
                            await Program.Bot.EditMessageTextAsync(chatId, q.Message.MessageId,
                                "Выберите количество карт *" + (Role) _stage++ + "*",
                                ParseMode.Markdown, false, MarkupInlineConf);
                        }
                        SomeoneHandlingCallbackQuery.Set();
                        return;
                    case "Отмена":
                        if (!_2)
                        {
                            _2 = true;

                            await Program.Bot.AnswerCallbackQueryAsync(q.Id, "Изменения отменены.");
                            await Program.Bot.EditMessageTextAsync(chatId, q.Message.MessageId,
                                CurrentConfigurationString,
                                ParseMode.Markdown, false,
                                new InlineKeyboardMarkup(new InlineKeyboardButton[] {"Задать"}));

                            _1 = false;
                        }
                        SomeoneHandlingCallbackQuery.Set();
                        return;
                    case "Сохранить":
                        if (!_2)
                        {
                            _2 = true;

                            for (var i = 1; i < 8; i++)
                            {
                                CurrentConfiguration[i] = _tempConf[i];
                            }
                            UpdateConfigurationString();
                            await Program.Bot.AnswerCallbackQueryAsync(q.Id, "Изменения сохранены.");
                            await Program.Bot.EditMessageTextAsync(chatId, q.Message.MessageId,
                                CurrentConfigurationString,
                                ParseMode.Markdown, false,
                                new InlineKeyboardMarkup(new InlineKeyboardButton[] {"Задать"}));

                            _1 = false;
                        }
                        SomeoneHandlingCallbackQuery.Set();
                        return;
                }

                if (_stage < 8)
                {
                    _tempConf[_stage - 1] = Convert.ToUInt32(q.Data);
                    await Program.Bot.EditMessageTextAsync(chatId, q.Message.MessageId,
                        "Выберите количество карт *" + (Role) _stage++ + "*",
                        ParseMode.Markdown, false, MarkupInlineConf);
                }
                else if (_stage == 8)
                {
                    _stage++; // чтобы сюда второй раз не попасть
                    _tempConf[7] = Convert.ToUInt32(q.Data);
                    var resultingConf = "*Введенная конфигурация:*\n";
                    for (var i = 1; i < 8; i++)
                    {
                        resultingConf += ((Role) i) + ": " + _tempConf[i] + "\n";
                    }
                    var mkp = new InlineKeyboardMarkup(new InlineKeyboardButton[] {"Сохранить", "Отмена"});
                    await Program.Bot.EditMessageTextAsync(chatId, q.Message.MessageId, resultingConf,
                        ParseMode.Markdown,
                        false, mkp);
                }

                SomeoneHandlingCallbackQuery.Set();
            }
            else
            {
                Program.Bot.AnswerCallbackQueryAsync(q.Id);
            }
        }

        private async void RemainingRoles(Chat c)
        {
            if (c.Id != Admin || remainingRoles == null) return;

            if (remainingRoles.Count == 0)
            {
                await Program.Bot.SendTextMessageAsync(c.Id, "Оставшихся ролей нет.", replyMarkup: MarkupAdmin);
            }
            else
            {
                var list = remainingRoles.Aggregate("Оставшиеся роли:\n", (current, role) => current + ("*" + role + "*\n"));
                await Program.Bot.SendTextMessageAsync(c.Id, list, replyMarkup: MarkupAdmin,
                    parseMode: ParseMode.Markdown);
            }
        }

        private async void ShowMeRole(Chat c)
        {
            if (roles == null) return;

            await Program.Bot.SendTextMessageAsync(c.Id, "*" + roles[c.Id] + "*",
                replyMarkup: c.Id == Admin ? MarkupAdmin : MarkupDefault, parseMode: ParseMode.Markdown);
        }

        private async void RoomID(Chat c)
        {
            await Program.Bot.SendTextMessageAsync(c.Id, "ID комнаты: " + Id);
        }

        private async void PlayersNumber(Chat c)
        {
            string membersList;
            lock (PlayersList)
            {
                membersList = string.Copy(PlayersList);
            }
            await Program.Bot.SendTextMessageAsync(c.Id, membersList, false, false, 0,
                null, ParseMode.Markdown);
        }

        private async void Configuration(Chat c)
        {
            string buf;
            lock (CurrentConfigurationString)
            {
                buf = string.Copy(CurrentConfigurationString);
            }

            if (c.Id == Admin)
            {
                var x = await Program.Bot.SendTextMessageAsync(c.Id, buf,
                    replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton[] {"Задать"}),
                    parseMode: ParseMode.Markdown);
                _currentMessageId = x.MessageId;
                SomeoneHandlingCallbackQuery.Set();
                _1 = false;
            }
            else
            {
                await Program.Bot.SendTextMessageAsync(c.Id, buf, parseMode: ParseMode.Markdown);
            }
        }

        private Dictionary<long, Role> roles;
        private List<Role> remainingRoles;

        private async void NewGame(Chat c)
        {
            if (c.Id != Admin) return;

            List<Chat> localMembers;

            lock (Members)
            {
                if (Members.Count > CurrentConfiguration.Sum(x => (int) x))
                {
                    Program.Bot.SendTextMessageAsync(c.Id, "Недостаточно карт. Невозможно начать новую игру.");
                    return;
                }
                if (CurrentConfiguration.Sum(x => (int) x) - Members.Count >=
                    CurrentConfiguration.Skip(2).Take(2).Sum(x => (int) x))
                {
                    Program.Bot.SendTextMessageAsync(c.Id,
                        "Количество оставшихся карт больше, чем количество плохих ролей. Невозможно начать новую игру.");
                    return;
                }
                localMembers = new List<Chat>(Members);
            }

            var localMembers2 = new List<Chat>(localMembers);
            var rnd = new Random(DateTime.Now.Millisecond);

            roles = new Dictionary<long, Role>(localMembers.Count);

            var leader = rnd.Next(0, localMembers.Count);
            roles.Add(localMembers[leader].Id, Role.Ведущий);
            localMembers.RemoveAt(leader);

            var rolesBag = new List<Role>();

            for (var i = 1; i < CurrentConfiguration.Length; i++)
            {
                for (var j = 0; j < CurrentConfiguration[i]; j++)
                {
                    rolesBag.Add((Role)i);
                }
            }

            while (localMembers.Count > 0)
            {
                var x = rnd.Next(0, rolesBag.Count);
                roles.Add(localMembers[0].Id, rolesBag[x]);
                localMembers.RemoveAt(0);
                rolesBag.RemoveAt(x);
            }

            remainingRoles = rolesBag;

            var markup = new ReplyKeyboardMarkup(new KeyboardButton[] {Strings.ShowMeRole}, true);

            foreach (var chat in localMembers2)
            {
                Program.Bot.SendTextMessageAsync(chat.Id, "Новая игра началась!",
                    replyMarkup: markup);
            }
        }

        private void LeaveRoom(Chat c)
        {
            MainMenu.LeaveRoom(c);
            lock (Members)
            {
                var i = Members.IndexOf(Members.First(x => c.Id == x.Id)); // ???
                Members.RemoveAt(i);
            }

            if (Members.Count > 0)
            {
                var tasks = new Task<Message>[Members.Count];
                for (var i = 0; i < Members.Count; i++)
                {
                    tasks[i] = Program.Bot.SendTextMessageAsync(Members[i].Id, "Игрок " + c.FirstName + " вышел из комнаты.");
                }
                Task.WaitAll(tasks);
            }
        }

        private void UpdateConfigurationString()
        {
            CurrentConfigurationString = "*Текущая конфигурация:*\n";
            for (var i = 0; i < CurrentConfiguration.Length; i++)
            {
                CurrentConfigurationString += (Role)i + ": " + CurrentConfiguration[i] + "\n";
            }
        }

        private async void GreetAdmin(long id)
        {
            await Program.Bot.SendTextMessageAsync(id, "Теперь вы *администратор* комнаты " + Id + ". Вы можете менять конфигурацию и начинать игру.",
                false, false, 0, MarkupAdmin, ParseMode.Markdown);
        }

        public void HandleMessage(Message msg)
        {
            try
            {
                if (Actions.ContainsKey(msg.Text)) Actions[msg.Text](msg.Chat);
            }
            catch (Exception e)
            {
                Console.WriteLine("GR\n" + e);
            }
        }

        //******************************************************
    }
}

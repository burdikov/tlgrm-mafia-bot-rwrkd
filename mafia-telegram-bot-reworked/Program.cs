using System;
using Telegram.Bot;

namespace mafia_telegram_bot_reworked
{
    class Program
    {
        public static TelegramBotClient Bot { get; private set; }

        public static int ExceptionCounter = 1;

        static void Main(string[] args)
        {
            Bot = new TelegramBotClient(Strings.Token);
            Bot.OnMessage += Bot_OnMessage;
            Bot.OnCallbackQuery += Bot_OnCallbackQuery;
            Bot.StartReceiving();

            Console.WriteLine("Бот запущен.");

            while (true) { }
        }

        private static void Bot_OnCallbackQuery(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
        {
            MainMenu.HandleCallbackQuery(e.CallbackQuery);
        }

        private static void Bot_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            MainMenu.HandleMessage(e.Message);
        }
    }
}

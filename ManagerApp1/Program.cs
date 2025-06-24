using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TaskManagerApp
{
    enum Приоритет { Низкий, Средний, Высокий }
    enum Статус { НеНачата, ВПроцессе, Завершена }

    record Задача(int Id, string Название, string Описание, Приоритет Приоритет, Статус Статус, string Пользователь)
    {
        public static Задача ИзСтроки(string строка)
        {
            var части = строка.Split('|');
            return new Задача(
                int.Parse(части[0]), части[1], части[2], 
                Enum.Parse<Приоритет>(части[3]), 
                Enum.Parse<Статус>(части[4]), 
                части[5]);
        }
        public override string ToString() => $"{Id}|{Название}|{Описание}|{Приоритет}|{Статус}|{Пользователь}";
    }

    record Пользователь(string Логин, string Пароль)
    {
        public static Пользователь ИзСтроки(string строка) => new(строка.Split('|')[0], строка.Split('|')[1]);
        public override string ToString() => $"{Логин}|{Пароль}";
    }

    class Хранилище
    {
        const string ФайлПользователей = "users.txt", ФайлЗадач = "tasks.txt";

        public async Task<List<Пользователь>> ЗагрузитьПользователей() => 
            File.Exists(ФайлПользователей) ? 
            (await File.ReadAllLinesAsync(ФайлПользователей)).Select(Пользователь.ИзСтроки).ToList() : 
            new();

        public async Task СохранитьПользователей(List<Пользователь> пользователи) => 
            await File.WriteAllLinesAsync(ФайлПользователей, пользователи.Select(u => u.ToString()));

        public async Task<List<Задача>> ЗагрузитьЗадачи() => 
            File.Exists(ФайлЗадач) ? 
            (await File.ReadAllLinesAsync(ФайлЗадач)).Select(Задача.ИзСтроки).ToList() : 
            new();

        public async Task СохранитьЗадачи(List<Задача> задачи) => 
            await File.WriteAllLinesAsync(ФайлЗадач, задачи.Select(t => t.ToString()));
    }

    class Авторизация(Хранилище хранилище)
    {
        public string ТекущийПользователь { get; private set; }

        public async Task<bool> Зарегистрировать(string логин, string пароль)
        {
            var пользователи = await хранилище.ЗагрузитьПользователей();
            if (пользователи.Any(u => u.Логин == логин)) return false;
            
            пользователи.Add(new Пользователь(логин, пароль));
            await хранилище.СохранитьПользователей(пользователи);
            return true;
        }

        public async Task<bool> Войти(string логин, string пароль)
        {
            var пользователи = await хранилище.ЗагрузитьПользователей();
            if (пользователи.FirstOrDefault(u => u.Логин == логин && u.Пароль == пароль) is { } пользователь)
            {
                ТекущийПользователь = пользователь.Логин;
                return true;
            }
            return false;
        }

        public void Выйти() => ТекущийПользователь = null;
    }

    class МенеджерЗадач(Хранилище хранилище, Авторизация авторизация)
    {
        public async Task ДобавитьЗадачу(string название, string описание, Приоритет приоритет)
        {
            var задачи = await хранилище.ЗагрузитьЗадачи();
            задачи.Add(new Задача(
                задачи.Any() ? задачи.Max(t => t.Id) + 1 : 1, 
                название, описание, приоритет, Статус.НеНачата, 
                авторизация.ТекущийПользователь));
            await хранилище.СохранитьЗадачи(задачи);
        }

        public async Task<List<Задача>> ПолучитьЗадачиПользователя() => 
            (await хранилище.ЗагрузитьЗадачи()).Where(t => t.Пользователь == авторизация.ТекущийПользователь).ToList();

        public async Task<bool> ОбновитьЗадачу(int id, string название, string описание, Приоритет? приоритет, Статус? статус)
        {
            var задачи = await хранилище.ЗагрузитьЗадачи();
            if (задачи.FirstOrDefault(t => t.Id == id && t.Пользователь == авторизация.ТекущийПользователь) is not { } задача) 
                return false;
            
            задачи.Remove(задача);
            задачи.Add(задача with 
            { 
                Название = string.IsNullOrEmpty(название) ? задача.Название : название,
                Описание = string.IsNullOrEmpty(описание) ? задача.Описание : описание,
                Приоритет = приоритет ?? задача.Приоритет,
                Статус = статус ?? задача.Статус
            });
            await хранилище.СохранитьЗадачи(задачи);
            return true;
        }

        public async Task<bool> УдалитьЗадачу(int id)
        {
            var задачи = await хранилище.ЗагрузитьЗадачи();
            if (задачи.FirstOrDefault(t => t.Id == id && t.Пользователь == авторизация.ТекущийПользователь) is not { } задача) 
                return false;
            
            задачи.Remove(задача);
            await хранилище.СохранитьЗадачи(задачи);
            return true;
        }
    }

    class Интерфейс(Авторизация авторизация, МенеджерЗадач задачи)
    {
        public async Task Запустить()
        {
            Console.WriteLine("Менеджер задач");
            while (true)
            {
                if (авторизация.ТекущийПользователь == null) await МенюАвторизации();
                else await ОсновноеМеню();
            }
        }

        async Task МенюАвторизации()
        {
            Console.WriteLine("\n1. Вход\n2. Регистрация\n3. Выход");
            switch (Console.ReadLine())
            {
                case "1":
                    Console.Write("Логин: ");
                    var логин = Console.ReadLine();
                    Console.Write("Пароль: ");
                    Console.WriteLine(await авторизация.Войти(логин, Console.ReadLine()) 
                        ? "Успешный вход!" : "Ошибка входа");
                    break;
                case "2":
                    Console.Write("Новый логин: ");
                    var новыйЛогин = Console.ReadLine();
                    Console.Write("Пароль: ");
                    Console.WriteLine(await авторизация.Зарегистрировать(новыйЛогин, Console.ReadLine()) 
                        ? "Успешная регистрация!" : "Логин занят");
                    break;
                case "3": Environment.Exit(0); break;
            }
        }

        async Task ОсновноеМеню()
        {
            Console.WriteLine($"\nПользователь: {авторизация.ТекущийПользователь}");
            Console.WriteLine("1. Мои задачи\n2. Добавить\n3. Редактировать\n4. Удалить\n5. Выйти");
            switch (Console.ReadLine())
            {
                case "1": await ПоказатьЗадачи(); break;
                case "2": await ДобавитьЗадачу(); break;
                case "3": await РедактироватьЗадачу(); break;
                case "4": await УдалитьЗадачу(); break;
                case "5": авторизация.Выйти(); break;
            }
        }

        async Task ПоказатьЗадачи()
        {
            var задачиПользователя = await задачи.ПолучитьЗадачиПользователя();
            Console.WriteLine(задачиПользователя.Any() 
                ? string.Join("\n", задачиПользователя.Select(t => 
                    $"ID: {t.Id}\nНазвание: {t.Название}\nОписание: {t.Описание}\nПриоритет: {t.Приоритет}\nСтатус: {t.Статус}\n"))
                : "Задач нет");
        }

        async Task ДобавитьЗадачу()
        {
            Console.Write("Название: ");
            var название = Console.ReadLine();
            Console.Write("Описание: ");
            var описание = Console.ReadLine();
            Console.Write("Приоритет (1-Низкий,2-Средний,3-Высокий): ");
            var приоритет = Console.ReadLine() switch { "1" => Приоритет.Низкий, "2" => Приоритет.Средний, _ => Приоритет.Высокий };
            await задачи.ДобавитьЗадачу(название, описание, приоритет);
            Console.WriteLine("Задача добавлена!");
        }

        async Task РедактироватьЗадачу()
        {
            await ПоказатьЗадачи();
            Console.Write("ID задачи: ");
            if (!int.TryParse(Console.ReadLine(), out var id)) return;

            Console.Write("Новое название (Enter - оставить): ");
            var название = Console.ReadLine();
            Console.Write("Новое описание (Enter - оставить): ");
            var описание = Console.ReadLine();

            Приоритет? приоритет = null;
            Console.Write("Изменить приоритет? (y/n): ");
            if (Console.ReadLine()?.ToLower() == "y")
            {
                Console.Write("Приоритет (1-3): ");
                приоритет = Console.ReadLine() switch { "1" => Приоритет.Низкий, "2" => Приоритет.Средний, _ => Приоритет.Высокий };
            }

            Статус? статус = null;
            Console.Write("Изменить статус? (y/n): ");
            if (Console.ReadLine()?.ToLower() == "y")
            {
                Console.Write("Статус (1-Не начата,2-В процессе,3-Завершена): ");
                статус = Console.ReadLine() switch { "1" => Статус.НеНачата, "2" => Статус.ВПроцессе, _ => Статус.Завершена };
            }

            Console.WriteLine(await задачи.ОбновитьЗадачу(id, название, описание, приоритет, статус) 
                ? "Обновлено!" : "Ошибка!");
        }

        async Task УдалитьЗадачу()
        {
            await ПоказатьЗадачи();
            Console.Write("ID задачи: ");
            if (int.TryParse(Console.ReadLine(), out var id))
                Console.WriteLine(await задачи.УдалитьЗадачу(id) ? "Удалено!" : "Ошибка!");
        }
    }

    class Program
    {
        static async Task Main()
        {
            var хранилище = new Хранилище();
            var авторизация = new Авторизация(хранилище);
            await new Интерфейс(авторизация, new МенеджерЗадач(хранилище, авторизация)).Запустить();
        }
    }
}
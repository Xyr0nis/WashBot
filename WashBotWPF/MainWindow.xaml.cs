using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Linq;

namespace WashBotWPF
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer timer;
        private IWebDriver? driver;
        private bool siteOpened = false;
        private bool bookingExecuted = false;
        private int retryCount = 0;
        private const int MAX_RETRIES = 3;
        private Config currentConfig;
        private readonly string configPath;

        public MainWindow()
        {
            InitializeComponent();

            configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WashBot",
                "config.json");

            // Инициализация комбобоксов
            FirstMachineComboBox.ItemsSource = new[] { "Машинка №1", "Машинка №2", "Машинка №3", "Машинка №4" };
            SecondMachineComboBox.ItemsSource = new[] { "Машинка №1", "Машинка №2", "Машинка №3", "Машинка №4" };

            var intervals = new[]
            {
                "8:00-9:30", "9:30-11:00", "11:00-12:30", "12:30-14:00",
                "14:00-15:30", "15:30-17:00", "17:00-18:30", "18:30-20:00",
                "20:00-21:30", "21:30-23:00"
            };

            FirstIntervalComboBox.ItemsSource = intervals;
            SecondIntervalComboBox.ItemsSource = intervals;

            // Инициализация таймера
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += Timer_Tick;

            LoadConfig();
            UpdateNextRunInfo();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    currentConfig = JsonSerializer.Deserialize<Config>(json) ?? new Config();
                }
                else
                {
                    currentConfig = new Config
                    {
                        SiteOpenTime = "07:20:00",
                        BookingTime = "07:30:00"
                    };
                }

                SetupAutostart();
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка загрузки конфигурации: {ex.Message}", true);
                currentConfig = new Config
                {
                    SiteOpenTime = "07:20:00",
                    BookingTime = "07:30:00"
                };
            }
        }

        private void SaveConfig()
        {
            try
            {
                if (!currentConfig.SaveCredentials)
                {
                    var tempConfig = new Config
                    {
                        SiteOpenTime = currentConfig.SiteOpenTime,
                        BookingTime = currentConfig.BookingTime,
                        AutoStart = currentConfig.AutoStart,
                        SaveCredentials = false
                    };
                    var json = JsonSerializer.Serialize(tempConfig);
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                    File.WriteAllText(configPath, json);
                }
                else
                {
                    var json = JsonSerializer.Serialize(currentConfig);
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                    File.WriteAllText(configPath, json);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка сохранения конфигурации: {ex.Message}", true);
            }
        }

        private void UpdateNextRunInfo()
        {
            try
            {
                var now = DateTime.Now;
                var openTime = TimeSpan.Parse(currentConfig.SiteOpenTime);
                var bookingTime = TimeSpan.Parse(currentConfig.BookingTime);

                DateTime nextRun;
                if (currentConfig.BookingMode == BookingMode.ScheduledBooking)
                {
                    // Находим следующий запланированный день
                    nextRun = now;
                    while (!currentConfig.ScheduledDays.Contains(nextRun.DayOfWeek))
                    {
                        nextRun = nextRun.AddDays(1);
                    }
                    nextRun = nextRun.Date.Add(bookingTime);
                }
                else
                {
                    nextRun = now.Date.Add(bookingTime);
                    if (now.TimeOfDay >= bookingTime)
                    {
                        nextRun = nextRun.AddDays(1);
                    }
                }

                NextRunInfoText.Text = $"Следующий запуск: {nextRun:dd.MM.yyyy HH:mm:ss}\n" +
                                     $"Открытие сайта: {currentConfig.SiteOpenTime}\n" +
                                     $"Выполнение записи: {currentConfig.BookingTime}\n" +
                                     $"Режим: {(currentConfig.BookingMode == BookingMode.SingleBooking ? "Однократная запись" : "По расписанию")}";
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка обновления информации о следующем запуске: {ex.Message}", true);
            }
        }

        private void LogMessage(string message, bool isError = false)
        {
            var status = new BookingStatus(message, isError);
            StatusText.Text = $"[{status.LastUpdateTime:HH:mm:ss}] {status.Status}\n" + StatusText.Text;

            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WashBot",
                    "logs.txt");

                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath,
                    $"[{status.LastUpdateTime:yyyy-MM-dd HH:mm:ss}] {status.Status}{Environment.NewLine}");
            }
            catch
            {
                // Игнорируем ошибки записи лога
            }

            if (isError)
            {
                MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeDriver()
        {
            try
            {
                var options = new ChromeOptions();
                options.AddArgument("--start-maximized");
                options.AddArgument("--disable-notifications");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");
                options.AddArgument("--remote-allow-origins=*");

                var service = ChromeDriverService.CreateDefaultService();
                service.HideCommandPromptWindow = true;

                driver = new ChromeDriver(service, options);
                driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

                LogMessage("ChromeDriver успешно инициализирован");
            }
            catch (Exception ex)
            {
                LogMessage(
                    "Ошибка запуска браузера. Убедитесь что:\n" +
                    "1. Google Chrome установлен\n" +
                    "2. Антивирус не блокирует работу бота\n" +
                    $"Техническая информация: {ex.Message}",
                    true);
                throw;
            }
        }

        private async Task OpenSiteAndLogin()
        {
            try
            {
                InitializeDriver();

                LogMessage("Открываю сайт...");
                driver.Navigate().GoToUrl("https://w5.togudv.ru/");

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

                // Используем точные селекторы
                var loginField = wait.Until(d => d.FindElement(By.Id("loginName")));
                var passwordField = wait.Until(d => d.FindElement(By.Id("loginPassword")));
                var loginButton = wait.Until(d => d.FindElement(
                    By.XPath("/html/body/div[1]/div/div/div/div/div/div/div/form/button")));

                LogMessage("Ввожу учетные данные...");
                loginField.SendKeys(currentConfig.Login);
                passwordField.SendKeys(currentConfig.Password);

                LogMessage("Нажимаю кнопку входа...");
                loginButton.Click();

                // Проверяем успешность входа
                wait.Until(d => d.Url != "https://w5.togudv.ru/");

                LogMessage("Вход выполнен успешно!");
            }
            catch (WebDriverTimeoutException)
            {
                LogMessage("Превышено время ожидания. Проверьте подключение к интернету.", true);
                throw;
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка входа: {ex.Message}", true);
                throw;
            }
        }

        private async Task ExecuteBooking()
        {
            try
            {
                if (driver == null)
                {
                    throw new Exception("Браузер не инициализирован");
                }

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));

                // Получаем индексы выбранных машин и интервалов
                var firstMachineIndex = FirstMachineComboBox.SelectedIndex;
                var firstIntervalIndex = FirstIntervalComboBox.SelectedIndex;
                var secondMachineIndex = SecondMachineComboBox.SelectedIndex;
                var secondIntervalIndex = SecondIntervalComboBox.SelectedIndex;

                // Формируем ID слотов
                var firstSlotId = $"i{firstMachineIndex}{firstIntervalIndex}";
                var secondSlotId = $"i{secondMachineIndex}{secondIntervalIndex}";

                LogMessage($"Пытаюсь забронировать слоты {firstSlotId} и {secondSlotId}");

                // Находим и кликаем по слотам
                var timeSlot1 = wait.Until(d => d.FindElement(By.Id(firstSlotId)));
                timeSlot1.Click();

                var timeSlot2 = wait.Until(d => d.FindElement(By.Id(secondSlotId)));
                timeSlot2.Click();

                // Проверка успешности записи
                var success = wait.Until(d => {
                    try
                    {
                        var slot1 = d.FindElement(By.Id(firstSlotId));
                        var slot2 = d.FindElement(By.Id(secondSlotId));
                        return !slot1.Enabled && !slot2.Enabled;
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (!success)
                {
                    throw new Exception("Не удалось подтвердить успешность записи");
                }

                LogMessage("Запись успешно выполнена!");
                MessageBox.Show("Запись успешно выполнена!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка записи: {ex.Message}", true);
                throw;
            }
        }

        private async Task RetryOperation(Func<Task> operation, string operationName)
        {
            while (retryCount < MAX_RETRIES)
            {
                try
                {
                    await operation();
                    retryCount = 0;
                    return;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    LogMessage($"Попытка {retryCount} из {MAX_RETRIES} для {operationName} не удалась: {ex.Message}");

                    if (retryCount >= MAX_RETRIES)
                    {
                        LogMessage(
                            $"Не удалось выполнить {operationName} после {MAX_RETRIES} попыток.\n" +
                            "Проверьте подключение к интернету и доступность сайта.",
                            true);
                        throw;
                    }

                    await Task.Delay(2000 * retryCount);
                }
            }
        }

        private TimeSpan GetAdjustedTime(TimeSpan originalTime)
        {
            return originalTime.Subtract(TimeSpan.FromSeconds(2));
        }

        private async Task EnsureReadyForBooking()
        {
            if (!driver?.Url.Contains("w5.togudv.ru") ?? true)
            {
                await RetryOperation(OpenSiteAndLogin, "подготовка к записи");
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var currentTime = DateTime.Now;
            var currentTimeSpan = currentTime.TimeOfDay;

            try
            {
                TimeSpan siteOpenTime = TimeSpan.Parse(currentConfig.SiteOpenTime);
                TimeSpan bookingTime = TimeSpan.Parse(currentConfig.BookingTime);

                var adjustedSiteOpenTime = GetAdjustedTime(siteOpenTime);
                var adjustedBookingTime = GetAdjustedTime(bookingTime);

                StatusText.Text = $"Текущее время: {currentTime:HH:mm:ss}\n";

                // Проверяем режим работы
                if (currentConfig.BookingMode == BookingMode.ScheduledBooking)
                {
                    // Если сегодняшний день не в расписании, пропускаем
                    if (!currentConfig.ScheduledDays.Contains(currentTime.DayOfWeek))
                    {
                        StatusText.Text += "Сегодня запись не запланирована\n";
                        return;
                    }
                }
                else if (bookingExecuted) // Для режима однократной записи
                {
                    StatusText.Text += "Запись уже выполнена\n";
                    return;
                }

                if (!siteOpened && currentTimeSpan >= adjustedSiteOpenTime && currentTimeSpan < bookingTime)
                {
                    LogMessage("Открываю сайт и выполняю вход...");
                    _ = EnsureReadyForBooking();
                    siteOpened = true;
                }
                else if (siteOpened && !bookingExecuted && currentTimeSpan >= adjustedBookingTime)
                {
                    LogMessage("Выполняю запись...");
                    _ = ExecuteBooking();
                    bookingExecuted = true;

                    // Для режима по расписанию сбрасываем флаги в конце дня
                    if (currentConfig.BookingMode == BookingMode.ScheduledBooking)
                    {
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(TimeSpan.FromHours(1)); // Ждем час после записи
                            await Dispatcher.InvokeAsync(() =>
                            {
                                siteOpened = false;
                                bookingExecuted = false;
                                LogMessage("Подготовка к следующему дню записи");
                            });
                        });
                    }
                }
                else if (!siteOpened)
                {
                    var timeUntilOpen = siteOpenTime - currentTimeSpan;
                    if (timeUntilOpen > TimeSpan.Zero)
                    {
                        StatusText.Text += $"До открытия сайта: {timeUntilOpen:hh\\:mm\\:ss}\n";
                    }
                }
                else if (siteOpened && !bookingExecuted)
                {
                    var timeUntilBooking = bookingTime - currentTimeSpan;
                    if (timeUntilBooking > TimeSpan.Zero)
                    {
                        StatusText.Text += $"До записи: {timeUntilBooking:hh\\:mm\\:ss}\n";
                    }
                }

                // Добавляем информацию о режиме работы
                StatusText.Text += $"\nРежим работы: {(currentConfig.BookingMode == BookingMode.SingleBooking ? "Однократная запись" : "По расписанию")}\n";
                if (currentConfig.BookingMode == BookingMode.ScheduledBooking)
                {
                    StatusText.Text += "Дни записи: " + string.Join(", ",
                        currentConfig.ScheduledDays.Select(d => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(d))) + "\n";
                }
            }
            catch (Exception ex)
            {
                HandleCriticalError(ex);
            }
        }

        private void HandleCriticalError(Exception ex)
        {
            LogMessage($"Критическая ошибка: {ex.Message}", true);
            CleanupDriver();
            timer.Stop();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void CleanupDriver()
        {
            try
            {
                if (driver != null)
                {
                    driver.Quit();
                    Process[] chromeDriverProcesses = Process.GetProcessesByName("chromedriver");
                    foreach (var process in chromeDriverProcesses)
                    {
                        try { process.Kill(); } catch { }
                    }
                }
            }
            catch { }
            finally
            {
                driver = null;
            }
        }

        private void SetupAutostart()
        {
            try
            {
                var startupPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "WashBot.lnk");

                if (currentConfig.AutoStart)
                {
                    if (!File.Exists(startupPath))
                    {
                        var shell = new IWshRuntimeLibrary.WshShell();
                        var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(startupPath);
                        shortcut.TargetPath = Process.GetCurrentProcess().MainModule?.FileName;
                        shortcut.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                        shortcut.Description = "WashBot Autostart";
                        shortcut.Save();
                    }
                }
                else
                {
                    if (File.Exists(startupPath))
                    {
                        File.Delete(startupPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage(
                    "Не удалось настроить автозапуск. Попробуйте запустить программу от имени администратора.\n" +
                    $"Техническая информация: {ex.Message}",
                    true);
            }
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(currentConfig.Login) ||
                string.IsNullOrWhiteSpace(currentConfig.Password))
            {
                MessageBox.Show("Введите логин и пароль в настройках!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (FirstMachineComboBox.SelectedItem == null ||
                FirstIntervalComboBox.SelectedItem == null ||
                SecondMachineComboBox.SelectedItem == null ||
                SecondIntervalComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите машинки и интервалы времени!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return ValidateTimeSettings();
        }

        private bool ValidateTimeSettings()
        {
            try
            {
                var openTime = TimeSpan.Parse(currentConfig.SiteOpenTime);
                var bookingTime = TimeSpan.Parse(currentConfig.BookingTime);

                if (bookingTime <= openTime)
                {
                    throw new Exception("Время записи должно быть позже времени открытия сайта");
                }

                if (bookingTime - openTime < TimeSpan.FromSeconds(5))
                {
                    throw new Exception("Интервал между открытием и записью должен быть не менее 5 секунд");
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка в настройках времени: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private async Task PerformPreCheck()
        {
            try
            {
                await OpenSiteAndLogin();

                // Проверяем доступность элементов интерфейса
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                var elements = new List<string>();

                // Генерируем все возможные ID
                for (int machine = 0; machine < 4; machine++)
                {
                    for (int interval = 0; interval < 10; interval++)
                    {
                        elements.Add($"i{machine}{interval}");
                    }
                }

                foreach (var id in elements)
                {
                    try
                    {
                        wait.Until(d => d.FindElement(By.Id(id)));
                    }
                    catch
                    {
                        throw new Exception($"Элемент {id} не найден. Возможно, структура сайта изменилась.");
                    }
                }

                MessageBox.Show("Предварительная проверка успешна",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Предварительная проверка не удалась: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                CleanupDriver();
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
                return;

            SaveConfig();
            SetupAutostart();

            siteOpened = false;
            bookingExecuted = false;
            retryCount = 0;
            timer.Start();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            LogMessage("Бот запущен...");
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            timer.Stop();
            CleanupDriver();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            siteOpened = false;
            bookingExecuted = false;
            LogMessage("Бот остановлен.");
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(currentConfig);
            settingsWindow.Owner = this;
            if (settingsWindow.ShowDialog() == true)
            {
                currentConfig = settingsWindow.CurrentConfig;
                SaveConfig();
                SetupAutostart();
                UpdateNextRunInfo();
            }
        }

        private async void PreCheckMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await PerformPreCheck();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "WashBot - Система автоматической записи на стирку\n" +
                "Версия 1.0.0\n\n" +
                "Создано для автоматизации процесса записи на стирку.\n" +
                "Разработчик: Xyr0nis\n" +
                "© 2024 WashBot",
                "О программе",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (timer.IsEnabled)
            {
                var result = MessageBox.Show(
                    "Программа активна. Вы уверены, что хотите закрыть её?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
            CleanupDriver();
        }
    }
}
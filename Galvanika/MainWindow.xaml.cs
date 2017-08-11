using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading;
using System.Windows.Documents;

namespace Galvanika
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Переменные
        public List<MemoryData> MemoryGridTable = new List<MemoryData>();
        public List<ProgramData> DataGridTable = new List<ProgramData>();
        public List<MyTimers> TimerGridTable = new List<MyTimers>();

        //124,126,93,1 - Исходное положение новое, 125,126,173,2 - старое.
        public List<int> InputData = new List<int>() { 0, 0, 0, 0 };
        //public List<int> MarkerData = new List<int>() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public List<int> MarkerData = Enumerable.Repeat(0, 20).ToList();
        public List<int> OutputData = new List<int>() { 0, 0, 0 };

        public Dictionary<string, string> DB = new Dictionary<string, string>();
        public Dictionary<int, int> StartEnd = new Dictionary<int, int>();

        public Dictionary<string, int> TimerSE = new Dictionary<string, int>();
        public Dictionary<string, int> TimerSA = new Dictionary<string, int>();

        public Dictionary<string, int> FrontP = new Dictionary<string, int>();
        public Dictionary<string, int> FrontN = new Dictionary<string, int>();

        public BackgroundWorker backgroundWorker = new BackgroundWorker();

        public Dictionary<string, int> Stek = new Dictionary<string, int>();
        public int GlobalCikl = 0;
        public int ErrorOP1 = 0;
        public int ErrorOP2 = 0;
        public string LogPath = "Log.txt";

        RSH rsh = new RSH();
        #endregion
        #region Отправка выражения в парсер
        private bool Parse(string value)
        {
            var tokens = new Tokenizer(value).Tokenize();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
        #endregion
        #region Чтение файла с программой
        private string Path;
        private List<string> tempDB;
        private List<string> tempProgramList;
        private bool ReadFileDB()
        {
            if (!File.Exists(Path))
                return false;

            tempDB = new List<string>();
            tempProgramList = new List<string>();

            using (StreamReader fs = new StreamReader(Path, Encoding.Default))
            {
                int start = 0;
                while (true)
                {
                    string temp = fs.ReadLine();
                    if (temp == null) break;
                    if (temp.Contains("END_STRUCT"))
                        break;

                    if (start == 1)
                        tempDB.Add(temp);

                    if (temp.Contains("STRUCT") && start != 1)
                        start = 1;
                }
                ParseDB();
                start = 0;
                while (true)
                {
                    string temp = fs.ReadLine();
                    if (temp == null) break;
                    if (temp.Contains("FUNCTION FC") && start != 1)
                        start = 1;
                    if (start == 1 && !temp.Contains("NOP"))
                        tempProgramList.Add(temp);
                    if (temp.Contains(": NOP"))
                        tempProgramList.Add(temp);
                }
            }
            FillGrid();
            return true;
        }
        private void ParseDB()
        {
            foreach (var item in tempDB)
            {
                if (string.IsNullOrEmpty(item))
                    break;
                var itemNew = item;
                if (item.Contains("//"))
                    itemNew = item.Substring(0, item.IndexOf('/'));
                var tempFirstString = itemNew.Split('_');
                var tempSecondString = tempFirstString[1].Split('i');
                string tempIndex = "";
                if (tempSecondString.Count() > 1)
                    tempIndex = tempSecondString[0] + "." + tempSecondString[1];
                else
                    tempIndex = tempSecondString[0];
                var tempThirdString = tempFirstString[tempFirstString.Count() - 1].Split('=');
                if (tempThirdString.Count() > 1)
                {
                    var endOfString = tempThirdString[1].Trim();

                    if (endOfString.Contains(';'))
                        endOfString = endOfString.Remove(endOfString.Length - 1, 1);

                    if (DB.ContainsKey(tempIndex.Trim()))
                        DB[tempIndex.Trim()] = endOfString;
                    else
                        DB.Add(tempIndex.Trim(), endOfString);
                }
                else
                {
                    if (tempThirdString[0].Contains("BOOL"))
                    {
                        if (DB.ContainsKey(tempIndex.Trim()))
                            DB[tempIndex.Trim()] = "False";
                        else
                            DB.Add(tempIndex.Trim(), "False");
                    }
                    else
                    {
                        if (DB.ContainsKey(tempIndex.Trim()))
                            DB[tempIndex.Trim()] = "0";
                        else
                            DB.Add(tempIndex.Trim(), "0");
                    }
                }
                var tempString = itemNew.Substring(itemNew.IndexOf('_') + 1); //Дважды удаляем до знака "_"
                tempString = tempString.Substring(tempString.IndexOf('_') + 1);
                var tempNameP = tempString.Split(':');
                MemoryData result = new MemoryData("", "", "", "", "");
                if (tempNameP.Count() > 2)
                {
                    var value = tempNameP[2].Replace('=', ' ');
                    value = value.Replace(';', ' ');
                    if (tempNameP[1].Contains("BOOL"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "bool", value.Trim().ToLower(), value.Trim().ToLower());
                    if (tempNameP[1].Contains("INT"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "integer", value.Trim(), value.Trim());
                    if (tempNameP[1].Contains("TIME"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "timer", value.Trim(), value.Trim());
                    if (tempNameP[0].Contains("Stek") && tempNameP[0].Trim() != "Stek2" && tempNameP[0].Trim() != "Stek1" || tempNameP[0].Contains("TKOP"))
                    {
                        var tempTimerData = value.ToLower().Split('#');
                        string tempTime;
                        int newTempTime;
                        if (tempTimerData[1].Contains("ms"))
                        {
                            tempTime = tempTimerData[1].Replace("ms", "");
                            newTempTime = Convert.ToInt32(tempTime);
                        }
                        else
                        {
                            tempTime = tempTimerData[1].Replace("s", "");
                            newTempTime = Convert.ToInt32(tempTime);
                            newTempTime = newTempTime * 1000;
                        }
                        if (Stek.ContainsKey(tempNameP[0].Trim()))
                            Stek[tempNameP[0].Trim()] = newTempTime;
                        else
                            Stek.Add(tempNameP[0].Trim(), newTempTime);
                    }
                }
                else
                {
                    if (tempNameP[1].Contains("BOOL"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "bool", "false", "false");
                    if (tempNameP[1].Contains("INT"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "integer", "0", "0");
                    if (tempNameP[1].Contains("TIME"))
                        result = new MemoryData(tempIndex, tempNameP[0].Trim(), "timer", "0", "0");
                }
                MemoryGridTable.Add(result);
            }
        }
        private void FillGrid()
        {
            var countKey = 0;
            var countText = 0;
            foreach (string item in tempProgramList) // Загоняем в таблицу данные программы из файла
            {
                try
                {
                    if (item.Contains("NETWORK") || item.Contains("TITLE") || item.Contains("END") || item.Contains("FUNCTION FC") || item.Contains("VERSION") || item.Contains("BEGIN") || item.Contains("AUF   DB"))
                    {
                        var result = new ProgramData(0, item, "", "", "", "", "", "");
                        DataGridTable.Add(result);
                        if (StartEnd.Count != 0) //Разбитие на подпрограммы
                        {
                            var lastStart = StartEnd.Last();
                            if (lastStart.Key == lastStart.Value)
                                StartEnd[lastStart.Key] = countKey + countText - 1;
                        }
                        countText++;
                    }
                    else if (item.Trim().Length != 0)
                    {
                        if (StartEnd.Count != 0) //Разбитие на подпрограммы
                        {
                            var lastStart = StartEnd.Last();
                            if (lastStart.Key != lastStart.Value)
                                StartEnd.Add(countKey + countText, countKey + countText);
                        }
                        else
                            StartEnd.Add(countText, countText);

                        var itemSplit = item.Replace(';', ' ');
                        var stringData = itemSplit.Split(' ').ToList();
                        stringData.RemoveAll(RemoveEmpty);
                        countKey++;
                        if (stringData.Count > 2)
                        {
                            var result = new ProgramData(countKey, item, stringData[0], stringData[1], stringData[2], "", "", "");
                            DataGridTable.Add(result);
                            if (stringData.Contains("FP"))
                                FrontP.Add(countKey.ToString(), 0);
                            if (stringData.Contains("FN"))
                                FrontN.Add(countKey.ToString(), 0);
                        }
                        else if (stringData.Count == 2)
                        {
                            if (stringData.Contains("SPBNB"))
                            {
                                var result = new ProgramData(countKey, item, stringData[0], stringData[1], "", "", "", "");
                                DataGridTable.Add(result);
                            }
                            else if (stringData.Contains("BLD"))
                            {
                                var result = new ProgramData(countKey, item, stringData[0], stringData[1], "", "", "", "");
                                DataGridTable.Add(result);
                            }
                            else if (stringData.Contains("S5T"))
                            {
                                var stringTimer = stringData[1].Split('#');
                                var result = new ProgramData(countKey, item, stringData[0], stringTimer[0], stringTimer[1], "", "", "");
                                DataGridTable.Add(result);
                            }
                            else if (stringData.Contains("L"))
                            {
                                var result = new ProgramData(countKey, item, stringData[0], stringData[1], "", "", "", "");
                                DataGridTable.Add(result);
                            }
                        }
                        else
                        {
                            var result = new ProgramData(countKey, item, stringData[0], "", "", "", "", "");
                            DataGridTable.Add(result);
                        }
                    }
                }
                catch
                {
                }
            }
        }
        private bool RemoveEmpty(String s)
        {
            return s.Length == 0;
        }
        #endregion
        #region Таймер и поток расчета
        private void timer_Tick(object sender, EventArgs e)
        {
            try
            {
                foreach (var item in TimerSE)
                {
                    var value = TimerGridTable.Where(u => u.Address == item.Key).SingleOrDefault();
                    if (value.Time < value.EndTime && value.Value != 1)
                        value.Time += 100;
                    else
                    {
                        value.Value = 1;
                        value.Time = 0;
                    }
                }
                foreach (var item in TimerSA)
                {
                    var value = TimerGridTable.Where(u => u.Address == item.Key).SingleOrDefault();
                    if (value.Time < value.EndTime && value.Time != -1 && value.Value != 0)
                        value.Time += 100;
                    else if (value.Time >= value.EndTime)
                    {

                        value.Value = 0;
                        value.Time = 0;
                        TimerSA.Remove(item.Key);
                    }
                }
            }
            catch
            {
                return;
            }
        }
        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            //if (backgroundWorker.CancellationPending)
            //{
            //    e.Cancel = true;
            //    ResetAll();
            //    return;
            //}
            //else
            if (newProgram != 2)
                Calculate();
        }
        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
            //(ThreadStart)delegate ()
            //{
                ServiceOutput();
            //}
            //);
            //if (!e.Cancelled)
                backgroundWorker.RunWorkerAsync();
        }
        bool timeCikl = false;
        int timee = 0;
        private void timeRefresh(object sender, EventArgs e)
        {
            currentTime.Content = DateTime.Now.ToString("F");
            if (!timeCikl)
                return;
            TimeSpan timeElapse = TimeSpan.FromSeconds(timee);
            timee++;
            currentTimeElapse.Content = string.Format("{0:D2}ч:{1:D2}м:{2:D2}с", timeElapse.Hours, timeElapse.Minutes, timeElapse.Seconds);
        }
        private void timerForVisualDataRefresh_Tick(object sender, EventArgs e)
        {
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    var tempBits = Convert.ToString(InputData[i], 2);
                    while (tempBits.Length < 8)
                        tempBits = tempBits.Insert(0, "0");
                    tempBits = ReverseString(tempBits);
                    for (int j = 0; j <= tempBits.Length; j++)
                    {
                        CheckBox chekbox = tabControl.FindName("Input" + i + "Bit" + j) as CheckBox;
                        if (chekbox != null)
                        {
                            if (tempBits[j] == '1')
                                chekbox.IsChecked = true;
                            else
                                chekbox.IsChecked = false;
                        }
                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    var tempBits = Convert.ToString(OutputData[i], 2);
                    while (tempBits.Length < 8)
                        tempBits = tempBits.Insert(0, "0");
                    tempBits = ReverseString(tempBits);
                    for (int j = 0; j <= tempBits.Length; j++)
                    {
                        CheckBox chekbox = tabControl.FindName("Output" + i + "Bit" + j) as CheckBox;
                        if (chekbox != null)
                        {
                            if (tempBits[j] == '1')
                                chekbox.IsChecked = true;
                            else
                                chekbox.IsChecked = false;
                        }
                    }
                }
                //Обновляем таблицу действий операторов
                zadOp1.Content = DB["46"];
                zadOp2.Content = DB["44"];
                istOp1.Content = DB["52"];
                istOp2.Content = DB["42"];
                rasOp1.Content = DB["50"];
                rasOp2.Content = DB["40"];

                deyOp1.Foreground = new SolidColorBrush(Colors.Lime);
                deyOp2.Foreground = new SolidColorBrush(Colors.Lime);

                if (DB["0.2"].ToLower() == "true")
                    deyOp1.Content = "Исходное";
                else
                    deyOp1.Content = "";

                if (DB["0.5"].ToLower() == "true")
                {
                    deyOp1.Foreground = new SolidColorBrush(Colors.Red);
                    deyOp1.Content = "Ошибка позиции";
                }
                if (DB["1.0"].ToLower() == "true")
                    deyOp1.Content = "Ожидание";
                if (DB["1.1"].ToLower() == "true")
                    deyOp1.Content = "Подъем";
                if (DB["1.2"].ToLower() == "true")
                    deyOp1.Content = "Опускание";
                if (DB["1.5"].ToLower() == "true")
                    deyOp1.Content = "Ход влево";
                if (DB["1.6"].ToLower() == "true")
                    deyOp1.Content = "Ход вправо";
                if (DB["1.7"].ToLower() == "true")
                {
                    deyOp1.Foreground = new SolidColorBrush(Colors.Red);
                    deyOp1.Content = "Ошибка датчиков";
                }
                if (DB["54.4"].ToLower() == "true")
                    deyOp1.Content = "Ожидание загрузки";

                if (DB["0.1"].ToLower() == "true")
                    deyOp2.Content = "Исходное";
                else
                    deyOp2.Content = "";
                if (DB["0.6"].ToLower() == "true")
                {
                    deyOp2.Foreground = new SolidColorBrush(Colors.Red);
                    deyOp2.Content = "Ошибка позиции";
                }
                if (DB["0.7"].ToLower() == "true")
                    deyOp2.Content = "Ожидание";
                if (DB["1.3"].ToLower() == "true")
                    deyOp2.Content = "Подъем";
                if (DB["1.4"].ToLower() == "true")
                    deyOp2.Content = "Опускание";
                if (DB["54.0"].ToLower() == "true")
                    deyOp2.Content = "Ход влево";
                if (DB["54.1"].ToLower() == "true")
                    deyOp2.Content = "Ход вправо";
                if (DB["54.2"].ToLower() == "true")
                {
                    deyOp2.Foreground = new SolidColorBrush(Colors.Red);
                    deyOp2.Content = "Ошибка датчиков";
                }
                if (DB["54.4"].ToLower() == "true")
                    deyOp2.Content = "Ожидание загрузки";

                if (DB["54.6"].ToLower() == "true")
                    deyOp1.Content = "Выдержка";
                if (DB["54.7"].ToLower() == "true")
                    deyOp2.Content = "Выдержка";
            }
            catch
            {
                return;
            }
        }
        #endregion
        public MainWindow()
        {
            InitializeComponent();
            if (File.Exists("!3.AWL"))
            {
                ProgramString.Content = "Выбрана программа 4";
                Path = "4.AWL";
                Program3.IsEnabled = true;
                Program4.IsEnabled = false;
            }
            else
            {
                ProgramString.Content = "Выбрана программа 3";
                Path = "3.AWL";
                Program3.IsEnabled = false;
                Program4.IsEnabled = true;
            }
            AddHandler(Keyboard.KeyDownEvent, (KeyEventHandler)HandleKeyDownEvent);
            //button_Start.Focus();
            var openFile = ReadFileDB();
            if (!openFile)
                return;


            backgroundWorker.DoWork += backgroundWorker_DoWork;
            backgroundWorker.RunWorkerCompleted += backgroundWorker_RunWorkerCompleted;
            backgroundWorker.WorkerSupportsCancellation = true;

            DispatcherTimer timerForTimer = new DispatcherTimer();
            timerForTimer.Tick += new EventHandler(timer_Tick);
            timerForTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timerForTimer.Start();

            //DispatcherTimer timerForInput = new DispatcherTimer();
            //timerForInput.Tick += new EventHandler(timer_Tick_Input);
            //timerForInput.Interval = new TimeSpan(0, 0, 0, 0, 100);
            //timerForInput.Start();

            DispatcherTimer timerForTimeRefresh = new DispatcherTimer();
            timerForTimeRefresh.Tick += new EventHandler(timeRefresh);
            timerForTimeRefresh.Interval = new TimeSpan(0, 0, 0, 1);
            timerForTimeRefresh.Start();

            //DispatcherTimer timerForVisualDataRefresh = new DispatcherTimer();
            //timerForVisualDataRefresh.Tick += new EventHandler(timerForVisualDataRefresh_Tick);
            //timerForVisualDataRefresh.Interval = new TimeSpan(0, 0, 0, 0, 100);
            //timerForVisualDataRefresh.Start();

            var result = rsh.Connect();
            ResetAll(); //Возможно избыточно
            backgroundWorker.RunWorkerAsync();
        }
        #region Расчет
        private void Calculate()
        {
            InputData = rsh.Read();
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
            (ThreadStart)delegate ()
            {
                timerForVisualDataRefresh();
            }
            );

            foreach (var item in StartEnd)
            {
                string output = "";
                string doubleBKT = ""; //переменная для двойной закрывающей скобки
                string Load = ""; //переменная, когда загружен наймер, но для помещения в другое место

                string BLD = ""; //Для фронта BLD

                var compareValues = new List<int>();
                for (int i = item.Key; i <= item.Value; i++)
                {
                    ProgramData value = DataGridTable[i];
                    if (value == null)
                        break;
                    if (value.Operator.Contains("=") && !value.Operator.Contains("I")) //Вывод
                    {
                        if (!string.IsNullOrEmpty(doubleBKT))
                        {
                            output += doubleBKT;
                            doubleBKT = "";
                        }

                        if (string.IsNullOrEmpty(output))
                            output = "false";

                        DataWrite(value, output);
                    }
                    else //Cчитываем дальше
                    {
                        //if ( value.Code.Contains("S     M      9.1"))
                        //{ }
                     

                        string thisOperator = "";
                        if (value.Operator.Contains(")"))
                        {
                            if (output.Trim().LastOrDefault() != '(')
                            {
                                output += ") " + doubleBKT;
                                doubleBKT = "";
                            }
                            else
                            {
                                var temp = output.Trim();
                                temp = temp.Remove(temp.Length - 1, 1);
                                temp = temp.Trim();

                                temp = temp.Substring(0, temp.LastIndexOf(' '));
                                output = temp;
                            }
                        }
                        else if (value.Operator.Contains("U"))
                        {
                            thisOperator = " and ";
                            if (value.Operator.Contains("("))
                                thisOperator += " ( ";
                        }
                        else if (value.Operator.Contains("O"))
                        {
                            thisOperator = " or ";
                            if (value.Operator.Contains("("))
                                thisOperator += " ( ";
                            if (string.IsNullOrEmpty(value.Bit))
                            {
                                thisOperator += " ( ";
                                doubleBKT += ")";
                            }
                        }

                        if (output.Length != 0)
                        {
                            if (output.TrimEnd().LastOrDefault() != '(')
                                output += thisOperator;
                            else
                            {
                                if (thisOperator.TrimEnd().LastOrDefault() == '(')
                                    output += "(";
                            }
                        }
                        else
                        {
                            if (thisOperator.Contains("("))
                                output += " ( ";
                        }

                        if (value.Operator.Contains("L") && !value.Operator.Contains("BLD"))
                        {
                            var timerData = ValueBool(value);
                            var timerFromDB = 0;
                            //Проверим на таймер в бд
                            if (value.AEM.Contains("DB") && DB.ContainsKey(value.Bit))
                            {
                                timerData = DB[value.Bit].ToLower();
                                if (timerData.Contains("s5t"))//То это таймер, иначе число
                                    timerFromDB = 1;
                            }
                            if (value.AEM.Contains("S5T") || timerData.Contains("s5t") || timerFromDB == 1) //Если таймер
                            {
                                if (timerData == "0")
                                    timerData = value.AEM.ToLower();
                                var temp = Parse(output);
                                ProgramData valueNext = DataGridTable[i + 1];
                                if (valueNext.Operator.Contains("SE"))
                                {
                                    //if (valueNext.Code.Contains("T      5"))
                                    //{ }

                                    i = i + 1;

                                    if (temp == false) //Обнуляем таймер если SE 
                                    {
                                        if (TimerSE.Keys.Contains(valueNext.Bit.ToString()))
                                        {
                                            TimerSE.Remove(valueNext.Bit.ToString());
                                            MyTimers valueTime = TimerGridTable.Where(u => u.Address == valueNext.Bit).SingleOrDefault();
                                            valueTime.Time = 0;
                                            valueTime.Value = 0;
                                        }
                                    }
                                    else //Создаем новый таймер если SE 
                                    {
                                        if (!TimerSE.Keys.Contains(valueNext.Bit.ToString()))
                                        {
                                            var tempTimerData = timerData.Split('#');
                                            string tempTime;
                                            int newTempTime;
                                            if (tempTimerData[1].Contains("ms"))
                                            {
                                                tempTime = tempTimerData[1].Replace("ms", "");
                                                newTempTime = Convert.ToInt32(tempTime);
                                            }
                                            else
                                            {
                                                tempTime = tempTimerData[1].Replace("s", "");
                                                newTempTime = Convert.ToInt32(tempTime);
                                                newTempTime = newTempTime * 1000;
                                            }
                                            TimerSE.Add(valueNext.Bit, newTempTime);
                                            var containsTimer = TimerGridTable.Where(u => u.Address == valueNext.Bit).SingleOrDefault();
                                            if (containsTimer == null)
                                            {
                                                MyTimers valueTime = new MyTimers(valueNext.Bit, 0, newTempTime, 0);
                                                TimerGridTable.Add(valueTime);
                                            }
                                            else
                                            {
                                                containsTimer.EndTime = newTempTime;
                                                containsTimer.Time = 0;
                                                containsTimer.Value = 0;
                                            }
                                        }
                                    }
                                }
                                else if (valueNext.Operator.Contains("SA")) //если SA то наоборот запускаем
                                {
                                    i = i + 1;

                                    if (temp == true) //Обнуляем таймер если SА тут обнуление это 1
                                    {
                                        if (!TimerSA.Keys.Contains(valueNext.Bit.ToString()))
                                        {
                                            var tempTimerData = timerData.Split('#');
                                            string tempTime;
                                            int newTempTime;
                                            if (tempTimerData[1].Contains("ms"))
                                            {
                                                tempTime = tempTimerData[1].Replace("ms", "");
                                                newTempTime = Convert.ToInt32(tempTime);
                                            }
                                            else
                                            {
                                                tempTime = tempTimerData[1].Replace("s", "");
                                                newTempTime = Convert.ToInt32(tempTime);
                                                newTempTime = newTempTime * 1000;
                                            }
                                            TimerSA.Add(valueNext.Bit, newTempTime);
                                            var containsTimer = TimerGridTable.Where(u => u.Address == valueNext.Bit).SingleOrDefault();
                                            if (containsTimer == null)
                                            {
                                                MyTimers valueTime = new MyTimers(valueNext.Bit, -1, newTempTime, 1);
                                                TimerGridTable.Add(valueTime);
                                            }
                                            else
                                            {
                                                containsTimer.EndTime = newTempTime;
                                                containsTimer.Time = -1;
                                                containsTimer.Value = 1;
                                            }
                                        }
                                        else
                                        {
                                            var containsTimer = TimerGridTable.Where(u => u.Address == valueNext.Bit).SingleOrDefault();
                                            if (containsTimer != null)
                                            {
                                                containsTimer.Time = -1;
                                                containsTimer.Value = 1;
                                            }

                                        }
                                    }
                                    else
                                    {
                                        if (TimerSA.Keys.Contains(valueNext.Bit.ToString()))
                                        {
                                            var containsTimer = TimerGridTable.Where(u => u.Address == valueNext.Bit).SingleOrDefault();
                                            if (containsTimer.Time == -1)
                                                containsTimer.Time = 0;//Т.к. SA запускается если с 1 стало 0, то мы из таймеров SA должны скопировать в таймеры SE, и потом проверять когда кончится время, то удалить из таймеров SE 
                                        }
                                    }
                                }
                                else
                                    Load = timerData;
                            }
                            else if (compareValues.Count == 0) //Если сравнение
                            {
                                int loadInt;
                                var result = Int32.TryParse(ValueBool(value), out loadInt);
                                if (!result)
                                {
                                    var temp = output.Trim();
                                    temp = temp.Remove(temp.Length - 1, 1);
                                    temp = temp.Trim();
                                    if (!string.IsNullOrEmpty(temp))
                                    {
                                        temp = temp.Substring(0, temp.LastIndexOf(' '));

                                        var tempValue = Parse(temp);
                                        if (tempValue == false)
                                            i = i + 2;
                                    }
                                }
                            }
                            var currentInt = ValueBool(value);
                            if (!currentInt.ToLower().Contains("s5t"))
                                compareValues.Add(Convert.ToInt32(currentInt));
                        }

                        if (value.Operator.Contains("=="))
                        {
                            if (compareValues[0] == compareValues[1])
                                output += "true";
                            else
                                output += "false";
                        }
                        else if (value.Operator.Contains("<>"))
                        {
                            if (compareValues[0] != compareValues[1])
                                output += "true";
                            else
                                output += "false";
                        }
                        else if (value.Operator.Contains("<"))
                        {
                            if (compareValues[0] < compareValues[1])
                                output += "true";
                            else
                                output += "false";
                        }
                        else if (value.Operator.Contains(">"))
                        {
                            if (compareValues[0] > compareValues[1])
                                output += "true";
                            else
                                output += "false";
                        }
                        else if (value.Operator.Contains("+"))
                        {
                            var temp = compareValues[0] + compareValues[1];
                            compareValues[0] = temp;
                        }
                        else if (value.Operator.Contains("-"))
                        {
                            var temp = compareValues[0] - compareValues[1];
                            compareValues[0] = temp;
                        }
                        if (value.Operator.Contains("T"))
                        {
                            if (string.IsNullOrEmpty(Load))
                                DB[value.Bit] = compareValues[0].ToString();
                            else
                                DB[value.Bit] = Load;
                        }



                        if (value.Operator.Contains("BLD")) 
                        {
                            if (string.IsNullOrEmpty(output))
                                break;
                            string tempOutput = output;
                            if (output.Contains("("))
                                tempOutput = output.Remove(output.LastIndexOf('('));

                            var tempOutputForParse = tempOutput.TrimEnd();
                            if (tempOutputForParse.LastIndexOf(' ') != -1)
                            {
                                tempOutput = tempOutputForParse.Substring(0, tempOutputForParse.LastIndexOf(' '));
                                //if (!Parse(tempOutputForParse))
                                    //break;
                            }
                            BLD = tempOutput.Trim();
//if(InputData[0]==126)
//                            { }
                            //if (output.Replace(tempOutput, "").Length != 0)
                            //{
                            //    BLD = output.Replace(tempOutput, "");
                            //    BLD = BLD.Remove(0, 1).Trim();
                            //}
                            //else
                            //    BLD = output;
                        }

                        if (value.Operator.Contains("SPBNB")) //Типа goto
                        {
                            bool tempValue;
                            if (!string.IsNullOrEmpty(output))
                                tempValue = Parse(output);
                            else
                                tempValue = false;

                            if (tempValue) //если перед нами 1 то идем сюда
                            {
                                //ProgramData valueNext = DataGridTable[i + 1];
                                //var valueToNext = ValueBool(valueNext);
                                //ProgramData valueNext2 = DataGridTable[i + 2];
                                //var memory = MemoryGridTable.Find(u => u.Address == valueNext2.Bit);
                                //memory.CurrentValue = valueToNext.ToUpper();
                                //DB[valueNext2.Bit] = memory.CurrentValue;
                            }
                            else
                            {//если нет, то перескакиваем. считаем сколько перескачить
                                int count = 0;
                                for (int j = i; j <= item.Value; j++)
                                {
                                    count++;
                                    ProgramData valueNext = DataGridTable[j + 1];
                                    if (valueNext.Operator == value.AEM + ":")
                                    {
                                        i = i + count;
                                        break;
                                    }
                                }
                            }
                        }
                        else if (value.Operator.Contains("S"))
                        {
                            bool tempValue;
                            if (!string.IsNullOrEmpty(output))
                                tempValue = Parse(output);
                            else
                                tempValue = false;
                            if (tempValue)
                                DataWrite(value, "true");
                            //else
                                //DataWrite(value, "false");

                            //Смотрим сл. строку, если там R или S то не обнуляем output
                            ProgramData valueNext = DataGridTable[i + 1];
                            if (!valueNext.Operator.Contains("R"))
                                if (!valueNext.Operator.Contains("S"))
                                    output = "";
                        }
                        else if (value.Operator.Contains("R"))
                        {
                            bool tempValue;
                            if (!string.IsNullOrEmpty(output))
                                tempValue = Parse(output);
                            else
                                tempValue = false;
                            if (tempValue)
                                DataWrite(value, "false");
                            //else
                                //DataWrite(value, "true");

                            //Смотрим сл. строку, если там R или S то не обнуляем output
                            ProgramData valueNext = DataGridTable[i + 1];
                            if (!valueNext.Operator.Contains("R"))
                                if (!valueNext.Operator.Contains("S"))
                                    output = "";
                        }
                        else if (value.Operator.Contains("FP"))
                        {
                            if (string.IsNullOrEmpty(BLD)) //Если нет BLD
                            {
                                var tempValue = Parse(output);
                                if (Convert.ToInt32(tempValue) != FrontP[value.Key.ToString()])
                                {
                                    if (FrontP[value.Key.ToString()] == 0)
                                    {
                                        FrontP[value.Key.ToString()] = 1;
                                        DataWrite(value, "true");
                                        output = "true";
                                    }
                                    else
                                    {
                                        //DataWrite(value, "false");
                                        if (FrontP[value.Key.ToString()] == 1)
                                            FrontP[value.Key.ToString()] = 0;
                                        output = "";
                                        int count = 0;
                                        for (int j = i; j <= item.Value; j++)
                                        {
                                            ProgramData valueNext = DataGridTable[j + 1];
                                            if (valueNext.Operator == "SPBNB" || valueNext.Operator == "S" || valueNext.Operator == "R" || valueNext.Operator == "=")
                                            {
                                                if (count == 0) //Это если надо пропустить след. строку, но она S или R
                                                {
                                                    count++;
                                                    break;
                                                }
                                                i = i + count; //чтоб в нее зашло
                                                break;
                                            }
                                            count++;
                                        }
                                    }
                                }
                                else
                                {      //Перескакиваем фронт
                                    output = "";
                                    int count = 0;
                                    for (int j = i; j <= item.Value; j++)
                                    {
                                        ProgramData valueNext = DataGridTable[j + 1];
                                        if (valueNext.Operator == "SPBNB" || valueNext.Operator == "S" || valueNext.Operator == "R" || valueNext.Operator == "=")
                                        {
                                            if (count == 0) //Это если надо пропустить след. строку, но она S или R
                                            {
                                                i = i + 1;
                                                break;
                                            }
                                            i = i + count; //чтоб в нее зашло
                                            break;
                                        }
                                        count++;
                                    }
                                }
                            }
                            else //Если есть BLD
                            {
                                if (Convert.ToInt32(Convert.ToBoolean(BLD)) != FrontP[value.Key.ToString()])
                                {
                                    if (FrontP[value.Key.ToString()] == 0)
                                    {
                                        for (int j = i; j <= item.Value; j++)
                                        {
                                            //int count = 0;
                                            ProgramData valueNext = DataGridTable[j + 1];
                                            if (valueNext.Operator == "=")
                                            {
                                                DataWrite(valueNext, "true");
                                                FrontP[value.Key.ToString()] = 1;
                                                //i = i + count + 1; //чтоб ее перепрыгнуло
                                                break;
                                            }
                                            //count++;
                                        }
                                        break;
                                    }
                                    else
                                    {
                                        FrontP[value.Key.ToString()] = 0;
                                        output = "false";
                                        //break;
                                        //for (int j = i; j <= item.Value; j++)
                                        //{
                                        //    //int count = 0;
                                        //    ProgramData valueNext = DataGridTable[j + 1];
                                        //    if (valueNext.Operator == "=")
                                        //    {
                                        //        DataWrite(valueNext, "false"); //Обнуляем маркер
                                        //        //i = i + count + 1; //чтоб ее перепрыгнуло
                                        //        break;
                                        //    }
                                        //    //count++;
                                        //}
                                        //break;
                                    }
                                }
                                else
                                {
                                    output = "false";
                                    //break;
                                    //for (int j = i; j <= item.Value; j++)
                                    //{
                                    //    int count = 0;
                                    //    ProgramData valueNext = DataGridTable[j + 1];
                                    //    if (valueNext.Operator == "=")
                                    //    {
                                    //        DataWrite(valueNext, "false");//Обнуляем маркер
                                    //        i = i + count + 1; //чтоб ее перепрыгнуло
                                    //        break;
                                    //    }
                                    //    count++;
                                    //}
                                }
                            }
                        }
                        else if (value.Operator.Contains("FN"))
                        {
                            if (string.IsNullOrEmpty(BLD)) //Если нет BLD
                            {
                                var tempValue = Parse(output);
                                if (Convert.ToInt32(tempValue) != FrontN[value.Key.ToString()])
                                {
                                    if (Convert.ToInt32(tempValue) == 1)
                                    {
                                        FrontN[value.Key.ToString()] = 1;
                                        output = "";
                                        int count = 0;
                                        for (int j = i; j <= item.Value; j++)
                                        {
                                            ProgramData valueNext = DataGridTable[j + 1];
                                            if (valueNext.Operator == "SPBNB" || valueNext.Operator == "S" || valueNext.Operator == "R" || valueNext.Operator == "=")
                                            {
                                                if (count == 0) //Это если надо пропустить след. строку, но она S или R
                                                {
                                                    i = i + 1;
                                                    break;
                                                }
                                                i = i + count; //чтоб в нее зашло
                                                break;
                                            }
                                            count++;
                                        }
                                    }
                                    else
                                    {
                                        if (FrontN[value.Key.ToString()] == 1)
                                        {
                                            DataWrite(value, "true");
                                            FrontN[value.Key.ToString()] = 0;
                                            output = "true";
                                        }
                                    }
                                }
                                else
                                //Перескакиваем в конец фронта
                                {
                                    output = "";
                                    int count = 0;
                                    for (int j = i; j <= item.Value; j++)
                                    {
                                        ProgramData valueNext = DataGridTable[j + 1];
                                        if (valueNext.Operator == "SPBNB" || valueNext.Operator == "S" || valueNext.Operator == "R" || valueNext.Operator == "=")
                                        {
                                            if (count == 0) //Это если надо пропустить след. строку, но она S или R
                                            {
                                                i = i + 1;
                                                break;
                                            }
                                            i = i + count; //чтоб в нее зашло
                                            break;
                                        }
                                        count++;
                                    }
                                }
                            }
                            else //Если есть BLD
                            {
                                if (Convert.ToInt32(Convert.ToBoolean(BLD)) != FrontN[value.Key.ToString()])
                                {
                                    if (Convert.ToInt32(Convert.ToBoolean(BLD)) == 1)
                                    {
                                        FrontN[value.Key.ToString()] = 1;
                                        output = "false";
                                        //break;
                                        //for (int j = i; j <= item.Value; j++)
                                        //{
                                        //    //int count = 0;
                                        //    ProgramData valueNext = DataGridTable[j + 1];
                                        //    if (valueNext.Operator == "=")
                                        //    {
                                        //        DataWrite(valueNext, "true");
                                        //        //DataWrite(valueNext, "false"); 
                                        //        //i = i + count + 1; //чтоб ее перепрыгнуло
                                        //        break;
                                        //    }
                                        //    //count++;
                                        //}
                                        //break;
                                    }
                                    else
                                    {
                                        for (int j = i; j <= item.Value; j++)
                                        {
                                            //int count = 0;
                                            ProgramData valueNext = DataGridTable[j + 1];
                                            if (valueNext.Operator == "=")
                                            {
                                                DataWrite(valueNext, "true");//Обнуляем маркер
                                                FrontN[value.Key.ToString()] = 0;
                                                //i = i + count + 1; //чтоб ее перепрыгнуло
                                                break;
                                            }
                                            //count++;
                                        }
                                        break;
                                    }
                                }
                                else
                                {
                                    output = "false";
                                    //break;
                                    //for (int j = i; j <= item.Value; j++)
                                    //{
                                    //    int count = 0;
                                    //    ProgramData valueNext = DataGridTable[j + 1];
                                    //    if (valueNext.Operator == "=")
                                    //    {
                                    //        DataWrite(valueNext, "false");//Обнуляем маркер
                                    //        i = i + count + 1; //чтоб ее перепрыгнуло
                                    //        break;
                                    //    }
                                    //    count++;
                                    //}
                                }
                            }
                        }
                        else
                        {
                            if (!thisOperator.Contains("("))
                                if (!value.Operator.Contains(")"))
                                    if (!value.Operator.Contains("L"))
                                        if (!value.Operator.Contains("T"))
                                            if (!value.Operator.Contains("="))
                                                if (!value.Operator.Contains("<>"))
                                                    if (!value.Operator.Contains("<"))
                                                        if (!value.Operator.Contains(">"))
                                                            if (!value.Operator.Contains("+"))
                                                                if (!value.Operator.Contains("-"))
                                                                    output += ValueBool(value);
                        }
                    }
                }
            }
            if (DB["54.5"].ToLower() == "true" && !timeCikl)
            {
                timee = 0;
                GlobalCikl++;
                if (GlobalCikl > 6)
                    GlobalCikl = 1;
                timeCikl = true;
            }
            if (DB["54.5"].ToLower() == "false")
            {
                timee = 0;
                //GlobalCikl = 0;
                timeCikl = false;
            }
            if (newProgram == 1)
                newProgram = 2;
        }
        #endregion
        #region Чтение и запись

        private void timerForVisualDataRefresh()
        {
            for (int i = 0; i < 4; i++)
            {
                var tempBits = Convert.ToString(InputData[i], 2);
                while (tempBits.Length < 8)
                    tempBits = tempBits.Insert(0, "0");
                tempBits = ReverseString(tempBits);
                for (int j = 0; j <= tempBits.Length; j++)
                {
                    if (tabControl.FindName("Input" + i + "Bit" + j) is CheckBox chekbox)
                    {
                        if (tempBits[j] == '1')
                            chekbox.IsChecked = true;
                        else
                            chekbox.IsChecked = false;
                    }
                }
            }
            for (int i = 0; i < 3; i++)
            {
                var tempBits = Convert.ToString(OutputData[i], 2);
                while (tempBits.Length < 8)
                    tempBits = tempBits.Insert(0, "0");
                tempBits = ReverseString(tempBits);
                for (int j = 0; j <= tempBits.Length; j++)
                {
                    if (tabControl.FindName("Output" + i + "Bit" + j) is CheckBox chekbox)
                    {
                        if (tempBits[j] == '1')
                            chekbox.IsChecked = true;
                        else
                            chekbox.IsChecked = false;
                    }
                }
            }
            //Обновляем таблицу действий операторов
            zadOp1.Content = DB["46"];
            zadOp2.Content = DB["44"];
            istOp1.Content = DB["52"];
            istOp2.Content = DB["42"];
            rasOp1.Content = DB["50"];
            rasOp2.Content = DB["40"];

            deyOp1.Foreground = new SolidColorBrush(Colors.Lime);
            string op1 = "", op2 = "", error = "";
            if (DB["0.2"].ToLower() == "true")
                deyOp1.Content = "Исходное";
            else
                deyOp1.Content = "";
            if (DB["1.0"].ToLower() == "true")
                deyOp1.Content = "Ожидание";
            if (DB["1.1"].ToLower() == "true")
                deyOp1.Content = "Подъем";
            if (DB["1.2"].ToLower() == "true")
                deyOp1.Content = "Опускание";
            if (DB["1.5"].ToLower() == "true")
                deyOp1.Content = "Ход влево";
            if (DB["1.6"].ToLower() == "true")
                deyOp1.Content = "Ход вправо";
            if (DB["54.4"].ToLower() == "true")
                deyOp1.Content = "Ожидание загрузки";
            if (DB["54.6"].ToLower() == "true")
                deyOp1.Content = "Выдержка";
            if (DB["0.5"].ToLower() == "true")
            {
                deyOp1.Foreground = new SolidColorBrush(Colors.Red);
                op1 = deyOp1.Content.ToString();
                deyOp1.Content = "Ошибка позиции";
                error = deyOp1.Content.ToString();
                if (tabControl.SelectedIndex == 5)
                    LoadLog();
            }
            if (DB["1.7"].ToLower() == "true")
            {
                deyOp1.Foreground = new SolidColorBrush(Colors.Red);
                op1 = deyOp1.Content.ToString();
                deyOp1.Content = "Ошибка датчиков";
                error = deyOp1.Content.ToString();
                if (tabControl.SelectedIndex == 5)
                    LoadLog();
            }

            deyOp2.Foreground = new SolidColorBrush(Colors.Lime);
            if (DB["0.1"].ToLower() == "true")
                deyOp2.Content = "Исходное";
            else
                deyOp2.Content = "";
            if (DB["0.7"].ToLower() == "true")
                deyOp2.Content = "Ожидание";
            if (DB["1.3"].ToLower() == "true")
                deyOp2.Content = "Подъем";
            if (DB["1.4"].ToLower() == "true")
                deyOp2.Content = "Опускание";
            if (DB["54.0"].ToLower() == "true")
                deyOp2.Content = "Ход влево";
            if (DB["54.1"].ToLower() == "true")
                deyOp2.Content = "Ход вправо";
            if (DB["54.4"].ToLower() == "true")
                deyOp2.Content = "Ожидание загрузки";
            if (DB["54.7"].ToLower() == "true")
                deyOp2.Content = "Выдержка";
            if (DB["0.6"].ToLower() == "true")
            {
                deyOp2.Foreground = new SolidColorBrush(Colors.Red);
                op2 = deyOp2.Content.ToString();
                deyOp2.Content = "Ошибка позиции";
                error = deyOp2.Content.ToString();
                if (tabControl.SelectedIndex == 5)
                    LoadLog();
            }
            if (DB["54.2"].ToLower() == "true")
            {
                deyOp2.Foreground = new SolidColorBrush(Colors.Red);
                op2 = deyOp2.Content.ToString();
                deyOp2.Content = "Ошибка датчиков";
                error = deyOp2.Content.ToString();
                if (tabControl.SelectedIndex == 5)
                    LoadLog();
            }

            if (ErrorOP1 == 1)
                if (DB["0.5"].ToLower() != "true")
                    if (DB["1.7"].ToLower() != "true")
                        ErrorOP1 = 0;

            if (ErrorOP2 == 1)
                if (DB["0.6"].ToLower() != "true")
                    if (DB["54.2"].ToLower() != "true")
                        ErrorOP2 = 0;
            if (ErrorOP1 == 0 && (DB["0.5"].ToLower() == "true" || DB["1.7"].ToLower() == "true"))
            {
                ErrorOP1 = 1;
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                (ThreadStart)delegate ()
                {
                    ErrorLog(op1, op2, 1, error);
                }
                );
            }

            if (ErrorOP2 == 0 && (DB["0.6"].ToLower() == "true" || DB["54.2"].ToLower() == "true"))
            {
                ErrorOP2 = 1;
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                (ThreadStart)delegate ()
                {
                    ErrorLog(op1, op2, 2, error);
                }
                );
            }
        }
        private void ErrorLog(string Operation1, string Operation2, int Operator, string Error)
        {
            if (!File.Exists(LogPath))
            {
                using (var fileStream = new FileStream(LogPath, FileMode.CreateNew))
                {
                }
            }
            using (var fileStream = new FileStream(LogPath, FileMode.Append))
            {
                using (var streamWriter = new StreamWriter(fileStream, Encoding.Default))
                {
                    string item = "\n*******************";
                    item += "\nВремя: " + DateTime.Now.ToString("F");
                    item += ".\nЦикл №" + GlobalCikl;
                    item += "\nОператор 1 - " + Operation1;
                    if (Operator == 1)
                        item += " / Ошибка: " + Error;
                    item += "\nЗаданная позиция: " + DB["46"] + ", Истинная позиция: " + DB["52"] + ", Расчетная позиция: " + DB["50"];
                    item += "\nОператор 2 - " + Operation2;
                    if (Operator == 2)
                        item += " / Ошибка: " + Error;
                    item += "\nЗаданная позиция: " + DB["44"] + ", Истинная позиция: " + DB["42"] + ", Расчетная позиция: " + DB["40"];

                    streamWriter.WriteLine(item);
                }
            }
        }
        private string ReverseString(string s)
        {
            char[] value = s.ToCharArray();
            Array.Reverse(value);
            return new string(value);
        }
        private bool DataRead(string byteAndBit, string path)
        {
            var address = byteAndBit.Split('.');
            var count = 0;//-1
            int value;
            switch (path)
            {
                case "input":
                    value = InputData[Convert.ToInt32(address[0])];
                    break;
                case "marker":
                    value = MarkerData[Convert.ToInt32(address[0])];
                    break;
                default: //output
                    value = OutputData[Convert.ToInt32(address[0])];
                    break;
            }
            var bits = Convert.ToString(value, 2);
            while (bits.Length < 8)
                bits = bits.Insert(0, "0");
            bits = ReverseString(bits);
            foreach (char ch in bits)
            {
                if (count.ToString() == address[1])
                {
                    var tempString = ch.ToString();
                    var tempInt = Convert.ToInt32(tempString);
                    var tempBool = Convert.ToBoolean(tempInt);
                    return tempBool;
                }
                count++;
            }
            return false;
        }
        private void DataWrite(ProgramData value, string output)
        {
            bool valueBool;
            try
            {
                valueBool = Parse(output);
            }
            catch
            {
                //МБ это уже и не нужно, не помню
                output = output.TrimEnd();
                output = output.Substring(0, output.LastIndexOf(' '));
                valueBool = Parse(output);
            }
            string path = "";

            if (value.AEM.Contains("A"))
            {
                path = "output";
                value.Output = Convert.ToInt32(valueBool).ToString();
            }
            if (value.AEM.Contains("M"))
            {
                path = "marker";
                value.Marker = Convert.ToInt32(valueBool).ToString();
            }
            if (value.AEM.Contains("E"))
            {
                path = "input";
                value.Input = Convert.ToInt32(valueBool).ToString();
            }
            if (value.AEM.Contains("DB"))
            {
                //Проверяем есть ли такое значение адреса в БД, если нет то это младший байт числа в другом адресе
                if (DB.ContainsKey(value.Bit))
                    DB[value.Bit] = valueBool.ToString();
                else
                {
                    var split = value.Bit.Split('.');
                    var olderByte = Convert.ToInt32(split[0]) - 1;
                    var valueOlderByte = Convert.ToString(Convert.ToInt32(DB[olderByte.ToString()]), 2);
                    while (valueOlderByte.Length < 8)
                        valueOlderByte = valueOlderByte.Insert(0, "0");
                    valueOlderByte = ReverseString(valueOlderByte);
                    valueOlderByte = valueOlderByte.Remove(Convert.ToInt16(split[1]), 1);
                    valueOlderByte = valueOlderByte.Insert(Convert.ToInt16(split[1]), Convert.ToInt16(valueBool).ToString());
                    valueOlderByte = ReverseString(valueOlderByte);
                    valueOlderByte = Convert.ToByte(valueOlderByte, 2).ToString();
                    DB[olderByte.ToString()] = valueOlderByte;
                    var memory2 = MemoryGridTable.Find(u => u.Address == olderByte.ToString());
                    memory2.CurrentValue = valueOlderByte.ToLower();
                    return;
                }
                value.Output = Convert.ToInt32(valueBool).ToString();
                var memory = MemoryGridTable.Find(u => u.Address == value.Bit);
                if (memory == null)
                {
                    var tempAddress = value.Bit.Split('.');
                    memory = MemoryGridTable.Find(u => u.Address == tempAddress[0]);
                    var tempBits = Convert.ToString(Convert.ToInt32(memory.CurrentValue), 2);
                    while (tempBits.Length < 8)
                        tempBits = tempBits.Insert(0, "0");
                    tempBits = ReverseString(tempBits);
                    tempBits = tempBits.Remove(Convert.ToInt16(tempAddress[1]), 1);
                    tempBits = tempBits.Insert(Convert.ToInt16(tempAddress[1]), value.Output);
                    tempBits = ReverseString(tempBits);
                    memory.CurrentValue = Convert.ToByte(tempBits, 2).ToString();
                    DB[tempAddress[0]] = memory.CurrentValue;
                    return;
                }
                memory.CurrentValue = valueBool.ToString().ToLower();
                DB[value.Bit] = memory.CurrentValue;
                return;
            }
            var address = value.Bit.Split('.');
            int valueTemp;
            switch (path)
            {
                case "input":
                    valueTemp = InputData[Convert.ToInt32(address[0])];
                    break;
                case "marker":
                    valueTemp = MarkerData[Convert.ToInt32(address[0])];
                    break;
                default: //output
                    valueTemp = OutputData[Convert.ToInt32(address[0])];
                    break;
            }
            var bits = Convert.ToString(valueTemp, 2);
            while (bits.Length < 8)
                bits = bits.Insert(0, "0");
            bits = ReverseString(bits);
            bits = bits.Remove(Convert.ToInt16(address[1]), 1);
            bits = bits.Insert(Convert.ToInt16(address[1]), Convert.ToInt16(valueBool).ToString());
            bits = ReverseString(bits);

            var byteToSave = Convert.ToByte(bits, 2);
            switch (path)
            {
                case "input":
                    InputData[Convert.ToInt32(address[0])] = byteToSave;
                    break;
                case "marker":
                    MarkerData[Convert.ToInt32(address[0])] = byteToSave;
                    break;
                default: //output
                    OutputData[Convert.ToInt32(address[0])] = byteToSave;
                    //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    //    (ThreadStart)delegate ()
                    //    {
                    //        ServiceOutput();
                    //    }
                    //);
                    break;
            }
            return;
        }
        private string ValueBool(ProgramData value)
        {
            var valueBool = false;

            if (!string.IsNullOrEmpty(value.AEM) && value.AEM.Contains("E"))
            {
                valueBool = DataRead(value.Bit, "input");
                value.Input = Convert.ToInt32(valueBool).ToString();
            }
            else if (!string.IsNullOrEmpty(value.AEM) && value.AEM.Contains("M") && !value.AEM.Contains("MS") && !value.AEM.Contains("ms"))
            {
                valueBool = DataRead(value.Bit, "marker");
                value.Marker = Convert.ToInt32(valueBool).ToString();
            }
            else if (!string.IsNullOrEmpty(value.AEM) && value.AEM.Contains("A"))
            {
                valueBool = DataRead(value.Bit, "output");
                value.Output = Convert.ToInt32(valueBool).ToString(); //Стоял input
            }
            else if (!string.IsNullOrEmpty(value.AEM) && value.AEM.Contains("DB"))
            {
                string tempValue;
                if (DB.ContainsKey(value.Bit))
                    tempValue = DB[value.Bit].ToLower();
                else
                {
                    var split = value.Bit.Split('.');
                    var olderByte = Convert.ToInt32(split[0]) - 1;
                    var valueOlderByte = Convert.ToString(Convert.ToInt32(DB[olderByte.ToString()]), 2);
                    while (valueOlderByte.Length < 8)
                        valueOlderByte = valueOlderByte.Insert(0, "0");
                    valueOlderByte = ReverseString(valueOlderByte);
                    valueOlderByte = valueOlderByte.Substring(Convert.ToInt16(split[1]), 1);
                    if (valueOlderByte == "0")
                        tempValue = "false";
                    else
                        tempValue = "true";
                }

                if (tempValue.Contains("true") || tempValue.Contains("false"))
                    valueBool = Convert.ToBoolean(tempValue);
                else
                    return tempValue;
                value.Input = Convert.ToInt32(valueBool).ToString();
            }
            else if (!string.IsNullOrEmpty(value.AEM) && value.AEM.Contains("T") && value.Bit.Length > 0)
            {
                var containsTimer = TimerGridTable.Where(u => u.Address == value.Bit).SingleOrDefault();
                if (containsTimer != null)
                    valueBool = Convert.ToBoolean(Convert.ToInt32(containsTimer.Value));
                else
                    valueBool = false;
            }
            else if (!string.IsNullOrEmpty(value.AEM))
            {
                //Значит тут наверно число
                var valueInt = value.AEM;
                int result;
                int.TryParse(valueInt, out result);
                return result.ToString();
            }
            //Проверяем на негатив
            if (value.Operator.Substring(value.Operator.Length - 1, 1) == "N")
                valueBool = Parse("!" + valueBool);

            return valueBool.ToString();
        }
        #endregion
        #region Изменение времени стекания
        private void SaveTextBoxes(DependencyObject obj)
        {
            TextBox tb = obj as TextBox;
            if (tb != null)
            {
                var newTime = 0;
                if (!string.IsNullOrEmpty(tb.Text.Trim()))
                    newTime = Convert.ToInt32(tb.Text.Trim());
                switch (tb.Name)
                {
                    case "Stek16":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(16));
                        break;
                    case "Stek_17_18":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(17));
                        break;
                    case "Stek_19":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(18));
                        break;
                    case "Stek_20":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(19));
                        break;
                    case "Stek_21":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(20));
                        break;
                    case "Stek_22":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(21));
                        break;
                    case "Stek_23":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(22));
                        break;
                    case "Stek_24_25":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(23));
                        break;
                    case "Stek_5_7":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(25));
                        break;
                    case "Stek_8_10":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(26));
                        break;
                    case "Stek_9":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(27));
                        break;
                    case "Stek_11":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(28));
                        break;
                    case "Stek_12":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(29));
                        break;
                    case "Stek_13":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(30));
                        break;
                    case "Stek_14":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(31));
                        break;
                    case "Stek_15":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(32));
                        break;
                    case "Tkop1":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(50));
                        break;
                    case "Tkop2":
                        SaveToFile(tb.Name, newTime, DB.ElementAt(51));
                        break;
                }
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
                SaveTextBoxes(VisualTreeHelper.GetChild(obj, i));
        }
        private void SaveToFile(string Stek, int newTime, KeyValuePair<string, string> DBAdress)
        {
            if (Stek.Contains("Tkop"))
                Stek = Stek.ToUpper();

            if (DB[DBAdress.Key] == "S5T#" + newTime + "S") //Если неизменилось значение, то выходим
                return;

            DB[DBAdress.Key] = "S5T#" + newTime + "S";
            this.Stek[Stek] = newTime * 1000;
            int start = 0;
            var allLines = new List<string>();
            using (StreamReader fs = new StreamReader(Path, Encoding.Default))
            {
                while (true)
                {
                    start++;
                    string tempLine = fs.ReadLine();
                    if (tempLine == null)
                        break;
                    if (tempLine.Contains(Stek))
                    {
                        var tempLines = tempLine.Split(':');
                        if (tempLines.Count() == 3)
                        {
                            var komment = tempLines[2].Split('/');
                            string komm = "";
                            if (komment.Count() > 1)
                                komm = "//" + komment[2];
                            allLines.Add(tempLines[0] + ":" + tempLines[1] + ":= S5T#" + Convert.ToInt32(newTime) + "S;	" + komm);
                        }
                        else
                            allLines.Add(tempLine);
                    }
                    else
                        allLines.Add(tempLine);
                }
            }
            using (var fileStream = new FileStream(Path, FileMode.Open))
            using (var streamWriter = new StreamWriter(fileStream, Encoding.Default))
            {
                foreach (var item in allLines)
                    streamWriter.WriteLine(item);
            }
        }
        #endregion
        #region Функции самой программы
        private void button_Menu_Click(object sender, RoutedEventArgs e)
        {
            switch (((Button)sender).TabIndex)
            {
                case 1:
                    if (tabControl.SelectedIndex == 4)
                        break;
                    if (DB["0.3"].ToLower() == "true")
                        CustomMessageBox.Show("Для того чтобы запустить программу с нуля, нужно выключить автоматический режим");
                    else
                    {
                        Confirm.Visibility = Visibility.Visible;
                        tabControl.SelectedIndex = 4;
                        tabControl.Focus();
                        break;
                        //ResetAll();
                        //ParseDB();
                        //backgroundWorker.CancelAsync();
                    }

                    tabControl.SelectedIndex = 0;
                    button_Start.Focus();
                    break;
                case 2:
                    if (tabControl.SelectedIndex == 4)
                        break;
                    if (DB["54.3"].ToLower() == "false")
                    {
                        DB["54.3"] = "true";
                        button_Stop.Foreground = Brushes.Red;
                    }
                    else
                    {
                        DB["54.3"] = "false";
                        button_Stop.Foreground = Brushes.Black;
                    }
                    break;
                case 3:
                    if (tabControl.SelectedIndex == 4)
                        break;
                    if (tabControl.SelectedIndex != 1)
                        tabControl.SelectedIndex = 1;
                    else
                        tabControl.SelectedIndex = 0;
                    FillTextBoxes();
                    tabControl.Focus();
                    break;
                case 4:
                    if (tabControl.SelectedIndex == 4)
                        break;
                    if (tabControl.SelectedIndex != 2)
                    {
                        tabControl.SelectedIndex = 2;
                        if (Program3.IsEnabled)
                            Program3.Focus();
                        else
                            Program4.Focus();
                    }
                    else
                    {
                        tabControl.SelectedIndex = 0;
                        button_Info.Focus();
                    }
                    break;
                case 5:
                    if (tabControl.SelectedIndex == 4)
                        break;
                    if (tabControl.SelectedIndex != 3)
                        tabControl.SelectedIndex = 3;
                    else
                        tabControl.SelectedIndex = 0;
                    button_Service.Focus();
                    break;
                default:
                    break;
            }
        }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1:
                    if (tabControl.SelectedIndex == 4)
                        break;
                    if (DB["0.3"].ToLower() == "true")
                        CustomMessageBox.Show("Для того чтобы запустить программу с нуля, нужно выключить автоматический режим");
                    else
                    {
                        Confirm.Visibility = Visibility.Visible;
                        tabControl.SelectedIndex = 4;
                        tabControl.Focus();
                        break;
                        //ResetAll();
                        //ParseDB();
                        //backgroundWorker.CancelAsync();
                    }

                    tabControl.SelectedIndex = 0;
                    button_Start.Focus();
                    break;
                case Key.Escape:
                    if (Confirm.Visibility == Visibility.Visible)
                    {
                        Confirm.Visibility = Visibility.Hidden;
                        tabControl.SelectedIndex = 0;
                    }
                    break;
                case Key.Enter:
                    if (Confirm.Visibility == Visibility.Visible)
                    {
                        Confirm.Visibility = Visibility.Hidden;
                        tabControl.SelectedIndex = 0;
                        ResetAll();
                        ParseDB();
                    }
                    break;
                case Key.F2:
                    if (tabControl.SelectedIndex == 4)
                        break;
                    if (DB["54.3"].ToLower() == "false")
                    {
                        DB["54.3"] = "true";
                        button_Stop.Foreground = Brushes.Red;
                    }
                    else
                    {
                        DB["54.3"] = "false";
                        button_Stop.Foreground = Brushes.Black;
                    }
                    //                    button_Stop.Focus();
                    break;
                case Key.F3:
                    if (tabControl.SelectedIndex == 4)
                        break;
                    if (tabControl.SelectedIndex != 1)
                        tabControl.SelectedIndex = 1;
                    else
                        tabControl.SelectedIndex = 0;
                    FillTextBoxes();
                    tabControl.Focus();
                    break;
                case Key.F4:
                    if (tabControl.SelectedIndex == 4)
                        break;
                    if (tabControl.SelectedIndex != 2)
                    {
                        tabControl.SelectedIndex = 2;
                        //if (Program3.IsEnabled)
                        //    Program3.Focus();
                        //else
                        //    Program4.Focus();
                    }
                    else
                    {
                        tabControl.SelectedIndex = 0;
                    }
                    button_Info.Focus();
                    break;
                case Key.F5:
                    if (tabControl.SelectedIndex == 4)
                        break;
                    if (tabControl.SelectedIndex != 3)
                        tabControl.SelectedIndex = 3;
                    else
                        tabControl.SelectedIndex = 0;
                    button_Service.Focus();
                    break;
//                case Key.F6:
//                    //LoadLog();
//                    //StartTest();
//                    DB["54.2"] = "true";
//                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
//(ThreadStart)delegate ()
//{
//    timerForVisualDataRefresh();
//}
//);
//                    break;
                //case Key.F7:
                //    DataWrite(DataGridTable[281], "true");
                //    break;
                case Key.F12:
                    if (tabControl.SelectedIndex != 5)
                    {
                        LoadLog();
                        //Scroll.ScrollToBottom();
                        tabControl.SelectedIndex = 5;
                    }
                    else
                        tabControl.SelectedIndex = 0;
                    break;
                case Key.D3:
                    if (tabControl.SelectedIndex == 2)
                        if (Program3.IsEnabled)
                            Program_Click(Program3, null);
                    break;
                case Key.D4:
                    if (tabControl.SelectedIndex == 2)
                        if (Program4.IsEnabled)
                            Program_Click(Program4, null);
                    break;
                //case Key.PageDown:
                //    if (tabControl.SelectedIndex == 5)
                //        Scroll.ScrollToVerticalOffset(Scroll.VerticalOffset + 100);
                //    break;
                //case Key.PageUp:
                //    if (tabControl.SelectedIndex == 5)
                //        Scroll.ScrollToVerticalOffset(Scroll.VerticalOffset - 100);
                //    break;
                default:
                    break;
            }
        }
        private void LoadLog()
        {
            if (File.Exists(LogPath))
                using (var sr = new StreamReader(LogPath, Encoding.Default))
                {
                    string text = sr.ReadToEnd();

                    var document = new FlowDocument();
                    var paragraph = new Paragraph();
                    paragraph.Inlines.Add(text);
                    document.Blocks.Add(paragraph);
                    document.ColumnWidth = 1000;
                    Log.Content = document;
                }

            //var b = new StreamReader(LogPath, Encoding.Default);
            //FlowDocument flow = new FlowDocument(new Paragraph(new Run(b.ReadToEnd())));

            //Log.Document = flow;
            //b.Dispose();
            //b.Close();


            //foreach (var item in readText)
            //{
            //    Log.Content += item + "\n";

            //}
            //using (StreamReader fs = new StreamReader(LogPath, Encoding.Default))
            //{
            //    Log.Content = "";
            //    while (true)
            //    {
            //        string read = fs.ReadLine();
            //        if (read == null) break;
            //        Log.Content += read + "\n";
            //    }
            //}
        }
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(LogPath))
                using (var fileStream = new FileStream(LogPath, FileMode.Truncate))
                    using (var streamWriter = new StreamWriter(fileStream, Encoding.Default))
                        streamWriter.WriteLine("");
            LoadLog();
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            ResetAll();
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveTextBoxes(tabControl);
            tabControl.SelectedIndex = 0;
        }
        private Boolean IsTextAllowed(String text)
        {
            return Array.TrueForAll<Char>(text.ToCharArray(),
                delegate (Char c) { return Char.IsDigit(c) || Char.IsControl(c); });
        }
        private void PreviewTextInputHandler(Object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }
        private void PastingHandler(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!IsTextAllowed(text)) e.CancelCommand();
            }
            else e.CancelCommand();
        }
        private void FillTextBoxes()
        {
            Stek16.Text = (Stek[Stek16.Name] / 1000).ToString();
            Stek_17_18.Text = (Stek[Stek_17_18.Name] / 1000).ToString();
            Stek_19.Text = (Stek[Stek_19.Name] / 1000).ToString();
            Stek_20.Text = (Stek[Stek_20.Name] / 1000).ToString();
            Stek_21.Text = (Stek[Stek_21.Name] / 1000).ToString();
            Stek_22.Text = (Stek[Stek_22.Name] / 1000).ToString();
            Stek_23.Text = (Stek[Stek_23.Name] / 1000).ToString();
            Stek_24_25.Text = (Stek[Stek_24_25.Name] / 1000).ToString();
            Stek_5_7.Text = (Stek[Stek_5_7.Name] / 1000).ToString();
            Stek_8_10.Text = (Stek[Stek_8_10.Name] / 1000).ToString();
            Stek_9.Text = (Stek[Stek_9.Name] / 1000).ToString();
            Stek_11.Text = (Stek[Stek_11.Name] / 1000).ToString();
            Stek_12.Text = (Stek[Stek_12.Name] / 1000).ToString();
            Stek_13.Text = (Stek[Stek_13.Name] / 1000).ToString();
            Stek_14.Text = (Stek[Stek_14.Name] / 1000).ToString();
            Stek_15.Text = (Stek[Stek_15.Name] / 1000).ToString();
            Tkop1.Text = (Stek[Tkop1.Name.ToUpper()] / 1000).ToString();
            Tkop2.Text = (Stek[Tkop2.Name.ToUpper()] / 1000).ToString();
        }
        private void HandleKeyDownEvent(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Q && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ResetAll();
                this.Close();
            }
        }
        public int newProgram = -1;
        Button pressButton;
        DispatcherTimer timer;
        private void Program_Click(object sender, RoutedEventArgs e)
        {
            if (DB["0.3"].ToLower() == "true")
            {
                CustomMessageBox.Show("Для того чтобы сменить программу, нужно выключить автоматический режим");
                return;
            }
            pressButton = sender as Button;
            //Отсанавливаем программу глобальной переменной 
            // -1 - работаем
            //  1 - ждем конца
            //  2 - закончили
            //Запускаем таймер
            timer = new DispatcherTimer();
            timer.Tick += new EventHandler(timer_ToNewProgram);
            timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timer.Start();
            newProgram = 1;
        }
        private void timer_ToNewProgram(object sender, EventArgs e)
        {
            if (newProgram == 2)
            {
                if (pressButton.Name == "Program3")
                {
                    ProgramString.Content = "Выбрана программа 3";
                    if (File.Exists("!3.AWL"))
                        File.Move("!3.AWL", "3.AWL");
                    File.Move("4.AWL", "!4.AWL");
                    Path = "3.AWL";
                    Program3.IsEnabled = false;
                    Program4.IsEnabled = true;
                    //Program4.Focus();
                }

                if (pressButton.Name == "Program4")
                {
                    ProgramString.Content = "Выбрана программа 4";
                    if (File.Exists("!4.AWL"))
                        File.Move("!4.AWL", "4.AWL");
                    File.Move("3.AWL", "!3.AWL");
                    Path = "4.AWL";
                    Program4.IsEnabled = false;
                    Program3.IsEnabled = true;
                    //Program3.Focus();
                }
                StartEnd.Clear();
                DataGridTable.Clear();
                ReadFileDB();
                newProgram = -1;
                timer.Stop();
            }
        }
        #endregion
        #region Обновление данных в сервисном режиме и на плате, считываение с платы
        private void ServiceOutput()
        {
            var result = rsh.Write(OutputData); //Обновляем данные платы
            var errorString = ErrorString.Content.ToString();
            if (!result && ErrorString.Visibility == Visibility.Hidden)
            {
                ErrorString.Visibility = Visibility.Visible;
                CustomMessageBox.Show(ErrorString.Content.ToString());
            }
            else if (result)
                ErrorString.Visibility = Visibility.Hidden;

            if (ErrorString.Visibility == Visibility.Hidden)
                ProgramString.Visibility = Visibility.Visible;
            else
                ProgramString.Visibility = Visibility.Hidden;
        }

        private void ResetAll()
        {
            InputData = new List<int>() { 0, 0, 0, 0 };
            MarkerData = Enumerable.Repeat(0, 20).ToList();
            //MarkerData[0] = 1;
            OutputData = new List<int>() { 0, 0, 0 };
            rsh.Write(OutputData);
        }
        #endregion
        #region Тестирование
        private void StartTest()
        {
            Service.Visibility = Visibility.Visible;
            MyInput0.Text = InputData[0].ToString();
            MyInput1.Text = InputData[1].ToString();
            MyInput2.Text = InputData[2].ToString();
            MyInput3.Text = InputData[3].ToString();
        }
        private void Test_Click(object sender, RoutedEventArgs e)
        {
            Service.Visibility = Visibility.Hidden;
            if (!string.IsNullOrEmpty(MyInput0.Text))
                InputData[0] = Convert.ToInt32(MyInput0.Text);
            if (!string.IsNullOrEmpty(MyInput1.Text))
                InputData[1] = Convert.ToInt32(MyInput1.Text);
            if (!string.IsNullOrEmpty(MyInput2.Text))
                InputData[2] = Convert.ToInt32(MyInput2.Text);
            if (!string.IsNullOrEmpty(MyInput3.Text))
                InputData[3] = Convert.ToInt32(MyInput3.Text);

        }
        #endregion
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace Dictionary
{
    /// <summary>
    /// Логика взаимодействия для Remembering.xaml
    /// </summary>
    public partial class Remembering : UserControl
    {
        DictionaryBook book; // Объекты model, подтягиваемые из ресурсов приложения
        ListPool pool;
        MasterViewModel vm; // Объект viewmodel
        public Remembering()
        {
            InitializeComponent();

            book = (DictionaryBook)FindResource("book");
            pool = (ListPool)FindResource("pool");
            vm = (MasterViewModel)FindResource("vm");
            vm.InitModel(book,pool); // Инициализация ViewModel ссылками на Model
            ((DirectCommand)FindResource("ShowNextCmd")).SetExecute((o) => vm.ShowNextCmd.Execute(o)); // Инициализируем глобальную команду в ресурсах окна, на действия в данном контроле


            cmbMode.ItemsSource= MyUtils.GetEnumList<ItemViewMode>();
            cmbOrder.ItemsSource = MyUtils.GetEnumList<OrderOfEnumeration>();
            cmbFilter.ItemsSource = MyUtils.GetEnumList<PartsOfSpeech>(); 


        }


        //Этот обработчик вместо команды приведен просто для демонстрации альтернативного подхода к проектированию!
        private void ButtonShowInDictionary_Click(object sender, RoutedEventArgs e)
        {
            // Через DataContext ListBoxItem'a легко выудить всю информацию о данных! 
            DictEditor de = (DictEditor)App.Current.MainWindow.FindName("dictEditor"); // Достаем приватных ребят через обход логического дерева (или рефлексию?)
            TabControl tc = (TabControl)App.Current.MainWindow.FindName("tabControl"); // Достаем приватных ребят через обход логического деревва (или рефлексию?)
            string word = ((WordPageViewModelDecorator)((Button)sender).DataContext).Wp.WordInDictionary; // Не забываем что все данные внутри событий шаблона доступны через DataContext! А вот обращение к элементам, возможно по имени в шаблоне, но это в коде XAML, а в code behind видимо рефлексия, хз
            tc.SelectedIndex = 1;
            de.SetWordIntoWiew(word); // А вот публичный метод Контрола можно вызывать явно, без рефлексии
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key != Key.Left && e.Key != Key.Right) return;
            e.Handled = true;
            vm.ShowNextCmd.Execute(e.Key == Key.Left  ? "-1" : "1");                
        }
    }

    public class MasterViewModel : Notifier
    {
        private ItemViewMode mode;
        public ItemViewMode Mode { get { return mode; } set 
            { 
                mode=value; ChildItems.ForEach(i => i.Mode = mode);
                var aaa = ChildItems;
                ChildItems = null;
                ChildItems = aaa;
                // Заставляем перерисовать список!
            } 
        }

        private ICollectionView viewForCmbRight;
        public ICollectionView ViewForCmbRight { get { return (ICollectionView)viewForCmbRight; } set { viewForCmbRight = value; OnPropertyChanged(); } }

        int nextCount = 5;
        int previouseCount = 5;

        private List<string> listOfWords;
        WordList wl;

        private int CombinedPriority(WordPage wp)
        {
            int priority = 0;
            if (wp.Tags.Contains(new TagString("W1")) || wp.Tags.Contains(new TagString("S1"))) priority += 300;
            if (wp.Tags.Contains(new TagString("W2")) || wp.Tags.Contains(new TagString("S2"))) priority += 200;
            if (wp.Tags.Contains(new TagString("W3")) || wp.Tags.Contains(new TagString("S3"))) priority += 100;
            return priority + wp.Priority;
        }

        private void StepForward()
        {            // Условный линк-запрос, совмещаем программные инструкции и язык линк.
            IEnumerable<string> tail;
            if (ChildItems==null || ChildItems.Last().Wp.WordInDictionary == listOfWords.Last()) tail = listOfWords;
            else tail = listOfWords.SkipWhile(w => w != ChildItems.Last().Wp.WordInDictionary).Skip(1);

            if (Mode == ItemViewMode.EnToRuSoundOnly) tail= tail.Where(w => book.Words[w].Exercises.Any(e => e.HasMP3));            
            ChildItems = tail.Take(nextCount).Select(o => new WordPageViewModelDecorator() { Wp = book.Words[o], Mode = Mode }).ToList();
        }

        private void StepBackward()
        {            // Условный линк-запрос, совмещаем программные инструкции и язык линк.
            IEnumerable<string> forepart;
            if (ChildItems == null || ChildItems.First().Wp.WordInDictionary == listOfWords.First()) forepart = listOfWords;
            else forepart = listOfWords.TakeWhile(w => w != ChildItems.First().Wp.WordInDictionary);
            if (Mode == ItemViewMode.EnToRuSoundOnly)
            {
                forepart = forepart.Where(w => book.Words[w].Exercises.Any(e => e.HasMP3));
            }

            ChildItems = forepart.Reverse().Take(previouseCount).Select(o => new WordPageViewModelDecorator() { Wp = book.Words[o], Mode = Mode }).Reverse().ToList();
        }
        
        private void SetLongList()
        {            
            if(Order==OrderOfEnumeration.Ascending) listOfWords = wl.lstEnglish.Select(w => w.Word).OrderBy(s => s).ToList();
            if (Order == OrderOfEnumeration.Random) listOfWords = wl.lstEnglish.Select(w => w.Word).GetRandomList(wl.lstEnglish.Count).ToList();
            if (Order == OrderOfEnumeration.Priority) listOfWords = wl.lstEnglish.Select(w => w.Word).OrderBy(w=>-book.Words[w].Priority).ToList();
            if (Order == OrderOfEnumeration.Combine) listOfWords = wl.lstEnglish.Select(w => w.Word).OrderBy(w => -CombinedPriority(book.Words[w])).ToList();

            ChildItems = null;
            StepForward();               
        }
        public WordList ListOfWords { get { return wl; } set { wl = value; SetLongList(); } }

        private IEnumerable<WordPageViewModelDecorator> childItems;
        private DictionaryBook book;
        public IEnumerable<WordPageViewModelDecorator> ChildItems { get { return childItems; } 
            set { childItems = value; OnPropertyChanged(); } }
        #region Команды кнопок ListBoxItem'a

        public DirectCommand ExpandAllCmd { get; private set; }
        public DirectCommand ShowNextCmd { get; private set; }

        public void InitModel(DictionaryBook book, ListPool pool)
        { 
            this.book = book;
            var lst= new CollectionViewSource { Source = pool.Lists };
            lst.SortDescriptions.Add(new SortDescription("Description", ListSortDirection.Ascending));
            ViewForCmbRight = lst.View;
            ViewForCmbRight.MoveCurrentTo(pool.MainList);
        }
                
        
        public OrderOfEnumeration order;
        public OrderOfEnumeration Order { get { return order; }  set { order = value; SetLongList(); } }
        public MasterViewModel()
        {            
            ExpandAllCmd = new DirectCommand();
            ShowNextCmd = new DirectCommand();

            ExpandAllCmd.SetExecute(o =>
            {
                Button btn = (Button)o;
                if ((string)btn.Content == (string)"Показать ответы")
                {
                    btn.Content = "Скрыть все ответы";
                    ChildItems.ToList().ForEach(i => i.ShowAnswerCmd.Execute(1));
                }
                else
                {
                    btn.Content = "Показать ответы";
                    ChildItems.ToList().ForEach(i => i.ShowAnswerCmd.Execute(-1));
                }
            });
             
            ShowNextCmd.SetExecute(p =>
            {
                int mode = int.Parse((string)p);
                if (mode == 1) StepForward(); else StepBackward();
            });




        }
        #endregion
    }

    public enum ItemViewMode
    {
        [Description("Знакомство. Англо-русский")]
        EnToRuFriendly = 9,
        [Description("Заучивание. Англо-русский")]
        EnToRu = 1,
        [Description("Упражнения. Русско-английский")]
        RuToEn = 0,
        [Description("Только слова. Русско-английский")]
        RuToEnWordOnly = 3,
        [Description("Только звук")]
        EnToRuSoundOnly = 4,
        [Description("Случайный")]
        Random = -1
    }

    public enum OrderOfEnumeration
    {
        [Description("Случайный")]
        Random=1,
        [Description("По приоритету")]
        Priority=2,
        [Description("Комбинированный")]
        Combine=3,
        [Description("Алфавитный")]
        Ascending=0
    }
           

    public class WordPageViewModelDecorator : Notifier
    {
        static Random rnd=new Random();
        ItemViewMode mode;
        public ItemViewMode Mode { get { return mode; } set {
                if (value == ItemViewMode.Random)
                {
                    bool includeSoundMode = wp.Exercises.All(e => !e.HasMP3);
                    var lst=MyUtils.GetEnumList<ItemViewMode>().Where(a => a != ItemViewMode.Random && (includeSoundMode || a!=ItemViewMode.EnToRuSoundOnly)).ToList();
                    value=lst[rnd.RandomIndex(lst.Count())];
                }
                if (value == ItemViewMode.RuToEn && wp.Examples.All(e => string.IsNullOrEmpty(e.Russian)))
                    value = ItemViewMode.RuToEnWordOnly;
                mode = value;                 
                SetMode(value); } }

        WordPage wp;
        public WordPage Wp { get { return wp; }
            set
            {
                wp = value;
                if (wp.Definition != null)
                    if (wp.Definition.IndexOf('&') < 0)
                    {
                        Def1 = wp.Definition;
                    }
                    else
                    {
                        Def1 = wp.Definition.Substring(0, wp.Definition.IndexOf('&'));
                        Def2 = wp.Definition.Substring(wp.Definition.IndexOf('&') + 1, wp.Definition.LastIndexOf('&') - wp.Definition.IndexOf('&') - 1);
                        Def3 = wp.Definition.Substring(wp.Definition.LastIndexOf('&') + 1);
                    }
                if (Def1 == string.Empty) Def1 = null;
                if (Def2 == string.Empty) Def2 = null;
                if (Def3 == string.Empty) Def3 = null;
                Translated1 = wp.TranslatedWords.First().word;
                if (wp.TranslatedWords.Count > 1) Translated2 = wp.TranslatedWords[1].word; else Translated3 = null;
                if (wp.TranslatedWords.Count > 2) Translated3 = wp.TranslatedWords[2].word; else Translated3 = null;


                SoundableExcersises = new List<Exercise>(wp.Exercises.Where(e => e.HasMP3));
                Collocations = wp.Examples.SkipWhile(e => e.Header == null || !(e.Header.Contains("ыражения") || e.Header.Contains("ловосочетания"))).Skip(1).TakeWhile(e => e.Header == null).ToList();
                Definition = GetDefinition();
                RuToEnProblems = wp.Examples.Where(e => !string.IsNullOrEmpty(e.Russian)).Select(e => new Problem(e.English, EnglishToOmition(e.English), e.Russian)).Take(7).ToList();





            }
        }

        
        
        public List<Example> Collocations { get; set; }
        public List<Example> Examples { get; set; }
        public string Def1 { get; set; }
        public string Def2 { get; set; }
        public string Def3 { get; set; }

        public string Translated1 { get; set; }
        public string Translated2 { get; set; }
        public string Translated3 { get; set; }

        public string Definition { get; set; }

        //public List<Exercise> Excercises { get; set; }

        public List<Exercise> SoundableExcersises { get; set; }

        public IList<Problem> RuProblems { get; set; } 

        public IList<Problem> RuToEnProblems { get; set; }

        #region Команды кнопок ListBoxItem'a

        public DirectCommand ShowAnswerCmd { get; private set; }
        public DirectCommand ShowHintCmd { get; private set; }
        public DirectCommand ShowDictionaryCmd { get; private set; }
        public static DirectSoundCommand PlaySoundWordCmd = new DirectSoundCommand();

        public static DirectCommand PlaySoundExampleCmd = new DirectSoundExCommand();

        public WordPageViewModelDecorator()
        {
            ShowAnswerCmd = new DirectCommand();
            ShowHintCmd = new DirectCommand();
            ShowDictionaryCmd = new DirectCommand();

            ShowAnswerCmd.SetExecute(o =>
            {
                int isShowing = int.Parse(o.ToString());
                VAnswer = (isShowing == 1 || isShowing==0 && VAnswer == Visibility.Collapsed) ? Visibility.Visible : Visibility.Collapsed;                
            });

            ShowHintCmd.SetExecute(o =>
            {
                if (Mode == ItemViewMode.RuToEn)
                {
                    VRuToEnProblemsHint = Visibility.Visible;
                }
                if (Mode == ItemViewMode.RuToEnWordOnly)
                {
                    VSyn = Visibility.Visible;
                }
            });

        }
        #endregion

        #region Булевы свойства видимости визуальных блоков ListBoxItem'a для привязки внутри его шаблона

        Visibility vAnswer = Visibility.Collapsed, vRuProblems, vEnProblems, vRuToEnProblems, vCollocations, vSoundsPanel, vEnExamples, vTranslations, vDefinitions, vSyn, vRuToEnProblemsHint;
        SolidColorBrush vEnWord;

        public Visibility VAnswer { get { return vAnswer; } 
            set { vAnswer = value; 
                if(VAnswer== Visibility.Collapsed)
                {
                    if(IsModeEnToRu())
                    {
                        ToolTip = (Mode == ItemViewMode.EnToRuFriendly) ? GetDefinition() : null;
                    }
                    else
                    {
                        VEnWord = new SolidColorBrush(Colors.Transparent);                        
                    }
                }
                else
                {
                    ToolTip = GetDefinition();
                    if (!IsModeEnToRu())
                    {
                        VEnWord = new SolidColorBrush(Colors.Black);
                    }
                }
                OnPropertyChanged(); } }
        public SolidColorBrush VEnWord { get { return vEnWord; } private set { vEnWord = value; OnPropertyChanged(); } }

        public Visibility VCollocations { get { return vCollocations; } private set {
                if (Collocations == null || Collocations.Count == 0) vCollocations = Visibility.Collapsed;
                else vCollocations = value;
                OnPropertyChanged();
            } }

        public Visibility VSoundsPanel { get { return vSoundsPanel; } private set {
                if (SoundableExcersises==null || SoundableExcersises.Count()==0) vSoundsPanel = Visibility.Collapsed;
                else vSoundsPanel = value;
                OnPropertyChanged();
            } }

        public Visibility VRuProblems { get { return vRuProblems; } private set {
                if (RuProblems == null || RuProblems.All(e => string.IsNullOrEmpty(e.RussianProblem))) vRuProblems = Visibility.Collapsed;
                else vRuProblems = value;
                OnPropertyChanged();
            } }

        public Visibility VRuToEnProblems
        {
            get { return vRuToEnProblems; }
            private set
            {
                if (RuToEnProblems == null || RuToEnProblems.Count==0) vRuToEnProblems = Visibility.Collapsed; else vRuToEnProblems = value;
                OnPropertyChanged();
            }
        }

        public Visibility VRuToEnProblemsHint
        {
            get { return vRuToEnProblemsHint; }
            private set
            {
                if (RuToEnProblems == null || RuToEnProblems.Count == 0) vRuToEnProblemsHint = Visibility.Collapsed; else vRuToEnProblemsHint = value;
                OnPropertyChanged();
            }
        }


        public Visibility VEnProblems
        {
            get { return vEnProblems; }
            private set
            {
                if (RuToEnProblems == null || RuToEnProblems.Count()==0) vEnProblems = Visibility.Collapsed;
                else vEnProblems = value;
                OnPropertyChanged();
            }
        }

        public Visibility VEnExamples { get { return vEnExamples; } private set {
                if (Examples == null || Examples.Count == 0) vEnExamples = Visibility.Collapsed;
                else vEnExamples = value;
                OnPropertyChanged();
            } }

        public Visibility VTranslations
        {
            get { return vTranslations; }
            private set
            {
                vTranslations = value;
                OnPropertyChanged();
            }
        }
        public Visibility VDefinitions
        {
            get { return vDefinitions; }
            private set
            {
                if (string.IsNullOrEmpty(Definition)) value = Visibility.Collapsed;
                vDefinitions = value;
                OnPropertyChanged();
            }
        }

        public Visibility VSyn
        {
            get { return vSyn; }
            private set
            {
                if (string.IsNullOrEmpty(wp.Syn)) value = Visibility.Collapsed;
                vSyn = value;
                OnPropertyChanged();
            }
        }



        #endregion

        #region Прочие свойства визуализации для привязки к View

        public string toolTip;
        public string ToolTip { get { return toolTip; } set { toolTip = value; OnPropertyChanged(); } }



        #endregion

        void SetMode(ItemViewMode mode)
        {
            VEnWord = new SolidColorBrush(Colors.Transparent);
            switch (mode)
            {
                case ItemViewMode.EnToRuFriendly:
                    {
                        RuProblems = wp.Exercises.Where(e => !string.IsNullOrEmpty(e.Russian)).Select(e => new Problem (e.English, "", e.Russian)).GetRandomList(2).ToList();
                        Examples = wp.Examples.Except(Collocations).Except(wp.Examples.Where(e => e.Header != null && (e.Header.Contains("ыражения") || e.Header.Contains("ловосочетания")))).Take(10).ToList();

                        VCollocations = VSoundsPanel = VEnExamples = VRuProblems = Visibility.Visible;
                        break;
                    }
                case ItemViewMode.EnToRu:
                    {
                        //Показываем ноль-два эталонных примера, на английском языке
                        int showType = (int)(4 * rnd.NextDouble());
                        if (showType == 0) Examples = wp.Examples.Where(e => e.Header == null).Take(2).ToList();
                        if (showType == 1) Examples = wp.Examples.Where(e => e.Header == null).Take(1).ToList();
                        if (showType == 2 || showType == 3)
                        {
                            VEnWord = new SolidColorBrush(Colors.Black);
                            Examples = null;
                        }
                        else VEnWord = new SolidColorBrush((Wp.WordInDictionary.IndexOf(' ') <= 0) ? Colors.Transparent : Colors.Black);  //Если слово не сложное по написанию, то скрываем его в режиме EnToRu

                        VRuProblems = VCollocations = Visibility.Collapsed;
                        VSoundsPanel = VEnExamples = Visibility.Visible;                       
                        break;
                    }
                case ItemViewMode.EnToRuSoundOnly:
                    {
                        VRuProblems=VCollocations = VEnExamples = Visibility.Collapsed;
                        VSoundsPanel = Visibility.Visible;
                        break;
                    }
                case ItemViewMode.RuToEn:
                    {
                        VTranslations = Visibility.Collapsed;
                        VDefinitions = Visibility.Collapsed;
                        if (rnd.Succes(30))
                        {
                            if (rnd.Succes(50) && !string.IsNullOrEmpty(Definition))
                                VDefinitions = Visibility.Visible;
                            else VTranslations = Visibility.Visible;                                                            
                        }

                        //Examples = wp.Examples.Except(Collocations).Except(wp.Examples.Where(e => e.Header != null && (e.Header.Contains("ыражения") || e.Header.Contains("ловосочетания")))).Take(10).ToList();
                        VRuToEnProblems = Visibility.Visible;
                        VRuToEnProblemsHint = Visibility.Collapsed;
                        VSyn = Visibility.Collapsed;


                        break;
                    }
                case ItemViewMode.RuToEnWordOnly:
                    {
                        VTranslations = Visibility.Collapsed;
                        VDefinitions = Visibility.Collapsed;
                        if (rnd.Succes(100))
                        {
                            if (rnd.Succes(50) && !string.IsNullOrEmpty(Definition))
                                VDefinitions = Visibility.Visible;
                            else VTranslations = Visibility.Visible;
                        }                            

                        VRuToEnProblems = VRuToEnProblemsHint = VSyn = Visibility.Collapsed;                        
                        break;
                    }
            }
            
            OnPropertyChanged("Examples");
            OnPropertyChanged("RuProblems");
        }

        public ICollectionView ViewTranslated { get {
                ICollectionView view = new CollectionViewSource { Source = Wp.TranslatedWords }.View;
                view.GroupDescriptions.Add(new PropertyGroupDescription("part"));
                return view;
            } }

        private string GetDefinition()
        {
            string definition = null;
                if (!string.IsNullOrEmpty(Def1)) definition = " - " + Def1;
                if (!string.IsNullOrEmpty(Def2)) definition += "\n" + " - " + Def2;
                if (!string.IsNullOrEmpty(Def3)) definition += "\n" + " - " + Def3;
            return definition;
        }

        private string EnglishToOmition(string s)
        {
            string word = wp.WordInDictionary.Trim();
            int iSpace=word.IndexOf(' ');
            if (iSpace > 0) if (word[iSpace + 1] != '[') return s; else word = word.Substring(0, iSpace);
            s = Regex.Replace(s, "\\b" +word+ "(\\w*?)\\b", "...");            
            return s;
        }

        public bool IsModeEnToRu() { return (Mode==ItemViewMode.EnToRu || Mode==ItemViewMode.EnToRuFriendly || Mode==ItemViewMode.EnToRuSoundOnly); }


    }


    public class RememberingViewModelTemplateSelector : DataTemplateSelector
    {
        public DataTemplate RuToEnFullTemplate { get; set; }
        public DataTemplate EnToRuFullTemplate { get; set; }

                              
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            WordPageViewModelDecorator witem = item as WordPageViewModelDecorator;
            if ((witem.Mode == ItemViewMode.EnToRu) || (witem.Mode == ItemViewMode.EnToRuFriendly) || (witem.Mode == ItemViewMode.EnToRuSoundOnly))
                return EnToRuFullTemplate;
            return RuToEnFullTemplate;
        }
    }

    [ValueConversion(typeof(string), typeof(string))]
    public class SentenceTo3Parts : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string sentence = (string)values[0];
            int param = int.Parse((string)parameter);
            string[] p = new string[3] { null, null, null };
            string wordInDictionary = (string)values[1];

            if (sentence == null) return null;
            
            if (wordInDictionary.IndexOf('(') > 0) wordInDictionary = wordInDictionary.Substring(0, wordInDictionary.IndexOf('(')).Trim();
            if (wordInDictionary.IndexOf('[') > 0) wordInDictionary = wordInDictionary.Substring(0, wordInDictionary.IndexOf('[')).Trim();            
            if (wordInDictionary.IndexOf(' ') > 0) return (param == 0) ? sentence : null;
            
            int iStart = sentence.IndexOf(wordInDictionary);
            if (iStart < 0) return (param==0) ? sentence: null;
            p[0] = sentence.Substring(0, iStart);
            int iEnd = sentence.IndexOf(' ', iStart);
            if (iEnd < 0) iEnd = sentence.Length;
            p[1] = sentence.Substring(iStart, iEnd - iStart);            
            p[2] = sentence.Substring(iEnd);
            if (p[2] == "") p[2] = null;
            return p[param];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public struct Problem
    {
        public string EnglishSolution { get; set; }
        public string EnglishProblem { get; set; }
        public string RussianProblem { get; set; }
        public Problem(string englishSolution, string englishProblem, string russian )
        {
            EnglishProblem = englishProblem; EnglishSolution = englishSolution; RussianProblem = russian;
        }
    }


}

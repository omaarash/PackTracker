using System.Collections.Generic;
using System.Linq;

namespace PackTracker.Controls
{
    /// <summary>
    /// Interaktionslogik für Statistic.xaml
    /// </summary>
    public partial class Statistic
    {
        public static Dictionary<int, int> obtained = new Dictionary<int, int>();
        public Statistic(PackTracker.History History, PackTracker.Settings settings)
        {
            this.InitializeComponent();

            var _statistics = new Dictionary<int, View.Statistic>();

            this.dd_Packs.SelectionChanged += (sender, e) =>
            {
                if (e.AddedItems.Count == 1)
                {
                    var selection = (int)e.AddedItems[0];
                    if (!_statistics.ContainsKey(selection))
                    {
                        _statistics.Add(selection, new View.Statistic(selection, History));
                    }

                    this.dp_Statistic.DataContext = _statistics[selection];
                }
                else
                {
                    this.dp_Statistic.DataContext = null;
                }
            };

            Loaded += (sender, e) =>
            {
                // Ensure ShowAllPacks is set before assigning the DataContext so the
                // PackDropDown populates with every known pack (including Cataclysm).
                this.dd_Packs.ShowAllPacks = true;
                this.dd_Packs.DataContext = History;
            };
            this.dd_Packs.Focus();
        }
    }
}

using MahApps.Metro.Controls;
using PackTracker.Entity;
using PackTracker.View;
using System.Collections.Generic;
using HearthDb.Enums;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace PackTracker.Controls
{
    public partial class PackDropDown : SplitButton
    {
        private ObservableCollection<int> _dropDown;    
        private List<int> _allPackTypes;
        // If true, show all known pack types even if not present in history or obtained stats
        public bool ShowAllPacks { get; set; } = false;
        // If set, only show these pack ids (plus any matching history pack ids). If null or empty, normal behavior applies.
        public List<int> AllowedPackIds { get; set; }

        public PackDropDown()
        {
            this.InitializeComponent();

            this._allPackTypes = new List<int>(PackNameConverter.PackNames.Keys);
            this._dropDown = new ObservableCollection<int>();
            this.dd_Packs.ItemsSource = this._dropDown;
        }

        private void dd_Packs_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is PackTracker.History newhist)
            {
                if (this.AllowedPackIds != null && this.AllowedPackIds.Count > 0)
                {
                    // Start with the allowed ids that are known in _allPackTypes
                    var result = new System.Collections.Generic.HashSet<int>(this._allPackTypes.Intersect(this.AllowedPackIds));

                    // Build a set of significant words from allowed pack names to find related history entries (e.g., old Cataclysm ids)
                    var allowedWords = new System.Collections.Generic.HashSet<string>();
                    try
                    {
                        var packNames = PackTracker.View.PackNameConverter.PackNames;
                        foreach (var id in this.AllowedPackIds)
                        {
                            if (packNames.ContainsKey(id))
                            {
                                if (packNames[id].TryGetValue(Locale.enUS, out var enName))
                                {
                                    foreach (var w in System.Text.RegularExpressions.Regex.Split(enName.ToLowerInvariant(), "[^a-z0-9]+"))
                                    {
                                        if (w.Length >= 4) allowedWords.Add(w);
                                    }
                                }
                                if (packNames[id].TryGetValue(Locale.enGB, out var gbName))
                                {
                                    foreach (var w in System.Text.RegularExpressions.Regex.Split(gbName.ToLowerInvariant(), "[^a-z0-9]+"))
                                    {
                                        if (w.Length >= 4) allowedWords.Add(w);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ignore any failures reading pack names
                    }

                    // Include any history pack ids that match allowed keywords (helps catch old Cataclysm ids)
                    foreach (var hid in newhist.Select(p => p.Id).Distinct())
                    {
                        if (result.Contains(hid)) continue;
                        try
                        {
                            var packNames = PackTracker.View.PackNameConverter.PackNames;
                            if (packNames.ContainsKey(hid))
                            {
                                var namesToCheck = new System.Collections.Generic.List<string>();
                                if (packNames[hid].TryGetValue(Locale.enUS, out var hEn)) namesToCheck.Add(hEn.ToLowerInvariant());
                                if (packNames[hid].TryGetValue(Locale.enGB, out var hGb)) namesToCheck.Add(hGb.ToLowerInvariant());

                                foreach (var name in namesToCheck)
                                {
                                    foreach (var w in allowedWords)
                                    {
                                        if (name.Contains(w))
                                        {
                                            result.Add(hid);
                                            break;
                                        }
                                    }
                                    if (result.Contains(hid)) break;
                                }
                            }
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    this._dropDown = new ObservableCollection<int>(result.OrderBy(x => x));
                }
                else if (this.ShowAllPacks)
                {
                    // show every pack known in PackNameConverter
                    this._dropDown = new ObservableCollection<int>(this._allPackTypes.OrderBy(x => x));
                }
                else
                {
                    this._dropDown = new ObservableCollection<int>(this._allPackTypes.Intersect(newhist.Select(p => p.Id).Concat(Statistic.obtained.Keys)).OrderBy(x => x));
                }

                this.dd_Packs.ItemsSource = this._dropDown;
                newhist.CollectionChanged += this.DropDown_NewEntry;
            }

            if (e.OldValue is PackTracker.History history)
            {
                history.CollectionChanged -= this.DropDown_NewEntry;
            }
        }
        private void DropDown_NewEntry(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (Pack newPack in e.NewItems)
                {
                    if (!this._dropDown.Contains(newPack.Id))
                    {
                        var isInserted = false;

                        foreach (var id in this._dropDown)
                        {
                            if (newPack.Id < id)
                            {
                                this._dropDown.Insert(this._dropDown.IndexOf(id), newPack.Id);
                                isInserted = true;
                                break;
                            }
                        }

                        if (!isInserted)
                        {
                            this._dropDown.Add(newPack.Id);
                        }
                    }

                    this.dd_Packs.SelectedItem = newPack.Id;
                }
            }
        }

        private void dd_Packs_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (this.dd_Packs.SelectedIndex > 0)
                {
                    this.dd_Packs.SelectedIndex--;
                }
            }
            else
            {
                if (this.dd_Packs.SelectedIndex < this.dd_Packs.Items.Count - 1)
                {
                    this.dd_Packs.SelectedIndex++;
                }
            }
        }

        private void dd_Packs_Click(object sender, RoutedEventArgs e)
        {
            this.dd_Packs.IsExpanded = !this.dd_Packs.IsExpanded;
        }
    }
}

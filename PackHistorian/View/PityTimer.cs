using HearthDb.Enums;
using PackTracker.Entity;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace PackTracker.View
{
    public class PityTimer : INotifyPropertyChanged
    {
        public int PackId { get; }
        public Rarity Rarity { get; }
        public bool Premium { get; }
        public bool SkipFirst { get; }
        public bool WaitForFirst { get; private set; }
        private Settings _settings;

        public int Current { get; private set; } = 0;
        public ObservableCollection<int> Prev { get; } = new ObservableCollection<int>();
        public int? Average => this.Prev.Count > 0 ? (int?)Math.Round(this.Prev.Average(), 0) : null;

        public PityTimer(History History, int packId, Rarity rarity, bool premium, bool skipFirst, Settings settings)
        {
            this.PackId = packId;
            this.Rarity = rarity;
            this.Premium = premium;
            this.SkipFirst = this.WaitForFirst = skipFirst;
            this._settings = settings;

            foreach (var Pack in History)
            {
                this.AddPack(Pack);
            }

            History.CollectionChanged += (sender, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (Pack Pack in e.NewItems)
                    {
                        this.AddPack(Pack);
                    }
                }
            };
        }

        private void AddPack(Pack Pack)
        {
            // Consider this Pack matching if either the id matches or any of the cards in the pack
            // match the predicate for this.PackId (handles old history entries that had no/old ids)
            var isMatch = Pack.Id == this.PackId;
            if (!isMatch && ManualPackInsert.AllowHistoryFallbackForPack(this.PackId))
            {
                try
                {
                    foreach (var c in Pack.Cards)
                    {
                        var dbCard = HearthDb.Cards.GetFromDbfId(c.HDTCard.DbfId);
                        if (dbCard != null && ManualPackInsert.CardMatchesPackId(this.PackId, dbCard))
                        {
                            isMatch = true;
                            break;
                        }
                    }
                }
                catch
                {
                    // ignore failures when attempting to resolve card metadata
                }
            }

            // Special-case: include historical golden packs that were recorded with the regular
            // Cataclysm id (1057) but whose cards indicate a golden Cataclysm pack.
            // This handles older history entries where golden packs didn't have a distinct id.
            if (!isMatch && this.Premium && this.PackId == 1058)
            {
                try
                {
                    // require exactly 5 recorded cards and all premium
                    if (Pack.Cards != null && Pack.Cards.Count() == 5 && Pack.Cards.All(c => c.Premium))
                    {
                        foreach (var c in Pack.Cards)
                        {
                            var dbCard = HearthDb.Cards.GetFromDbfId(c.HDTCard.DbfId);
                            if (dbCard != null && ManualPackInsert.CardMatchesPackId(1057, dbCard))
                            {
                                isMatch = true;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (!isMatch)
            {
                return;
            }

            if (this.Condition(Pack))
            {
                var newCurr = this.Current;
                this.Current = 0;

                if (this.WaitForFirst)
                {
                    this.WaitForFirst = false;
                }
                else
                {
                    this.Prev.Add(newCurr);
                    this.OnPropertyChanged("Average");
                }
            }
            else
            {
                this.Current++;
            }

            this.OnPropertyChanged("Current");
        }

        private bool Condition(Pack Pack)
        {
            // Determine whether this pack should be treated as premium (a golden pack).
            // A pack is considered golden iff all 5 cards in the pack were premium.
            // This is strict by design — partially-golden packs do not count as golden.
            var packIsGolden = Pack.Cards != null && Pack.Cards.Count() == 5 && Pack.Cards.All(c => c.Premium);

            // If we're computing the golden pity timer, only count packs that are golden and contain
            // at least one card of the configured rarity and premium flag.
            if (this.Premium)
            {
                // For golden pity timers, only golden packs reset the golden pity.
                // Since packIsGolden already enforces all cards are premium, we only need to
                // check that at least one card of the desired rarity exists.
                return packIsGolden && Pack.Cards.Any(x => x.Rarity == this.Rarity);
            }

            // For regular pity timers:
            // - If GoldenResetRegularPityTimer is enabled in settings, any pack that contains a card
            //   of the rarity resets the regular pity, regardless of premium state.
            // - Otherwise, only non-premium cards count towards resetting the regular pity.
            if (this._settings.GoldenResetRegularPityTimer)
            {
                return Pack.Cards.Any(x => x.Rarity == this.Rarity);
            }

            return Pack.Cards.Any(x => x.Rarity == this.Rarity && !x.Premium);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}

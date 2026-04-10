using HearthDb.Enums;
using PackTracker.Entity;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace PackTracker.View
{
    internal class Statistic : INotifyPropertyChanged
    {
        private int _packId;
        private List<Pack> _packs;
        private int _commonPacks = 0;
        private int _rarePacks = 0;
        private int _epicPacks = 0;
        private int _legendaryPacks = 0;
        private int _totalAmount = 0;
        private int _epicCurrStreak = 0;
        private int _legendaryCurrStreak = 0;

        public int CommonAmount { get; private set; } = 0;
        public double CommonCards => this._totalAmount == 0 ? 0 : (double)this.CommonAmount / this._totalAmount;
        public double CommonPacks => this._packs.Count == 0 ? 0 : (double)this._commonPacks / this._packs.Count;

        public int RareAmount { get; private set; } = 0;
        public double RareCards => this._totalAmount == 0 ? 0 : (double)this.RareAmount / this._totalAmount;
        public double RarePacks => this._packs.Count == 0 ? 0 : (double)this._rarePacks / this._packs.Count;

        public int EpicAmount { get; private set; } = 0;
        public double EpicCards => this._totalAmount == 0 ? 0 : (double)this.EpicAmount / this._totalAmount;
        public double EpicPacks => this._packs.Count == 0 ? 0 : (double)this._epicPacks / this._packs.Count;

        public int LegendaryAmount { get; private set; } = 0;
        public double LegendaryCards => this._totalAmount == 0 ? 0 : (double)this.LegendaryAmount / this._totalAmount;
        public double LegendaryPacks => this._packs.Count == 0 ? 0 : (double)this._legendaryPacks / this._packs.Count;

        public string TotalPacks => Controls.Statistic.obtained.ContainsKey(this._packId)
            ? Controls.Statistic.obtained[this._packId] == this._packs.Count
                ? $"{this._packs.Count} Packs (Tracked)"
                : $"{this._packs.Count} Tracked / { Controls.Statistic.obtained[this._packId]} Total"
            : $"{this._packs.Count} Packs";

        public int EpicStreak { get; private set; } = 0;
        public int LegendaryStreak { get; private set; } = 0;

        public Statistic(int packId, History History)
        {
            this._packId = packId;

            // Build initial pack list. Include packs that either have the explicit pack id
            // or (for certain pack ids) match by card content (historical no-id packs).
            this._packs = new List<Pack>(History.Where(x =>
                x.Id == packId
                || (ManualPackInsert.AllowHistoryFallbackForPack(packId) && PackMatchesByCards(x, packId))
                || (packId == 1058 && IsHistoricGoldenCataclysm(x))
            ));

            foreach (var Pack in this._packs)
            {
                this.CountRarity(Pack);
                this.CountStreak(Pack);
            }

            PackWatcher.UpdateGranted();

            History.CollectionChanged += (sender, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (Pack Pack in e.NewItems)
                    {
                        if (Pack.Id == this._packId || (ManualPackInsert.AllowHistoryFallbackForPack(this._packId) && PackMatchesByCards(Pack, this._packId)) || (this._packId == 1058 && IsHistoricGoldenCataclysm(Pack)))
                        {
                            this._packs.Add(Pack);
                            this.CountRarity(Pack);
                            this.CountStreak(Pack);

                            if (Pack.Cards.Any(x => x.Rarity == Rarity.COMMON))
                            {
                                this.OnPropertyChanged("CommonAmount");
                            }

                            if (Pack.Cards.Any(x => x.Rarity == Rarity.RARE))
                            {
                                this.OnPropertyChanged("RareAmount");
                            }

                            if (Pack.Cards.Any(x => x.Rarity == Rarity.EPIC))
                            {
                                this.OnPropertyChanged("EpicAmount");
                            }

                            if (Pack.Cards.Any(x => x.Rarity == Rarity.LEGENDARY))
                            {
                                this.OnPropertyChanged("LegendaryAmount");
                            }

                            this.OnPropertyChanged("CommonCards");
                            this.OnPropertyChanged("CommonPacks");
                            this.OnPropertyChanged("RareCards");
                            this.OnPropertyChanged("RarePacks");
                            this.OnPropertyChanged("EpicCards");
                            this.OnPropertyChanged("EpicPacks");
                            this.OnPropertyChanged("LegendaryCards");
                            this.OnPropertyChanged("LegendaryPacks");
                            this.OnPropertyChanged("TotalPacks");
                        }
                    }
                }

                PackWatcher.UpdateGranted();
            };
        }

        private static bool PackMatchesByCards(Pack pack, int packId)
        {
            try
            {
                foreach (var c in pack.Cards)
                {
                    var dbCard = HearthDb.Cards.GetFromDbfId(c.HDTCard.DbfId);
                    if (dbCard != null && ManualPackInsert.CardMatchesPackId(packId, dbCard))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        private static bool IsHistoricGoldenCataclysm(Pack pack)
        {
            try
            {
                if (pack.Cards == null) return false;
                if (pack.Cards.Count() != 5) return false;
                if (!pack.Cards.All(c => c.Premium)) return false;

                // If any card matches the Cataclysm filter (1057), consider this a historic golden Cataclysm
                foreach (var c in pack.Cards)
                {
                    var dbCard = HearthDb.Cards.GetFromDbfId(c.HDTCard.DbfId);
                    if (dbCard != null && ManualPackInsert.CardMatchesPackId(1057, dbCard))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        private void CountRarity(Pack Pack)
        {
            bool
              hasCommon = false,
              hasRare = false,
              hasEpic = false,
              hasLegendary = false;

            foreach (var Card in Pack.Cards)
            {
                switch (Card.Rarity)
                {
                    case Rarity.COMMON:
                        this.CommonAmount++;
                        hasCommon = true;
                        break;
                    case Rarity.RARE:
                        this.RareAmount++;
                        hasRare = true;
                        break;
                    case Rarity.EPIC:
                        this.EpicAmount++;
                        hasEpic = true;
                        break;
                    case Rarity.LEGENDARY:
                        hasLegendary = true;
                        this.LegendaryAmount++;
                        break;
                }

                this._totalAmount++;
            }

            if (hasCommon)
            {
                this._commonPacks++;
            }

            if (hasRare)
            {
                this._rarePacks++;
            }

            if (hasEpic)
            {
                this._epicPacks++;
            }

            if (hasLegendary)
            {
                this._legendaryPacks++;
            }
        }

        private void CountStreak(Pack Pack)
        {
            if (Pack.Cards.Any(x => x.Rarity == Rarity.EPIC))
            {
                this._epicCurrStreak = 0;
            }
            else
            {
                this._epicCurrStreak++;

                if (this._epicCurrStreak > this.EpicStreak)
                {
                    this.EpicStreak = this._epicCurrStreak;
                    this.OnPropertyChanged("EpicStreak");
                }
            }

            if (Pack.Cards.Any(x => x.Rarity == Rarity.LEGENDARY))
            {
                this._legendaryCurrStreak = 0;
            }
            else
            {
                this._legendaryCurrStreak++;

                if (this._legendaryCurrStreak > this.LegendaryStreak)
                {
                    this.LegendaryStreak = this._legendaryCurrStreak;
                    this.OnPropertyChanged("LegendaryStreak");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}

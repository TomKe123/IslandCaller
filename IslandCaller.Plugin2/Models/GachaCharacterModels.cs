namespace IslandCaller.Models
{
    public enum GachaRarity
    {
        ThreeStar = 3,
        FourStar = 4,
        FiveStar = 5
    }

    public sealed class GachaPityState
    {
        public int FiveStarPity { get; set; }
        public int FourStarPity { get; set; }
        public bool IsFiveStarFeaturedGuaranteed { get; set; }
        public bool IsFourStarFeaturedGuaranteed { get; set; }
        public int TotalDrawCount { get; set; }
    }
}

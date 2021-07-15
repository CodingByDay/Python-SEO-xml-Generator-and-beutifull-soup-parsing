using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Emmares4.Models.HomeViewModels
{
    public class ProviderViewModel
    {
        public int ProviderID { get; set; }
        public string Name { get; set; }
        public int Subscriptions { get; set; }
        public int FieldID { get; set; }
        public string Field { get; set; }
        public string Genre { get; set; }
        public double AverageRating { get; set; }
        public DateTime AddedOn { get; set; }
        public int RecipientCount { get; set; }
        public int Records { get; set; }
        public bool IsSubscribed { get; set; }
    }
}

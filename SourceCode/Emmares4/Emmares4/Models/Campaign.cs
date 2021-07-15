using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Emmares4.Models
{
    public class Campaign
    {
        public Guid ID { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public int Recipients { get; set; }
        public double Budget { get; set; }
        public string Snippet { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime DateModified { get; set; }
        public Provider Publisher { get; set; }
        public int GenreID { get; set; }
        public Genre Genre { get; set; }
        public int ContentTypeID { get; set; }
        public ContentType ContentType { get; set; }
        public int RegionID { get; set; }
        public Region Region { get; set; }
        public ICollection<Statistic> Statistics { get; set; }

        [NotMapped]
        public double AvailableBalance { get; set; }
    }
   
}
//[Bind("ID,Name,Publisher,Recipients,Budget,Snippet,ContentTypeID,GenreID,RegionID")] 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Emmares4.Models
{
    public class UserInterest
    {
        public int ID { get; set; }
        public ApplicationUser User { get; set; }

        public ContentType ContentType { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime DateModified { get; set; }
    }
}

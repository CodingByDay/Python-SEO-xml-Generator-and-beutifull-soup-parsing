using Emmares4.Data;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Emmares4.Models.HomeViewModels
{
    public class FavoritesViewModel
    {
        ApplicationDbContext _context;
        public FavoritesViewModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ApplicationUser user)
        {
            _context = context;

            var interests = _context.Entry(user)
               .Collection(s => s.Interests)
               .Query()
               .Select(p => p.ContentType.Name)
               .ToList();

            Favorites = interests;
        }

        public List<string> Favorites { get; set; }

        public int ContentTypeID { get; set; }
    }
}

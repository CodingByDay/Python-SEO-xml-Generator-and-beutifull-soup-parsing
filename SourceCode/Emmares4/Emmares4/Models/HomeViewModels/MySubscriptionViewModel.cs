using System.Security.Claims;
using Emmares4.Data;
using Emmares4.Models;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Data.Common;
using Dapper;

namespace Emmares4.Models.HomeViewModels
{
    public class MySubscriptionViewModel
    {
        private ApplicationDbContext _context;
        private UserManager<ApplicationUser> _userManager;        
        private readonly DbConnection _dbConnection;

        public MySubscriptionViewModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            ApplicationUser user, DbConnection dbConnection, string favorite = "")
        {
            _dbConnection = dbConnection;
            _context = context;
            _userManager = userManager;
            _context = context;
         
            var interests = _context.Entry(user)
               .Collection(s => s.Interests)
               .Query()
               .Select(p => p.ContentType.Name)
               .ToList();

            favorite = string.IsNullOrEmpty(favorite) ? (interests.Any() ? interests?.First() : "") : favorite;

            var result = _dbConnection.QueryAsync<ProviderViewModel>("dbo.GetSubscriptionsByUserAndContentType", new { Owner = user.Id, ContentType = favorite }, null, null,
                System.Data.CommandType.StoredProcedure).Result;

            Subscriptions = result.ToList();

            result = _dbConnection.QueryAsync<ProviderViewModel>("dbo.GetSubscriptionsByUser", new { Owner = user.Id }, null, null,
                System.Data.CommandType.StoredProcedure).Result;

            AllSubscriptions = result.ToList();

            Favorite = favorite;

        }

        public string Favorite { get; set; }
        public List<ProviderViewModel> Subscriptions { get; set; }
        public List<ProviderViewModel> AllSubscriptions { get; set; }

    }

    public class ProviderViewModelComparer : IEqualityComparer<ProviderViewModel>
    {
        public bool Equals(ProviderViewModel x, ProviderViewModel y)
        {
            return x.ProviderID == y.ProviderID;
        }

        public int GetHashCode(ProviderViewModel obj)
        {
            return obj.GetHashCode();
        }
    }
}
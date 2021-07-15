using Dapper;
using Emmares4.Data;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Emmares4.Models.HomeViewModels
{
    public class MyContentViewModel
    {
        ApplicationDbContext _context;
        DbConnection _dbConnection;
        public MyContentViewModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ApplicationUser user, DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
            _context = context;

            //var providers =  _dbConnection.QueryAsync<ProviderViewModel>("dbo.GetAllProviders", null, null, null, 
            //    System.Data.CommandType.StoredProcedure).Result;

            _context.Entry(user)
                .Collection(x => x.Statistics)
                .Load();         


            FavoritesVM = new FavoritesViewModel(context, userManager, user);
            SubscriptionsVM = new MySubscriptionViewModel(context, userManager, user, _dbConnection);
            //Providers = providers.ToList();
        }

        public FavoritesViewModel FavoritesVM { get; set; }
        public MySubscriptionViewModel SubscriptionsVM { get; set; }
        //public List<ProviderViewModel> Providers { get; set; }
     


    }
}

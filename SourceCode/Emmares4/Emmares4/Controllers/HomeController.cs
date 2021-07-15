using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Emmares4.Models;
using Emmares4.Data;
using Emmares4.Models.HomeViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using System.IO;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using Emmares4.Extensions;
using System.Data.Common;
using Dapper;

namespace Emmares4.Controllers
{
    
    public class HomeController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger _logger;
        ApplicationDbContext _db;
        UserManager<ApplicationUser> _userManager;
        DbConnection _dbConnection;
        ApplicationUser _user;

        public HomeController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager,
    ILogger<HomeController> logger, SignInManager<ApplicationUser> signInManager,
    DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
            _db = dbContext;
            _userManager = userManager;
            _logger = logger;
            _signInManager = signInManager;
        }
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {

            // FileDetails fileDetails;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                var fileName = ContentDispositionHeaderValue
                .Parse(file.ContentDisposition)
                .FileName
                .Trim('"');
                _user = _userManager.GetUserAsync(User).Result;
                if (fileName.Contains(".jpg") || fileName.Contains(".png") || fileName.Contains(".jpeg"))
                {
                    fileName = _user.Id + ".jpg";

                    if (file.Length > 0)
                        using (var fileStream = new FileStream(Path.Combine($"wwwroot/images", fileName), FileMode.Create))
                            await file.CopyToAsync(fileStream);
                }
                else
                {
                    return RedirectToAction("AccountSettings", new { wasRedirected = true });
                }
            }
            return RedirectToAction(nameof(AccountSettings));
        }

        public IActionResult Dashboard(int chartType = 1)
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;
            if (_user == null)
                return RedirectToAction("Login", "Account");
            //if (User.IsInRole("Marketeer"))
            //{
            //    return RedirectToAction("Index", "Campaigns");
            //}
            var viewModel = new DashboardViewModel(_db, _dbConnection, _userManager, _user, (ChartInterval)chartType);
            return View(viewModel);
        }

        public IActionResult GetChart(int chartType)
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;
            var viewModel = new ChartViewModel(_db, _dbConnection, _user, false, (ChartInterval)chartType);
            return PartialView("_chartView", viewModel);
        }

        public async Task<IActionResult> GetProviders()
        {

            var popularity = Request.Form["Popularity"].ToString();
            var contentType = Request.Form["ContentType"].ToString();
            var interval = Request.Form["Interval"].ToString();
            var genre = Request.Form["Genre"].ToString();
            var region = Request.Form["region"].ToString();

            var start = Request.Form["start"].ToString();
            var draw = Request.Form["draw"].ToString();


            var query = string.Empty;

            query = @"Select p.ID as ProviderID, p.Name, cnt.Id as FieldID, cnt.Name as Field, count(sub.ID) subscriptions, 
                            cast(AVG(cast(Rating as decimal)) as numeric(10,2)) as AverageRating,
                            Records = COUNT(*) OVER()
                        from Providers p
                        left outer join Campaigns cmp on cmp.PublisherID = p.ID
                        inner join ContentTypes cnt on cmp.ContentTypeID = cnt.ID
                        inner join Genres g on cmp.GenreID = g.ID
                        inner join Regions r on cmp.RegionID = r.ID
                        left outer join [Statistics] s on s.CampaignID = cmp.ID
                        left outer join Subscriptions sub on p.ID = sub.ProviderID and cmp.ContentTypeID = sub.ContentTypeID
                        {0}
                        group by p.ID, p.Name, cnt.ID, cnt.Name, p.DateAdded
                        {1} 
                        OFFSET {2} ROWS FETCH FIRST 10 ROWS ONLY";

            var orderByClause = string.IsNullOrEmpty(popularity) ? " Subscriptions Desc " : $" {popularity} Desc ";
            orderByClause = $"ORDER BY {orderByClause}, p.DateAdded Desc";

            var whereClause = string.Empty;
            if (!string.IsNullOrEmpty(contentType))
                whereClause = string.IsNullOrEmpty(contentType) ? "" : $" cnt.ID = {contentType} ";

            if (!string.IsNullOrEmpty(genre))
                whereClause = (string.IsNullOrEmpty(whereClause) ? "" : $" {whereClause}  AND ") + $" g.ID = {genre} ";

            if (!string.IsNullOrEmpty(region))
                whereClause = (string.IsNullOrEmpty(whereClause) ? "" : $" {whereClause}  AND ") + $"  r.ID = {region} ";

            if (!string.IsNullOrEmpty(interval))
                whereClause = (string.IsNullOrEmpty(whereClause) ? "" : $" {whereClause}  AND ")
                    + $" (p.DateAdded BETWEEN DATEADD(day, -{interval}, GETDATE()) and GETDATE() ) ";

            whereClause = string.IsNullOrEmpty(whereClause) ? "" : " WHERE " + whereClause;

            query = string.Format(query, whereClause, orderByClause, start);

            var providers = await _dbConnection.QueryAsync<ProviderViewModel>(query);

            var p = providers.FirstOrDefault();
            int recordCount = 0;
            if (p != null)
                recordCount = p.Records;

            return Json(new { draw, recordsTotal = recordCount, recordsFiltered = recordCount, data = providers });
        }

        Dictionary<string, string> _popularityMap = new Dictionary<string, string>()
        {
                { "Most subscribed", "Subscriptions" },
                { "Best rated" , "AverageRating"},
                { "Most ratings", "Stats" },
                { "Most sent", "RecipientCount" }
        };

        Dictionary<string, string> _intervalMap = new Dictionary<string, string>()
        {
                { "All Time", "0" },
                { "This Week" , "6"},
                { "This Month", "29" },
                { "This Year", "364" }
        };


        public IActionResult MyContent()
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            var viewModel = new MyContentViewModel(_db, _userManager, _user, _dbConnection);
            PopulateGridFields();
            return View(viewModel);
        }

        public IActionResult DeleteField(string ContentTypeID)
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            var interest = _db.ContentTypes.Where(c => c.Name == ContentTypeID).FirstOrDefault();

            _db.Entry(_user)
                .Collection(x => x.Interests)
                .Load();

            UserInterest userInterest = null;

            if (_user != null && interest != null)
                userInterest = _user.Interests.Where(ui => ui.User == _user && ui.ContentType == interest).FirstOrDefault();

            if (userInterest != null)
            {
                _user.Interests.Remove(userInterest);
                _db.SaveChanges();
            }

            return RedirectToAction(nameof(MyContent));

        }
        public IActionResult AddField(int ContentTypeID)
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            var interest = _db.ContentTypes.Where(c => c.ID == ContentTypeID).FirstOrDefault();

            if (interest != null)
            {
                _db.Entry(_user)
                .Collection(x => x.Interests)
                .Load();

                var interestExists = _user.Interests.Where(c => c.User == _user && c.ContentType == interest).FirstOrDefault();
                if (interestExists == null)
                {
                    _user.Interests.Add(new UserInterest() { User = _user, ContentType = interest });
                    _db.SaveChanges();
                }
            }

            var contentTypeQuery = from c in _db.ContentTypes
                                   orderby c.Name
                                   select c;
            ViewBag.ContentTypeID = new SelectList(contentTypeQuery.AsNoTracking(), "ID", "Name", null);

            var vm = new FavoritesViewModel(_db, _userManager, _user);

            return PartialView("_favorites", vm);
        }

        private void PopulateGridFields(object selectedContentType = null, object selectedGenre = null, object selectedRegion = null)
        {

            ViewBag.Popularity = new SelectList(_popularityMap, "Value", "Key");
            ViewBag.Interval = new SelectList(_intervalMap, "Value", "Key");

            var contentTypeQuery = from c in _db.ContentTypes
                                   orderby c.Name
                                   select c;
            ViewBag.ContentTypeID = new SelectList(contentTypeQuery.AsNoTracking(), "ID", "Name", selectedContentType);

            var genreQuery = from c in _db.Genres
                             orderby c.Name
                             select c;
            ViewBag.GenreID = new SelectList(genreQuery.AsNoTracking(), "ID", "Name", selectedGenre);

            var regionQuery = from c in _db.Regions
                              orderby c.Name
                              select c;
            ViewBag.RegionID = new SelectList(regionQuery.AsNoTracking(), "ID", "Name", selectedRegion);
        }


        private void PopulateDropDownList(object selectedContentType = null, object selectedGenre = null, object selectedRegion = null)
        {
            var contentTypeQuery = from c in _db.ContentTypes
                                   orderby c.Name
                                   select c;
            ViewBag.ContentTypeID = new SelectList(contentTypeQuery.AsNoTracking(), "ID", "Name", selectedContentType);

            var genreQuery = from c in _db.Genres
                             orderby c.Name
                             select c;
            ViewBag.GenreID = new SelectList(genreQuery.AsNoTracking(), "ID", "Name", selectedGenre);

            var regionQuery = from c in _db.Regions
                              orderby c.Name
                              select c;
            ViewBag.RegionID = new SelectList(regionQuery.AsNoTracking(), "ID", "Name", selectedRegion);
        }

        public IActionResult MySubscriptions(string favorite = null)
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            var viewModel = new MySubscriptionViewModel(_db, _userManager, _user, _dbConnection, favorite);
            return PartialView("_SubscribedProviders", viewModel);
        }

        public IActionResult Subscribe(int ProviderID, int FieldID)
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            var p1 = _db.Providers.Where(p => p.ID == ProviderID).FirstOrDefault();
            var c1 = _db.ContentTypes.Where(c => c.ID == FieldID).FirstOrDefault();

            if (p1 != null && c1 != null)
            {
                if (!_db.Subscriptions.Any(x => x.Subscriber == _user && x.Provider == p1 && x.ContentType == c1))
                {
                    var sub = new Subscription() { Subscriber = _user, Provider = p1, ContentType = c1 };
                    _db.Subscriptions.Add(sub);
                    _db.SaveChanges();
                }
            }

            return RedirectToAction(nameof(MyContent));
        }

        public IActionResult Unsubscribe(int ProviderID, int FieldID)
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            var p1 = _db.Providers.Where(p => p.ID == ProviderID).FirstOrDefault();
            var c1 = _db.ContentTypes.Where(c => c.ID == FieldID).FirstOrDefault();

            if (p1 != null && c1 != null)
            {
                var subscription = _db.Subscriptions
                    .Where(x => x.Subscriber == _user && x.Provider == p1 && x.ContentType == c1)
                    .FirstOrDefault();

                if (subscription != null)
                {
                    _db.Subscriptions.Remove(subscription);
                    _db.SaveChanges();
                }
            }

            return RedirectToAction(nameof(MyContent));
        }

        public IActionResult Achievements()
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            var viewModel = new AchievementsViewModel(_db, _userManager, _user);
            return View(viewModel);
        }

        public IActionResult Withdrawl()
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            var viewModel = new WithdrawlViewModel(_db, _userManager, _user);
            return View(viewModel);
        }

        [TempData]
        public string StatusMessage { get; set; }



        public IActionResult AccountSettings(bool? wasRedirected)
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            var vm = new AccountSettingsViewModel(_db, _userManager, _user);
            if (wasRedirected != null)
            {
                ModelState.AddModelError(string.Empty, "Invalid file type.");
                vm.StatusMessage = "uploadForm";

            }
            return View(vm);
        }
        [HttpPost]
        public ActionResult UpdateWallet(AccountSettingsViewModel vm)
        {
            vm.StatusMessage = "inputForm";

            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            if (_user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }
            vm.UserImage = _user.Id + ".jpg";
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, "Some error occured.");
                return View(vm);
            }
            if (!string.IsNullOrEmpty(vm.WalletAddress) && vm.WalletAddress.StartsWith("0x") && vm.WalletAddress.Length == 42)
            {
                ModelState.AddModelError(string.Empty, "Wallet address updated");
                _user.WalletAddress = vm.WalletAddress;
                _db.SaveChanges();
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Wallet address is not valid");
                vm.WalletAddress = null;
            }
            return RedirectToAction(nameof(AccountSettings));
        }
        [HttpPost]
        public async Task<IActionResult> AccountSettings(AccountSettingsViewModel vm)
        {

            vm.StatusMessage = "inputForm";

            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            if (_user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }
            vm.UserImage = _user.Id + ".jpg";
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, "Some error occured.");
                return View(vm);
            }

            if (string.IsNullOrEmpty(vm.OldPassword))
            {
                ModelState.AddModelError(string.Empty, "Current password cannot be null.");
                return View(vm);
            }
            if (string.IsNullOrEmpty(vm.NewPassword))
            {
                ModelState.AddModelError(string.Empty, "New Password cannot be null.");
                return View(vm);
            }
            if (string.IsNullOrEmpty(vm.ConfirmPassword))
            {
                ModelState.AddModelError(string.Empty, "Confirm Password cannot be null.");
                return View(vm);
            }
            if (!string.IsNullOrEmpty(vm.NewPassword) && !string.IsNullOrEmpty(vm.NewPassword) && !string.IsNullOrEmpty(vm.ConfirmPassword))
            {
                var changePasswordResult = await _userManager.ChangePasswordAsync(_user, vm.OldPassword, vm.NewPassword);
                if (!changePasswordResult.Succeeded)
                {
                    AddErrors(changePasswordResult);
                    return View(vm);
                }

                await _signInManager.SignInAsync(_user, isPersistent: false);
                _logger.LogInformation("User changed their password successfully.");
                //StatusMessage = "Your password has been changed.";
                ModelState.AddModelError(string.Empty, "You have changed your password successfully.");
                return View(vm);
            }

            return RedirectToAction(nameof(AccountSettings));
        }
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }


        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

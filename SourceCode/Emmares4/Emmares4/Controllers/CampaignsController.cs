using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Emmares4.Data;
using Emmares4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Emmares4.Models.HomeViewModels;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Emmares4.Models.ManageViewModels;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net.Http.Headers;
using System.Data.Common;
using Dapper;

namespace Emmares4.Controllers
{
    [Authorize(Roles = "Marketeer")]
    [Route("[controller]/[action]")]
    public class CampaignsController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger _logger;
        ApplicationDbContext _db;
        UserManager<ApplicationUser> _userManager;
        ApplicationUser _user;
        DbConnection _dbConnection;

        public CampaignsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            DbConnection dbConnection,
            ILogger<HomeController> logger, SignInManager<ApplicationUser> signInManager)
        {
            _db = context;
            _userManager = userManager;
            _logger = logger;
            _signInManager = signInManager;
            _dbConnection = dbConnection;
        }

        // GET: Campaigns
        public async Task<IActionResult> Index()
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;
            if (_user == null)
                return RedirectToAction("Login", "Account");
            var query = @"SELECT c.Id, c.Name, cnt.Name AS Field, r.Name AS Region,g.Name AS Genre, c.Recipients, c.Budget, 
                                    (c.Budget - coalesce(sum(s.Reward), 0)) Remaining, CAST(c.DateAdded as date) AS AddedOn
                            FROM Campaigns c
                            INNER JOIN ContentTypes cnt on c.ContentTypeID = cnt.ID
                            INNER JOIN Genres g on c.GenreID = g.ID
                            INNER JOIN Regions r on c.RegionID = r.ID
                            INNER JOIN Providers p on p.ID = c.PublisherID
                            LEFT OUTER JOIN [Statistics] s on c.ID = s.CampaignID
                            WHERE p.OwnerId = @user
                            GROUP BY c.Id, c.Name, cnt.Name , r.Name ,g.Name , c.Recipients, c.Budget,c.DateAdded
                            ORDER BY c.DateAdded;";

            var camp = await _dbConnection.QueryAsync<CampaignViewModel>(query, new { user = _user.Id });

            return View(camp.ToList());
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
        // GET: Campaigns/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;
            var campaign = await _db.Campaigns
                .SingleOrDefaultAsync(m => m.ID == id);
            PopulateDropDownList();

            if (campaign == null)
            {
                return NotFound();
            }
            return View(campaign);
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

        public IActionResult Analytics()
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;

            var viewModel = new AnalyticsViewModel(_db, _dbConnection, _userManager, _user, ChartInterval.Week);
            return View(viewModel);
        }

        public IActionResult GetChart(int chartType)
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;
            var viewModel = new ChartViewModel(_db, _dbConnection, _user, true, (ChartInterval)chartType);
            return PartialView("_chartView", viewModel);
        }

        // GET: Campaigns/Create
        public IActionResult Create()
        {
            if (_user == null)
                _user = _userManager.GetUserAsync(User).Result;


            PopulateDropDownList();

            Campaign c = new Campaign();
            c.AvailableBalance = _user.Balance;
            _db.Providers.Include(pc => pc.Owner).Load();

            c.Publisher = _db.Providers.FirstOrDefault(x => x.Owner == _user);

            c.Snippet = "<html></html>";

            return View(c);
        }
      

        // POST: Campaigns/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Campaign campaign)
        {
            PopulateDropDownList();
            if (ModelState.IsValid)
            {
                if (_user == null)
                    _user = _userManager.GetUserAsync(User).Result;

                if (campaign.Budget > campaign.AvailableBalance)
                {
                    PopulateDropDownList();
                    ModelState.AddModelError(string.Empty, "You dont have enough balance to create this campaign.");
                    return View(campaign);
                }
                campaign.DateAdded = DateTime.UtcNow;

                campaign.Publisher = _db.Providers.Where(p => p.Owner == _user).FirstOrDefault();

                _db.Add(campaign);
                _user.Balance = _user.Balance - campaign.Budget;
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(campaign);
        }

        // GET: Campaigns/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var campaign = await _db.Campaigns.SingleOrDefaultAsync(m => m.ID == id);
            if (campaign == null)
            {
                return NotFound();
            }
            return View(campaign);
        }

        // POST: Campaigns/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("ID,Name,Publisher,Recipients,Budget,Snippet,GenreID,RegionID,ContentTypeID")] Campaign campaign)
        {
            if (id != campaign.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _db.Update(campaign);
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CampaignExists(campaign.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(campaign);
        }

        // GET: Campaigns/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var campaign = await _db.Campaigns
                .SingleOrDefaultAsync(m => m.ID == id);
            if (campaign == null)
            {
                return NotFound();
            }

            return View(campaign);
        }

        // POST: Campaigns/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var campaign = await _db.Campaigns.SingleOrDefaultAsync(m => m.ID == id);
            _db.Campaigns.Remove(campaign);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CampaignExists(Guid id)
        {
            return _db.Campaigns.Any(e => e.ID == id);
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
            var publisher = _db.Providers.Where(x => x.Owner == _user).FirstOrDefault();
            {
                if (publisher == null)
                    _db.Providers.Add(new Provider() { Name = vm.PublisherName, Owner = _user, DateAdded = DateTime.UtcNow });
                else if (!string.IsNullOrEmpty(vm.PublisherName))
                    publisher.Name = vm.PublisherName;
                _db.SaveChanges();
            }
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
